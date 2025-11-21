using Microsoft.Extensions.Logging;
using SMBLibrary;
using SMBLibrary.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace InkSoft.SmbAbstraction;

public class SmbDirectory(
    SmbFileSystem smbFileSystem,
    ILogger<SmbDirectory>? logger
): DirectoryWrapper(smbFileSystem.NonSmbFileSystem), IDirectory
{
    /// <inheritdoc cref="SmbFileSystem"/>
    public new IFileSystem FileSystem => smbFileSystem;

    private SmbDirectoryInfoFactory DirectoryInfoFactory => (SmbDirectoryInfoFactory)smbFileSystem.DirectoryInfo;

    public SMBTransportType Transport { get; set; } = SMBTransportType.DirectTCPTransport;

    public override IDirectoryInfo CreateDirectory(string path) => CreateDirectory(path, null);

    private IDirectoryInfo CreateDirectory(string path, ISmbCredential? credential)
    {
        if (!path.IsSharePath())
            return base.CreateDirectory(path);

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed to CreateDirectory {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        credential ??= smbFileSystem.CredentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed to CreateDirectory {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));

        if (Exists(path))
            return DirectoryInfoFactory.New(path);

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string shareName = path.ShareName();
            string relativePath = path.ShareRelativePath();
            logger?.LogTrace("Trying to CreateDirectory {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);
            using var connection = SmbConnection.CreateSmbConnection(smbFileSystem.ClientFactory, ipAddress, Transport, credential, smbFileSystem.Options);
            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
            status.AssertSuccess();

            const AccessMask c_syncAndMaxAllowed = AccessMask.SYNCHRONIZE | AccessMask.MAXIMUM_ALLOWED;
            const ShareAccess c_readWrite = ShareAccess.Read | ShareAccess.Write;
            const CreateDisposition c_fileOpenIf = CreateDisposition.FILE_OPEN_IF;
            const CreateOptions c_fileSyncIoNonAlertOrDirectoryFile = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_DIRECTORY_FILE;

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            do
            {
                if (status == NTStatus.STATUS_PENDING)
                    logger?.LogTrace("STATUS_PENDING while trying to create directory {path}. {stopwatchElapsedTotalSeconds}/{smbFileSystemOptionsClientSessionTimeout} seconds elapsed.", path, stopwatch.Elapsed.TotalSeconds, smbFileSystem.Options.ClientSessionTimeout);

                status = fileStore.CreateFile(out handle, out _, relativePath, c_syncAndMaxAllowed, 0, c_readWrite, c_fileOpenIf, c_fileSyncIoNonAlertOrDirectoryFile, null);

                if (status == NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
                {
                    CreateDirectory(path.GetParentPath(), credential);
                    status = fileStore.CreateFile(out handle, out _, relativePath, c_syncAndMaxAllowed, 0, c_readWrite, c_fileOpenIf, c_fileSyncIoNonAlertOrDirectoryFile, null);
                }
            } while (status == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= smbFileSystem.Options.ClientSessionTimeout);

            stopwatch.Stop();
            status.AssertSuccess();
            FileStoreUtilities.CloseFile(fileStore, ref handle);
            return DirectoryInfoFactory.New(path, credential)!;
        }
        catch (Exception ex)
        {
            throw new SmbException($"Failed to CreateDirectory {path}", ex);
        }
        finally
        {
            FileStoreUtilities.CloseFile(fileStore, ref handle);
        }
    }

    public override void Delete(string path) => Delete(path, null);

    internal void Delete(string path, ISmbCredential? credential)
    {
        if (!path.IsSharePath())
        {
            base.Delete(path);
            return;
        }

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed to Delete {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        if (!Exists(path))
            return;

        credential ??= smbFileSystem.CredentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed to Delete {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));

        if (EnumerateFileSystemEntries(path).Any())
            throw new SmbException($"Failed to Delete {path}", new IOException("Cannot delete directory. Directory is not empty."));

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string shareName = path.ShareName();
            string relativePath = path.ShareRelativePath();
            logger?.LogTrace("Trying to Delete {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);
            using var connection = SmbConnection.CreateSmbConnection(smbFileSystem.ClientFactory, ipAddress, Transport, credential, smbFileSystem.Options);
            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
            status.AssertSuccess();
            const AccessMask c_syncOrDelete = AccessMask.SYNCHRONIZE | AccessMask.DELETE;
            const CreateOptions c_syncIoNonAlertOrDeleteOnClose = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_DELETE_ON_CLOSE;

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            do
            {
                if (status == NTStatus.STATUS_PENDING)
                    logger?.LogTrace("STATUS_PENDING while trying to delete directory {path}. {stopwatchElapsedTotalSeconds}/{smbFileSystemOptionsClientSessionTimeout} seconds elapsed.", path, stopwatch.Elapsed.TotalSeconds, smbFileSystem.Options.ClientSessionTimeout);

                status = fileStore.CreateFile(out handle, out _, relativePath, c_syncOrDelete, 0, ShareAccess.Delete, CreateDisposition.FILE_OPEN, c_syncIoNonAlertOrDeleteOnClose, null);
            }
            while (status == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= smbFileSystem.Options.ClientSessionTimeout);
            stopwatch.Stop();

            status.AssertSuccess();

            // This is the correct delete command, but it doesn't work for some reason. You have to use FILE_DELETE_ON_CLOSE
            // fileStore.SetFileInformation(handle, new FileDispositionInformation());

            FileStoreUtilities.CloseFile(fileStore, ref handle);
        }
        catch (Exception ex)
        {
            throw new SmbException($"Failed to Delete {path}", ex);
        }
        finally
        {
            FileStoreUtilities.CloseFile(fileStore, ref handle);
        }
    }

    public override void Delete(string path, bool recursive) => Delete(path, recursive, null);

    public void Delete(string path, bool recursive, ISmbCredential? credential)
    {
        if (!path.IsSharePath())
        {
            base.Delete(path, recursive);
            return;
        }

        if (recursive)
        {
            if (!path.TryResolveHostnameFromPath(out var ipAddress))
                throw new SmbException($"Failed to Delete {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

            credential ??= smbFileSystem.CredentialProvider.GetSmbCredential(path) ?? throw new SmbException($"Failed to delete {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));

            ISMBFileStore fileStore = null;
            object handle = null;

            try
            {
                string shareName = path.ShareName();
                string relativePath = path.ShareRelativePath();
                logger?.LogTrace("Trying to Delete {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);
                using var connection = SmbConnection.CreateSmbConnection(smbFileSystem.ClientFactory, ipAddress, Transport, credential, smbFileSystem.Options);
                fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
                status.AssertSuccess();
                const AccessMask c_syncOrRead = AccessMask.SYNCHRONIZE | AccessMask.GENERIC_READ;
                const CreateOptions c_syncIoNonAlertOrDirectoryFile = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_DIRECTORY_FILE;

                var stopwatch = new Stopwatch();
                stopwatch.Start();
                do
                {
                    if (status == NTStatus.STATUS_PENDING)
                        logger?.LogTrace("STATUS_PENDING while trying to delete directory {path}. {stopwatchElapsedTotalSeconds}/{smbFileSystemOptionsClientSessionTimeout} seconds elapsed.", path, stopwatch.Elapsed.TotalSeconds, smbFileSystem.Options.ClientSessionTimeout);

                    status = fileStore.CreateFile(out handle, out _, relativePath, c_syncOrRead, 0, ShareAccess.Delete, CreateDisposition.FILE_OPEN, c_syncIoNonAlertOrDirectoryFile, null);
                } while (status == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= smbFileSystem.Options.ClientSessionTimeout);

                stopwatch.Stop();
                status.AssertSuccess();
                fileStore.QueryDirectory(out var queryDirectoryFileInformation, handle, "*", FileInformationClass.FileDirectoryInformation);

                foreach (var file in queryDirectoryFileInformation)
                {
                    if (file.FileInformationClass == FileInformationClass.FileDirectoryInformation)
                    {
                        var fileDirectoryInformation = (FileDirectoryInformation)file;
                        if (fileDirectoryInformation.FileName is "." or ".." or ".DS_Store")
                            continue;

                        if (fileDirectoryInformation.FileAttributes.HasFlag(SMBLibrary.FileAttributes.Directory))
                            Delete(smbFileSystem.Path.Combine(path, fileDirectoryInformation.FileName), true, credential);

                        smbFileSystem.File.Delete(smbFileSystem.Path.Combine(path, fileDirectoryInformation.FileName));
                    }
                }

                FileStoreUtilities.CloseFile(fileStore, ref handle);

                Delete(path, credential);
            }
            catch (Exception ex)
            {
                throw new SmbException($"Failed to Delete {path}", ex);
            }
            finally
            {
                FileStoreUtilities.CloseFile(fileStore, ref handle);
            }
        }
        else
        {
            Delete(path);
        }
    }

    public override IEnumerable<string> EnumerateDirectories(string path) => path.IsSharePath() ? EnumerateDirectories(path, "*") : base.EnumerateDirectories(path);

    public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern) => path.IsSharePath() ? EnumerateDirectories(path, searchPattern, SearchOption.TopDirectoryOnly) : base.EnumerateDirectories(path, searchPattern);

    public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) => EnumerateDirectories(path, searchPattern, searchOption, null);

    private IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption, ISmbCredential? credential)
    {
        if (!path.IsSharePath())
            return base.EnumerateDirectories(path, searchPattern, searchOption);

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed to EnumerateDirectories for {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        credential ??= smbFileSystem.CredentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed to EnumerateDirectories for {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));

        ISMBFileStore? fileStore = null;
        object? handle = null;

        try
        {
            string shareName = path.ShareName();
            string relativePath = path.ShareRelativePath();
            logger?.LogTrace("Trying to EnumerateDirectories {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);
            using var connection = SmbConnection.CreateSmbConnection(smbFileSystem.ClientFactory, ipAddress, Transport, credential, smbFileSystem.Options);
            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
            status.AssertSuccess();
            status = fileStore.CreateFile(out handle, out _, relativePath, AccessMask.GENERIC_READ, 0, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
            status.AssertSuccess();
            fileStore.QueryDirectory(out var queryDirectoryFileInformation, handle, searchPattern, FileInformationClass.FileDirectoryInformation);
            logger?.LogTrace("Found {queryDirectoryFileInformationCount} FileDirectoryInformation for {path}", queryDirectoryFileInformation.Count, path);
            var files = new List<string>();

            foreach (var file in queryDirectoryFileInformation)
            {
                if (file.FileInformationClass == FileInformationClass.FileDirectoryInformation)
                {
                    var fileDirectoryInformation = (FileDirectoryInformation)file;
                    if (fileDirectoryInformation.FileName is "." or "..")
                        continue;

                    if (fileDirectoryInformation.FileAttributes.HasFlag(SMBLibrary.FileAttributes.Directory))
                    {
                        files.Add(smbFileSystem.Path.Combine(path, fileDirectoryInformation.FileName));
                        if (searchOption == SearchOption.AllDirectories)
                            files.AddRange(EnumerateDirectories(smbFileSystem.Path.Combine(path, fileDirectoryInformation.FileName), searchPattern, searchOption, credential));
                    }
                }
            }

            return files;
        }
        catch (Exception ex)
        {
            throw new SmbException($"Failed to EnumerateDirectories for {path}", ex);
        }
        finally
        {
            FileStoreUtilities.CloseFile(fileStore, ref handle);
        }
    }

    public override IEnumerable<string> EnumerateFiles(string path) => path.IsSharePath() ? EnumerateFiles(path, "*") : base.EnumerateFiles(path);

    public override IEnumerable<string> EnumerateFiles(string path, string searchPattern) => path.IsSharePath() ? EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly) : base.EnumerateFiles(path, searchPattern);

    public override IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => EnumerateFiles(path, searchPattern, searchOption, null);

    private IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption, ISmbCredential? credential)
    {
        if (!path.IsSharePath())
            return base.EnumerateFiles(path, searchPattern, searchOption);

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed to EnumerateFiles for {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        credential ??= smbFileSystem.CredentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed to EnumerateFiles for {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string shareName = path.ShareName();
            string relativePath = path.ShareRelativePath();
            logger?.LogTrace("Trying to EnumerateFiles for {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);
            using var connection = SmbConnection.CreateSmbConnection(smbFileSystem.ClientFactory, ipAddress, Transport, credential, smbFileSystem.Options);
            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
            status.AssertSuccess();
            status = fileStore.CreateFile(out handle, out _, relativePath, AccessMask.GENERIC_READ, 0, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
            status.AssertSuccess();
            fileStore.QueryDirectory(out var queryDirectoryFileInformation, handle, searchPattern, FileInformationClass.FileDirectoryInformation);
            logger?.LogTrace("Found {queryDirectoryFileInformationCount} FileDirectoryInformation for {path}", queryDirectoryFileInformation.Count, path);

            var files = new List<string>();

            foreach (var file in queryDirectoryFileInformation)
            {
                if (file.FileInformationClass == FileInformationClass.FileDirectoryInformation)
                {
                    var fileDirectoryInformation = (FileDirectoryInformation)file;
                    if (fileDirectoryInformation.FileName is "." or ".." or ".DS_Store")
                        continue;

                    if (fileDirectoryInformation.FileAttributes.HasFlag(SMBLibrary.FileAttributes.Directory))
                    {
                        if (searchOption == SearchOption.AllDirectories)
                            files.AddRange(EnumerateFiles(smbFileSystem.Path.Combine(path, fileDirectoryInformation.FileName), searchPattern, searchOption, credential));
                    }
                    else
                    {
                        files.Add(smbFileSystem.Path.Combine(path, fileDirectoryInformation.FileName.RemoveLeadingAndTrailingSeparators()));
                    }
                }
            }
            FileStoreUtilities.CloseFile(fileStore, ref handle);

            return files;
        }
        catch (Exception ex)
        {
            throw new SmbException($"Failed to EnumerateFiles {path}", ex);
        }
        finally
        {
            FileStoreUtilities.CloseFile(fileStore, ref handle);
        }
    }

    public override IEnumerable<string> EnumerateFileSystemEntries(string path) => path.IsSharePath() ? EnumerateFileSystemEntries(path, "*") : base.EnumerateFileSystemEntries(path);

    public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern) => path.IsSharePath() ? EnumerateFileSystemEntries(path, searchPattern, SearchOption.TopDirectoryOnly) : base.EnumerateFileSystemEntries(path, searchPattern);

    public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption) => EnumerateFileSystemEntries(path, searchPattern, searchOption, null);

    private IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption, ISmbCredential? credential)
    {
        if (!path.IsSharePath())
            return base.EnumerateFileSystemEntries(path, searchPattern, searchOption);

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed to EnumerateFileSystemEntries for {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        credential ??= smbFileSystem.CredentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed to EnumerateFileSystemEntries for {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string shareName = path.ShareName();
            string relativePath = path.ShareRelativePath();
            logger?.LogTrace("Trying to EnumerateFileSystemEntries {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);
            using var connection = SmbConnection.CreateSmbConnection(smbFileSystem.ClientFactory, ipAddress, Transport, credential, smbFileSystem.Options);
            fileStore = connection.SmbClient.TreeConnect(shareName, out var ntStatus);
            ntStatus.AssertSuccess();
            ntStatus = fileStore.CreateFile(out handle, out _, relativePath, AccessMask.GENERIC_READ, 0, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
            ntStatus.AssertSuccess();
            fileStore.QueryDirectory(out var queryDirectoryFileInformation, handle, searchPattern, FileInformationClass.FileDirectoryInformation);
            logger?.LogTrace("Found {queryDirectoryFileInformation.Count} FileDirectoryInformation for {path}", queryDirectoryFileInformation.Count, path);

            var files = new List<string>();

            foreach (var file in queryDirectoryFileInformation)
            {
                if (file.FileInformationClass == FileInformationClass.FileDirectoryInformation)
                {
                    var fileDirectoryInformation = (FileDirectoryInformation)file;
                    if (fileDirectoryInformation.FileName is "." or ".." or ".DS_Store")
                        continue;

                    if (fileDirectoryInformation.FileAttributes.HasFlag(SMBLibrary.FileAttributes.Directory) && searchOption == SearchOption.AllDirectories)
                        files.AddRange(EnumerateFileSystemEntries(smbFileSystem.Path.Combine(path, fileDirectoryInformation.FileName), searchPattern, searchOption, credential));

                    files.Add(smbFileSystem.Path.Combine(path, fileDirectoryInformation.FileName));
                }
            }

            return files;
        }
        catch (Exception ex)
        {
            throw new SmbException($"Failed to EnumerateFileSystemEntries for {path}", ex);
        }
        finally
        {
            FileStoreUtilities.CloseFile(fileStore, ref handle);
        }
    }

    public override bool Exists(string path)
    {
        if (!path.IsSharePath())
            return base.Exists(path);

        // TBD: At one point, we had to check for the presence of a file with the given path to be sure, but it may or may not be necessary since Adam did some additional refactoring.
        if (smbFileSystem.File.Exists(path))
            return false;

        ISMBFileStore? fileStore = null;
        object? dirLookupHandle = null;

        try
        {
            if (!path.TryResolveHostnameFromPath(out var ipAddress))
                throw new SmbException($"Failed to determine if {path} exists", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

            string sharePath = path.SharePath();

            // SMBLibrary (or maybe some SMB servers?) don't seem to like forward slashes in the path, so we're replacing them with backslashes.
            string shareRelativePath = path[sharePath.Length..].Replace("/", "\\");

            logger?.LogTrace("Trying to determine if {{shareRelativePath: {shareRelativePath}}} exists as a dir for {{sharePath: {sharePath}}}", shareRelativePath, sharePath);

            using var smbConnection = SmbConnection.CreateSmbConnection(
                smbFileSystem.ClientFactory,
                ipAddress,
                Transport,
                smbFileSystem.CredentialProvider.GetSmbCredential(path) ?? throw new SmbException($"Failed to determine if {path} exists because there is no corresponding credential logged with the CredentialProvider."),
                smbFileSystem.Options
            );
            fileStore = smbConnection.SmbClient.TreeConnect(path.ShareName(), out var ntStatus);
            ntStatus.AssertSuccess();
            ntStatus = fileStore.CreateFile(out dirLookupHandle, out _, shareRelativePath, AccessMask.GENERIC_READ, 0, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);

            if (ntStatus.IsAbsent())
                return false;

            ntStatus.AssertSuccess();
            return true;
        }
        catch (Exception ex)
        {
            // TBD: Should we really be returning false instead of throwing?
            logger?.LogError(ex, "Failed to determine if {path} exists.", path);
            return false;
        }
        finally
        {
            FileStoreUtilities.CloseFile(fileStore, ref dirLookupHandle);
        }
    }

    public override DateTime GetCreationTime(string path) => path.IsSharePath() ? DirectoryInfoFactory.New(path).CreationTime : base.GetCreationTime(path);

    public override DateTime GetCreationTimeUtc(string path) => path.IsSharePath() ? DirectoryInfoFactory.New(path).CreationTimeUtc : base.GetCreationTimeUtc(path);

    public override string[] GetDirectories(string path) => path.IsSharePath() ? GetDirectories(path, "*") : base.GetDirectories(path);

    public override string[] GetDirectories(string path, string searchPattern) => path.IsSharePath() ? GetDirectories(path, searchPattern, SearchOption.TopDirectoryOnly) : base.GetDirectories(path, searchPattern);

    public override string[] GetDirectories(string path, string searchPattern, SearchOption searchOption) => path.IsSharePath() ? EnumerateDirectories(path, searchPattern, searchOption).ToArray() : base.GetDirectories(path, searchPattern, searchOption);

    public override string GetDirectoryRoot(string path) => path.IsSharePath() ? smbFileSystem.Path.GetPathRoot(path)! : base.GetDirectoryRoot(path);

    public override string[] GetFiles(string path) => path.IsSharePath() ? GetFiles(path, "*") : base.GetFiles(path);

    public override string[] GetFiles(string path, string searchPattern) => path.IsSharePath() ? GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly) : base.GetFiles(path, searchPattern);

    public override string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => path.IsSharePath() ? EnumerateFiles(path, searchPattern, searchOption).ToArray() : base.GetFiles(path, searchPattern, searchOption);

    public override string[] GetFileSystemEntries(string path) => path.IsSharePath() ? GetFileSystemEntries(path, "*") : base.GetFileSystemEntries(path);

    public override string[] GetFileSystemEntries(string path, string searchPattern) => path.IsSharePath() ? EnumerateFileSystemEntries(path, searchPattern).ToArray() : base.GetFileSystemEntries(path, searchPattern);

    public override DateTime GetLastAccessTime(string path) => path.IsSharePath() ? DirectoryInfoFactory.New(path).LastAccessTime : base.GetLastAccessTime(path);

    public override DateTime GetLastAccessTimeUtc(string path) => path.IsSharePath() ? DirectoryInfoFactory.New(path).LastAccessTimeUtc : base.GetLastAccessTimeUtc(path);

    public override DateTime GetLastWriteTime(string path) => path.IsSharePath() ? DirectoryInfoFactory.New(path).LastWriteTime : base.GetLastWriteTime(path);

    public override DateTime GetLastWriteTimeUtc(string path) => path.IsSharePath() ? DirectoryInfoFactory.New(path).LastWriteTimeUtc : base.GetLastWriteTimeUtc(path);

    public override IDirectoryInfo GetParent(string path) => path.IsSharePath() ? GetParent(path, null) : base.GetParent(path);

    internal IDirectoryInfo GetParent(string path, ISmbCredential? credential) => path.IsSharePath() ? DirectoryInfoFactory.New(path.GetParentPath(), credential) : base.GetParent(path);

    public override void Move(string sourceDirName, string destDirName) => Move(sourceDirName, destDirName, null, null);

    private void Move(string sourceDirName, string destDirName, ISmbCredential? sourceCredential, ISmbCredential? destinationCredential)
    {
        sourceCredential ??= smbFileSystem.CredentialProvider.GetSmbCredential(sourceDirName);
        destinationCredential ??= smbFileSystem.CredentialProvider.GetSmbCredential(destDirName);
        CreateDirectory(destDirName, destinationCredential);

        foreach (string? dir in EnumerateDirectories(sourceDirName, "*", SearchOption.TopDirectoryOnly, sourceCredential))
            Move(dir, smbFileSystem.Path.Combine(destDirName, new Uri(dir).Segments.Last()), sourceCredential, destinationCredential);

        foreach (string? file in EnumerateFiles(sourceDirName, "*", SearchOption.TopDirectoryOnly, sourceCredential))
        {
            string? destFilePath = smbFileSystem.Path.Combine(destDirName, new Uri(file).Segments.Last());
            var smbFile = (SmbFile)smbFileSystem.File;
            smbFile.Move(file, destFilePath, sourceCredential, destinationCredential);
        }
    }

    public override void SetCreationTime(string path, DateTime creationTime)
    {
        if (!path.IsSharePath())
        {
            base.SetCreationTime(path, creationTime);
            return;
        }

        var dirInfo = DirectoryInfoFactory.New(path);
        dirInfo.CreationTime = creationTime.ToUniversalTime();
        DirectoryInfoFactory.SaveDirectoryInfo((SmbDirectoryInfo)dirInfo);
    }

    public override void SetCreationTimeUtc(string path, DateTime creationTimeUtc)
    {
        if (!path.IsSharePath())
        {
            base.SetCreationTimeUtc(path, creationTimeUtc);
            return;
        }

        var dirInfo = DirectoryInfoFactory.New(path);
        dirInfo.CreationTime = creationTimeUtc;
        DirectoryInfoFactory.SaveDirectoryInfo((SmbDirectoryInfo)dirInfo);
    }

    public override void SetLastAccessTime(string path, DateTime lastAccessTime)
    {
        if (!path.IsSharePath())
        {
            base.SetLastAccessTime(path, lastAccessTime);
            return;
        }

        var dirInfo = DirectoryInfoFactory.New(path);
        dirInfo.LastAccessTime = lastAccessTime.ToUniversalTime();
        DirectoryInfoFactory.SaveDirectoryInfo((SmbDirectoryInfo)dirInfo);
    }

    public override void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)
    {
        if (!path.IsSharePath())
        {
            base.SetLastAccessTimeUtc(path, lastAccessTimeUtc);
            return;
        }

        var dirInfo = DirectoryInfoFactory.New(path);
        dirInfo.LastAccessTime = lastAccessTimeUtc;
        DirectoryInfoFactory.SaveDirectoryInfo((SmbDirectoryInfo)dirInfo);
    }

    public override void SetLastWriteTime(string path, DateTime lastWriteTime)
    {
        if (!path.IsSharePath())
        {
            base.SetLastWriteTime(path, lastWriteTime);
            return;
        }

        var dirInfo = DirectoryInfoFactory.New(path);
        dirInfo.LastWriteTime = lastWriteTime.ToUniversalTime();
        DirectoryInfoFactory.SaveDirectoryInfo((SmbDirectoryInfo)dirInfo);
    }

    public override void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)
    {
        if (!path.IsSharePath())
        {
            base.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
            return;
        }

        var dirInfo = DirectoryInfoFactory.New(path);
        dirInfo.LastWriteTime = lastWriteTimeUtc;
        DirectoryInfoFactory.SaveDirectoryInfo((SmbDirectoryInfo)dirInfo);
    }
}
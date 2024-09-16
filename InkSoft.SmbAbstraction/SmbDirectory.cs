using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using InkSoft.SmbAbstraction.Utilities;
using Microsoft.Extensions.Logging;
using SMBLibrary;
using SMBLibrary.Client;

namespace InkSoft.SmbAbstraction;

public class SmbDirectory(
    ISmbClientFactory smbClientFactory,
    ISmbCredentialProvider credentialProvider,
    IFileSystem fileSystem, uint maxBufferSize,
    ISmbFileSystemSettings? smbFileSystemSettings = null,
    ILoggerFactory? loggerFactory = null
) : DirectoryWrapper(new FileSystem()), IDirectory
{
    private readonly ILogger<SmbDirectory>? _logger = loggerFactory?.CreateLogger<SmbDirectory>();
    private readonly ISmbFileSystemSettings _smbFileSystemSettings = smbFileSystemSettings ?? new SmbFileSystemSettings();
    private SmbDirectoryInfoFactory DirectoryInfoFactory => fileSystem.DirectoryInfo as SmbDirectoryInfoFactory;

    public SMBTransportType Transport { get; set; } = SMBTransportType.DirectTCPTransport;

    public override IDirectoryInfo CreateDirectory(string path) => CreateDirectory(path, null);

    private IDirectoryInfo CreateDirectory(string path, ISmbCredential? credential)
    {
        if (!path.IsSharePath())
            return base.CreateDirectory(path);

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed to CreateDirectory {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        credential ??= credentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed to CreateDirectory {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));

        if (Exists(path))
            return DirectoryInfoFactory.New(path);

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();
            _logger?.LogTrace("Trying to CreateDirectory {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);
            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, maxBufferSize);
            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
            status.HandleStatus();

            const AccessMask c_accessMask = AccessMask.SYNCHRONIZE | AccessMask.MAXIMUM_ALLOWED;
            const ShareAccess c_shareAccess = ShareAccess.Read | ShareAccess.Write;
            const CreateDisposition c_disposition = CreateDisposition.FILE_OPEN_IF;
            const CreateOptions c_createOptions = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_DIRECTORY_FILE;

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            do
            {
                if (status == NTStatus.STATUS_PENDING)
                    _logger?.LogTrace($"STATUS_PENDING while trying to create directory {path}. {stopwatch.Elapsed.TotalSeconds}/{_smbFileSystemSettings.ClientSessionTimeout} seconds elapsed.");

                status = fileStore.CreateFile(out handle, out _, relativePath, c_accessMask, 0, c_shareAccess, c_disposition, c_createOptions, null);

                if (status == NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
                {
                    CreateDirectory(path.GetParentPath(), credential);
                    status = fileStore.CreateFile(out handle, out _, relativePath, c_accessMask, 0, c_shareAccess, c_disposition, c_createOptions, null);
                }
            } while (status == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= _smbFileSystemSettings.ClientSessionTimeout);

            stopwatch.Stop();
            status.HandleStatus();
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

    internal void Delete(string path, ISmbCredential credential)
    {
        if (!path.IsSharePath())
        {
            base.Delete(path);
            return;
        }

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
        {
            throw new SmbException($"Failed to Delete {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));
        }

        if (!Exists(path))
        {
            return;
        }

        var status = NTStatus.STATUS_SUCCESS;

        if (credential == null)
        {
            credential = credentialProvider.GetSmbCredential(path);
        }

        if (credential == null)
        {
            throw new SmbException($"Failed to Delete {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));
        }

        if (EnumerateFileSystemEntries(path).Count() > 0)
        {
            throw new SmbException($"Failed to Delete {path}", new IOException("Cannot delete directory. Directory is not empty."));
        }

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();

            _logger?.LogTrace($"Trying to Delete {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}");

            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, maxBufferSize);
            fileStore = connection.SmbClient.TreeConnect(shareName, out status);

            status.HandleStatus();

            const AccessMask c_accessMask = AccessMask.SYNCHRONIZE | AccessMask.DELETE;
            const ShareAccess c_shareAccess = ShareAccess.Delete;
            const CreateDisposition c_disposition = CreateDisposition.FILE_OPEN;
            const CreateOptions c_createOptions = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_DELETE_ON_CLOSE;

            var stopwatch = new Stopwatch();

            stopwatch.Start();
            do
            {
                if (status == NTStatus.STATUS_PENDING)
                    _logger.LogTrace($"STATUS_PENDING while trying to delete directory {path}. {stopwatch.Elapsed.TotalSeconds}/{_smbFileSystemSettings.ClientSessionTimeout} seconds elapsed.");

                status = fileStore.CreateFile(out handle, out var fileStatus, relativePath, c_accessMask, 0, c_shareAccess, c_disposition, c_createOptions, null);
            }
            while (status == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= _smbFileSystemSettings.ClientSessionTimeout);
            stopwatch.Stop();

            status.HandleStatus();

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

    public void Delete(string path, bool recursive, ISmbCredential credential)
    {
        if (!path.IsSharePath())
        {
            base.Delete(path, recursive);
            return;
        }

        if (recursive)
        {
            if (!path.TryResolveHostnameFromPath(out var ipAddress))
            {
                throw new SmbException($"Failed to Delete {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));
            }

            if (credential == null)
            {
                credential = credentialProvider.GetSmbCredential(path);
            }

            if (credential == null)
            {
                throw new SmbException($"Failed to Delete {path}", new InvalidCredentialException("Unable to find credential in SMBCredentialProvider for path: {path}"));
            }

            ISMBFileStore fileStore = null;
            object handle = null;

            try
            {
                string? shareName = path.ShareName();
                string? relativePath = path.RelativeSharePath();

                _logger?.LogTrace($"Trying to Delete {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}");

                using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, maxBufferSize);
                fileStore = connection.SmbClient.TreeConnect(shareName, out var status);

                status.HandleStatus();

                const AccessMask c_accessMask = AccessMask.SYNCHRONIZE | AccessMask.GENERIC_READ;
                const ShareAccess c_shareAccess = ShareAccess.Delete;
                const CreateDisposition c_disposition = CreateDisposition.FILE_OPEN;
                const CreateOptions c_createOptions = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_DIRECTORY_FILE;

                var stopwatch = new Stopwatch();

                stopwatch.Start();
                do
                {
                    if (status == NTStatus.STATUS_PENDING)
                        _logger.LogTrace($"STATUS_PENDING while trying to delete directory {path}. {stopwatch.Elapsed.TotalSeconds}/{_smbFileSystemSettings.ClientSessionTimeout} seconds elapsed.");

                    status = fileStore.CreateFile(out handle, out var fileStatus, relativePath, c_accessMask, 0, c_shareAccess,
                        c_disposition, c_createOptions, null);
                } while (status == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= _smbFileSystemSettings.ClientSessionTimeout);

                stopwatch.Stop();

                status.HandleStatus();

                fileStore.QueryDirectory(out var queryDirectoryFileInformation, handle, "*", FileInformationClass.FileDirectoryInformation);

                foreach (var file in queryDirectoryFileInformation)
                {
                    if (file.FileInformationClass == FileInformationClass.FileDirectoryInformation)
                    {
                        var fileDirectoryInformation = (FileDirectoryInformation)file;
                        if (fileDirectoryInformation.FileName == "."
                            || fileDirectoryInformation.FileName == ".."
                            || fileDirectoryInformation.FileName == ".DS_Store")
                        {
                            continue;
                        }
                        else if (fileDirectoryInformation.FileAttributes.HasFlag(SMBLibrary.FileAttributes.Directory))
                        {
                            Delete(fileSystem.Path.Combine(path, fileDirectoryInformation.FileName), recursive, credential);
                        }

                        fileSystem.File.Delete(fileSystem.Path.Combine(path, fileDirectoryInformation.FileName));
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

    public override IEnumerable<string> EnumerateDirectories(string path)
    {
        if (!path.IsSharePath())
        {
            return base.EnumerateDirectories(path);
        }

        return EnumerateDirectories(path, "*");
    }

    public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern)
    {
        if (!path.IsSharePath())
        {
            return base.EnumerateDirectories(path, searchPattern);
        }

        return EnumerateDirectories(path, searchPattern, SearchOption.TopDirectoryOnly);
    }

    public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) => EnumerateDirectories(path, searchPattern, searchOption, null);

    private IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption, ISmbCredential credential)
    {
        if (!path.IsSharePath())
        {
            return base.EnumerateDirectories(path, searchPattern, searchOption);
        }

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
        {
            throw new SmbException($"Failed to EnumerateDirectories for {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));
        }

        var status = NTStatus.STATUS_SUCCESS;

        if (credential == null)
        {
            credential = credentialProvider.GetSmbCredential(path);
        }

        if (credential == null)
        {
            throw new SmbException($"Failed to EnumerateDirectories for {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));
        }

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();

            _logger?.LogTrace($"Trying to EnumerateDirectories {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}");

            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, maxBufferSize);
            fileStore = connection.SmbClient.TreeConnect(shareName, out status);

            status.HandleStatus();

            status = fileStore.CreateFile(out handle, out var fileStatus, relativePath, AccessMask.GENERIC_READ, 0, ShareAccess.Read,
                CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);

            status.HandleStatus();

            fileStore.QueryDirectory(out var queryDirectoryFileInformation, handle, searchPattern, FileInformationClass.FileDirectoryInformation);

            _logger?.LogTrace($"Found {queryDirectoryFileInformation.Count} FileDirectoryInformation for {path}");

            var files = new List<string>();

            foreach (var file in queryDirectoryFileInformation)
            {
                if (file.FileInformationClass == FileInformationClass.FileDirectoryInformation)
                {
                    var fileDirectoryInformation = (FileDirectoryInformation)file;

                    if (fileDirectoryInformation.FileName == "." || fileDirectoryInformation.FileName == "..")
                    {
                        continue;
                    }

                    if (fileDirectoryInformation.FileAttributes.HasFlag(SMBLibrary.FileAttributes.Directory))
                    {
                        files.Add(fileSystem.Path.Combine(path, fileDirectoryInformation.FileName));
                        if (searchOption == SearchOption.AllDirectories)
                        {
                            files.AddRange(EnumerateDirectories(fileSystem.Path.Combine(path, fileDirectoryInformation.FileName), searchPattern, searchOption, credential));
                        }
                    }
                }
            }
            FileStoreUtilities.CloseFile(fileStore, ref handle);

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

    public override IEnumerable<string> EnumerateFiles(string path)
    {
        if (!path.IsSharePath())
        {
            return base.EnumerateFiles(path);
        }

        return EnumerateFiles(path, "*");
    }

    public override IEnumerable<string> EnumerateFiles(string path, string searchPattern)
    {
        if (!path.IsSharePath())
        {
            return base.EnumerateFiles(path, searchPattern);
        }

        return EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
    }

    public override IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => EnumerateFiles(path, searchPattern, searchOption, null);

    private IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption, ISmbCredential credential)
    {
        if (!path.IsSharePath())
        {
            return base.EnumerateFiles(path, searchPattern, searchOption);
        }

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
        {
            throw new SmbException($"Failed to EnumerateFiles for {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));
        }

        var status = NTStatus.STATUS_SUCCESS;

        if (credential == null)
        {
            credential = credentialProvider.GetSmbCredential(path);
        }

        if (credential == null)
        {
            throw new SmbException($"Failed to EnumerateFiles for {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));
        }

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();

            _logger?.LogTrace($"Trying to EnumerateFiles for {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}");

            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, maxBufferSize);
            fileStore = connection.SmbClient.TreeConnect(shareName, out status);

            status.HandleStatus();

            status = fileStore.CreateFile(out handle, out var fileStatus, relativePath, AccessMask.GENERIC_READ, 0, ShareAccess.Read,
                CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);

            status.HandleStatus();

            fileStore.QueryDirectory(out var queryDirectoryFileInformation, handle, searchPattern, FileInformationClass.FileDirectoryInformation);

            _logger?.LogTrace($"Found {queryDirectoryFileInformation.Count} FileDirectoryInformation for {path}");

            var files = new List<string>();

            foreach (var file in queryDirectoryFileInformation)
            {
                if (file.FileInformationClass == FileInformationClass.FileDirectoryInformation)
                {
                    var fileDirectoryInformation = (FileDirectoryInformation)file;
                    if (fileDirectoryInformation.FileName == "."
                        || fileDirectoryInformation.FileName == ".."
                        || fileDirectoryInformation.FileName == ".DS_Store")
                    {
                        continue;
                    }

                    if (fileDirectoryInformation.FileAttributes.HasFlag(SMBLibrary.FileAttributes.Directory))
                    {
                        if (searchOption == SearchOption.AllDirectories)
                        {
                            files.AddRange(EnumerateFiles(fileSystem.Path.Combine(path, fileDirectoryInformation.FileName), searchPattern, searchOption, credential));
                        }
                    }
                    else
                    {
                        files.Add(fileSystem.Path.Combine(path, fileDirectoryInformation.FileName.RemoveLeadingAndTrailingSeparators()));
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

    public override IEnumerable<string> EnumerateFileSystemEntries(string path)
    {
        if (!path.IsSharePath())
        {
            return base.EnumerateFileSystemEntries(path);
        }

        return EnumerateFileSystemEntries(path, "*");
    }

    public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern)
    {
        if (!path.IsSharePath())
        {
            return base.EnumerateFileSystemEntries(path, searchPattern);
        }

        return EnumerateFileSystemEntries(path, searchPattern, SearchOption.TopDirectoryOnly);
    }


    public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption) => EnumerateFileSystemEntries(path, searchPattern, searchOption, null);

    private IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption, ISmbCredential credential)
    {
        if (!path.IsSharePath())
        {
            return base.EnumerateFileSystemEntries(path, searchPattern, searchOption);
        }

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
        {
            throw new SmbException($"Failed to EnumerateFileSystemEntries for {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));
        }

        var status = NTStatus.STATUS_SUCCESS;

        if (credential == null)
        {
            credential = credentialProvider.GetSmbCredential(path);
        }

        if (credential == null)
        {
            throw new SmbException($"Failed to EnumerateFileSystemEntries for {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));
        }

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();

            _logger?.LogTrace($"Trying to EnumerateFileSystemEntries {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}");

            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, maxBufferSize);
            fileStore = connection.SmbClient.TreeConnect(shareName, out status);

            status.HandleStatus();

            status = fileStore.CreateFile(out handle, out var fileStatus, relativePath, AccessMask.GENERIC_READ, 0, ShareAccess.Read,
                CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);

            status.HandleStatus();

            fileStore.QueryDirectory(out var queryDirectoryFileInformation, handle, searchPattern, FileInformationClass.FileDirectoryInformation);

            _logger?.LogTrace($"Found {queryDirectoryFileInformation.Count} FileDirectoryInformation for {path}");

            var files = new List<string>();

            foreach (var file in queryDirectoryFileInformation)
            {
                if (file.FileInformationClass == FileInformationClass.FileDirectoryInformation)
                {
                    var fileDirectoryInformation = (FileDirectoryInformation)file;
                    if (fileDirectoryInformation.FileName == "." || fileDirectoryInformation.FileName == ".." || fileDirectoryInformation.FileName == ".DS_Store")
                    {
                        continue;
                    }


                    if (fileDirectoryInformation.FileAttributes.HasFlag(SMBLibrary.FileAttributes.Directory))
                    {
                        if (searchOption == SearchOption.AllDirectories)
                        {
                            files.AddRange(EnumerateFileSystemEntries(fileSystem.Path.Combine(path, fileDirectoryInformation.FileName), searchPattern, searchOption, credential));
                        }
                    }

                    files.Add(fileSystem.Path.Combine(path, fileDirectoryInformation.FileName));
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
        {
            return base.Exists(path);
        }

        // For some reason Directory.Exists is returning true if a file exists at that path. 
        // File.Exists works properly so as long as we check it here we are fine.
        if(fileSystem.File.Exists(path))
        {
            return false;
        }

        ISMBFileStore fileStore = null;
        object? handle = null;

        try
        {
            if (!path.TryResolveHostnameFromPath(out var ipAddress))
            {
                throw new SmbException($"Failed to determine if {path} exists", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));
            }

            var credential = credentialProvider.GetSmbCredential(path);

            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, maxBufferSize);
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();

            _logger?.LogTrace(
                $"Trying to determine if {{RelativePath: {relativePath}}} Exists for {{ShareName: {shareName}}}");

            if (string.IsNullOrEmpty(relativePath))
                return true;

            string? parentFullPath = path.GetParentPath();
            string? parentPath = parentFullPath.RelativeSharePath();
            string? directoryName = path.GetLastPathSegment().RemoveLeadingAndTrailingSeparators();

            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);

            status.HandleStatus();

            status = fileStore.CreateFile(out handle, out var fileStatus, parentPath, AccessMask.GENERIC_READ, 0, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);

            status.HandleStatus();

            fileStore.QueryDirectory(out var queryDirectoryFileInformation, handle, string.IsNullOrEmpty(directoryName) ? "*" : directoryName, FileInformationClass.FileDirectoryInformation);

            foreach (var file in queryDirectoryFileInformation)
            {
                if (file.FileInformationClass == FileInformationClass.FileDirectoryInformation)
                {
                    var fileDirectoryInformation = (FileDirectoryInformation)file;
                    if (fileDirectoryInformation.FileName == directoryName)
                        return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogTrace(ex, $"Failed to determine if {path} exists.");
            return false;
        }
        finally
        {
            FileStoreUtilities.CloseFile(fileStore, ref handle);
        }
    }

    public override DateTime GetCreationTime(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetCreationTime(path);
        }

        return DirectoryInfoFactory.New(path).CreationTime;
    }

    public override DateTime GetCreationTimeUtc(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetCreationTimeUtc(path);
        }

        return DirectoryInfoFactory.New(path).CreationTimeUtc;
    }

    public override string GetCurrentDirectory() => base.GetCurrentDirectory();

    public override string[] GetDirectories(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetDirectories(path);
        }

        return GetDirectories(path, "*");
    }

    public override string[] GetDirectories(string path, string searchPattern)
    {
        if (!path.IsSharePath())
        {
            return base.GetDirectories(path, searchPattern);
        }

        return GetDirectories(path, searchPattern, SearchOption.TopDirectoryOnly);
    }

    public override string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
    {
        if (!path.IsSharePath())
        {
            return base.GetDirectories(path, searchPattern, searchOption);
        }

        return EnumerateDirectories(path, searchPattern, searchOption).ToArray();
    }

    public override string GetDirectoryRoot(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetDirectoryRoot(path);
        }

        return fileSystem.Path.GetPathRoot(path);
    }

    public override string[] GetFiles(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetFiles(path);
        }

        return GetFiles(path, "*");
    }

    public override string[] GetFiles(string path, string searchPattern)
    {
        if (!path.IsSharePath())
        {
            return base.GetFiles(path, searchPattern);
        }

        return GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
    }

    public override string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        if (!path.IsSharePath())
        {
            return base.GetFiles(path, searchPattern, searchOption);
        }

        return EnumerateFiles(path, searchPattern, searchOption).ToArray();
    }

    public override string[] GetFileSystemEntries(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetFileSystemEntries(path);
        }

        return GetFileSystemEntries(path, "*");
    }

    public override string[] GetFileSystemEntries(string path, string searchPattern)
    {
        if (!path.IsSharePath())
        {
            return base.GetFileSystemEntries(path, searchPattern);
        }

        return EnumerateFileSystemEntries(path, searchPattern).ToArray();
    }

    public override DateTime GetLastAccessTime(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetLastAccessTime(path);
        }

        return DirectoryInfoFactory.New(path).LastAccessTime;
    }

    public override DateTime GetLastAccessTimeUtc(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetLastAccessTimeUtc(path);
        }

        return DirectoryInfoFactory.New(path).LastAccessTimeUtc;
    }

    public override DateTime GetLastWriteTime(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetLastWriteTime(path);
        }

        return DirectoryInfoFactory.New(path).LastWriteTime;
    }

    public override DateTime GetLastWriteTimeUtc(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetLastWriteTimeUtc(path);
        }

        return DirectoryInfoFactory.New(path).LastWriteTimeUtc;
    }

    public override IDirectoryInfo GetParent(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetParent(path);
        }

        return GetParent(path, null);
    }

    internal IDirectoryInfo GetParent(string path, ISmbCredential credential)
    {
        if (!path.IsSharePath())
        {
            return base.GetParent(path);
        }

        return DirectoryInfoFactory.New(path.GetParentPath(), credential);
    }

    public override void Move(string sourceDirName, string destDirName) => Move(sourceDirName, destDirName, null, null);

    private void Move(string sourceDirName, string destDirName, ISmbCredential sourceCredential, ISmbCredential destinationCredential)
    {
        if (sourceCredential == null)
        {
            sourceCredential = credentialProvider.GetSmbCredential(sourceDirName);
        }

        if (destinationCredential == null)
        {
            destinationCredential = credentialProvider.GetSmbCredential(destDirName);
        }

        CreateDirectory(destDirName, destinationCredential);

        var dirs = EnumerateDirectories(sourceDirName, "*", SearchOption.TopDirectoryOnly, sourceCredential);

        foreach (string? dir in dirs)
        {
            string? destDirPath = fileSystem.Path.Combine(destDirName, new Uri(dir).Segments.Last());
            Move(dir, destDirPath, sourceCredential, destinationCredential);
        }

        var files = EnumerateFiles(sourceDirName, "*", SearchOption.TopDirectoryOnly, sourceCredential);

        foreach (string? file in files)
        {
            string? destFilePath = fileSystem.Path.Combine(destDirName, new Uri(file).Segments.Last());
            var smbFile = fileSystem.File as SmbFile;
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

    public override void SetCurrentDirectory(string path)
    {
        if (!path.IsSharePath())
        {
            base.SetCurrentDirectory(path);
            return;
        }

        throw new NotImplementedException();
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
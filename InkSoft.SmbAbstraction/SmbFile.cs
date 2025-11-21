using Microsoft.Extensions.Logging;
using SMBLibrary;
using SMBLibrary.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;

namespace InkSoft.SmbAbstraction;

/// <inheritdoc />
#if FEATURE_SERIALIZABLE
[Serializable]
#endif
public partial class SmbFile(
    SmbFileSystem smbFileSystem,
    ILogger<SmbFile>? logger
): FileWrapper(smbFileSystem.NonSmbFileSystem)
{
    /// <inheritdoc cref="SmbFileSystem"/>
    public new IFileSystem FileSystem => smbFileSystem;

    private SmbFileInfoFactory FileInfoFactory => (SmbFileInfoFactory)smbFileSystem.FileInfo;

    public SMBTransportType Transport { get; set; } = SMBTransportType.DirectTCPTransport;

    public override void AppendAllLines(string path, IEnumerable<string> contents)
    {
        if (!path.IsSharePath())
        {
            base.AppendAllLines(path, contents);
            return;
        }

        using Stream s = OpenSmb(path, FileMode.OpenOrCreate, FileAccess.Write);
        s.Seek(0, SeekOrigin.End);
        using var sw = new StreamWriter(s);
        sw.Write(contents);
    }

    public override void AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding)
    {
        if (!path.IsSharePath())
        {
            base.AppendAllLines(path, contents, encoding);
            return;
        }

        using Stream s = OpenSmb(path, FileMode.OpenOrCreate, FileAccess.Write);
        s.Seek(0, SeekOrigin.End);
        using var sw = new StreamWriter(s, encoding);
        sw.Write(contents);
    }

    public override void AppendAllText(string path, string contents)
    {
        if (!path.IsSharePath())
        {
            base.AppendAllText(path, contents);
            return;
        }

        using Stream s = OpenSmb(path, FileMode.OpenOrCreate, FileAccess.Write);
        s.Seek(0, SeekOrigin.End);
        using var sw = new StreamWriter(s);
        sw.Write(contents);
    }

    public override void AppendAllText(string path, string contents, Encoding encoding)
    {
        if (!path.IsSharePath())
        {
            base.AppendAllText(path, contents, encoding);
            return;
        }

        using Stream s = OpenSmb(path, FileMode.OpenOrCreate, FileAccess.Write);
        s.Seek(0, SeekOrigin.End);
        using var sw = new StreamWriter(s, encoding);
        sw.Write(contents);
    }

    public override StreamWriter AppendText(string path)
    {
        if (!path.IsSharePath())
            return base.AppendText(path);

        Stream s = OpenSmb(path, FileMode.OpenOrCreate, FileAccess.Write);
        s.Seek(0, SeekOrigin.End);
        return new(s);
    }

    public override void Copy(string sourceFileName, string destFileName)
    {
        using Stream sourceStream = sourceFileName.IsSharePath() ? OpenSmb(sourceFileName, FileMode.Open, FileAccess.Read) : base.OpenRead(sourceFileName);
        using Stream destStream = destFileName.IsSharePath() ? OpenSmb(destFileName, FileMode.Create, FileAccess.Write) : base.Open(destFileName, FileMode.Create, FileAccess.Write);
        sourceStream.CopyTo(destStream, Convert.ToInt32(smbFileSystem.Options.MaxBufferSize));
    }

    public override void Copy(string sourceFileName, string destFileName, bool overwrite)
    {
        if (overwrite && Exists(destFileName))
            Delete(destFileName);

        Copy(sourceFileName, destFileName);
    }

    public override FileSystemStream Create(string path) => path.IsSharePath() ? OpenSmb(path, FileMode.Create) : base.Create(path);

    public override FileSystemStream Create(string path, int bufferSize) => path.IsSharePath() ? OpenSmb(path, FileMode.Create) : base.Create(path, bufferSize);

    public override FileSystemStream Create(string path, int bufferSize, FileOptions options) => path.IsSharePath() ? OpenSmb(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, options) : base.Create(path, bufferSize, options);

    public override StreamWriter CreateText(string path) => path.IsSharePath() ? new(OpenSmb(path, FileMode.Create, FileAccess.Write)) : base.CreateText(path);

    public override void Delete(string path)
    {
        if (!path.IsSharePath())
        {
            base.Delete(path);
            return;
        }

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed to Delete {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        var credential = smbFileSystem.CredentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed to Delete {path}", new InvalidCredentialException($"Unable to find credential in smbFileSystem.CredentialProvider for path: {path}"));

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

            const AccessMask c_accessMask = AccessMask.SYNCHRONIZE | AccessMask.DELETE;
            const ShareAccess c_shareAccess = ShareAccess.Read | ShareAccess.Delete;
            const CreateDisposition c_disposition = CreateDisposition.FILE_OPEN;
            const CreateOptions c_createOptions = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_DELETE_ON_CLOSE;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            do
            {
                if(status == NTStatus.STATUS_PENDING)
                    logger?.LogTrace("STATUS_PENDING while trying to delete file {path}. {stopwatchElapsedTotalSeconds}/{smbFileSystemOptionsClientSessionTimeout} seconds elapsed.", path, stopwatch.Elapsed.TotalSeconds, smbFileSystem.Options.ClientSessionTimeout);

                status = fileStore.CreateFile(out handle, out _, relativePath, c_accessMask, 0, c_shareAccess, c_disposition, c_createOptions, null);
            }
            while (status == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= smbFileSystem.Options.ClientSessionTimeout);

            stopwatch.Stop();
            status.AssertSuccess();

            // There should be a separate option to delete, but it doesn't seem to exist in the library we are using, so this should work for now. Really hacky though.
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

    public override bool Exists([NotNullWhen(true)] string? path)
    {
        if (!path.IsSharePath())
            return base.Exists(path);

        string? fileName = smbFileSystem.Path.GetFileName(path);

        // If the path ends with a slash or a dot, it's not a valid filename.
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        ISMBFileStore fileStore = null;
        object? fileLookupHandle = null;

        try
        {
            if (!path.TryResolveHostnameFromPath(out var ipAddress))
                throw new SmbException($"Failed to determine if {path} exists", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

            string sharePath = path.SharePath();

            // SMBLibrary (or maybe some SMB servers?) don't seem to like forward slashes in the path, so we're replacing them with backslashes.
            string shareRelativePath = path[sharePath.Length..].Replace("/", "\\");
            logger?.LogTrace("Trying to determine if {{shareRelativePath: {shareRelativePath}}} exists as a file for {{sharePath: {sharePath}}}", shareRelativePath, sharePath);

            using var smbConnection = SmbConnection.CreateSmbConnection(
                smbFileSystem.ClientFactory,
                ipAddress,
                Transport,
                smbFileSystem.CredentialProvider.GetSmbCredential(path) ?? throw new SmbException($"Failed to determine if {path} exists because there is no corresponding credential logged with the smbFileSystem.CredentialProvider."),
                smbFileSystem.Options
            );
            fileStore = smbConnection.SmbClient.TreeConnect(path.ShareName(), out var ntStatus);
            ntStatus.AssertSuccess();
            ntStatus = fileStore.CreateFile(
                out fileLookupHandle,
                out _,
                shareRelativePath,
                AccessMask.SYNCHRONIZE | AccessMask.GENERIC_READ,
                0,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_NON_DIRECTORY_FILE,
                null
            );

            if (ntStatus.IsAbsent() || ntStatus == NTStatus.STATUS_FILE_IS_A_DIRECTORY)
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
            FileStoreUtilities.CloseFile(fileStore, ref fileLookupHandle);
        }
    }

    public override System.IO.FileAttributes GetAttributes(string path) => path.IsSharePath() ? FileInfoFactory.New(path).Attributes : base.GetAttributes(path);

    public override DateTime GetCreationTime(string path) => path.IsSharePath() ? FileInfoFactory.New(path).CreationTime : base.GetCreationTime(path);

    public override DateTime GetCreationTimeUtc(string path) => path.IsSharePath() ? FileInfoFactory.New(path).CreationTimeUtc : base.GetCreationTimeUtc(path);

    public override DateTime GetLastAccessTime(string path) => path.IsSharePath() ? FileInfoFactory.New(path).LastAccessTime : base.GetLastAccessTime(path);

    public override DateTime GetLastAccessTimeUtc(string path) => path.IsSharePath() ? FileInfoFactory.New(path).LastAccessTimeUtc : base.GetLastAccessTimeUtc(path);

    public override DateTime GetLastWriteTime(string path) => path.IsSharePath() ? FileInfoFactory.New(path).LastAccessTimeUtc : base.GetLastWriteTime(path);

    public override DateTime GetLastWriteTimeUtc(string path) => path.IsSharePath() ? FileInfoFactory.New(path).LastAccessTimeUtc : base.GetLastWriteTimeUtc(path);

    public override void Move(string sourceFileName, string destFileName)
    {
        if (!sourceFileName.IsSharePath() && !destFileName.IsSharePath())
            base.Move(sourceFileName, destFileName);
        else
            Move(sourceFileName, destFileName, null, null);
    }

    internal void Move(string sourceFileName, string destFileName, ISmbCredential? sourceCredential, ISmbCredential? destinationCredential)
    {
        using (Stream sourceStream = sourceFileName.IsSharePath() ? OpenSmb(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.None, sourceCredential) : base.OpenRead(sourceFileName))
        {
            using Stream destStream = destFileName.IsSharePath() ? OpenSmb(destFileName, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.None, destinationCredential) : base.Open(destFileName, FileMode.Create, FileAccess.Write);
            sourceStream.CopyTo(destStream, Convert.ToInt32(smbFileSystem.Options.MaxBufferSize));
        }

        smbFileSystem.File.Delete(sourceFileName);
    }

    public override FileSystemStream Open(string path, FileMode mode) => path.IsSharePath() ? OpenSmb(path, mode) : base.Open(path, mode);

    public override FileSystemStream Open(string path, FileMode mode, FileAccess access) => path.IsSharePath() ? OpenSmb(path, mode, access) : base.Open(path, mode, access);

    public override FileSystemStream Open(string path, FileMode mode, FileAccess access, FileShare share) => path.IsSharePath() ? OpenSmb(path, mode, access, share) : base.Open(path, mode, access, share);

    /// <summary>
    /// Core file read/write method. Not available via IFileSystem interface. <paramref name="path"/> must be a validated as an SMB share path before calling this method.
    /// </summary>
    public FileSystemStream OpenSmb(string path, FileMode mode, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.None, FileOptions fileOptions = FileOptions.None, ISmbCredential? credential = null)
    {
        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed to Open {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        CreateOptions createOptions;
        switch (fileOptions)
        {
            case FileOptions.DeleteOnClose:
                createOptions = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_DELETE_ON_CLOSE;
                break;
            case FileOptions.RandomAccess:
                createOptions = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_RANDOM_ACCESS;
                break;
            case FileOptions.SequentialScan:
                createOptions = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_SEQUENTIAL_ONLY;
                break;
            case FileOptions.WriteThrough:
                createOptions = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_WRITE_THROUGH;
                break;
            case FileOptions.None:
            // Encrypted and Asynchronous are not supported unless one of the original authors is missing something.
            case FileOptions.Encrypted:
            case FileOptions.Asynchronous:
            default:
                createOptions = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_NON_DIRECTORY_FILE;
                break;
        }

        AccessMask accessMask;
        ShareAccess shareAccess;
        switch (access)
        {
            case FileAccess.Read:
                accessMask = AccessMask.SYNCHRONIZE | AccessMask.GENERIC_READ;
                shareAccess = ShareAccess.Read;
                break;
            case FileAccess.Write:
                accessMask = AccessMask.SYNCHRONIZE | AccessMask.GENERIC_WRITE;
                shareAccess = ShareAccess.Write;
                break;
            case FileAccess.ReadWrite:
                accessMask = AccessMask.SYNCHRONIZE | AccessMask.GENERIC_READ | AccessMask.GENERIC_WRITE;
                shareAccess = ShareAccess.Read | ShareAccess.Write;
                break;
            default:
                accessMask = AccessMask.MAXIMUM_ALLOWED;
                shareAccess = ShareAccess.None;
                break;
        }

        credential ??= smbFileSystem.CredentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed to Open {path}", new InvalidCredentialException($"Unable to find credential in smbFileSystem.CredentialProvider for path: {path}"));

        SmbConnection smbConnection = null;
        try
        {
            smbConnection = SmbConnection.CreateSmbConnectionForStream(smbFileSystem.ClientFactory, ipAddress, Transport, credential, smbFileSystem.Options);
            string shareName = path.ShareName();
            string relativePath = path.ShareRelativePath();
            var fileStore = smbConnection.SmbClient.TreeConnect(shareName, out var ntStatus);
            ntStatus.AssertSuccess();

            var createDisposition = mode switch
            {
                FileMode.Create => CreateDisposition.FILE_OVERWRITE_IF,
                FileMode.CreateNew => CreateDisposition.FILE_CREATE,
                FileMode.OpenOrCreate => CreateDisposition.FILE_OPEN_IF,
                FileMode.Open => CreateDisposition.FILE_OPEN,
                FileMode.Truncate => CreateDisposition.FILE_OVERWRITE_IF,
                FileMode.Append => CreateDisposition.FILE_OPEN_IF,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };

            object handle;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            do
            {
                if (ntStatus == NTStatus.STATUS_PENDING)
                    logger?.LogTrace("STATUS_PENDING while trying to open file {path}. {stopwatchElapsedTotalSeconds}/{smbFileSystemOptionsClientSessionTimeout} seconds elapsed.", path, stopwatch.Elapsed.TotalSeconds, smbFileSystem.Options.ClientSessionTimeout);

                ntStatus = fileStore.CreateFile(out handle, out _, relativePath, accessMask, 0, shareAccess, createDisposition, createOptions, null);
            }
            while (ntStatus == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= smbFileSystem.Options.ClientSessionTimeout);
            stopwatch.Stop();

            ntStatus.AssertSuccess();
            FileInformation fileInfo;

            stopwatch.Reset();
            stopwatch.Start();
            do {
                ntStatus = fileStore.GetFileInformation(out fileInfo, handle, FileInformationClass.FileStandardInformation);
            } while (ntStatus == NTStatus.STATUS_NETWORK_NAME_DELETED && stopwatch.Elapsed.TotalSeconds <= smbFileSystem.Options.ClientSessionTimeout);
            stopwatch.Stop();

            ntStatus.AssertSuccess();
            var s = new SmbFsStream(fileStore, handle, smbConnection, ((FileStandardInformation)fileInfo).EndOfFile, smbFileSystem.Options, path, false);

            if (mode == FileMode.Append)
                s.Seek(0, SeekOrigin.End);

            return s;
        }
        catch (Exception ex)
        {
            // Dispose connection if fail to open stream
            smbConnection?.Dispose();
            throw new SmbException($"Failed to Open {path}", ex);
        }
    }

    public override FileSystemStream OpenRead(string path) => path.IsSharePath() ? OpenSmb(path, FileMode.Open, FileAccess.Read) : base.OpenRead(path);

    public override StreamReader OpenText(string path) => path.IsSharePath() ? new(OpenSmb(path, FileMode.Open, FileAccess.Read)) : base.OpenText(path);

    public override FileSystemStream OpenWrite(string path) => path.IsSharePath() ? OpenSmb(path, FileMode.OpenOrCreate, FileAccess.Write) : base.OpenWrite(path);

    public override byte[] ReadAllBytes(string path)
    {
        if (!path.IsSharePath())
            return base.ReadAllBytes(path);

        using var ms = new MemoryStream();

        using (Stream s = OpenSmb(path, FileMode.Open, FileAccess.Read))
            s.CopyTo(ms, Convert.ToInt32(smbFileSystem.Options.MaxBufferSize));

        return ms.ToArray();
    }

    public override string[] ReadAllLines(string path) => path.IsSharePath() ? ReadLines(path).ToArray() : base.ReadAllLines(path);

    public override string[] ReadAllLines(string path, Encoding encoding) => path.IsSharePath() ? ReadLines(path, encoding).ToArray() : base.ReadAllLines(path, encoding);

    public override string ReadAllText(string path)
    {
        if (!path.IsSharePath())
            return base.ReadAllText(path);

        using var sr = new StreamReader(OpenSmb(path, FileMode.Open, FileAccess.Read));
        return sr.ReadToEnd();
    }

    public override string ReadAllText(string path, Encoding encoding)
    {
        if (!path.IsSharePath())
            return base.ReadAllText(path, encoding);

        using var sr = new StreamReader(OpenSmb(path, FileMode.Open, FileAccess.Read), encoding);
        return sr.ReadToEnd();
    }

    public override IEnumerable<string> ReadLines(string path)
    {
        if (!path.IsSharePath())
            return base.ReadLines(path);

        var lines = new List<string>();
        using (var sr = new StreamReader(OpenSmb(path, FileMode.Open, FileAccess.Read)))
        {
            while (sr.ReadLine() is { } line)
                lines.Add(line);
        }

        return lines.ToArray();
    }

    public override IEnumerable<string> ReadLines(string path, Encoding encoding)
    {
        if (!path.IsSharePath())
            return base.ReadLines(path, encoding);

        var lines = new List<string>();
        using (var sr = new StreamReader(OpenSmb(path, FileMode.Open, FileAccess.Read), encoding))
        {
            while (sr.ReadLine() is { } line)
                lines.Add(line);
        }

        return lines.ToArray();
    }

    public override void SetAttributes(string path, System.IO.FileAttributes fileAttributes)
    {
        if (!path.IsSharePath())
        {
            base.SetAttributes(path, fileAttributes);
            return;
        }

        throw new NotSupportedException();
    }

    public override void SetCreationTime(string path, DateTime creationTime)
    {
        if (!path.IsSharePath())
        {
            base.SetCreationTime(path, creationTime);
            return;
        }

        var fileInfo = FileInfoFactory.New(path);
        fileInfo.CreationTime = creationTime;
        FileInfoFactory.SaveFileInfo((SmbFileInfo)fileInfo);
    }

    public override void SetCreationTimeUtc(string path, DateTime creationTimeUtc)
    {
        if (!path.IsSharePath())
        {
            base.SetCreationTimeUtc(path, creationTimeUtc);
            return;
        }

        var fileInfo = FileInfoFactory.New(path);
        fileInfo.CreationTimeUtc = creationTimeUtc.ToUniversalTime();
        FileInfoFactory.SaveFileInfo((SmbFileInfo)fileInfo);
    }

    public override void SetLastAccessTime(string path, DateTime lastAccessTime)
    {
        if (!path.IsSharePath())
        {
            base.SetLastAccessTime(path, lastAccessTime);
            return;
        }

        var fileInfo = FileInfoFactory.New(path);
        fileInfo.LastAccessTime = lastAccessTime;
        FileInfoFactory.SaveFileInfo((SmbFileInfo)fileInfo);
    }

    public override void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)
    {
        if (!path.IsSharePath())
        {
            base.SetLastAccessTimeUtc(path, lastAccessTimeUtc);
            return;
        }

        var fileInfo = FileInfoFactory.New(path);
        fileInfo.LastAccessTime = lastAccessTimeUtc.ToUniversalTime();
        FileInfoFactory.SaveFileInfo((SmbFileInfo)fileInfo);
    }

    public override void SetLastWriteTime(string path, DateTime lastWriteTime)
    {
        if (!path.IsSharePath())
        {
            base.SetLastWriteTime(path, lastWriteTime);
            return;
        }

        var fileInfo = FileInfoFactory.New(path);
        fileInfo.LastWriteTime = lastWriteTime;
        FileInfoFactory.SaveFileInfo((SmbFileInfo)fileInfo);
    }

    public override void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)
    {
        if (!path.IsSharePath())
        {
            base.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
            return;
        }

        var fileInfo = FileInfoFactory.New(path);
        fileInfo.LastWriteTime = lastWriteTimeUtc.ToUniversalTime();
        FileInfoFactory.SaveFileInfo((SmbFileInfo)fileInfo);
    }

    public override void WriteAllBytes(string path, byte[] bytes)
    {
        if (!path.IsSharePath())
        {
            base.WriteAllBytes(path, bytes);
            return;
        }

        using var sr = OpenSmb(path, FileMode.OpenOrCreate, FileAccess.Write);
        sr.Write(bytes, 0, bytes.Length);
    }

    public override void WriteAllLines(string path, IEnumerable<string> contents)
    {
        if (!path.IsSharePath())
        {
            base.WriteAllLines(path, contents);
            return;
        }

        WriteAllLines(path, contents.ToArray());
    }

    public override void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding)
    {
        if (!path.IsSharePath())
        {
            base.WriteAllLines(path, contents, encoding);
            return;
        }

        WriteAllLines(path, contents.ToArray(), encoding);
    }

    public override void WriteAllLines(string path, string[] contents)
    {
        if (!path.IsSharePath())
        {
            base.WriteAllLines(path, contents);
            return;
        }

        using var sr = new StreamWriter(OpenSmb(path, FileMode.Create, FileAccess.Write));
        sr.Write(contents);
    }

    public override void WriteAllLines(string path, string[] contents, Encoding encoding)
    {
        if (!path.IsSharePath())
        {
            base.WriteAllLines(path, contents, encoding);
            return;
        }

        using var sr = new StreamWriter(OpenSmb(path, FileMode.Create, FileAccess.Write), encoding);
        sr.Write(contents);
    }

    public override void WriteAllText(string path, string contents)
    {
        if (!path.IsSharePath())
        {
            base.WriteAllText(path, contents);
            return;
        }

        using var sw = new StreamWriter(OpenSmb(path, FileMode.Create, FileAccess.Write));
        sw.Write(contents);
    }

    public override void WriteAllText(string path, string contents, Encoding encoding)
    {
        if (!path.IsSharePath())
        {
            base.WriteAllText(path, contents, encoding);
            return;
        }

        using var sw = new StreamWriter(OpenSmb(path, FileMode.Create, FileAccess.Write), encoding);
        sw.Write(contents);
    }
}
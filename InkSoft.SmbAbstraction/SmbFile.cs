using InkSoft.SmbAbstraction.Utilities;
using Microsoft.Extensions.Logging;
using SMBLibrary;
using SMBLibrary.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    IFileSystem fileSystem,
    ISmbClientFactory smbClientFactory,
    ISmbCredentialProvider credentialProvider,
    SmbFileSystemOptions? smbFileSystemOptions = null,
    ILoggerFactory? loggerFactory = null
) : FileWrapper(new FileSystem())
{
    /// <inheritdoc cref="SmbFileSystem"/>
    public new IFileSystem FileSystem => fileSystem;

    private readonly ILogger<SmbFile>? _logger = loggerFactory?.CreateLogger<SmbFile>();
    
    private readonly SmbFileSystemOptions _smbFileSystemOptions = smbFileSystemOptions ??= new();
    
    private SmbFileInfoFactory FileInfoFactory => (SmbFileInfoFactory)FileSystem.FileInfo;

    public SMBTransportType Transport { get; set; } = SMBTransportType.DirectTCPTransport;

    public override void AppendAllLines(string path, IEnumerable<string> contents)
    {
        if (!path.IsSharePath())
        {
            base.AppendAllLines(path, contents);
            return;
        }

        using Stream s = OpenWrite(path);
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

        using Stream s = OpenWrite(path);
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

        using Stream s = OpenWrite(path);
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

        using Stream s = OpenWrite(path);
        s.Seek(0, SeekOrigin.End);
        using var sw = new StreamWriter(s, encoding);
        sw.Write(contents);
    }

    public override StreamWriter AppendText(string path)
    {
        if (!path.IsSharePath())
        {
            return base.AppendText(path);
        }

        Stream s = OpenWrite(path);
        s.Seek(0, SeekOrigin.End);
        return new(s);
    }

    public override void Copy(string sourceFileName, string destFileName)
    {
        using Stream sourceStream = OpenRead(sourceFileName);
        using Stream destStream = Open(destFileName, FileMode.Create, FileAccess.Write);
        sourceStream.CopyTo(destStream, Convert.ToInt32(_smbFileSystemOptions.MaxBufferSize));
    }

    public override void Copy(string sourceFileName, string destFileName, bool overwrite)
    {
        if (overwrite && Exists(destFileName))
            Delete(destFileName);

        Copy(sourceFileName, destFileName);
    }

    public override FileSystemStream Create(string path) => !path.IsSharePath() ? base.Create(path) : Open(path, FileMode.Create, FileAccess.ReadWrite);

    public override FileSystemStream Create(string path, int bufferSize) => path.IsSharePath()
        ? throw new NotImplementedException()
        // return new BufferedStream(Open(path, FileMode.Create, FileAccess.ReadWrite), bufferSize);
        : base.Create(path, bufferSize);

    public override FileSystemStream Create(string path, int bufferSize, FileOptions options)
    {
        if (!path.IsSharePath())
            return base.Create(path, bufferSize, options);

        throw new NotImplementedException();
        // return new BufferedStream(Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, options, null), bufferSize);
    }

    public override StreamWriter CreateText(string path) => path.IsSharePath() ? new(Open(path, FileMode.Create, FileAccess.Write)) : base.CreateText(path);

    public override void Delete(string path)
    {
        if (!path.IsSharePath())
        {
            base.Delete(path);
            return;
        }

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed to Delete {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        var credential = credentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed to Delete {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();

            _logger?.LogTrace("Trying to Delete {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);

            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, _smbFileSystemOptions);
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
                    _logger?.LogTrace("STATUS_PENDING while trying to delete file {path}. {stopwatchElapsedTotalSeconds}/{smbFileSystemOptionsClientSessionTimeout} seconds elapsed.", path, stopwatch.Elapsed.TotalSeconds, _smbFileSystemOptions.ClientSessionTimeout);

                status = fileStore.CreateFile(out handle, out _, relativePath, c_accessMask, 0, c_shareAccess, c_disposition, c_createOptions, null);
            }
            while (status == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= _smbFileSystemOptions.ClientSessionTimeout);

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

    public override bool Exists(string path)
    {
        if (!path.IsSharePath())
            return base.Exists(path);

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            if (!path.TryResolveHostnameFromPath(out var ipAddress))
                throw new SmbException($"Failed to determine if {path} exists", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

            var credential = credentialProvider.GetSmbCredential(path);
            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, _smbFileSystemOptions);
            string? shareName = path.ShareName();
            // SMBLibrary (or maybe some SMB servers?) don't seem to like forward slashes in the path, so we're replacing them with backslashes.
            string? directoryPath = FileSystem.Path.GetDirectoryName(path).Replace(path.SharePath(), "").RemoveLeadingAndTrailingSeparators().Replace('/','\\');
            string? fileName = FileSystem.Path.GetFileName(path);
            _logger?.LogTrace("Trying to determine if {{DirectoryPath: {directoryPath}}} {{FileName: {fileName}}} Exists for {{ShareName: {shareName}}}", directoryPath, fileName, shareName);
            fileStore = connection.SmbClient.TreeConnect(shareName, out var ntStatus);
            ntStatus.AssertSuccess();
            
            ntStatus = fileStore.CreateFile(
                out handle,
                out _,
                directoryPath,
                AccessMask.SYNCHRONIZE | AccessMask.GENERIC_READ,
                0,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_DIRECTORY_FILE,
                null
            );
            
            if (ntStatus.IsAbsent())
                return false;

            ntStatus.AssertSuccess();
            fileStore.QueryDirectory(out var queryDirectoryFileInformation, handle, string.IsNullOrEmpty(fileName) ? "*" : fileName, FileInformationClass.FileDirectoryInformation);
            bool exists = queryDirectoryFileInformation.Any(file => file.FileInformationClass == FileInformationClass.FileDirectoryInformation && ((FileDirectoryInformation)file).FileName == fileName);
            return exists;
        }
        catch (Exception ex)
        {
            // TBD: Should we really be returning false instead of throwing?
            _logger?.LogError(ex, "Failed to determine if {path} exists.", path);
            return false;
        }
        finally
        {
            FileStoreUtilities.CloseFile(fileStore, ref handle);
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
        using (Stream sourceStream = OpenRead(sourceFileName, sourceCredential))
        {
            using Stream destStream = Open(destFileName, FileMode.Create, FileAccess.Write, destinationCredential);
            sourceStream.CopyTo(destStream, Convert.ToInt32(_smbFileSystemOptions.MaxBufferSize));
        }

        FileSystem.File.Delete(sourceFileName);
    }

    public override FileSystemStream Open(string path, FileMode mode) => path.IsSharePath() ? Open(path, mode, FileAccess.ReadWrite, null) : base.Open(path, mode);

    public override FileSystemStream Open(string path, FileMode mode, FileAccess access) => Open(path, mode, access, null);

    private FileSystemStream Open(string path, FileMode mode, FileAccess access, ISmbCredential? credential) => path.IsSharePath() ? Open(path, mode, access, FileShare.None, credential) : base.Open(path, mode, access);

    public override FileSystemStream Open(string path, FileMode mode, FileAccess access, FileShare share) => Open(path, mode, access, share, null);

    private FileSystemStream Open(string path, FileMode mode, FileAccess access, FileShare share, ISmbCredential? credential)
    {
        if (!path.IsSharePath())
            return base.Open(path, mode, access, share);

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed to Open {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        // TODO: Why are we switching on FileOptions.None, making most of this unreachable?
        CreateOptions createOptions;
        switch (FileOptions.None)
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

        var accessMask = AccessMask.MAXIMUM_ALLOWED;
        var shareAccess = ShareAccess.None;
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
        }

        credential ??= credentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed to Open {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));

        SmbConnection smbConnection = null;
        try
        {
            smbConnection = SmbConnection.CreateSmbConnectionForStream(smbClientFactory, ipAddress, Transport, credential, _smbFileSystemOptions);
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();
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
                    _logger?.LogTrace("STATUS_PENDING while trying to open file {path}. {stopwatchElapsedTotalSeconds}/{smbFileSystemOptionsClientSessionTimeout} seconds elapsed.", path, stopwatch.Elapsed.TotalSeconds, _smbFileSystemOptions.ClientSessionTimeout);

                ntStatus = fileStore.CreateFile(out handle, out _, relativePath, accessMask, 0, shareAccess, createDisposition, createOptions, null);
            }
            while (ntStatus == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= _smbFileSystemOptions.ClientSessionTimeout);
            stopwatch.Stop();

            ntStatus.AssertSuccess();
            FileInformation fileInfo;
                
            stopwatch.Reset();
            stopwatch.Start();
            do
            {
                ntStatus = fileStore.GetFileInformation(out fileInfo, handle, FileInformationClass.FileStandardInformation);
            }
            while (ntStatus == NTStatus.STATUS_NETWORK_NAME_DELETED && stopwatch.Elapsed.TotalSeconds <= _smbFileSystemOptions.ClientSessionTimeout);
            stopwatch.Stop();
                
            ntStatus.AssertSuccess();
            var s = new SmbFsStream(fileStore, handle, smbConnection, ((FileStandardInformation)fileInfo).EndOfFile, _smbFileSystemOptions, path, false);

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

    public override FileSystemStream OpenRead(string path) => OpenRead(path, null);

    private FileSystemStream OpenRead(string path, ISmbCredential? credential) => path.IsSharePath() ? Open(path, FileMode.Open, FileAccess.Read, credential) : base.OpenRead(path);

    public override StreamReader OpenText(string path) => path.IsSharePath() ? new(OpenRead(path)) : base.OpenText(path);

    public override FileSystemStream OpenWrite(string path) => path.IsSharePath() ? Open(path, FileMode.OpenOrCreate, FileAccess.Write, null) : base.OpenWrite(path);

    public override byte[] ReadAllBytes(string path)
    {
        if (!path.IsSharePath())
            return base.ReadAllBytes(path);

        using var ms = new MemoryStream();
        using (Stream s = OpenRead(path))
        {
            s.CopyTo(ms, Convert.ToInt32(_smbFileSystemOptions.MaxBufferSize));
        }
        return ms.ToArray();
    }

    public override string[] ReadAllLines(string path) => path.IsSharePath() ? ReadLines(path).ToArray() : base.ReadAllLines(path);

    public override string[] ReadAllLines(string path, Encoding encoding) => path.IsSharePath() ? ReadLines(path, encoding).ToArray() : base.ReadAllLines(path, encoding);

    public override string ReadAllText(string path)
    {
        if (!path.IsSharePath())
            return base.ReadAllText(path);

        using var sr = new StreamReader(OpenRead(path));
        return sr.ReadToEnd();
    }

    public override string ReadAllText(string path, Encoding encoding)
    {
        if (!path.IsSharePath())
            return base.ReadAllText(path, encoding);

        using var sr = new StreamReader(OpenRead(path), encoding);
        return sr.ReadToEnd();
    }

    public override IEnumerable<string> ReadLines(string path)
    {
        if (!path.IsSharePath())
            return base.ReadLines(path);

        var lines = new List<string>();
        using (var sr = new StreamReader(OpenRead(path)))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                lines.Add(line);
            }
        }

        return lines.ToArray();
    }

    public override IEnumerable<string> ReadLines(string path, Encoding encoding)
    {
        if (!path.IsSharePath())
            return base.ReadLines(path, encoding);

        var lines = new List<string>();
        using (var sr = new StreamReader(OpenRead(path), encoding))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                lines.Add(line);
            }
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

        using var sr = new StreamWriter(OpenWrite(path));
        sr.Write(bytes);
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

        using var sr = new StreamWriter(Open(path, FileMode.Create, FileAccess.Write));
        sr.Write(contents);
    }

    public override void WriteAllLines(string path, string[] contents, Encoding encoding)
    {
        if (!path.IsSharePath())
        {
            base.WriteAllLines(path, contents, encoding);
            return;
        }

        using var sr = new StreamWriter(Open(path, FileMode.Create, FileAccess.Write), encoding);
        sr.Write(contents);
    }

    public override void WriteAllText(string path, string contents)
    {
        if (!path.IsSharePath())
        {
            base.WriteAllText(path, contents);
            return;
        }

        using var sw = new StreamWriter(Open(path, FileMode.Create, FileAccess.Write));
        sw.Write(contents);
    }

    public override void WriteAllText(string path, string contents, Encoding encoding)
    {
        if (!path.IsSharePath())
        {
            base.WriteAllText(path, contents, encoding);
            return;
        }

        using var sw = new StreamWriter(Open(path, FileMode.Create, FileAccess.Write), encoding);
        sw.Write(contents);
    }
}
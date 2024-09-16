using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InkSoft.SmbAbstraction.Utilities;
using Microsoft.Extensions.Logging;
using SMBLibrary;
using SMBLibrary.Client;

namespace InkSoft.SmbAbstraction;

/// <inheritdoc />
#if FEATURE_SERIALIZABLE
[Serializable]
#endif
public partial class SmbFile(
    ISmbClientFactory smbClientFactory,
    ISmbCredentialProvider credentialProvider,
    IFileSystem fileSystem,
    uint maxBufferSize = 65536,
    ISmbFileSystemSettings? smbFileSystemSettings = null,
    ILoggerFactory? loggerFactory = null
) : FileWrapper(fileSystem)
{
    private readonly ILogger<SmbFile>? _logger = loggerFactory?.CreateLogger<SmbFile>();
    private readonly ISmbFileSystemSettings _smbFileSystemSettings = smbFileSystemSettings ?? new SmbFileSystemSettings();
    private readonly ISmbClientFactory _smbClientFactory = smbClientFactory;
    private readonly ISmbCredentialProvider _credentialProvider = credentialProvider;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly uint _maxBufferSize = maxBufferSize;
    private SmbFileInfoFactory FileInfoFactory => (SmbFileInfoFactory)_fileSystem.FileInfo;

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
        using Stream destStream = OpenWrite(destFileName);
        sourceStream.CopyTo(destStream, Convert.ToInt32(_maxBufferSize));
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
        {
            return base.Create(path, bufferSize, options);
        }

        throw new NotImplementedException();
        // return new BufferedStream(Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, options, null), bufferSize);
    }

    public override StreamWriter CreateText(string path) => path.IsSharePath() ? new(OpenWrite(path)) : base.CreateText(path);

    public override void Delete(string path)
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

        var credential = _credentialProvider.GetSmbCredential(path);

        if (credential == null)
        {
            throw new SmbException($"Failed to Delete {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));
        }

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();

            _logger?.LogTrace($"Trying to Delete {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}");

            using var connection = SmbConnection.CreateSmbConnection(_smbClientFactory, ipAddress, Transport, credential, _maxBufferSize);
            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);

            status.HandleStatus();

            const AccessMask c_accessMask = AccessMask.SYNCHRONIZE | AccessMask.DELETE;
            const ShareAccess c_shareAccess = ShareAccess.Read | ShareAccess.Delete;
            const CreateDisposition c_disposition = CreateDisposition.FILE_OPEN;
            const CreateOptions c_createOptions = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_DELETE_ON_CLOSE;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            do
            {
                if(status == NTStatus.STATUS_PENDING)
                    _logger.LogTrace($"STATUS_PENDING while trying to delete file {path}. {stopwatch.Elapsed.TotalSeconds}/{_smbFileSystemSettings.ClientSessionTimeout} seconds elapsed.");

                status = fileStore.CreateFile(out handle, out var fileStatus, relativePath, c_accessMask, 0, c_shareAccess, c_disposition, c_createOptions, null);
            }
            while (status == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= _smbFileSystemSettings.ClientSessionTimeout);

            stopwatch.Stop();
            status.HandleStatus();

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
        {
            return base.Exists(path);
        }

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            if (!path.TryResolveHostnameFromPath(out var ipAddress))
                throw new SmbException($"Failed to determine if {path} exists", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

            var credential = _credentialProvider.GetSmbCredential(path);

            using var connection = SmbConnection.CreateSmbConnection(_smbClientFactory, ipAddress, Transport, credential, _maxBufferSize);
            string? shareName = path.ShareName();
            string? directoryPath = _fileSystem.Path.GetDirectoryName(path).Replace(path.SharePath(), "").RemoveLeadingAndTrailingSeparators();
            string? fileName = _fileSystem.Path.GetFileName(path);
            _logger?.LogTrace($"Trying to determine if {{DirectoryPath: {directoryPath}}} {{FileName: {fileName}}} Exists for {{ShareName: {shareName}}}");
            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
            status.HandleStatus();

            const AccessMask c_accessMask = AccessMask.SYNCHRONIZE | AccessMask.GENERIC_READ;
            const ShareAccess c_shareAccess = ShareAccess.Read;
            const CreateDisposition c_disposition = CreateDisposition.FILE_OPEN;
            const CreateOptions c_createOptions = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_DIRECTORY_FILE;

            status = fileStore.CreateFile(out handle, out _, directoryPath, c_accessMask, 0, c_shareAccess, c_disposition, c_createOptions, null);
            status.HandleStatus();
            fileStore.QueryDirectory(out var queryDirectoryFileInformation, handle, string.IsNullOrEmpty(fileName) ? "*" : fileName, FileInformationClass.FileDirectoryInformation);

            foreach (var file in queryDirectoryFileInformation)
            {
                if (file.FileInformationClass == FileInformationClass.FileDirectoryInformation)
                {
                    var fileDirectoryInformation = (FileDirectoryInformation)file;
                    if (fileDirectoryInformation.FileName == fileName)
                    {
                        FileStoreUtilities.CloseFile(fileStore, ref handle);
                        return true;
                    }
                }
            }

            FileStoreUtilities.CloseFile(fileStore, ref handle);

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

    public override System.IO.FileAttributes GetAttributes(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetAttributes(path);
        }

        var fileInfo = FileInfoFactory.New(path);

        return fileInfo.Attributes;
    }

    public override DateTime GetCreationTime(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetCreationTime(path);
        }

        var fileInfo = FileInfoFactory.New(path);

        return fileInfo.CreationTime;
    }

    public override DateTime GetCreationTimeUtc(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetCreationTimeUtc(path);
        }

        var fileInfo = FileInfoFactory.New(path);

        return fileInfo.CreationTimeUtc;
    }

    public override DateTime GetLastAccessTime(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetLastAccessTime(path);
        }

        var fileInfo = FileInfoFactory.New(path);

        return fileInfo.LastAccessTime;
    }

    public override DateTime GetLastAccessTimeUtc(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetLastAccessTimeUtc(path);
        }

        var fileInfo = FileInfoFactory.New(path);

        return fileInfo.LastAccessTimeUtc;
    }

    public override DateTime GetLastWriteTime(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetLastWriteTime(path);
        }

        var fileInfo = FileInfoFactory.New(path);

        return fileInfo.LastAccessTimeUtc;
    }

    public override DateTime GetLastWriteTimeUtc(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetLastWriteTimeUtc(path);
        }

        var fileInfo = FileInfoFactory.New(path);

        return fileInfo.LastAccessTimeUtc;
    }

    public override void Move(string sourceFileName, string destFileName)
    {
        if (!sourceFileName.IsSharePath() && !destFileName.IsSharePath())
        {
            base.Move(sourceFileName, destFileName);
        }
        else
        {
            Move(sourceFileName, destFileName, null, null);
        }
    }

    internal void Move(string sourceFileName, string destFileName, ISmbCredential sourceCredential, ISmbCredential destinationCredential)
    {
        using (Stream sourceStream = OpenRead(sourceFileName, sourceCredential))
        {
            using (Stream destStream = OpenWrite(destFileName, destinationCredential))
            {
                sourceStream.CopyTo(destStream, Convert.ToInt32(_maxBufferSize));
            }
        }

        _fileSystem.File.Delete(sourceFileName);
    }

    public override FileSystemStream Open(string path, FileMode mode) => Open(path, mode, null);

    private FileSystemStream Open(string path, FileMode mode, ISmbCredential? credential) => path.IsSharePath() ? Open(path, mode, FileAccess.ReadWrite, credential) : base.Open(path, mode);

    public override FileSystemStream Open(string path, FileMode mode, FileAccess access) => Open(path, mode, access, null);

    private FileSystemStream Open(string path, FileMode mode, FileAccess access, ISmbCredential? credential) => path.IsSharePath() ? Open(path, mode, access, FileShare.None, credential) : base.Open(path, mode, access);

    public override FileSystemStream Open(string path, FileMode mode, FileAccess access, FileShare share) => Open(path, mode, access, share, null);

    private FileSystemStream Open(string path, FileMode mode, FileAccess access, FileShare share, ISmbCredential? credential)
    {
        if (!path.IsSharePath())
            return base.Open(path, mode, access, share);

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed to Open {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        var accessMask = AccessMask.MAXIMUM_ALLOWED;
        var shareAccess = ShareAccess.None;
        var disposition = CreateDisposition.FILE_OPEN;
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

        credential ??= _credentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed to Open {path}", new InvalidCredentialException($"Unable to find credential in SMBCredentialProvider for path: {path}"));

        SmbConnection connection = null;
        try
        {
            connection = SmbConnection.CreateSmbConnectionForStream(_smbClientFactory, ipAddress, Transport, credential, _maxBufferSize);
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();
            var fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
            status.HandleStatus();

            switch (mode)
            {
                case FileMode.Create:
                    disposition = CreateDisposition.FILE_OVERWRITE_IF;
                    break;
                case FileMode.CreateNew:
                    disposition = CreateDisposition.FILE_CREATE;
                    break;
                case FileMode.Open:
                    disposition = CreateDisposition.FILE_OPEN;
                    break;
                case FileMode.OpenOrCreate:
                    disposition = CreateDisposition.FILE_OPEN_IF;
                    break;
            }

            object handle;
            var stopwatch = new Stopwatch();
                
            stopwatch.Start();
            do
            {
                if (status == NTStatus.STATUS_PENDING)
                    _logger.LogTrace($"STATUS_PENDING while trying to open file {path}. {stopwatch.Elapsed.TotalSeconds}/{_smbFileSystemSettings.ClientSessionTimeout} seconds elapsed.");

                status = fileStore.CreateFile(out handle, out _, relativePath, accessMask, 0, shareAccess,
                    disposition, createOptions, null);
            }
            while (status == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= _smbFileSystemSettings.ClientSessionTimeout);
            stopwatch.Stop();

            status.HandleStatus();

            FileInformation fileInfo;
                
            stopwatch.Reset();
            stopwatch.Start();
            do
            {
                status = fileStore.GetFileInformation(out fileInfo, handle, FileInformationClass.FileStandardInformation);
            }
            while (status == NTStatus.STATUS_NETWORK_NAME_DELETED && stopwatch.Elapsed.TotalSeconds <= _smbFileSystemSettings.ClientSessionTimeout);
            stopwatch.Stop();
                
            status.HandleStatus();

            var fileStandardInfo = (FileStandardInformation)fileInfo;

            var s = new SmbFsStream(fileStore, handle, connection, fileStandardInfo.EndOfFile, _smbFileSystemSettings, path, false);

            if (mode == FileMode.Append)
            {
                s.Seek(0, SeekOrigin.End);
            }

            return s;
        }
        catch (Exception ex)
        {
            // Dispose connection if fail to open stream
            connection?.Dispose();
            throw new SmbException($"Failed to Open {path}", ex);
        }
    }

    public override FileSystemStream OpenRead(string path) => OpenRead(path, null);

    private FileSystemStream OpenRead(string path, ISmbCredential? credential) => path.IsSharePath() ? Open(path, FileMode.Open, FileAccess.Read, credential) : base.OpenRead(path);

    public override StreamReader OpenText(string path) => path.IsSharePath() ? new(OpenRead(path)) : base.OpenText(path);

    public override FileSystemStream OpenWrite(string path) => OpenWrite(path, null);

    private FileSystemStream OpenWrite(string path, ISmbCredential? credential) => path.IsSharePath() ? Open(path, FileMode.OpenOrCreate, FileAccess.Write, credential) : base.OpenWrite(path);

    public override byte[] ReadAllBytes(string path)
    {
        if (!path.IsSharePath())
        {
            return base.ReadAllBytes(path);
        }

        using var ms = new MemoryStream();
        using (Stream s = OpenRead(path))
        {
            s.CopyTo(ms, Convert.ToInt32(_maxBufferSize));
        }
        return ms.ToArray();
    }

    public override string[] ReadAllLines(string path)
    {
        if (!path.IsSharePath())
        {
            return base.ReadAllLines(path);
        }

        return ReadLines(path).ToArray();
    }

    public override string[] ReadAllLines(string path, Encoding encoding)
    {
        if (!path.IsSharePath())
        {
            return base.ReadAllLines(path, encoding);
        }

        return ReadLines(path, encoding).ToArray();
    }

    public override string ReadAllText(string path)
    {
        if (!path.IsSharePath())
        {
            return base.ReadAllText(path);
        }

        using var sr = new StreamReader(OpenRead(path));
        return sr.ReadToEnd();
    }

    public override string ReadAllText(string path, Encoding encoding)
    {
        if (!path.IsSharePath())
        {
            return base.ReadAllText(path, encoding);
        }

        using var sr = new StreamReader(OpenRead(path), encoding);
        return sr.ReadToEnd();
    }

    public override IEnumerable<string> ReadLines(string path)
    {
        if (!path.IsSharePath())
        {
            return base.ReadLines(path);
        }

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
        {
            return base.ReadLines(path, encoding);
        }

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

        using var sr = new StreamWriter(OpenWrite(path));
        sr.Write(contents);
    }

    public override void WriteAllLines(string path, string[] contents, Encoding encoding)
    {
        if (!path.IsSharePath())
        {
            base.WriteAllLines(path, contents, encoding);
            return;
        }

        using var sr = new StreamWriter(OpenWrite(path), encoding);
        sr.Write(contents);
    }

    public override void WriteAllText(string path, string contents)
    {
        if (!path.IsSharePath())
        {
            base.WriteAllText(path, contents);
            return;
        }

        using var sw = new StreamWriter(OpenWrite(path));
        sw.Write(contents);
    }

    public override void WriteAllText(string path, string contents, Encoding encoding)
    {
        if (!path.IsSharePath())
        {
            base.WriteAllText(path, contents, encoding);
            return;
        }

        using var sw = new StreamWriter(OpenWrite(path), encoding);
        sw.Write(contents);
    }
}
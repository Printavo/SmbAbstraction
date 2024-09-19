using System;
using System.IO.Abstractions;
using SMBLibrary;
using System.IO;

namespace InkSoft.SmbAbstraction;

/// <remarks>
/// TBD: Why do we create a new FileSystem to the base constructor instead of passing the fileSystem parameter?
/// </remarks>
public class SmbFileInfo(IFileSystem fileSystem, string path) : FileInfoWrapper(new FileSystem(), new(path)), IFileInfo
{
    private SmbFile File => (SmbFile)_fileSystem.File;
    
    private SmbFileInfoFactory FileInfoFactory => (SmbFileInfoFactory)_fileSystem.FileInfo;
    
    private SmbDirectoryInfoFactory DirInfoFactory => (SmbDirectoryInfoFactory)_fileSystem.DirectoryInfo;
    
    private readonly IFileSystem _fileSystem = fileSystem;

    internal SmbFileInfo(IFileSystem fileSystem, FileInfo fileInfo) : this(fileSystem, fileInfo.FullName)
    {
        _creationTime = fileInfo.CreationTime;
        _creationTimeUtc = fileInfo.CreationTimeUtc;
        _lastAccessTime = fileInfo.LastAccessTime;
        _lastAccessTimeUtc = fileInfo.LastAccessTimeUtc;
        _lastWriteTime = fileInfo.LastWriteTime;
        _lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        _attributes = fileInfo.Attributes;
        _directory = DirInfoFactory.New(fileInfo.Directory.FullName);
        _directoryName = fileInfo.DirectoryName;
        _exists = fileInfo.Exists;
        _isReadOnly = fileInfo.IsReadOnly;
        _length = fileInfo.Length;
    }

    internal SmbFileInfo(IFileSystem fileSystem, string path, FileBasicInformation fileBasicInformation, FileStandardInformation fileStandardInformation, ISmbCredential credential) : this(fileSystem, path)
    {
        if (fileBasicInformation.CreationTime.Time.HasValue)
        {
            _creationTime = fileBasicInformation.CreationTime.Time.Value;
            _creationTimeUtc = CreationTime.ToUniversalTime();
        }
        if (fileBasicInformation.LastAccessTime.Time.HasValue)
        {
            _lastAccessTime = fileBasicInformation.LastAccessTime.Time.Value;
            _lastAccessTimeUtc = LastAccessTime.ToUniversalTime();
        }
        if (fileBasicInformation.LastWriteTime.Time.HasValue)
        {
            _lastWriteTime = fileBasicInformation.LastWriteTime.Time.Value;
            _lastWriteTimeUtc = LastWriteTime.ToUniversalTime();
        }

        _attributes = (System.IO.FileAttributes)fileBasicInformation.FileAttributes;
        string? parentPath = _fileSystem.Path.GetDirectoryName(path);

        _directory = DirInfoFactory.New(parentPath, credential);
        _directoryName = parentPath;
        _exists = File.Exists(path);
        _isReadOnly = fileBasicInformation.FileAttributes.HasFlag(SMBLibrary.FileAttributes.ReadOnly);
        _length = fileStandardInformation.EndOfFile;
    }

    private IDirectoryInfo _directory;
    private string _directoryName;
    private bool _isReadOnly;
    private long _length;
    private System.IO.FileAttributes _attributes;
    private DateTime _creationTime;
    private DateTime _creationTimeUtc;
    private bool _exists;
    private string _fullName = path;
    private DateTime _lastAccessTime;
    private DateTime _lastAccessTimeUtc;
    private DateTime _lastWriteTime;
    private DateTime _lastWriteTimeUtc;

    public override IDirectoryInfo Directory => _directory;
    public override string DirectoryName => _directoryName;
    public override bool IsReadOnly => _isReadOnly;
    public override long Length => _length;
    public override System.IO.FileAttributes Attributes => _attributes;
    public sealed override DateTime CreationTime { get => _creationTime; set => _creationTime = value; }
    public override DateTime CreationTimeUtc { get => _creationTimeUtc; set => _creationTimeUtc = value; }
    public override bool Exists => _exists;
    public override string FullName => _fullName;
    public sealed override DateTime LastAccessTime { get => _lastAccessTime; set => _lastAccessTime = value; }
    public override DateTime LastAccessTimeUtc { get => _lastAccessTimeUtc; set => _lastAccessTimeUtc = value; }
    public sealed override DateTime LastWriteTime { get => _lastWriteTime; set => _lastWriteTime = value; }
    public override DateTime LastWriteTimeUtc { get => _lastWriteTimeUtc; set => _lastWriteTimeUtc = value; }

    public override StreamWriter AppendText() => File.AppendText(FullName);

    public override IFileInfo CopyTo(string destFileName)
    {
        File.Copy(FullName, destFileName);
        return FileInfoFactory.New(destFileName);
    }

    public override IFileInfo CopyTo(string destFileName, bool overwrite)
    {
        File.Copy(FullName, destFileName, overwrite);
        return FileInfoFactory.New(destFileName);
    }

    public override FileSystemStream Create()
    {
        var stream = File.Create(FullName);
        _exists = true;
        return stream;
    }

    public override StreamWriter CreateText()
    {
        var streamWriter = File.CreateText(FullName);
        _exists = true;
        return streamWriter;
    }

    public override void Delete()
    {
        File.Delete(FullName);
        _exists = false;
    }

    public override void MoveTo(string destFileName) => File.Move(FullName, destFileName);

    public override FileSystemStream OpenRead()
    {
        var stream = File.OpenRead(FullName);
        _exists = true;
        return stream;
    }

    public override FileSystemStream Open(FileMode mode)
    {
        var stream = File.Open(FullName, mode);
        _exists = true;
        return stream;
    }

    public override FileSystemStream Open(FileMode mode, FileAccess access)
    {
        var stream = File.Open(FullName, mode, access);
        _exists = true;
        return stream;
    }

    public override FileSystemStream Open(FileMode mode, FileAccess access, FileShare share)
    {
        var stream = File.Open(FullName, mode, access, share);
        _exists = true;
        return stream;
    }

    public override StreamReader OpenText()
    {
        var streamReader = File.OpenText(FullName);
        _exists = true;
        return streamReader;
    }

    public override FileSystemStream OpenWrite()
    {
        var stream = File.OpenWrite(FullName);
        _exists = true;
        return stream;
    }

    public override void Refresh()
    {
        var fileInfo = FileInfoFactory.New(FullName);

        _directory = fileInfo.Directory;
        _directoryName = fileInfo.DirectoryName;
        _isReadOnly = fileInfo.IsReadOnly;
        _length = fileInfo.Length;
        _attributes = fileInfo.Attributes;
        _creationTime = fileInfo.CreationTime;
        _creationTimeUtc = fileInfo.CreationTimeUtc;
        _exists = fileInfo.Exists;
        _fullName = fileInfo.FullName;
        _lastAccessTime = fileInfo.LastAccessTime;
        _lastAccessTimeUtc = fileInfo.LastAccessTimeUtc;
        _lastWriteTime = fileInfo.LastWriteTime;
        _lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
    }

    internal FileInformation ToSmbFileInformation(ISmbCredential? credential = null)
    {
        var fileBasicInformation = new FileBasicInformation();
        fileBasicInformation.CreationTime.Time = CreationTime;
        fileBasicInformation.LastAccessTime.Time = LastAccessTime;
        fileBasicInformation.LastWriteTime.Time = LastWriteTime;
        fileBasicInformation.FileAttributes = (SMBLibrary.FileAttributes)Attributes;

        if (IsReadOnly)
            fileBasicInformation.FileAttributes |= SMBLibrary.FileAttributes.ReadOnly;
        else
            fileBasicInformation.FileAttributes &= SMBLibrary.FileAttributes.ReadOnly;

        return fileBasicInformation;
    }

    public override void Decrypt()
    {
        if (!FullName.IsSharePath())
            base.Decrypt();

        throw new NotImplementedException();
    }

    public override void Encrypt()
    {
        if(!FullName.IsSharePath())
            base.Encrypt();

        throw new NotImplementedException();
    }

    public override IFileInfo Replace(string destinationFilePath, string destinationBackupFilePath) => Replace(destinationFilePath, destinationBackupFilePath, false);

    public override IFileInfo Replace(string destinationFilePath, string destinationBackupFilePath, bool ignoreMetadataErrors)
    {
        if (string.IsNullOrEmpty(destinationFilePath))
            throw new ArgumentNullException(nameof(destinationFilePath));

        if(destinationBackupFilePath == string.Empty)
        {
            // https://docs.microsoft.com/en-us/dotnet/api/system.io.fileinfo.replace?view=netcore-3.1
            throw new ArgumentNullException(nameof(destinationBackupFilePath), "Destination backup path cannot be empty. Pass null if you do not want to create backup of file being replaced.");
        }

        string? path = FullName;
            
        if (!path.IsSharePath() && !destinationFilePath.IsSharePath())
            return base.Replace(destinationFilePath, destinationBackupFilePath, ignoreMetadataErrors);

        // Check if destination file exists. Throw if it doesn't.
        if (!File.Exists(destinationFilePath))
            throw new FileNotFoundException($"Destination file {destinationFilePath} not found.");

        // If backupPath is specified, delete the backup file if it exits. Then, copy destinationFile to backupPath.
        if (!string.IsNullOrEmpty(destinationBackupFilePath))
        {
            if(File.Exists(destinationBackupFilePath))
                File.Delete(destinationBackupFilePath);

            File.Copy(destinationFilePath, destinationBackupFilePath);
        }

        // Copy and overwrite destinationFile with current file. Then, delete original file.
        File.Copy(path, destinationFilePath, overwrite: true);
        File.Delete(path);
            
        var replacedFile = FileInfoFactory.New(destinationFilePath);
        return replacedFile;
    }
}
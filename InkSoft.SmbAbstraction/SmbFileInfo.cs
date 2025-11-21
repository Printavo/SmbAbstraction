using System;
using System.IO.Abstractions;
using SMBLibrary;
using System.IO;

namespace InkSoft.SmbAbstraction;

/// <remarks>
/// TBD: Why do we create a new FileSystem to the base constructor instead of passing the fileSystem parameter?
/// </remarks>
public class SmbFileInfo(SmbFileSystem smbFileSystem, string path): FileInfoWrapper(smbFileSystem.NonSmbFileSystem, new(path)), IFileInfo
{
    private SmbFile SmbFile => (SmbFile)smbFileSystem.File;

    private SmbFileInfoFactory FileInfoFactory => (SmbFileInfoFactory)smbFileSystem.FileInfo;

    private SmbDirectoryInfoFactory DirInfoFactory => (SmbDirectoryInfoFactory)smbFileSystem.DirectoryInfo;

    internal SmbFileInfo(SmbFileSystem smbFileSystem, FileInfo fileInfo) : this(smbFileSystem, fileInfo.FullName)
    {
        CreationTime = fileInfo.CreationTime;
        _creationTimeUtc = fileInfo.CreationTimeUtc;
        LastAccessTime = fileInfo.LastAccessTime;
        _lastAccessTimeUtc = fileInfo.LastAccessTimeUtc;
        LastWriteTime = fileInfo.LastWriteTime;
        _lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        _attributes = fileInfo.Attributes;

        if (fileInfo.Directory != null)
            _directory = DirInfoFactory.New(fileInfo.Directory.FullName);

        _directoryName = fileInfo.DirectoryName;
        _exists = fileInfo.Exists;
        _isReadOnly = fileInfo.IsReadOnly;
        _length = fileInfo.Length;
    }

    internal SmbFileInfo(SmbFileSystem smbFileSystem, string path, FileBasicInformation fileBasicInformation, FileStandardInformation fileStandardInformation, ISmbCredential credential): this(smbFileSystem, path)
    {
        if (fileBasicInformation.CreationTime.Time.HasValue)
        {
            CreationTime = fileBasicInformation.CreationTime.Time.Value;
            _creationTimeUtc = CreationTime.ToUniversalTime();
        }

        if (fileBasicInformation.LastAccessTime.Time.HasValue)
        {
            LastAccessTime = fileBasicInformation.LastAccessTime.Time.Value;
            _lastAccessTimeUtc = LastAccessTime.ToUniversalTime();
        }

        if (fileBasicInformation.LastWriteTime.Time.HasValue)
        {
            LastWriteTime = fileBasicInformation.LastWriteTime.Time.Value;
            _lastWriteTimeUtc = LastWriteTime.ToUniversalTime();
        }

        _attributes = (System.IO.FileAttributes)fileBasicInformation.FileAttributes;
        _directoryName = smbFileSystem.Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(_directoryName))
            _directory = DirInfoFactory.New(_directoryName, credential);

        _exists = SmbFile.Exists(path);
        _isReadOnly = fileBasicInformation.FileAttributes.HasFlag(SMBLibrary.FileAttributes.ReadOnly);
        _length = fileStandardInformation.EndOfFile;
    }

    private IDirectoryInfo? _directory;
    private string? _directoryName;
    private bool _isReadOnly;
    private long _length;
    private System.IO.FileAttributes _attributes;
    private DateTime _creationTimeUtc;
    private bool _exists;
    private string _fullName = path;
    private DateTime _lastAccessTimeUtc;
    private DateTime _lastWriteTimeUtc;

    public override IDirectoryInfo? Directory => _directory;
    public override string? DirectoryName => _directoryName;
    public override bool IsReadOnly => _isReadOnly;
    public override long Length => _length;
    public override System.IO.FileAttributes Attributes => _attributes;
    public sealed override DateTime CreationTime { get; set; }

    public override DateTime CreationTimeUtc { get => _creationTimeUtc; set => _creationTimeUtc = value; }
    public override bool Exists => _exists;
    public override string FullName => _fullName;
    public sealed override DateTime LastAccessTime { get; set; }

    public override DateTime LastAccessTimeUtc { get => _lastAccessTimeUtc; set => _lastAccessTimeUtc = value; }
    public sealed override DateTime LastWriteTime { get; set; }

    public override DateTime LastWriteTimeUtc { get => _lastWriteTimeUtc; set => _lastWriteTimeUtc = value; }

    public override StreamWriter AppendText() => SmbFile.AppendText(FullName);

    public override IFileInfo CopyTo(string destFileName)
    {
        SmbFile.Copy(FullName, destFileName);
        return FileInfoFactory.New(destFileName);
    }

    public override IFileInfo CopyTo(string destFileName, bool overwrite)
    {
        SmbFile.Copy(FullName, destFileName, overwrite);
        return FileInfoFactory.New(destFileName);
    }

    public override FileSystemStream Create()
    {
        var stream = FullName.IsSharePath() ? SmbFile.OpenSmb(FullName, FileMode.Create) : base.Create();
        _exists = true;
        return stream;
    }

    public override StreamWriter CreateText()
    {
        var streamWriter = FullName.IsSharePath() ? new(SmbFile.OpenSmb(FullName, FileMode.Create, FileAccess.Write)) : base.CreateText();
        _exists = true;
        return streamWriter;
    }

    public override void Delete()
    {
        SmbFile.Delete(FullName);
        _exists = false;
    }

    public override void MoveTo(string destFileName) => SmbFile.Move(FullName, destFileName);

    public override FileSystemStream OpenRead()
    {
        var stream = FullName.IsSharePath() ? SmbFile.OpenSmb(FullName, FileMode.Open, FileAccess.Read) : base.OpenRead();
        _exists = true;
        return stream;
    }

    public override FileSystemStream Open(FileMode mode)
    {
        var stream = FullName.IsSharePath() ? SmbFile.OpenSmb(FullName, mode) : base.Open(mode);
        _exists = true;
        return stream;
    }

    public override FileSystemStream Open(FileMode mode, FileAccess access)
    {
        var stream = FullName.IsSharePath() ? SmbFile.OpenSmb(FullName, mode, access) : base.Open(mode, access);
        _exists = true;
        return stream;
    }

    public override FileSystemStream Open(FileMode mode, FileAccess access, FileShare share)
    {
        var stream = FullName.IsSharePath() ? SmbFile.OpenSmb(FullName, mode, access, share) : base.Open(mode, access, share);
        _exists = true;
        return stream;
    }

    public override StreamReader OpenText()
    {
        var streamReader = FullName.IsSharePath() ? new(SmbFile.OpenSmb(FullName, FileMode.Open, FileAccess.Read)) : base.OpenText();
        _exists = true;
        return streamReader;
    }

    public override FileSystemStream OpenWrite()
    {
        var stream = FullName.IsSharePath() ? SmbFile.OpenSmb(FullName, FileMode.OpenOrCreate, FileAccess.Write) : base.OpenWrite();
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
        CreationTime = fileInfo.CreationTime;
        _creationTimeUtc = fileInfo.CreationTimeUtc;
        _exists = fileInfo.Exists;
        _fullName = fileInfo.FullName;
        LastAccessTime = fileInfo.LastAccessTime;
        _lastAccessTimeUtc = fileInfo.LastAccessTimeUtc;
        LastWriteTime = fileInfo.LastWriteTime;
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
        if (!SmbFile.Exists(destinationFilePath))
            throw new FileNotFoundException($"Destination file {destinationFilePath} not found.");

        // If backupPath is specified, delete the backup file if it exits. Then, copy destinationFile to backupPath.
        if (!string.IsNullOrEmpty(destinationBackupFilePath))
        {
            if(SmbFile.Exists(destinationBackupFilePath))
                SmbFile.Delete(destinationBackupFilePath);

            SmbFile.Copy(destinationFilePath, destinationBackupFilePath);
        }

        // Copy and overwrite destinationFile with current file. Then, delete original file.
        SmbFile.Copy(path, destinationFilePath, overwrite: true);
        SmbFile.Delete(path);

        var replacedFile = FileInfoFactory.New(destinationFilePath);
        return replacedFile;
    }
}
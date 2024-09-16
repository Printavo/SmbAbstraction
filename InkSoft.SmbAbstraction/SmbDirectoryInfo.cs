using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using SMBLibrary;
using System.IO;

namespace InkSoft.SmbAbstraction;

public class SmbDirectoryInfo(string fileName, IFileSystem fileSystem, ISmbCredentialProvider credentialProvider) : DirectoryInfoWrapper(new FileSystem(), new(fileName)), IDirectoryInfo
{
    private SmbDirectory SmbDirectory => _fileSystem.Directory as SmbDirectory;
    private SmbFile SmbFile => _fileSystem.File as SmbFile;
    private SmbDirectoryInfoFactory DirectoryInfoFactory => _fileSystem.DirectoryInfo as SmbDirectoryInfoFactory;
    private SmbFileInfoFactory FileInfoFactory => _fileSystem.FileInfo as SmbFileInfoFactory;
    private readonly IFileSystem _fileSystem = fileSystem;

    internal SmbDirectoryInfo(DirectoryInfo directoryInfo, 
        IFileSystem fileSystem, 
        ISmbCredentialProvider credentialProvider) : this(directoryInfo.FullName, fileSystem, credentialProvider)
    {
        _creationTime = directoryInfo.CreationTime;
        _creationTimeUtc = directoryInfo.CreationTimeUtc;
        _fileSystem = fileSystem;
        _lastAccessTime = directoryInfo.LastAccessTime;
        _lastAccessTimeUtc = directoryInfo.LastAccessTimeUtc;
        _lastWriteTime = directoryInfo.LastWriteTime;
        _lastWriteTimeUtc = directoryInfo.LastWriteTimeUtc;
        _parent = (directoryInfo.Parent != null) ? DirectoryInfoFactory.New(directoryInfo.Parent.FullName) : null;
        _root = new SmbDirectoryInfo(directoryInfo.Root.FullName, fileSystem, credentialProvider);
        _exists = directoryInfo.Exists;
        _extension = directoryInfo.Extension;
        _name = directoryInfo.Name;
    }

    internal SmbDirectoryInfo(string fileName,
        FileInformation fileInfo, 
        IFileSystem fileSystem, 
        ISmbCredentialProvider credentialProvider, 
        ISmbCredential credential): this(fileName, fileSystem, credentialProvider)
    {
        var fileDirectoryInformation = (FileBasicInformation)fileInfo;
        if (fileDirectoryInformation.CreationTime.Time.HasValue)
        {
            _creationTime = fileDirectoryInformation.CreationTime.Time.Value;
            _creationTimeUtc = CreationTime.ToUniversalTime();
        }
        if (fileDirectoryInformation.LastAccessTime.Time.HasValue)
        {
            _lastAccessTime = fileDirectoryInformation.LastAccessTime.Time.Value;
            _lastAccessTimeUtc = LastAccessTime.ToUniversalTime();
        }
        if (fileDirectoryInformation.LastWriteTime.Time.HasValue)
        {
            _lastWriteTime = fileDirectoryInformation.LastWriteTime.Time.Value;
            _lastWriteTimeUtc = LastWriteTime.ToUniversalTime();
        }

        _parent = SmbDirectory.GetParent(fileName, credential);
        _fileSystem = fileSystem;
        string? pathRoot = _fileSystem.Path.GetPathRoot(fileName);

        if(pathRoot != fileName)
        {
            _root = DirectoryInfoFactory.New(pathRoot, credential);
        }
        else
        {
            _root = this;
        }           

        _exists = _fileSystem.Directory.Exists(FullName);
        _extension = string.Empty;
        _name = _fullName.GetLastPathSegment().RemoveLeadingAndTrailingSeparators();
    }

    private IDirectoryInfo _root;
    private IDirectoryInfo _parent;
    private System.IO.FileAttributes _attributes;
    private DateTime _creationTime;
    private DateTime _creationTimeUtc;
    private bool _exists;
    private string _extension;
    private string _fullName = fileName;
    private DateTime _lastAccessTime;
    private DateTime _lastAccessTimeUtc;
    private DateTime _lastWriteTime;
    private DateTime _lastWriteTimeUtc;
    private string _name;

    public override IDirectoryInfo Parent => _parent;
    public override IDirectoryInfo Root => _root;

    public override System.IO.FileAttributes Attributes 
    { 
        get => _attributes; 
        set => _attributes = value; 
    }

    public override DateTime CreationTime 
    { 
        get => _creationTime; 
        set => _creationTime = value; 
    }

    public override DateTime CreationTimeUtc 
    { 
        get => _creationTimeUtc;
        set => _creationTime = value;
    }

    public override bool Exists => _exists;
    public override string Extension => _extension;
    public override string FullName => _fullName;

    public override DateTime LastAccessTime 
    { 
        get => _lastAccessTime;
        set => _lastAccessTime = value;
    }

    public override DateTime LastAccessTimeUtc 
    { 
        get => _lastAccessTimeUtc;
        set => _lastAccessTimeUtc = value;
    }

    public override DateTime LastWriteTime 
    { 
        get => _lastWriteTime;
        set => _lastWriteTime = value;
    }

    public override DateTime LastWriteTimeUtc 
    { 
        get => _lastWriteTimeUtc;
        set => _lastWriteTimeUtc = value;
    }

    public override string Name => _name;

    public override void Create() => SmbDirectory.CreateDirectory(_fullName);

    public override IDirectoryInfo CreateSubdirectory(string path) => SmbDirectory.CreateDirectory(_fileSystem.Path.Combine(_fullName, path));

    public override void Delete(bool recursive) => SmbDirectory.Delete(_fullName, recursive);

    public override void Delete() => SmbDirectory.Delete(_fullName);

    public override IEnumerable<IDirectoryInfo> EnumerateDirectories() => EnumerateDirectories("*");

    public override IEnumerable<IDirectoryInfo> EnumerateDirectories(string searchPattern) => EnumerateDirectories(searchPattern, SearchOption.TopDirectoryOnly);

    public override IEnumerable<IDirectoryInfo> EnumerateDirectories(string searchPattern, SearchOption searchOption)
    {
        if(!_fullName.IsSharePath())
        {
            return base.EnumerateDirectories(searchPattern, searchOption);
        }

        var paths = SmbDirectory.EnumerateDirectories(_fullName, searchPattern, searchOption);

        var rootCredential = credentialProvider.GetSmbCredential(_fullName);

        var directoryInfos = new List<IDirectoryInfo>();
        foreach (string? path in paths)
        {
            directoryInfos.Add(DirectoryInfoFactory.New(path, rootCredential));
        }

        return directoryInfos;
    }

    public override IEnumerable<IFileInfo> EnumerateFiles() => EnumerateFiles("*");

    public override IEnumerable<IFileInfo> EnumerateFiles(string searchPattern) => EnumerateFiles(searchPattern, SearchOption.TopDirectoryOnly);

    public override IEnumerable<IFileInfo> EnumerateFiles(string searchPattern, SearchOption searchOption)
    {
        if(!_fullName.IsSharePath())
            return base.EnumerateFiles(searchPattern, searchOption);

        var paths = SmbDirectory.EnumerateFiles(FullName, searchPattern, searchOption);
        var rootCredential = credentialProvider.GetSmbCredential(FullName);
        return paths.Select(path => FileInfoFactory.New(path, rootCredential)).ToList();
    }

    public override IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos() => EnumerateFileSystemInfos("*");

    public override IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(string searchPattern) => EnumerateFileSystemInfos(searchPattern, SearchOption.TopDirectoryOnly);

    public override IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(string searchPattern, SearchOption searchOption)
    {
        if(!_fullName.IsSharePath())
        {
            return base.EnumerateFileSystemInfos(searchPattern, searchOption);
        }

        var paths = SmbDirectory.EnumerateFileSystemEntries(_fullName, searchPattern, searchOption);

        var rootCredential = credentialProvider.GetSmbCredential(_fullName);

        var fileSystemInfos = new List<IFileSystemInfo>();
        foreach (string? path in paths)
        {
            if (SmbFile.Exists(path))
            {
                fileSystemInfos.Add(FileInfoFactory.New(path, rootCredential));
            }
            else
            {
                fileSystemInfos.Add(DirectoryInfoFactory.New(path, rootCredential));
            }
        }

        return fileSystemInfos;
    }

    public override IDirectoryInfo[] GetDirectories() => EnumerateDirectories().ToArray();

    public override IDirectoryInfo[] GetDirectories(string searchPattern) => EnumerateDirectories(searchPattern).ToArray();

    public override IDirectoryInfo[] GetDirectories(string searchPattern, SearchOption searchOption) => EnumerateDirectories(searchPattern, searchOption).ToArray();

    public override IFileInfo[] GetFiles() => EnumerateFiles().ToArray();

    public override IFileInfo[] GetFiles(string searchPattern) => EnumerateFiles(searchPattern).ToArray();

    public override IFileInfo[] GetFiles(string searchPattern, SearchOption searchOption) => EnumerateFiles(searchPattern, searchOption).ToArray();

    public override IFileSystemInfo[] GetFileSystemInfos() => EnumerateFileSystemInfos().ToArray();

    public override IFileSystemInfo[] GetFileSystemInfos(string searchPattern) => EnumerateFileSystemInfos(searchPattern).ToArray();

    public override IFileSystemInfo[] GetFileSystemInfos(string searchPattern, SearchOption searchOption) => EnumerateFileSystemInfos(searchPattern, searchOption).ToArray();

    public override void MoveTo(string destDirName)
    {
        SmbDirectory.Move(_fullName, destDirName);
        SmbDirectory.Delete(_fullName);
    }

    public override void Refresh()
    {
        var info = DirectoryInfoFactory.New(_fullName);
        _parent = info.Parent;
        _root = info.Root;
        _attributes = info.Attributes;
        _creationTime = info.CreationTime;
        _creationTimeUtc = info.CreationTimeUtc;
        _lastAccessTime = info.LastAccessTime;
        _lastAccessTimeUtc = info.LastAccessTimeUtc;
        _lastWriteTime = info.LastWriteTime;
        _lastWriteTimeUtc = info.LastWriteTimeUtc;
    }

    internal FileInformation ToSmbFileInformation(ISmbCredential credential = null)
    {
        var fileBasicInformation = new FileBasicInformation();

        fileBasicInformation.CreationTime.Time = CreationTime;
        fileBasicInformation.LastAccessTime.Time = LastAccessTime;
        fileBasicInformation.LastWriteTime.Time = LastWriteTime;

        fileBasicInformation.FileAttributes = (SMBLibrary.FileAttributes)Attributes;

        return fileBasicInformation;
    }
}
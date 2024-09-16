using System;
using System.IO;
using System.IO.Abstractions;

namespace InkSoft.SmbAbstraction;

public class SmbDriveInfo : IDriveInfo
{
    private SmbDirectoryInfoFactory DirInfoFactory => FileSystem.DirectoryInfo as SmbDirectoryInfoFactory;
    private readonly string _volumeLabel;

    public SmbDriveInfo(string path, IFileSystem fileSystem, SmbFileSystemInformation smbFileSystemInformation, ISmbCredential credential)
    {
        FileSystem = fileSystem;
        DriveFormat = smbFileSystemInformation.AttributeInformation?.FileSystemName;
        Name = path.ShareName();
        string? rootPath = fileSystem.Path.GetPathRoot(path);
        RootDirectory = DirInfoFactory.New(rootPath, credential);
        long actualAvailableAllocationUnits = smbFileSystemInformation.SizeInformation.ActualAvailableAllocationUnits;
        uint sectorsPerUnit = smbFileSystemInformation.SizeInformation.SectorsPerAllocationUnit;
        uint bytesPerSector = smbFileSystemInformation.SizeInformation.BytesPerSector;
        long totalAllocationUnits = smbFileSystemInformation.SizeInformation.TotalAllocationUnits;
        long availableAllocationUnits = smbFileSystemInformation.SizeInformation.CallerAvailableAllocationUnits;  

        AvailableFreeSpace = availableAllocationUnits * sectorsPerUnit * bytesPerSector;
        TotalFreeSpace = actualAvailableAllocationUnits * sectorsPerUnit * bytesPerSector;
        TotalSize = totalAllocationUnits * sectorsPerUnit * bytesPerSector;
        _volumeLabel = smbFileSystemInformation.VolumeInformation.VolumeLabel;
    }

    public IFileSystem FileSystem { get; }

    public long AvailableFreeSpace { get; }

    public string DriveFormat { get; }

    public DriveType DriveType => DriveType.Network;

    public bool IsReady => throw new NotImplementedException();

    public string Name { get; }

    public IDirectoryInfo RootDirectory { get; }

    public long TotalFreeSpace { get; }

    public long TotalSize { get; }

    public string VolumeLabel { get => _volumeLabel; set => throw new NotSupportedException(); }
}
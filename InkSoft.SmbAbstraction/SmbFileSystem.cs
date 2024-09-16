using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

namespace InkSoft.SmbAbstraction;

public class SmbFileSystem : FileSystemBase
{
    public SmbFileSystem(ISmbClientFactory iSmbClientFactory, ISmbCredentialProvider credentialProvider, uint maxBufferSize = 65536, ISmbFileSystemSettings? smbFileSystemSettings = null, ILoggerFactory? loggerFactory = null)
    {
        smbFileSystemSettings ??= new SmbFileSystemSettings();
            
        DriveInfo = new SmbDriveInfoFactory(this, credentialProvider, iSmbClientFactory, maxBufferSize, loggerFactory);
        DirectoryInfo = new SmbDirectoryInfoFactory(this, credentialProvider, iSmbClientFactory, maxBufferSize, loggerFactory);
        FileInfo = new SmbFileInfoFactory(this, credentialProvider, iSmbClientFactory, maxBufferSize, loggerFactory);
        Path = new SmbPath(this);
        File = new SmbFile(iSmbClientFactory, credentialProvider, this, maxBufferSize, smbFileSystemSettings, loggerFactory);
        Directory = new SmbDirectory(iSmbClientFactory, credentialProvider, this, maxBufferSize, smbFileSystemSettings, loggerFactory);
        FileStream = new SmbFileStreamFactory(this);
        FileSystemWatcher = new SmbFileSystemWatcherFactory(this);
    }

    public override IDirectory Directory { get; }

    public override IFile File { get; }

    public override IFileInfoFactory FileInfo { get; }

    public override IFileStreamFactory FileStream { get; }

    public override IPath Path { get; }

    public override IDirectoryInfoFactory DirectoryInfo { get; }

    public override IDriveInfoFactory DriveInfo { get; }

    public override IFileSystemWatcherFactory FileSystemWatcher { get; }
}
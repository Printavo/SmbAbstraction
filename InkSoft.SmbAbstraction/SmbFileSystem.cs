using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

namespace InkSoft.SmbAbstraction;

/// <summary>
/// A file system abstraction for accessing SMB/UNC shares without relying on OS APIs for authentication.
/// </summary>
public class SmbFileSystem : FileSystemBase
{
    /// <summary>
    /// Creates a new instance of <see cref="SmbFileSystem"/>.
    /// </summary>
    /// <param name="smbClientFactory">Equates to a wrapper around "new SMB2Client()".</param>
    /// <param name="credentialProvider">Credential provider service reference.</param>
    /// <param name="smbFileSystemOptions">Uses default values when null. Same as passing "new()".</param>
    /// <param name="loggerFactory">App logger service. Foregoes logging when null.</param>
    public SmbFileSystem(ISmbClientFactory smbClientFactory, ISmbCredentialProvider credentialProvider, SmbFileSystemOptions? smbFileSystemOptions, ILoggerFactory? loggerFactory)
    {
        smbFileSystemOptions ??= new();

        LoggerFactory = loggerFactory;
        DriveInfo = new SmbDriveInfoFactory(this, smbClientFactory, credentialProvider, smbFileSystemOptions, loggerFactory);
        DirectoryInfo = new SmbDirectoryInfoFactory(this, smbClientFactory, credentialProvider, smbFileSystemOptions, loggerFactory);
        FileInfo = new SmbFileInfoFactory(this, smbClientFactory, credentialProvider, smbFileSystemOptions, loggerFactory);
        Path = new SmbPath(this);
        File = new SmbFile(this, smbClientFactory, credentialProvider, smbFileSystemOptions, loggerFactory);
        Directory = new SmbDirectory(this, smbClientFactory, credentialProvider, smbFileSystemOptions, loggerFactory);
        FileStream = new SmbFileStreamFactory(this);
        FileSystemWatcher = new SmbFileSystemWatcherFactory(this);
    }

    /// <summary>
    /// Exposing the internal logger factory, mostly for the benefit of IFileSystem extension methods.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; }

    /// <inheritdoc cref="SmbDriveInfoFactory"/>
    public override IDriveInfoFactory DriveInfo { get; }

    /// <inheritdoc cref="SmbDirectoryInfoFactory"/>
    public override IDirectoryInfoFactory DirectoryInfo { get; }

    /// <inheritdoc cref="SmbFileInfoFactory"/>
    public override IFileInfoFactory FileInfo { get; }

    /// <inheritdoc cref="SmbPath"/>
    public override IPath Path { get; }

    /// <inheritdoc cref="SmbFile"/>
    public override IFile File { get; }

    /// <inheritdoc cref="SmbDirectory"/>
    public override IDirectory Directory { get; }

    /// <inheritdoc cref="SmbFileStreamFactory"/>
    public override IFileStreamFactory FileStream { get; }

    /// <inheritdoc cref="SmbFileSystemWatcherFactory"/>
    public override IFileSystemWatcherFactory FileSystemWatcher { get; }
}
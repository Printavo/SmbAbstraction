using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

namespace InkSoft.SmbAbstraction;

/// <summary>
/// A file system abstraction for accessing SMB/UNC shares without relying on OS APIs for authentication.
/// </summary>
public class SmbFileSystem: FileSystemBase
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
        ClientFactory = smbClientFactory;
        CredentialProvider = credentialProvider;
        Options = smbFileSystemOptions ?? new();
        LoggerFactory = loggerFactory;
        DriveInfo = new SmbDriveInfoFactory(this, loggerFactory?.CreateLogger<SmbDriveInfoFactory>());
        DirectoryInfo = new SmbDirectoryInfoFactory(this, loggerFactory?.CreateLogger<SmbDirectoryInfoFactory>());
        FileInfo = new SmbFileInfoFactory(this, loggerFactory?.CreateLogger<SmbFileInfoFactory>());
        FileVersionInfo = new SmbFileVersionInfoFactory(this);
        Path = new SmbPath(this);
        File = new SmbFile(this, loggerFactory?.CreateLogger<SmbFile>());
        Directory = new SmbDirectory(this, loggerFactory?.CreateLogger<SmbDirectory>());
        FileStream = new SmbFileStreamFactory(this);
        FileSystemWatcher = new SmbFileSystemWatcherFactory(this);
    }

    public ISmbClientFactory ClientFactory { get; set; }

    public ISmbCredentialProvider CredentialProvider { get; set; }

    public SmbFileSystemOptions Options { get; set; }

    /// <summary>
    /// Exposing the internal logger factory, mostly for the benefit of IFileSystem extension methods.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; }

    /// <summary>
    /// <see cref="IFileSystem"/> to which operations on non-SMB paths are delegated. Defaults to <see cref="FileSystem"/>.
    /// </summary>
    public IFileSystem NonSmbFileSystem { get; set; } = new FileSystem();

    /// <inheritdoc cref="SmbDriveInfoFactory"/>
    public override IDriveInfoFactory DriveInfo { get; }

    /// <inheritdoc cref="SmbDirectoryInfoFactory"/>
    public override IDirectoryInfoFactory DirectoryInfo { get; }

    /// <inheritdoc cref="SmbFileInfoFactory"/>
    public override IFileInfoFactory FileInfo { get; }

    /// <inheritdoc cref="SmbFileVersionInfoFactory"/>
    public override IFileVersionInfoFactory FileVersionInfo { get; }

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
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using InkSoft.SmbAbstraction.Utilities;
using Microsoft.Extensions.Logging;
using SMBLibrary;
using SMBLibrary.Client;

namespace InkSoft.SmbAbstraction;

public class SmbDirectoryInfoFactory(
    IFileSystem fileSystem,
    ISmbCredentialProvider credentialProvider,
    ISmbClientFactory smbClientFactory,
    uint maxBufferSize,
    ILoggerFactory? loggerFactory = null
) : IDirectoryInfoFactory
{
    private readonly ILogger<SmbDirectoryInfoFactory> _logger = loggerFactory?.CreateLogger<SmbDirectoryInfoFactory>();
    private SmbDirectory? SmbDirectory => fileSystem.Directory as SmbDirectory;
    private SmbFile? SmbFile => fileSystem.File as SmbFile;
    private SmbFileInfoFactory? FileInfoFactory => fileSystem.FileInfo as SmbFileInfoFactory;

    public SMBTransportType Transport { get; set; } = SMBTransportType.DirectTCPTransport;
    
    public IFileSystem FileSystem { get; } = fileSystem;

    public IDirectoryInfo New(string directoryName)
    {
        if (!directoryName.IsSharePath())
        {
            var dirInfo = new DirectoryInfo(directoryName);
            return new SmbDirectoryInfo(dirInfo, fileSystem, credentialProvider);
        }

        return New(directoryName, null);
    }

    internal IDirectoryInfo New(string path, ISmbCredential? credential)
    {
        // TBD: This doesn't seem appropriate.
        if (!path.IsSharePath() || !path.IsValidSharePath())
            return null;

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
        {
            throw new SmbException($"Failed FromDirectoryName for {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));
        }

        credential ??= credentialProvider.GetSmbCredential(path);

        if (credential == null)
        {
            throw new SmbException($"Failed FromDirectoryName for {path}", new InvalidCredentialException($"Unable to find credential for path: {path}"));
        }

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();

            _logger?.LogTrace("Trying FromDirectoryName {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);

            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, maxBufferSize);
                
            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);

            status.HandleStatus();

            status = fileStore.CreateFile(out handle, out var fileStatus, relativePath, AccessMask.GENERIC_READ, 0, ShareAccess.Read,
                CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);

            status.HandleStatus();

            status = fileStore.GetFileInformation(out var fileInfo, handle, FileInformationClass.FileBasicInformation); // If you call this with any other FileInformationClass value
            // it doesn't work for some reason
            return status != NTStatus.STATUS_SUCCESS ? null : new SmbDirectoryInfo(path, fileInfo, fileSystem, credentialProvider, credential);
        }
        catch (Exception ex)
        {
            throw new SmbException($"Failed FromDirectoryName for {path}", ex);
        }
        finally
        {
            FileStoreUtilities.CloseFile(fileStore, ref handle);
        }
    }

    internal void SaveDirectoryInfo(SmbDirectoryInfo dirInfo, ISmbCredential? credential = null)
    {
        string? path = dirInfo.FullName;

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed to SaveDirectoryInfo for {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        credential ??= credentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed to SaveDirectoryInfo for {path}", new InvalidCredentialException($"Unable to find credential for path: {path}"));

        ISMBFileStore? fileStore = null;
        object? handle = null;

        try
        {
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();

            _logger?.LogTrace("Trying to SaveDirectoryInfo {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);
            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, maxBufferSize);
            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
            status.HandleStatus();
            status = fileStore.CreateFile(out handle, out var fileStatus, relativePath, AccessMask.GENERIC_WRITE, 0, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
            status.HandleStatus();
            var fileInfo = dirInfo.ToSmbFileInformation(credential);
            status = fileStore.SetFileInformation(handle, fileInfo);
            status.HandleStatus();
        }
        catch (Exception ex)
        {
            throw new SmbException($"Failed to SaveDirectoryInfo for {path}", ex);
        }
        finally
        {
            FileStoreUtilities.CloseFile(fileStore, ref handle);
        }
    }

    [return: NotNullIfNotNull("directoryInfo")]
    public IDirectoryInfo Wrap(DirectoryInfo directoryInfo) => throw new NotImplementedException();
}
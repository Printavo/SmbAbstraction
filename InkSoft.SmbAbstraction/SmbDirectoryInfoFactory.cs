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
    ISmbClientFactory smbClientFactory,
    ISmbCredentialProvider credentialProvider,
    SmbFileSystemOptions? smbFileSystemOptions,
    ILoggerFactory? loggerFactory = null
) : IDirectoryInfoFactory
{
    private readonly ILogger<SmbDirectoryInfoFactory>? _logger = loggerFactory?.CreateLogger<SmbDirectoryInfoFactory>();
    
    /// <inheritdoc cref="SmbFileSystem"/>
    public IFileSystem FileSystem => fileSystem;

    public SMBTransportType Transport { get; set; } = SMBTransportType.DirectTCPTransport;
    
    public IDirectoryInfo New(string directoryName) => directoryName.IsSharePath() ? New(directoryName, null) : new SmbDirectoryInfo(new DirectoryInfo(directoryName), FileSystem, credentialProvider);

    internal IDirectoryInfo New(string path, ISmbCredential? credential)
    {
        // TBD: This doesn't seem appropriate.
        if (!path.IsSharePath() || !path.IsValidSharePath())
            return null;

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed SmbDirectoryInfo.New for {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        credential ??= credentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed SmbDirectoryInfo.New for {path}", new InvalidCredentialException($"Unable to find credential for path: {path}"));

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string shareName = path.ShareName();
            string relativePath = path.ShareRelativePath();
            _logger?.LogTrace("Trying SmbDirectoryInfo.New {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);
            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, smbFileSystemOptions);
            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
            status.AssertSuccess();
            status = fileStore.CreateFile(out handle, out _, relativePath, AccessMask.GENERIC_READ, 0, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
            status.AssertSuccess();

            // If you call GetFileInformation with any other FileInformationClass value, it doesn't work for some reason.
            return fileStore.GetFileInformation(out var fileInfo, handle, FileInformationClass.FileBasicInformation) == NTStatus.STATUS_SUCCESS
                ? new SmbDirectoryInfo(path, fileInfo, FileSystem, credentialProvider, credential)
                : null // TODO: Is returning null appropriate, ever?
            ;
        }
        catch (Exception ex)
        {
            throw new SmbException($"Failed SmbDirectoryInfo.New for {path}", ex);
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
            string shareName = path.ShareName();
            string relativePath = path.ShareRelativePath();

            _logger?.LogTrace("Trying to SaveDirectoryInfo {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);
            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, smbFileSystemOptions);
            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
            status.AssertSuccess();
            status = fileStore.CreateFile(out handle, out _, relativePath, AccessMask.GENERIC_WRITE, 0, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
            status.AssertSuccess();
            var fileInfo = dirInfo.ToSmbFileInformation(credential);
            status = fileStore.SetFileInformation(handle, fileInfo);
            status.AssertSuccess();
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

    [return: NotNullIfNotNull(nameof(directoryInfo))]
    public IDirectoryInfo? Wrap(DirectoryInfo? directoryInfo) => fileSystem.DirectoryInfo.Wrap(directoryInfo);
}
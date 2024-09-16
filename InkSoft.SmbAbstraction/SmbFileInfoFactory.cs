using System;
using System.IO;
using System.IO.Abstractions;
using InkSoft.SmbAbstraction.Utilities;
using Microsoft.Extensions.Logging;
using SMBLibrary;
using SMBLibrary.Client;

namespace InkSoft.SmbAbstraction;

public class SmbFileInfoFactory(
    IFileSystem fileSystem,
    ISmbCredentialProvider credentialProvider,
    ISmbClientFactory smbClientFactory,
    uint maxBufferSize,
    ILoggerFactory? loggerFactory = null
) : IFileInfoFactory
{
    public IFileSystem FileSystem => fileSystem;

    private readonly ILogger<SmbFileInfoFactory>? _logger = loggerFactory?.CreateLogger<SmbFileInfoFactory>();
    
    private SmbDirectoryInfoFactory? DirInfoFactory => fileSystem.DirectoryInfo as SmbDirectoryInfoFactory;

    public SMBTransportType Transport { get; set; } = SMBTransportType.DirectTCPTransport;

    public IFileInfo New(string fileName) => fileName.IsSharePath() ? New(fileName, null) : new SmbFileInfo(fileSystem, new FileInfo(fileName));

    internal IFileInfo New(string path, ISmbCredential? credential)
    {
        // TBD: Original code returned null. Is that appropriate?
        if (!path.IsSharePath())
            throw new InvalidOperationException("Path must be a share path.");

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed FromFileName for {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        credential ??= credentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed FromFileName for {path}", new InvalidCredentialException($"Unable to find credential for path: {path}"));

        ISMBFileStore fileStore = null;
        object handle = null;

        try
        {
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();
            _logger?.LogTrace("Trying FromFileName {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);
            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, maxBufferSize);
            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
            status.HandleStatus();

            const AccessMask c_accessMask = AccessMask.SYNCHRONIZE | AccessMask.GENERIC_READ;
            const ShareAccess c_shareAccess = ShareAccess.Read;
            const CreateDisposition c_disposition = CreateDisposition.FILE_OPEN;
            const CreateOptions c_createOptions = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_NON_DIRECTORY_FILE;

            status = fileStore.CreateFile(out handle, out _, relativePath, c_accessMask, 0, c_shareAccess, c_disposition, c_createOptions, null);

            status.HandleStatus();
            status = fileStore.GetFileInformation(out var fileBasicInfo, handle, FileInformationClass.FileBasicInformation);
            status.HandleStatus();
            status = fileStore.GetFileInformation(out var fileStandardInfo, handle, FileInformationClass.FileStandardInformation);
            status.HandleStatus();
            FileStoreUtilities.CloseFile(fileStore, ref handle);

            return new SmbFileInfo(fileSystem, path, (FileBasicInformation)fileBasicInfo, (FileStandardInformation)fileStandardInfo, credential);
        }
        catch (Exception ex)
        {
            throw new SmbException($"Failed FromFileName for {path}", ex);
        }
        finally
        {
            FileStoreUtilities.CloseFile(fileStore, ref handle);
        }
    }

    internal void SaveFileInfo(SmbFileInfo fileInfo, ISmbCredential? credential = null)
    {
        string? path = fileInfo.FullName;

        if (!path.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed to SaveFileInfo for {path}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

        credential ??= credentialProvider.GetSmbCredential(path);

        if (credential == null)
            throw new SmbException($"Failed to SaveFileInfo for {path}", new InvalidCredentialException($"Unable to find credential for path: {path}"));

        ISMBFileStore? fileStore = null;
        object? handle = null;

        try
        {
            string? shareName = path.ShareName();
            string? relativePath = path.RelativeSharePath();
            _logger?.LogTrace("Trying to SaveFileInfo {{RelativePath: {relativePath}}} for {{ShareName: {shareName}}}", relativePath, shareName);
            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, maxBufferSize);
            fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
            status.HandleStatus();

            const AccessMask c_accessMask = AccessMask.SYNCHRONIZE | AccessMask.GENERIC_WRITE;
            const ShareAccess c_shareAccess = ShareAccess.Read;
            const CreateDisposition c_disposition = CreateDisposition.FILE_OPEN;
            const CreateOptions c_createOptions = CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_NON_DIRECTORY_FILE;

            status = fileStore.CreateFile(out handle, out var fileStatus, relativePath, c_accessMask, 0, c_shareAccess, c_disposition, c_createOptions, null);
            status.HandleStatus();
            var smbFileInfo = fileInfo.ToSmbFileInformation(credential);
            status = fileStore.SetFileInformation(handle, smbFileInfo);
            status.HandleStatus();
        }
        catch (Exception ex)
        {
            throw new SmbException($"Failed to SaveFileInfo for {path}", ex);
        }
        finally
        {
            FileStoreUtilities.CloseFile(fileStore, ref handle);
        }
    }

    public IFileInfo Wrap(FileInfo fileInfo) => throw new NotImplementedException();
}
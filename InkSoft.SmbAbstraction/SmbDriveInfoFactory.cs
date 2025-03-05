using Microsoft.Extensions.Logging;
using SMBLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace InkSoft.SmbAbstraction;

public class SmbDriveInfoFactory(IFileSystem fileSystem, ISmbClientFactory smbClientFactory, ISmbCredentialProvider smbCredentialProvider, SmbFileSystemOptions smbFileSystemOptions, ILoggerFactory? loggerFactory = null) : IDriveInfoFactory
{
    private readonly ILogger<SmbDriveInfoFactory>? _logger = loggerFactory?.CreateLogger<SmbDriveInfoFactory>();
    
    private readonly FileSystem _baseFileSystem = new();
    
    /// <inheritdoc cref="SmbFileSystem"/>
    public IFileSystem FileSystem => fileSystem;

    public SMBTransportType Transport { get; set; } = SMBTransportType.DirectTCPTransport;
    
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <remarks>
    /// Note that although <paramref name="driveName"/> is the canonical param name, it can also be any fully qualified path.
    /// </remarks>
    public IDriveInfo New(string driveName)
    {
        if (!driveName.IsSharePath())
            return new DriveInfoWrapper(new FileSystem(), new(driveName));

        var credential = smbCredentialProvider.GetSmbCredential(driveName) ?? throw new SmbException("Unable to find credential in SMBCredentialProvider for "+driveName);
        string correspondingCredentialPath = credential.Path!;

        if (!correspondingCredentialPath.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed FromDriveName for {driveName}", new ArgumentException($"Unable to resolve \"{correspondingCredentialPath.Hostname()}\""));

        try
        {
            using var smbConnection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, smbFileSystemOptions);
            var fileStore = smbConnection.SmbClient.TreeConnect(driveName.ShareName(), out var ntStatus);
            ntStatus.AssertSuccess();
            return new SmbDriveInfo(driveName, FileSystem, new(fileStore), credential);
        }
        catch (Exception ex)
        {
            throw new SmbException($"Failed FromDriveName for {driveName}", ex);
        }
    }

    public IDriveInfo[] GetDrives()
    {
        var drives = new List<IDriveInfo>();
        drives.AddRange(GetDrives(null));
        drives.AddRange(_baseFileSystem.DriveInfo.GetDrives());
        return drives.ToArray();
    }

    public IDriveInfo Wrap(DriveInfo driveInfo) => fileSystem.DriveInfo.Wrap(driveInfo);

    internal IDriveInfo[] GetDrives(ISmbCredential? smbCredential)
    {
        var credentialsToCheck = smbCredentialProvider.GetSmbCredentials().ToList();
        var driveInfos = new List<IDriveInfo>();

        if (smbCredential == null && credentialsToCheck.Count == 0)
        {
            _logger?.LogTrace("No provided credentials and no credentials stored credentials in SMBCredentialProvider.");
            return driveInfos.ToArray();
        }

        if (smbCredential != null)
            credentialsToCheck.Add(smbCredential);
        else
            credentialsToCheck = smbCredentialProvider.GetSmbCredentials().ToList();

        var shareHostNames = credentialsToCheck.Select(c => c.Path.Hostname()).Distinct().ToList();

        foreach (string? shareHost in shareHostNames)
        {
            var credential = credentialsToCheck.First(c => c.Path.Hostname().Equals(shareHost));
            try
            {
                string? path = credential.Path;
                if (!path.TryResolveHostnameFromPath(out var ipAddress))
                    throw new SmbException($"Failed to connect to {path.Hostname()}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

                using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, smbFileSystemOptions);
                var shareNames = connection.SmbClient.ListShares(out var ntStatus);

                foreach (string? shareName in shareNames)
                {
                    string? sharePath = path.BuildSharePath(shareName);
                    _logger?.LogTrace("Trying to get drive info for {shareName}", shareName);

                    try
                    {
                        var fileStore = connection.SmbClient.TreeConnect(shareName, out ntStatus);
                        ntStatus.AssertSuccess();
                        driveInfos.Add(new SmbDriveInfo(sharePath, FileSystem, new(fileStore), credential));
                    }
                    catch (IOException ioEx)
                    {
                        _logger?.LogTrace(ioEx, "Failed to get drive info for {shareName}", shareName);
                        throw new SmbException($"Failed to get drive info for {shareName}", new AggregateException($"Unable to connect to {shareName}", ioEx));
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogTrace(ex, "Failed to get drive info for {shareName}", shareName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogTrace(ex, "Failed to GetDrives for {shareHost}.", shareHost);
            }
        }

        return driveInfos.ToArray();
    }
}
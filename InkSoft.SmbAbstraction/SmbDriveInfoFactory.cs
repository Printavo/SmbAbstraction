using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.Extensions.Logging;
using SMBLibrary;
using SMBLibrary.Client;

namespace InkSoft.SmbAbstraction;

public class SmbDriveInfoFactory(IFileSystem fileSystem, ISmbCredentialProvider smbCredentialProvider, ISmbClientFactory smbClientFactory, uint maxBufferSize, ILoggerFactory? loggerFactory = null) : IDriveInfoFactory
{
    private readonly ILogger<SmbDriveInfoFactory>? _logger = loggerFactory?.CreateLogger<SmbDriveInfoFactory>();
    private readonly FileSystem _baseFileSystem = new();

    public SMBTransportType Transport { get; set; } = SMBTransportType.DirectTCPTransport;


    public IDriveInfo New(string driveName)
    {
        if(string.IsNullOrEmpty(driveName))
        {
            throw new SmbException($"Failed FromDriveName", new ArgumentException("Drive name cannot be null or empty.", nameof(driveName)));
        }

        if (driveName.IsSharePath() || PossibleShareName(driveName))
        {
            return New(driveName, null);
        }

        var driveInfo = new DriveInfo(driveName);
        return new DriveInfoWrapper(new FileSystem(), driveInfo);
    }

    internal IDriveInfo New(string shareName, ISmbCredential? credential)
    {
        if (credential == null)
        {
            if(shareName.IsValidSharePath())
            {
                credential = smbCredentialProvider.GetSmbCredential(shareName);
                shareName = shareName.ShareName();
            }
            else
            {
                credential = smbCredentialProvider.GetSmbCredentials().Where(c => c.Path.ShareName().Equals(shareName)).FirstOrDefault();
            }

            if (credential == null)
            {
                _logger?.LogTrace($"Unable to find credential in SMBCredentialProvider for path: {shareName}");
                return null;
            }
        }

        string? path = credential.Path;
        if (!path.TryResolveHostnameFromPath(out var ipAddress))
        {
            throw new SmbException($"Failed FromDriveName for {shareName}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));
        }

        var status = NTStatus.STATUS_SUCCESS;
        try
        {
            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, maxBufferSize);

            string? relativePath = path.RelativeSharePath();

            var fileStore = connection.SmbClient.TreeConnect(shareName, out status);

            status.HandleStatus();

            var smbFileSystemInformation = new SmbFileSystemInformation(fileStore, path, status);

            var smbDriveInfo = new SmbDriveInfo(path, fileSystem, smbFileSystemInformation, credential);

            return smbDriveInfo;
        }
        catch (Exception ex)
        {
            throw new SmbException($"Failed FromDriveName for {shareName}", ex);
        }
    }

    public IDriveInfo[] GetDrives()
    {
        var drives = new List<IDriveInfo>();

        drives.AddRange(GetDrives(null));
        drives.AddRange(_baseFileSystem.DriveInfo.GetDrives());

        return drives.ToArray();
    }

    public IDriveInfo Wrap(DriveInfo driveInfo) => throw new NotImplementedException();

    internal IDriveInfo[] GetDrives(ISmbCredential? smbCredential)
    {
        var credentialsToCheck = new List<ISmbCredential>();
        credentialsToCheck = smbCredentialProvider.GetSmbCredentials().ToList();

        var driveInfos = new List<IDriveInfo>();

        if (smbCredential == null && credentialsToCheck.Count == 0)
        {
            _logger?.LogTrace($"No provided credentials and no credentials stored credentials in SMBCredentialProvider.");
            return driveInfos.ToArray();
        }

        var status = NTStatus.STATUS_SUCCESS;

        var shareHostNames = new List<string>();

        if (smbCredential != null)
        {
            credentialsToCheck.Add(smbCredential);
        }
        else
        {
            credentialsToCheck = smbCredentialProvider.GetSmbCredentials().ToList();
        }

        shareHostNames = credentialsToCheck.Select(smbCredential => smbCredential.Path.Hostname()).Distinct().ToList();

        var shareHostShareNames = new Dictionary<string, IEnumerable<string>>();

        foreach (string? shareHost in shareHostNames)
        {
            var credential = credentialsToCheck.Where(smbCredential => smbCredential.Path.Hostname().Equals(shareHost)).First();
            try
            {
                string? path = credential.Path;
                if (!path.TryResolveHostnameFromPath(out var ipAddress))
                {
                    throw new SmbException($"Failed to connect to {path.Hostname()}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));
                }

                using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, maxBufferSize);

                var shareNames = connection.SmbClient.ListShares(out status);
                var shareDirectoryInfoFactory = new SmbDirectoryInfoFactory(fileSystem, smbCredentialProvider, smbClientFactory, maxBufferSize);

                foreach (string? shareName in shareNames)
                {
                    string? sharePath = path.BuildSharePath(shareName);
                    string? relativeSharePath = sharePath.RelativeSharePath();

                    _logger?.LogTrace($"Trying to get drive info for {shareName}");

                    try
                    {
                        var fileStore = connection.SmbClient.TreeConnect(shareName, out status);

                        status.HandleStatus();

                        var smbFileSystemInformation = new SmbFileSystemInformation(fileStore, sharePath, status);

                        var smbDriveInfo = new SmbDriveInfo(sharePath, fileSystem, smbFileSystemInformation, credential);

                        driveInfos.Add(smbDriveInfo);
                    }
                    catch (IOException ioEx)
                    {
                        _logger?.LogTrace(ioEx, $"Failed to get drive info for {shareName}");
                        throw new SmbException($"Failed to get drive info for {shareName}", new AggregateException($"Unable to connect to {shareName}", ioEx));
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogTrace(ex, $"Failed to get drive info for {shareName}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogTrace(ex,$"Failed to GetDrives for {shareHost}.");
                continue;
            }
        }

        return driveInfos.ToArray();
    }

    private bool PossibleShareName(string input)
    {
        var drives = DriveInfo.GetDrives();
        return drives.All(d => !input.StartsWith(d.Name));
    }

    public IFileSystem FileSystem { get; }
}
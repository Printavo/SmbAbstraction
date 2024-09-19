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
    
    public IDriveInfo New(string driveName)
    {
        if(string.IsNullOrEmpty(driveName))
        {
            throw new SmbException("Failed FromDriveName", new ArgumentException("Drive name cannot be null or empty.", nameof(driveName)));
        }

        if (driveName.IsSharePath() || PossibleShareName(driveName))
        {
            return New(driveName, null);
        }

        var driveInfo = new DriveInfo(driveName);
        return new DriveInfoWrapper(new FileSystem(), driveInfo);
    }

    internal IDriveInfo? New(string shareName, ISmbCredential? credential)
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
                credential = smbCredentialProvider.GetSmbCredentials().FirstOrDefault(c => c.Path.ShareName().Equals(shareName));
            }

            if (credential == null)
            {
                _logger?.LogTrace("Unable to find credential in SMBCredentialProvider for path: {shareName}", shareName);
                return null;
            }
        }

        string? path = credential.Path;
        if (!path.TryResolveHostnameFromPath(out var ipAddress))
        {
            throw new SmbException($"Failed FromDriveName for {shareName}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));
        }

        try
        {
            using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, smbFileSystemOptions);

            string? relativePath = path.RelativeSharePath();
            var fileStore = connection.SmbClient.TreeConnect(shareName, out var status);
            status.AssertSuccess();
            var smbFileSystemInformation = new SmbFileSystemInformation(fileStore, path, status);
            var smbDriveInfo = new SmbDriveInfo(path, FileSystem, smbFileSystemInformation, credential);
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
        var credentialsToCheck = smbCredentialProvider.GetSmbCredentials().ToList();
        var driveInfos = new List<IDriveInfo>();

        if (smbCredential == null && credentialsToCheck.Count == 0)
        {
            _logger?.LogTrace("No provided credentials and no credentials stored credentials in SMBCredentialProvider.");
            return driveInfos.ToArray();
        }

        if (smbCredential != null)
        {
            credentialsToCheck.Add(smbCredential);
        }
        else
        {
            credentialsToCheck = smbCredentialProvider.GetSmbCredentials().ToList();
        }

        var shareHostNames = credentialsToCheck.Select(c => c.Path.Hostname()).Distinct().ToList();

        foreach (string? shareHost in shareHostNames)
        {
            var credential = credentialsToCheck.First(c => c.Path.Hostname().Equals(shareHost));
            try
            {
                string? path = credential.Path;
                if (!path.TryResolveHostnameFromPath(out var ipAddress))
                {
                    throw new SmbException($"Failed to connect to {path.Hostname()}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));
                }

                using var connection = SmbConnection.CreateSmbConnection(smbClientFactory, ipAddress, Transport, credential, smbFileSystemOptions);

                var shareNames = connection.SmbClient.ListShares(out var status);
                var shareDirectoryInfoFactory = new SmbDirectoryInfoFactory(FileSystem, smbClientFactory, smbCredentialProvider, smbFileSystemOptions);

                foreach (string? shareName in shareNames)
                {
                    string? sharePath = path.BuildSharePath(shareName);
                    string? relativeSharePath = sharePath.RelativeSharePath();

                    _logger?.LogTrace("Trying to get drive info for {shareName}", shareName);

                    try
                    {
                        var fileStore = connection.SmbClient.TreeConnect(shareName, out status);

                        status.AssertSuccess();

                        var smbFileSystemInformation = new SmbFileSystemInformation(fileStore, sharePath, status);

                        var smbDriveInfo = new SmbDriveInfo(sharePath, FileSystem, smbFileSystemInformation, credential);

                        driveInfos.Add(smbDriveInfo);
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

    private static bool PossibleShareName(string input)
    {
        var drives = DriveInfo.GetDrives();
        return drives.All(d => !input.StartsWith(d.Name));
    }
}
using Microsoft.Extensions.Logging;
using SMBLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace InkSoft.SmbAbstraction;

public class SmbDriveInfoFactory(SmbFileSystem smbFileSystem, ILogger<SmbDriveInfoFactory>? logger): IDriveInfoFactory
{
    /// <inheritdoc cref="SmbFileSystem"/>
    public IFileSystem FileSystem => smbFileSystem;

    public SMBTransportType Transport { get; set; } = SMBTransportType.DirectTCPTransport;

    /// <inheritdoc/>
    /// <remarks>
    /// Note that although <paramref name="driveName"/> is the canonical param name, it can also be any fully qualified path.
    /// </remarks>
    public IDriveInfo New(string driveName)
    {
        if (!driveName.IsSharePath())
            return new DriveInfoWrapper(smbFileSystem.NonSmbFileSystem, new(driveName));

        var credential = smbFileSystem.CredentialProvider.GetSmbCredential(driveName) ?? throw new SmbException("Unable to find credential in smbFileSystem.CredentialProvider for "+driveName);
        string correspondingCredentialPath = credential.Path!;

        if (!correspondingCredentialPath.TryResolveHostnameFromPath(out var ipAddress))
            throw new SmbException($"Failed FromDriveName for {driveName}", new ArgumentException($"Unable to resolve \"{correspondingCredentialPath.Hostname()}\""));

        try
        {
            using var smbConnection = SmbConnection.CreateSmbConnection(smbFileSystem.ClientFactory, ipAddress, Transport, credential, smbFileSystem.Options);
            var fileStore = smbConnection.SmbClient.TreeConnect(driveName.ShareName(), out var ntStatus);
            ntStatus.AssertSuccess();
            return new SmbDriveInfo(smbFileSystem, driveName, new(fileStore), credential);
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
        drives.AddRange(smbFileSystem.NonSmbFileSystem.DriveInfo.GetDrives());
        return drives.ToArray();
    }

    public IDriveInfo Wrap(DriveInfo driveInfo) => smbFileSystem.DriveInfo.Wrap(driveInfo);

    internal IDriveInfo[] GetDrives(ISmbCredential? smbCredential)
    {
        var credentialsToCheck = smbFileSystem.CredentialProvider.GetSmbCredentials().ToList();
        var driveInfos = new List<IDriveInfo>();

        if (smbCredential == null && credentialsToCheck.Count == 0)
        {
            logger?.LogTrace("No provided credentials and no credentials stored credentials in smbFileSystem.CredentialProvider.");
            return driveInfos.ToArray();
        }

        if (smbCredential != null)
            credentialsToCheck.Add(smbCredential);
        else
            credentialsToCheck = smbFileSystem.CredentialProvider.GetSmbCredentials().ToList();

        var shareHostNames = credentialsToCheck.Select(c => c.Path.Hostname()).Distinct().ToList();

        foreach (string? shareHost in shareHostNames)
        {
            var credential = credentialsToCheck.First(c => c.Path.Hostname().Equals(shareHost));
            try
            {
                string? path = credential.Path;
                if (!path.TryResolveHostnameFromPath(out var ipAddress))
                    throw new SmbException($"Failed to connect to {path.Hostname()}", new ArgumentException($"Unable to resolve \"{path.Hostname()}\""));

                using var connection = SmbConnection.CreateSmbConnection(smbFileSystem.ClientFactory, ipAddress, Transport, credential, smbFileSystem.Options);
                var shareNames = connection.SmbClient.ListShares(out var ntStatus);

                foreach (string? shareName in shareNames)
                {
                    string? sharePath = path.BuildSharePath(shareName);
                    logger?.LogTrace("Trying to get drive info for {shareName}", shareName);

                    try
                    {
                        var fileStore = connection.SmbClient.TreeConnect(shareName, out ntStatus);
                        ntStatus.AssertSuccess();
                        driveInfos.Add(new SmbDriveInfo(smbFileSystem, sharePath, new(fileStore), credential));
                    }
                    catch (IOException ioEx)
                    {
                        logger?.LogTrace(ioEx, "Failed to get drive info for {shareName}", shareName);
                        throw new SmbException($"Failed to get drive info for {shareName}", new AggregateException($"Unable to connect to {shareName}", ioEx));
                    }
                    catch (Exception ex)
                    {
                        logger?.LogTrace(ex, "Failed to get drive info for {shareName}", shareName);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogTrace(ex, "Failed to GetDrives for {shareHost}.", shareHost);
            }
        }

        return driveInfos.ToArray();
    }
}
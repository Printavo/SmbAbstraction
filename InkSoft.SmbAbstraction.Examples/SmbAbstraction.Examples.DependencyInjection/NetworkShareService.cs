using InkSoft.SmbAbstraction;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Abstractions;
using System.Net;

public class NetworkShareService(
    [FromKeyedServices(nameof(SmbFileSystem))] IFileSystem fileSystem,
    ISmbCredentialProvider credentialProvider
){
    private const string c_sharePath = @"\\host\ShareName"; // e.g. \\host\ShareName or smb://host/sharename
    
    private string Path => fileSystem.Path.Combine(c_sharePath, "test.txt");

    /// <summary>
    /// With IDisposable
    /// </summary>
    public void FileOpsInContext()
    {
        // SMBCredential will parse the share path from path. These credentials are removed from the cache when the using block is exited.
        using var credential = SmbCredential.AddToProvider("domain", "username", "password", c_sharePath, credentialProvider);
        
        // FileInfo
        var fileInfo = fileSystem.FileInfo.New(Path);

        // DirectoryInfo
        var directoryInfo = fileSystem.DirectoryInfo.New(c_sharePath);

        // Stream
        using (var stream = fileSystem.File.Open(Path, System.IO.FileMode.Open))
        {
            // Do stuff...
        }
    }

    /// <summary>
    /// With Stored Credentials
    /// </summary>
    public void StoreCredentialsForShare(NetworkCredential credential)
    {
        string? domain = credential.Domain;
        string? username = credential.UserName;
        string? password = credential.Password;

        // You can add/cache credentials for a share by using the credentials outside a using block, perhaps here, at app startup, or elsewhere, by specifying false for the last param.
        SmbCredential.AddToProvider(domain, username, password, c_sharePath, credentialProvider, false);
    }

    public void UseStoredCredentialsForFileOp()
    {
        // FileInfo
        var fileInfo = fileSystem.FileInfo.New(Path);

        // DirectoryInfo
        var directoryInfo = fileSystem.DirectoryInfo.New(c_sharePath);

        // Stream
        using (var stream = fileSystem.File.Open(Path, System.IO.FileMode.Open))
        {
            // Do stuff...
        }
    }
}

# SmbAbstraction

This project is a fork of https://github.com/jordanlytle/SmbAbstraction with a few key differences: 1) It uses the original SMBLibrary as opposed to SMBLibraryLite, 2) It conforms to newer versions of https://github.com/TestableIO/System.IO.Abstractions interfaces, and 3) it also targets .Net Standard 2.0. This library implements the System.IO.Abstractions interfaces for interacting with the filesystem and adds support for interacting with UNC or SMB paths. The intent is to provide an intuitive way to operate against SMB/UNC shares along with being able to operate on UNC shares from Linux/OSX or even Windows clients outside of AD without having to use 'PInvoke' to wrap file share access. This project isn't popular (yet?) and is not guaranteed to work for your specific application.

# Usage

## Examples

Example projects are available to view in `InkSoft.SmbAbstraction.Examples`

## Basic (Program.cs top level statement file)
```CSharp
using InkSoft.SmbAbstraction;
using System.IO.Abstractions;

const string c_domain = "domain";
const string c_username = "username";
const string c_password = "password";
const string? c_sharePath = @"\\host\ShareName"; // e.g. \\host\ShareName or smb://host/sharename

ISmbCredentialProvider credentialProvider = new SmbCredentialProvider();
ISmbClientFactory clientFactory = new Smb2ClientFactory();
IFileSystem fileSystem = new SmbFileSystem(clientFactory, credentialProvider, null, null);
string path = fileSystem.Path.Combine(c_sharePath, "test.txt");

// SMBCredential will parse the share path from path
using var credential = SmbCredential.AddToProvider(c_domain, c_username, c_password, c_sharePath, credentialProvider);

// FileInfo
var fileInfo = fileSystem.FileInfo.New(path);

// DirectoryInfo
var directoryInfo = fileSystem.DirectoryInfo.New(path);

// Stream
using (var stream = fileSystem.File.Open(path, System.IO.FileMode.Open))
{
    // Do stuff...
}
```

## Dependency Injection

### Registering Services
```CSharp
public static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging => { logging.AddConsole(); logging.AddDebug(); })
    .ConfigureServices((hostContext, services) => services
        .AddSingleton<ISmbCredentialProvider>(p => new SmbCredentialProvider())
        // If you don't want to use SmbFileSystem as the default IFileSystem, you can use something like following line instead for specifically requesting it via [FromKeyedServices(nameof(SmbFileSystem))]
        // without using the SmbFileSystem type. This makes it such that you can unit-test code with MockFileSystem by registering MockFileSystem to the keyed service instead of an actual SmbFileSystem.
        .AddKeyedSingleton<IFileSystem>(nameof(SmbFileSystem), (p, _) => new SmbFileSystem(new Smb2ClientFactory(), p.GetRequiredService<ISmbCredentialProvider>(), null, p.GetRequiredService<ILoggerFactory>()))
        // Otherwise, this makes SmbFileSystem the default file system:
        .AddSingleton<IFileSystem>(p => new SmbFileSystem(
            new Smb2ClientFactory(),
            p.GetRequiredService<ISmbCredentialProvider>(),
            null,
            p.GetRequiredService<ILoggerFactory>()
        )));
}
```

### Making calls
```CSharp
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
```
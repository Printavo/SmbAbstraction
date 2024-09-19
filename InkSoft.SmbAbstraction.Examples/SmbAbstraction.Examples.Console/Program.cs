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
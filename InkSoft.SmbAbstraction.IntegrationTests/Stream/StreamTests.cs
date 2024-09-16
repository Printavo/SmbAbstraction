using System;
using System.IO.Abstractions;
using System.Linq;
using Xunit;

namespace InkSoft.SmbAbstraction.IntegrationTests.Stream;

public abstract class StreamTests
{
    readonly TestFixture _fixture;
    readonly IFileSystem _fileSystem;

    protected StreamTests(TestFixture fixture)
    {
        _fixture = fixture;
        _fileSystem = _fixture.FileSystem;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CheckStreamLength()
    {
        string? tempFileName = $"temp-CheckStreamLength-{DateTime.Now.ToFileTimeUtc()}.txt";
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());
        string? tempFilePath = _fileSystem.Path.Combine(_fixture.LocalTempDirectory, tempFileName);
        byte[]? byteArray = new byte[100];
        using var credential = new SmbCredential(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider);

        if (!_fileSystem.File.Exists(tempFilePath))
        {
            using var stream = _fileSystem.File.Create(tempFilePath);
            stream.Write(byteArray, 0, 100);
        }

        var fileInfo = _fileSystem.FileInfo.New(tempFilePath);
        long fileSize = fileInfo.Length;
        string? destinationFilePath = _fileSystem.Path.Combine(directory, tempFileName);
        fileInfo = fileInfo.CopyTo(destinationFilePath);
        Assert.True(fileInfo.Exists);
            
        using (var stream = fileInfo.OpenRead())
        {
            Assert.Equal(stream.Length, fileSize);
        }

        _fileSystem.File.Delete(fileInfo.FullName);
    }
}
using System;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace InkSoft.SmbAbstraction.IntegrationTests.DirectoryInfo;

public abstract class DirectoryInfoTests
{
    readonly TestFixture _fixture;
    readonly IFileSystem _fileSystem;

    public DirectoryInfoTests(TestFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture.WithLoggerFactory(outputHelper.ToLoggerFactory());
        _fileSystem = _fixture.FileSystem;
    }


    [Fact, Trait("Category", "Integration")]
    public void CanCreateNewDirectoryInfo()
    {
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());

        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider);

        var directoryInfo = _fileSystem.DirectoryInfo.New(directory);

        Assert.NotNull(directoryInfo);
    }

    [Fact, Trait("Category", "Integration")]
    public void CanCreateNewDirectoryInfo_WithTrailingSeparator()
    {
        var credentials = _fixture.ShareCredentials;

        char trailingSeparator = (_fixture.PathType == PathType.SmbUri || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ? '/' : '\\';
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First()) + trailingSeparator;

        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider);

        var directoryInfo = _fileSystem.DirectoryInfo.New(directory);

        Assert.NotNull(directoryInfo);
    }

    [Fact, Trait("Category", "Integration")]
    public void CheckMoveDirectory()
    {
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());
        string? newDirectory = _fileSystem.Path.Combine(directory, $"{DateTime.Now.ToFileTimeUtc()}");

        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider);

        string? createDirectoryPath = _fileSystem.Path.Combine(_fixture.RootPath, $"test-move-local-directory-{DateTime.Now.ToFileTimeUtc()}");
        var directoryInfo = _fileSystem.Directory.CreateDirectory(createDirectoryPath);
            
        directoryInfo.MoveTo(newDirectory);

        Assert.True(_fileSystem.Directory.Exists(newDirectory));

        _fileSystem.Directory.Delete(newDirectory);
    }

    [Fact, Trait("Category", "Integration")]
    public void CheckMoveDirectory_WithTrailingSeparator()
    {
        var credentials = _fixture.ShareCredentials;

        char trailingSeparator = (_fixture.PathType == PathType.SmbUri || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ? '/' : '\\';
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First()) + trailingSeparator;
        string? newDirectory = _fileSystem.Path.Combine(directory, $"{DateTime.Now.ToFileTimeUtc()}") + trailingSeparator;

        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider);

        string? createDirectoryPath = _fileSystem.Path.Combine(_fixture.RootPath, $"test-move-local-directory-{DateTime.Now.ToFileTimeUtc()}");
        var directoryInfo = _fileSystem.Directory.CreateDirectory(createDirectoryPath);

        directoryInfo.MoveTo(newDirectory);

        Assert.True(_fileSystem.Directory.Exists(newDirectory));

        _fileSystem.Directory.Delete(newDirectory);
    }
}
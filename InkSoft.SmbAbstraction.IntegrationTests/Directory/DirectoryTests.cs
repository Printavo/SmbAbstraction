using System;
using Xunit;
using System.Linq;
using System.IO.Abstractions;
using Xunit.Abstractions;
using System.Runtime.InteropServices;

namespace InkSoft.SmbAbstraction.IntegrationTests.Directory;

public abstract class DirectoryTests
{
    private string _createdTestDirectoryPath;
    readonly TestFixture _fixture;
    readonly IFileSystem _fileSystem;

    public DirectoryTests(TestFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture.WithLoggerFactory(outputHelper.ToLoggerFactory());
        _fileSystem = _fixture.FileSystem;
    }

    public void Dispose() => _fileSystem.Directory.Delete(_createdTestDirectoryPath);

    [Fact, Trait("Category", "Integration")]
    public void CanCopyFolderRecursively()
    {
        using var credential = SmbCredential.AddToProvider(_fixture.ShareCredentials.Domain, _fixture.ShareCredentials.Username, _fixture.ShareCredentials.Password, _fixture.RootPath, _fixture.SmbCredentialProvider);
        string testRunId = Guid.NewGuid().ToString("N");
        string sourcePath = _fileSystem.Path.Combine(_fixture.RootPath, $"{nameof(CanCopyFolderRecursively)}Source{testRunId}");
        string destinationPath = _fileSystem.Path.Combine(_fixture.RootPath, $"{nameof(CanCopyFolderRecursively)}Destination{testRunId}");
        const string c_tripleNestedSubfolder = @"hello\123\456";
        const string c_helloWorld = "Hello, World!";
        var tripleNestedSourcePath = _fileSystem.Directory.CreateDirectory(_fileSystem.Path.Combine(sourcePath, c_tripleNestedSubfolder));
        var path2 = _fileSystem.Directory.CreateDirectory(_fileSystem.Path.Combine(sourcePath, @"again\123"));
        var path3 = _fileSystem.Directory.CreateDirectory(_fileSystem.Path.Combine(sourcePath, "another"));
        _fileSystem.File.WriteAllText(_fileSystem.Path.Combine(sourcePath, "file.txt"), testRunId);
        _fileSystem.File.WriteAllText(_fileSystem.Path.Combine(tripleNestedSourcePath.FullName, "file.txt"), testRunId);
        _fileSystem.File.WriteAllText(_fileSystem.Path.Combine(tripleNestedSourcePath.Parent!.FullName, "file.txt"), testRunId);
        _fileSystem.File.WriteAllText(_fileSystem.Path.Combine(path2.FullName, "file.txt"), testRunId);
        _fileSystem.File.WriteAllText(_fileSystem.Path.Combine(path3.FullName, "file.txt"), testRunId);
        var logger = _fixture.LoggerFactory.CreateLogger(nameof(CanCopyFolderRecursively));
        _fileSystem.Directory.Copy(sourcePath, destinationPath, true, logger);
        Assert.Equal(testRunId, _fileSystem.File.ReadAllText(_fileSystem.Path.Combine(destinationPath, c_tripleNestedSubfolder, "file.txt")));

        // Confirming we won't throw and it actually overwrites if we try to copy twice with overwrite.
        _fileSystem.File.WriteAllText(_fileSystem.Path.Combine(tripleNestedSourcePath.FullName, "file.txt"), c_helloWorld);
        // Testing with null logger param, while we're at it.
        _fileSystem.Directory.Copy(sourcePath, destinationPath, true);
        Assert.Equal(c_helloWorld, _fileSystem.File.ReadAllText(_fileSystem.Path.Combine(destinationPath, c_tripleNestedSubfolder, "file.txt")));

        // Confirming we do not overwrite when we request to not overwrite.
        _fileSystem.File.WriteAllText(_fileSystem.Path.Combine(tripleNestedSourcePath.FullName, "file.txt"), "This text should be in the source file, but not copied to destination.");
        _fileSystem.Directory.Copy(sourcePath, destinationPath, false, logger);
        Assert.Equal(c_helloWorld, _fileSystem.File.ReadAllText(_fileSystem.Path.Combine(destinationPath, c_tripleNestedSubfolder, "file.txt")));
    }

    [Fact, Trait("Category", "Integration")]
    public void CanCreateDirectoryInRootDirectory()
    {
        var credentials = _fixture.ShareCredentials;
        _createdTestDirectoryPath = _fileSystem.Path.Combine(_fixture.RootPath, $"test_directory-{DateTime.Now.ToFileTimeUtc()}");
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, _createdTestDirectoryPath, _fixture.SmbCredentialProvider);
        var directoryInfo = _fileSystem.Directory.CreateDirectory(_createdTestDirectoryPath);
        Assert.True(_fileSystem.Directory.Exists(_createdTestDirectoryPath));
    }

    [Fact, Trait("Category", "Integration")]
    public void CanCreateDirectoryInRootDirectory_WithTrailingSeparator()
    {
        var credentials = _fixture.ShareCredentials;
        char trailingSeparator = (_fixture.PathType == PathType.SmbUri || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ? '/' : '\\';
        _createdTestDirectoryPath = _fileSystem.Path.Combine(_fixture.RootPath, $"test_directory-{DateTime.Now.ToFileTimeUtc()}") + trailingSeparator;
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, _createdTestDirectoryPath, _fixture.SmbCredentialProvider);
        var directoryInfo = _fileSystem.Directory.CreateDirectory(_createdTestDirectoryPath);
        Assert.True(_fileSystem.Directory.Exists(_createdTestDirectoryPath));
    }


    [Fact, Trait("Category", "Integration")]
    public void CheckCreateDirectoryForExistingDirectory()
    {
        var credentials = _fixture.ShareCredentials;
        string? existingDirectory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, existingDirectory, _fixture.SmbCredentialProvider);
        var existingDirectoryInfo = _fileSystem.DirectoryInfo.New(existingDirectory);
        var directoryInfo = _fileSystem.Directory.CreateDirectory(existingDirectory);
        Assert.Equal(existingDirectoryInfo.FullName, directoryInfo.FullName);
    }

    [Fact, Trait("Category", "Integration")]
    public void CheckCreateDirectoryForExistingDirectory_WithTrailingSeparator()
    {
        var credentials = _fixture.ShareCredentials;
        char trailingSeparator = (_fixture.PathType == PathType.SmbUri || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ? '/' : '\\';
        string? existingDirectory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First()) + trailingSeparator;
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, existingDirectory, _fixture.SmbCredentialProvider);
        var existingDirectoryInfo = _fileSystem.DirectoryInfo.New(existingDirectory);
        var directoryInfo = _fileSystem.Directory.CreateDirectory(existingDirectory);
        Assert.Equal(existingDirectoryInfo.FullName, directoryInfo.FullName);
    }


    [Fact, Trait("Category", "Integration")]
    public void CanCreateNestedDirectoryInRootDirectory()
    {
        var credentials = _fixture.ShareCredentials;
        string? parentDirectoryPath = _fileSystem.Path.Combine(_fixture.RootPath, $"test_directory_parent-{DateTime.Now.ToFileTimeUtc()}");
        _createdTestDirectoryPath = _fileSystem.Path.Combine(parentDirectoryPath, $"test_directory_child-{DateTime.Now.ToFileTimeUtc()}");
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, _createdTestDirectoryPath, _fixture.SmbCredentialProvider);
        var directoryInfo = _fileSystem.Directory.CreateDirectory(_createdTestDirectoryPath);
        Assert.True(_fileSystem.Directory.Exists(_createdTestDirectoryPath));
        _fileSystem.Directory.Delete(_createdTestDirectoryPath);
    }

    [Fact, Trait("Category", "Integration")]
    public void CanCreateNestedDirectoryInRootDirectory_WithTrailingSeparator()
    {
        var credentials = _fixture.ShareCredentials;
        char trailingSeparator = (_fixture.PathType == PathType.SmbUri || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ? '/' : '\\';
        string? parentDirectoryPath = _fileSystem.Path.Combine(_fixture.RootPath, $"test_directory_parent-{DateTime.Now.ToFileTimeUtc()}") + trailingSeparator;
        _createdTestDirectoryPath = _fileSystem.Path.Combine(parentDirectoryPath, $"test_directory_child-{DateTime.Now.ToFileTimeUtc()}") + trailingSeparator;
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, _createdTestDirectoryPath, _fixture.SmbCredentialProvider);
        var directoryInfo = _fileSystem.Directory.CreateDirectory(_createdTestDirectoryPath);
        Assert.True(_fileSystem.Directory.Exists(_createdTestDirectoryPath));
        _fileSystem.Directory.Delete(_createdTestDirectoryPath);
    }

    [Fact, Trait("Category", "Integration")]
    public void CanEnumerateFilesRootDirectory()
    {
        var credentials = _fixture.ShareCredentials;
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, _fixture.RootPath, _fixture.SmbCredentialProvider);
        var files = _fileSystem.Directory.EnumerateFiles(_fixture.RootPath, "*").ToList();
        Assert.True(files.Count >= 0); //Include 0 in case directory is empty. If an exception is thrown, the test will fail.
    }

    [Fact, Trait("Category", "Integration")]
    public void CanEnumerateFilesRootDirectory_WithTrailingSeparator()
    {
        var credentials = _fixture.ShareCredentials;
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, _fixture.RootPath, _fixture.SmbCredentialProvider);
        char trailingSeparator = (_fixture.PathType == PathType.SmbUri || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ? '/' : '\\';
        var files = _fileSystem.Directory.EnumerateFiles($"{_fixture.RootPath}{trailingSeparator}", "*").ToList();
        Assert.True(files.Count >= 0); //Include 0 in case directory is empty. If an exception is thrown, the test will fail.
    }

    [Fact, Trait("Category", "Integration")]
    public void CanEnumerateFileSystemEntries()
    {
        var credentials = _fixture.ShareCredentials;
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, _fixture.RootPath, _fixture.SmbCredentialProvider);
        var fileSystemEntries = _fileSystem.Directory.EnumerateFileSystemEntries(_fixture.RootPath).ToList();
        Assert.True(fileSystemEntries.Count >= 0);
    }

    [Fact, Trait("Category", "Integration")]
    public void CanEnumerateFileSystemEntries_WithTrailingSeparator()
    {
        var credentials = _fixture.ShareCredentials;
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, _fixture.RootPath, _fixture.SmbCredentialProvider);
        char trailingSeparator = (_fixture.PathType == PathType.SmbUri || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ? '/' : '\\';
        var fileSystemEntries = _fileSystem.Directory.EnumerateFileSystemEntries($"{_fixture.RootPath}{trailingSeparator}").ToList();
        Assert.True(fileSystemEntries.Count >= 0);
    }

    [Fact, Trait("Category", "Integration")]
    public void CheckDirectoryExists()
    {
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, _fixture.RootPath, _fixture.SmbCredentialProvider);
        Assert.True(_fileSystem.Directory.Exists(directory));
    }

    [Fact, Trait("Category", "Integration")]
    public void CheckDirectoryDoesNotExistWhenPathIsFile()
    {
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Files.First());
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, _fixture.RootPath, _fixture.SmbCredentialProvider);
        Assert.False(_fileSystem.Directory.Exists(directory));
    }

    [Fact, Trait("Category", "Integration")]
    public void CheckDirectoryExists_WithTrailingSeparator()
    {
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, _fixture.RootPath, _fixture.SmbCredentialProvider);
        char trailingSeparator = (_fixture.PathType == PathType.SmbUri || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ? '/' : '\\';
        Assert.True(_fileSystem.Directory.Exists($"{directory}{trailingSeparator}"));
    }
}
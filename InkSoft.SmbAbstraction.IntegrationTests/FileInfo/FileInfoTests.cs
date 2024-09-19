using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace InkSoft.SmbAbstraction.IntegrationTests.FileInfo;

public abstract class FileInfoTests
{
    readonly TestFixture _fixture;
    readonly IFileSystem _fileSystem;

    public FileInfoTests(TestFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture.WithLoggerFactory(outputHelper.ToLoggerFactory());
        _fileSystem = _fixture.FileSystem;
    }

    [Fact, Trait("Category", "Integration")]
    public void CanCreateFileInfo()
    {
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());
        string? filePath = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Files.First());

        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider);

        var fileInfo = _fileSystem.FileInfo.New(filePath);

        Assert.NotNull(fileInfo);
    }

    [Fact, Trait("Category", "Integration")]
    public void CopyFromLocalDirectoryToShareDirectory()
    {
        string? tempFileName = $"temp-{DateTime.Now.ToFileTimeUtc()}.txt";
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());
        string? tempFilePath = _fileSystem.Path.Combine(_fixture.LocalTempDirectory, tempFileName);

        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider);

        if (!_fileSystem.File.Exists(tempFilePath))
        {
            using var streamWriter = new StreamWriter(_fileSystem.File.Create(tempFilePath));
            streamWriter.WriteLine("Test");
        }

        var fileInfo = _fileSystem.FileInfo.New(tempFilePath);
        fileInfo = fileInfo.CopyTo(_fileSystem.Path.Combine(directory, tempFileName));
            
        Assert.True(fileInfo.Exists);

        _fileSystem.File.Delete(tempFilePath);
        _fileSystem.File.Delete(fileInfo.FullName);
    }

    [Fact, Trait("Category", "Integration")]
    public void CheckFileSize()
    {
        string? tempFileName = $"temp-{DateTime.Now.ToFileTimeUtc()}.txt";
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());
        string? tempFilePath = _fileSystem.Path.Combine(_fixture.LocalTempDirectory, tempFileName);

        byte[]? byteArray = new byte[100];

        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider);

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
        Assert.Equal(fileSize, fileInfo.Length);

        _fileSystem.File.Delete(fileInfo.FullName);
        _fileSystem.File.Delete(tempFilePath);
    }

    [Fact, Trait("Category", "Integration")]
    public void CheckFileExists()
    {
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());
        string? filePath = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Files.First());
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider);
        bool exists = _fileSystem.FileInfo.New(filePath).Exists;
        Assert.True(exists);
    }

    [Fact, Trait("Category", "Integration")]
    public void CheckFileExtensionMatches()
    {
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());
        string? filePath = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Files.First());
        string? fileExtension = _fileSystem.Path.GetExtension(filePath);
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider);
        string? extenstion = _fileSystem.FileInfo.New(filePath).Extension;
        Assert.Equal(fileExtension, extenstion);
    }

    [Fact, Trait("Category", "Integration")]
    public void CheckFullNameMatches()
    {
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());
        string? filePath = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Files.First());
        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider);
        string? fullName = _fileSystem.FileInfo.New(filePath).FullName;
        Assert.Equal(filePath, fullName);
    }

    [Fact, Trait("Category", "Integration")]
    public void CheckReplaceWithBackup()
    {
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());

        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider);

        long originalFileTime = DateTime.Now.ToFileTimeUtc();
        string? originalFilePath = _fileSystem.Path.Combine(directory, $"replace-file-{originalFileTime}.txt");
        string? originalFileBackupPath = _fileSystem.Path.Combine(directory, $"replace-file-{originalFileTime}.bak");

        if (!_fileSystem.File.Exists(originalFilePath))
        {
            using var streamWriter = new StreamWriter(_fileSystem.File.Create(originalFilePath));
            streamWriter.WriteLine($"{originalFileTime}");
        }

        long newFileTime = DateTime.Now.ToFileTimeUtc();
        string? newFilePath = _fileSystem.Path.Combine(_fixture.RootPath, $"replace-file-{newFileTime}.txt");

        if (!_fileSystem.File.Exists(newFilePath))
        {
            using var streamWriter = new StreamWriter(_fileSystem.File.Create(newFilePath));
            streamWriter.WriteLine($"{newFileTime}");
        }

        var newFileInfo = _fileSystem.FileInfo.New(newFilePath);

        newFileInfo = newFileInfo.Replace(originalFilePath, originalFileBackupPath);

        Assert.Equal(originalFilePath, newFileInfo.FullName);
        Assert.False(_fileSystem.File.Exists(newFilePath));
        Assert.True(_fileSystem.File.Exists(originalFileBackupPath));

        using (var streamReader = new StreamReader(_fileSystem.File.OpenRead(newFileInfo.FullName)))
        {
            string? line = streamReader.ReadLine();

            Assert.Equal(newFileTime.ToString(), line);
        }

        _fileSystem.File.Delete(originalFilePath);
        _fileSystem.File.Delete(originalFileBackupPath);
    }

    [Fact, Trait("Category", "Integration")]
    public void CheckReplaceWithoutBackup()
    {
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());

        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider);

        long originalFileTime = DateTime.Now.ToFileTimeUtc();
        string? originalFilePath = _fileSystem.Path.Combine(directory, $"replace-file-{originalFileTime}.txt");

        if (!_fileSystem.File.Exists(originalFilePath))
        {
            using var streamWriter = new StreamWriter(_fileSystem.File.Create(originalFilePath));
            streamWriter.WriteLine($"{originalFileTime}");
        }

        long newFileTime = DateTime.Now.ToFileTimeUtc();
        string? newFilePath = _fileSystem.Path.Combine(_fixture.RootPath, $"replace-file-{newFileTime}.txt");

        if (!_fileSystem.File.Exists(newFilePath))
        {
            using var streamWriter = new StreamWriter(_fileSystem.File.Create(newFilePath));
            streamWriter.WriteLine($"{newFileTime}");
        }

        var newFileInfo = _fileSystem.FileInfo.New(newFilePath);

        newFileInfo = newFileInfo.Replace(originalFilePath, null);

        Assert.Equal(originalFilePath, newFileInfo.FullName);
        Assert.False(_fileSystem.File.Exists(newFilePath));

        using (var streamReader = new StreamReader(_fileSystem.File.OpenRead(newFileInfo.FullName)))
        {
            string? line = streamReader.ReadLine();

            Assert.Equal(newFileTime.ToString(), line);
        }

        _fileSystem.File.Delete(originalFilePath);
    }

    [Fact, Trait("Category", "Integration")]
    public void TestCopyWithOverride()
    {
        string? tempFileName = $"temp-copyto-override-{DateTime.Now.ToFileTimeUtc()}.txt";
        var credentials = _fixture.ShareCredentials;
        string? testFilePath = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Files.First());
        string? tempFilePath = _fileSystem.Path.Combine(_fixture.RootPath, tempFileName);

        using var credential = SmbCredential.AddToProvider(credentials.Domain, credentials.Username, credentials.Password, _fixture.RootPath, _fixture.SmbCredentialProvider);

        if (!_fileSystem.File.Exists(tempFilePath))
        {
            var stream = _fileSystem.File.Create(tempFilePath);
            stream.Close();
        }

        var testFileInfo = _fileSystem.FileInfo.New(testFilePath);

        testFileInfo.CopyTo(tempFilePath, overwrite: true);


        Assert.True(_fileSystem.File.Exists(tempFilePath));

        _fileSystem.File.Delete(tempFilePath);
    }


}
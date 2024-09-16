using System.IO.Abstractions;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace InkSoft.SmbAbstraction.IntegrationTests.DriveInfo;

public abstract class DriveInfoTests
{
    readonly TestFixture _fixture;
    private IFileSystem _fileSystem;

    public DriveInfoTests(TestFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture.WithLoggerFactory(outputHelper.ToLoggerFactory());
        _fileSystem = _fixture.FileSystem;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void FromDriveName_ReturnsNotNull()
    {
        var credentials = _fixture.ShareCredentials;

        _fixture.SmbCredentialProvider.AddSmbCredential(new SmbCredential(credentials.Domain, credentials.Username, credentials.Password, _fixture.RootPath, _fixture.SmbCredentialProvider));

        var smbDriveInfoFactory = new SmbDriveInfoFactory(_fileSystem, _fixture.SmbCredentialProvider, _fixture.SmbClientFactory, 65536);

        var shareInfo = smbDriveInfoFactory.New(_fixture.ShareName);

        Assert.NotNull(shareInfo);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void FromDriveName_WithFileName_ReturnsNotNull()
    {
        var credentials = _fixture.ShareCredentials;
        string? fileName = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Files.First());

        _fixture.SmbCredentialProvider.AddSmbCredential(new SmbCredential(credentials.Domain, credentials.Username, credentials.Password, fileName, _fixture.SmbCredentialProvider));

        var smbDriveInfoFactory = new SmbDriveInfoFactory(_fileSystem, _fixture.SmbCredentialProvider, _fixture.SmbClientFactory, 65536);

        var shareInfo = smbDriveInfoFactory.New(fileName);

        Assert.NotNull(shareInfo);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void FromDriveName_WithDirectory_ReturnsNotNull()
    {
        var credentials = _fixture.ShareCredentials;
        string? directory = _fileSystem.Path.Combine(_fixture.RootPath, _fixture.Directories.First());

        _fixture.SmbCredentialProvider.AddSmbCredential(new SmbCredential(credentials.Domain, credentials.Username, credentials.Password, directory, _fixture.SmbCredentialProvider));

        var smbDriveInfoFactory = new SmbDriveInfoFactory(_fileSystem, _fixture.SmbCredentialProvider, _fixture.SmbClientFactory, 65536);

        var shareInfo = smbDriveInfoFactory.New(directory);

        Assert.NotNull(shareInfo);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GetDrives_WithCredentials_ReturnsNotNull()
    {
        var credentials = _fixture.ShareCredentials;
            
        _fixture.SmbCredentialProvider.AddSmbCredential(new SmbCredential(credentials.Domain, credentials.Username, credentials.Password, _fixture.RootPath, _fixture.SmbCredentialProvider));

        var smbDriveInfoFactory = new SmbDriveInfoFactory(_fileSystem, _fixture.SmbCredentialProvider, _fixture.SmbClientFactory, 65536);

        var shares = smbDriveInfoFactory.GetDrives();

        Assert.NotNull(shares);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GetDrives_WithNoCredentials_ReturnsNotNull()
    {
        var credentials = _fixture.ShareCredentials;

        var smbDriveInfoFactory = new SmbDriveInfoFactory(_fileSystem, _fixture.SmbCredentialProvider, _fixture.SmbClientFactory, 65536);

        var shares = smbDriveInfoFactory.GetDrives();

        Assert.NotNull(shares);
    }
}
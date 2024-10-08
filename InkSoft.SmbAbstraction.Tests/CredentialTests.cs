using Xunit;
using FakeItEasy;

namespace InkSoft.SmbAbstraction.Tests.Path;

public class CredentialTests
{
    private readonly string _domain = "domain";
    private readonly string _userName = "user";
    private readonly string _path = "\\\\host\\sharename";

    [Fact]
    public void SetDomainNameFromUserNameIfNull()
    {
        var credential = SmbCredential.AddToProvider(null, $"{_domain}\\{_userName}", "password", _path, A.Fake<ISmbCredentialProvider>()); 
        Assert.Equal(_domain, credential.Domain);
        Assert.Equal(_userName, credential.Username);
    }

    [Fact]
    public void DoNotSetDomainNameFromUserNameIfNotNull()
    {
        string? domain = "domain";
        string? userName = "user";
        string? combinedUserName = $"{domain}\\{userName}";

        var credential = SmbCredential.AddToProvider(domain, combinedUserName, "password", _path, A.Fake<ISmbCredentialProvider>()); 
        Assert.Equal(domain, credential.Domain);
        Assert.Equal(combinedUserName, credential.Username);
    }
}
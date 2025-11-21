using Xunit;
using FakeItEasy;

namespace InkSoft.SmbAbstraction.Tests.Path;

public class CredentialTests
{
    private const string c_domain = "domain";
    private const string c_userName = "user";
    private const string c_path = "\\\\host\\sharename";

    [Fact]
    public void SetDomainNameFromUserNameIfNull()
    {
        var credential = SmbCredential.AddToProvider(null, $"{c_domain}\\{c_userName}", "password", c_path, A.Fake<ISmbCredentialProvider>());
        Assert.Equal(c_domain, credential.Domain);
        Assert.Equal(c_userName, credential.Username);
    }

    [Fact]
    public void DoNotSetDomainNameFromUserNameIfNotNull()
    {
        string? domain = "domain";
        string? userName = "user";
        string? combinedUserName = $"{domain}\\{userName}";

        var credential = SmbCredential.AddToProvider(domain, combinedUserName, "password", c_path, A.Fake<ISmbCredentialProvider>());
        Assert.Equal(domain, credential.Domain);
        Assert.Equal(combinedUserName, credential.Username);
    }
}
using FakeItEasy;
using SMBLibrary;
using System.Collections.Generic;
using System.Net;
using Xunit;

namespace InkSoft.SmbAbstraction.Tests.Path;

public class SmbConnectionTests
{
    public SmbConnectionTests()
    {
    }

    [Fact]
    public void ThrowExceptionForInvalidCredential()
    {
        string? domain = "domain";
        string? userName = "user";
        string? password = "password";
        string? path = "\\\\host\\sharename";
        var ipAddress = IPAddress.Parse("127.0.0.1");

        var credentials = new List<SmbCredential>() {
            new(null, userName, password, path, A.Fake<ISmbCredentialProvider>()),
            new(domain, null, password, path, A.Fake<ISmbCredentialProvider>()),
            new(domain, userName, null, path, A.Fake<ISmbCredentialProvider>())
        };

        foreach(var credential in credentials)
        {
            Assert.Throws<InvalidCredentialException>(() => { SmbConnection.CreateSmbConnection(A.Fake<ISmbClientFactory>(), ipAddress, SMBTransportType.DirectTCPTransport, credential, 0); });
        }
    }
}
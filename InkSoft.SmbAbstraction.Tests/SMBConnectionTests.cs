using FakeItEasy;
using SMBLibrary;
using System.Collections.Generic;
using System.Net;
using Xunit;

namespace InkSoft.SmbAbstraction.Tests.Path;

public class SmbConnectionTests
{
    [Fact]
    public void ThrowExceptionForInvalidCredential()
    {
        const string? c_domain = "domain";
        const string? c_userName = "user";
        const string? c_password = "password";
        const string? c_path = "\\\\host\\sharename";
        var ipAddress = IPAddress.Parse("127.0.0.1");

        var credentials = new List<SmbCredential>
        {
            SmbCredential.AddToProvider(null, c_userName, c_password, c_path, A.Fake<ISmbCredentialProvider>()),
            SmbCredential.AddToProvider(c_domain, null, c_password, c_path, A.Fake<ISmbCredentialProvider>()),
            SmbCredential.AddToProvider(c_domain, c_userName, null, c_path, A.Fake<ISmbCredentialProvider>())
        };

        foreach(var credential in credentials)
        {
            Assert.Throws<InvalidCredentialException>(() => { SmbConnection.CreateSmbConnection(A.Fake<ISmbClientFactory>(), ipAddress, SMBTransportType.DirectTCPTransport, credential, new(){MaxBufferSize = 0}); });
        }
    }
}
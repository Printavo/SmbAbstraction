using FakeItEasy;
using SMBLibrary;
using SMBLibrary.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Xunit;

namespace InkSoft.SmbAbstraction.Tests;

public class SmbStreamTests
{
    private const int c_negotiatedSize = 1024 * 1024;

    private readonly ISMBFileStore _fileStore = A.Fake<ISMBFileStore>();

    private readonly List<(long Offset, byte[] Data)> _writes = [];

    public SmbStreamTests()
    {
        int ignored;
        A.CallTo(() => _fileStore.WriteFile(out ignored, A<object>._, A<long>._, A<byte[]>._))
            .ReturnsLazily((int _, object _, long offset, byte[] data) =>
            {
                _writes.Add((offset, data));
                return NTStatus.STATUS_SUCCESS;
            })
            .AssignsOutAndRefParametersLazily((int _, object _, long _, byte[] data) => [data.Length]);
    }

    private SmbStream CreateStream(uint maxBufferSize)
    {
        var options = new SmbFileSystemOptions { MaxBufferSize = maxBufferSize };
        var client = A.Fake<ISMBClient>();
        A.CallTo(() => client.Connect(A<IPAddress>._, A<SMBTransportType>._)).Returns(true);
        A.CallTo(() => client.MaxWriteSize).Returns((uint)c_negotiatedSize);
        A.CallTo(() => client.MaxReadSize).Returns((uint)c_negotiatedSize);
        var clientFactory = A.Fake<ISmbClientFactory>();
        A.CallTo(() => clientFactory.CreateClient(A<SmbFileSystemOptions?>._)).Returns(client);
        var credential = SmbCredential.AddToProvider("domain", "user", "password", "\\\\host\\share", A.Fake<ISmbCredentialProvider>());
        var connection = SmbConnection.CreateSmbConnectionForStream(clientFactory, IPAddress.Loopback, SMBTransportType.DirectTCPTransport, credential, options);
        return new(_fileStore, new(), connection, 0, options);
    }

    [Theory]
    [InlineData(65536u, 65536)]
    [InlineData(4u * 1024 * 1024, c_negotiatedSize)]
    [InlineData(0u, c_negotiatedSize)]
    public void LargeWriteIsSplitIntoChunks(uint maxBufferSize, int expectedChunkSize)
    {
        byte[] bytes = Enumerable.Range(0, 3 * c_negotiatedSize + 123).Select(i => (byte)i).ToArray();

        using (var stream = CreateStream(maxBufferSize))
            stream.Write(bytes, 0, bytes.Length);

        Assert.All(_writes, w => Assert.True(w.Data.Length <= expectedChunkSize));
        Assert.Equal(expectedChunkSize, _writes[0].Data.Length);
        Assert.Equal(bytes, _writes.SelectMany(w => w.Data));

        long expectedOffset = 0;
        foreach (var write in _writes)
        {
            Assert.Equal(expectedOffset, write.Offset);
            expectedOffset += write.Data.Length;
        }
    }

    [Fact]
    public void WriteHonorsBufferOffset()
    {
        byte[] bytes = Enumerable.Range(0, 200_000).Select(i => (byte)i).ToArray();
        const int c_chunk = 64 * 1024;

        using (var stream = CreateStream(65536))
        {
            for (int offset = 0; offset < bytes.Length; offset += c_chunk)
                stream.Write(bytes, offset, Math.Min(c_chunk, bytes.Length - offset));

            Assert.Equal(bytes.Length, stream.Position);
        }

        Assert.Equal(bytes, _writes.SelectMany(w => w.Data));
    }

    [Theory]
    [InlineData(SeekOrigin.Begin, 10, 10)]
    [InlineData(SeekOrigin.Current, 10, 110)]
    [InlineData(SeekOrigin.End, 0, 1000)]
    [InlineData(SeekOrigin.End, -10, 990)]
    public void SeekSetsPosition(SeekOrigin origin, long offset, long expectedPosition)
    {
        FileInformation ignored;
        A.CallTo(() => _fileStore.GetFileInformation(out ignored, A<object>._, FileInformationClass.FileStandardInformation))
            .Returns(NTStatus.STATUS_SUCCESS)
            .AssignsOutAndRefParameters(new FileStandardInformation { EndOfFile = 1000 });

        using var stream = CreateStream(65536);
        stream.Position = 100;

        Assert.Equal(expectedPosition, stream.Seek(offset, origin));
        Assert.Equal(expectedPosition, stream.Position);
    }

    [Fact]
    public void ReadRequestsAreCappedAtMaxBufferSize()
    {
        byte[] ignored;
        A.CallTo(() => _fileStore.ReadFile(out ignored, A<object>._, A<long>._, A<int>._))
            .Returns(NTStatus.STATUS_SUCCESS)
            .AssignsOutAndRefParametersLazily((byte[] _, object _, long _, int maxCount) => [new byte[maxCount]]);

        using var stream = CreateStream(65536);
        int read = stream.Read(new byte[c_negotiatedSize], 0, c_negotiatedSize);

        Assert.Equal(65536, read);
        A.CallTo(() => _fileStore.ReadFile(out ignored, A<object>._, A<long>._, 65536)).MustHaveHappenedOnceExactly();
    }
}

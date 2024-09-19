using SMBLibrary.Client;

namespace InkSoft.SmbAbstraction;

public interface ISmbClientFactory
{
    ISMBClient CreateClient(SmbFileSystemOptions? smbFileSystemOptions);
}
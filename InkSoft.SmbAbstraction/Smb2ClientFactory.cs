using SMBLibrary.Client;

namespace InkSoft.SmbAbstraction;

public interface ISmbClientFactory
{
    ISMBClient CreateClient(SmbFileSystemOptions? smbFileSystemOptions);
}

public class Smb2ClientFactory : ISmbClientFactory
{
    public ISMBClient CreateClient(SmbFileSystemOptions? smbFileSystemOptions) => new SMB2Client();
}
using SMBLibrary.Client;

namespace InkSoft.SmbAbstraction;

public class Smb2ClientFactory : ISmbClientFactory
{
    public ISMBClient CreateClient(SmbFileSystemOptions? smbFileSystemOptions) => new SMB2Client();
}
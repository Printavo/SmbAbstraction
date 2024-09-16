using SMBLibrary.Client;

namespace InkSoft.SmbAbstraction;

public class Smb2ClientFactory : ISmbClientFactory
{
    public ISMBClient CreateClient(uint maxBufferSize) => new SMB2Client();
}
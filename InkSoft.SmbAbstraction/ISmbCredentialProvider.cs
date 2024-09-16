using System.Collections.Generic;

namespace InkSoft.SmbAbstraction;

public interface ISmbCredentialProvider
{
    ISmbCredential GetSmbCredential(string path);
    IEnumerable<ISmbCredential> GetSmbCredentials();
    void AddSmbCredential(ISmbCredential credential);
    void RemoveSmbCredential(ISmbCredential credential);
}
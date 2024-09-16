using System.Collections.Generic;
using System.Linq;

namespace InkSoft.SmbAbstraction;

public class SmbCredentialProvider : ISmbCredentialProvider
{
    List<ISmbCredential> _credentials = [];
    private static readonly object s_credentialsLock = new();

    public ISmbCredential GetSmbCredential(string path)
    {
        lock(s_credentialsLock)
        {
            string? host = path.Hostname();
            string? shareName = path.ShareName();

            var credential = _credentials.Where(q => q.Host == host && q.ShareName == shareName).FirstOrDefault();
            if(credential != null)
            {
                return credential;
            }
            else
            {
                return null;
            }
        }
    }

    public IEnumerable<ISmbCredential> GetSmbCredentials() => _credentials;

    public void AddSmbCredential(ISmbCredential credential)
    {
        lock(s_credentialsLock)
        {
            _credentials.Add(credential);
        }
    }

    public void RemoveSmbCredential(ISmbCredential credential)
    {
        lock(s_credentialsLock)
        {
            _credentials.Remove(credential);
        }
    }
}
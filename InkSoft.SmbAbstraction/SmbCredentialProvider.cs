using System.Collections.Generic;
using System.Linq;

namespace InkSoft.SmbAbstraction;

public class SmbCredentialProvider : ISmbCredentialProvider
{
    private readonly List<ISmbCredential> _credentials = [];
    
    private static readonly object s_credentialsLock = new();

    /// <inheritdoc/>
    public ISmbCredential? GetSmbCredential(string path)
    {
        lock (s_credentialsLock)
        {
            string? host = path.Hostname();
            string? shareName = path.ShareName();
            return _credentials.FirstOrDefault(c => c.Host == host && c.ShareName == shareName) ?? _credentials.FirstOrDefault(c => c.Host == host && c.ShareName == null);
        }
    }

    /// <inheritdoc/>
    public ISmbCredential[] GetSmbCredentials()
    {
        lock (s_credentialsLock)
        {
            return _credentials.ToArray();
        }
    }

    /// <inheritdoc/>
    public void AddSmbCredential(ISmbCredential credential)
    {
        lock (s_credentialsLock)
        {
            _credentials.Add(credential);
        }
    }

    /// <inheritdoc/>
    public void RemoveSmbCredential(ISmbCredential credential)
    {
        lock (s_credentialsLock)
        {
            _credentials.Remove(credential);
        }
    }
}
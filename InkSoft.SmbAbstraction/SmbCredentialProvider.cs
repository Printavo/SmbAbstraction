using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace InkSoft.SmbAbstraction;

public interface ISmbCredentialProvider
{
    ISmbCredential? GetSmbCredential(string path);

    /// <summary>
    /// A shallow copy of the internally stored credential list.
    /// </summary>
    ISmbCredential[] GetSmbCredentials();

    /// <summary>
    /// You need not call this method directly in most cases. Instead, pass this <see cref="ISmbCredentialProvider"/> to <see cref="SmbCredential.AddToProvider"/>.
    /// </summary>
    void AddSmbCredential(ISmbCredential credential);

    void RemoveSmbCredential(ISmbCredential credential);
}

public class SmbCredentialProvider: ISmbCredentialProvider
{
    private readonly List<ISmbCredential> _credentials = [];

    private static readonly Lock s_credentialsLock = new();

    /// <inheritdoc/>
    public ISmbCredential? GetSmbCredential(string path)
    {
        lock (s_credentialsLock)
        {
            string host = path.Hostname();
            string shareName = path.ShareName();
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
using System.Collections.Generic;

namespace InkSoft.SmbAbstraction;

public interface ISmbCredentialProvider
{
    ISmbCredential GetSmbCredential(string path);
    
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
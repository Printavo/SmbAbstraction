using System;

namespace InkSoft.SmbAbstraction;

public class SmbCredential : ISmbCredential
{
    private readonly ISmbCredentialProvider _credentialProvider;
    
    public string Domain { get; }

    public string Username { get; }

    public string Password { get; }

    public string Host { get; }

    public string? ShareName { get; }

    public string? Path { get; }

    private readonly bool _removeFromProviderWhenDisposed;

    /// <summary>
    /// Private constructor because the factory method is more appropriate for indicating the credential is automatically added to the provider.
    /// </summary>
    private SmbCredential(string? domain, string username, string password, string path, ISmbCredentialProvider credentialProvider, bool removeFromProviderWhenDisposed)
    {
        Domain = domain!;
        Username = username;
        Password = password;

        if (!path.Contains('\\') && !path.Contains('/'))
            path = $@"\\{path}\";

        Host = path.Hostname();
        ShareName = path.ShareName();
        Path = path;

        _credentialProvider = credentialProvider;
        _removeFromProviderWhenDisposed = removeFromProviderWhenDisposed;
        if (string.IsNullOrEmpty(Domain) && Username.Contains('\\'))
        {
            string[]? userNameParts = Username.Split('\\');
            if (userNameParts.Length == 2)
            {
                Domain = userNameParts[0];
                Username = userNameParts[1];
            }
        }

        credentialProvider.AddSmbCredential(this);
    }

    /// <summary>
    /// Makes it such that attempts to access <paramref name="path"/> are performed with the given credentials.
    /// </summary>
    /// <param name="domain">If the domain isn't separate from <paramref name="username"/> already, you should/may just pass the whole DOMAIN\Username string via the username property and it will be split there.</param>
    /// <param name="username">The username with which to access directories or files under <paramref name="path"/>. May optionally be prefixed with the domain as DOMAIN\Username instead of splitting and passing the domain via <paramref name="domain"/>.</param>
    /// <param name="password">The user account's password.</param>
    /// <param name="path">The path prefix for which this account will be used to authenticate. It may be just a host, a host plus share name, or a host, share name, and some amount of subdirectories within the share.</param>
    /// <param name="credentialProvider">The credential provider that manages the larger set of credentials, to which this credential will be added.</param>
    /// <param name="removeFromProviderWhenDisposed">Whether to remove this credential from <paramref name="credentialProvider"/> when it's disposed or collected by the garbage collector.</param>
    /// <returns>The credential added to <paramref name="credentialProvider"/>.</returns>
    public static SmbCredential AddToProvider(string? domain, string username, string password, string path, ISmbCredentialProvider credentialProvider, bool removeFromProviderWhenDisposed = true) => new(domain, username, password, path, credentialProvider, removeFromProviderWhenDisposed);

    public void Dispose()
    {
        if (_removeFromProviderWhenDisposed)
            _credentialProvider.RemoveSmbCredential(this);

        GC.SuppressFinalize(this);
    }
}
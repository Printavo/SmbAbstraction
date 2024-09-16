namespace InkSoft.SmbAbstraction;

public class SmbCredential : ISmbCredential
{
    private readonly ISmbCredentialProvider _credentialProvider;
    
    public string Host { get; }
    
    public string ShareName { get; }

    public string Domain { get; }
    
    public string UserName { get; }
    
    public string Password { get; }
    
    public string Path { get; }

    public SmbCredential(string domain, string userName, string password, string path, ISmbCredentialProvider credentialProvider)
    {
        Domain = domain;
        UserName = userName;
        Password = password;
        Path = path;
        _credentialProvider = credentialProvider;

        Host = path.Hostname();
        ShareName = path.ShareName();

        if(string.IsNullOrEmpty(Domain) && UserName.Contains('\\'))
        {
            string[]? userNameParts = UserName.Split('\\');
            if(userNameParts.Length == 2)
            {
                Domain = userNameParts[0];
                UserName = userNameParts[1];
            }
        }

        credentialProvider.AddSmbCredential(this);
    }

    public void Dispose() => _credentialProvider.RemoveSmbCredential(this);
}
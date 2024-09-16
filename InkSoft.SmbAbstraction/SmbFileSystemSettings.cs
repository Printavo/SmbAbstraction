namespace InkSoft.SmbAbstraction;

public class SmbFileSystemSettings : ISmbFileSystemSettings
{
    /// <summary>
    /// Timeout (in seconds) for client to wait before determining the connection to the share is lost. Default: 45
    /// </summary>
    public int ClientSessionTimeout { get; set; } = 45;
}
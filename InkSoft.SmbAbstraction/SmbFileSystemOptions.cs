namespace InkSoft.SmbAbstraction;

/// <summary>
/// A set of config primitives, either explicitly created via new() or possibly deserialized via IConfiguration or IOptions.
/// </summary>
public class SmbFileSystemOptions
{
    /// <summary>
    /// Timeout (in seconds) for client to wait before determining the connection to the share is lost. Default: 45
    /// </summary>
    public int ClientSessionTimeout { get; set; } = 45;

    public uint MaxBufferSize { get; set; } = 65536;
}
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

    /// <summary>
    /// Largest number of bytes sent in a single SMB read or write request, and the buffer size used when copying. Larger reads and writes are split into several requests. Values above 64 KiB use more than one SMB2 credit per request and may fail with "Not enough credits" if the server grants few credits. Default: 65536
    /// </summary>
    public uint MaxBufferSize { get; set; } = 65536;
}
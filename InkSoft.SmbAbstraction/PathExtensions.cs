using System;
using System.Linq;
using System.Net;

namespace InkSoft.SmbAbstraction;

public static class PathExtensions
{
    public static bool IsValidSharePath(this string path)
    {
        try
        {
            return new Uri(path).Segments.Length >= 2 && path.IsSharePath();
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSharePath(this string path)
    {
        try
        {
            var uri = new Uri(path);
            return uri.Scheme.Equals("smb") || uri.IsUnc;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSmbUri(this string path)
    {
        try
        {
            return new Uri(path).Scheme.Equals("smb");
        }
        catch
        {
            return false;
        }
    }

    public static bool IsUncPath(this string path)
    {
        try
        {
            return new Uri(path).IsUnc;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// It's debatable if consumers of this library would like to see these extension methods via Intellisense for strings.
/// </summary>
internal static class PathExtensionsInternal
{
    public static string BuildSharePath(this string path, string shareName) => !new Uri(path).IsUnc ? $"smb://{path.Hostname()}/{shareName}" : $@"\\{path.Hostname()}\{shareName}";

    public static string Hostname(this string path)
    {
        try 
        {
            return new Uri(path).Host;
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Unable to parse hostname for path: {path}", nameof(path), ex);
        }
    }

    public static bool TryResolveHostnameFromPath(this string path, out IPAddress ipAddress)
    {
        string host = path.Hostname();
        bool parsedIpAddress = IPAddress.TryParse(host, out ipAddress);

        if (parsedIpAddress)
            return true;

        try
        {
            var hostEntry = Dns.GetHostEntry(host);
            ipAddress = hostEntry.AddressList.First(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return true;
        }
        catch
        {
            ipAddress = IPAddress.None;
            return false;
        }
    }

    /// <summary>
    /// The first path segment after the host name. Returns null if the path contains a host only.
    /// </summary>
    public static string? ShareName(this string path)
    {
        var uri = new Uri(path);
        
        // First segment is root "/", so share names are the second segment.
        return uri.Segments.Length > 1 ? uri.Segments[1].RemoveAnySeparators() : null;
    }

    public static string SharePath(this string path)
    {
        var uri = new Uri(path);
        string sharePath = "";
        if (uri.Scheme.Equals("smb"))
            sharePath = $"{uri.Scheme}://{uri.Host}/{uri.Segments[1].RemoveAnySeparators()}";
        else if (uri.IsUnc)
            sharePath = $@"\\{uri.Host}\{uri.Segments[1].RemoveAnySeparators()}";

        return sharePath;
    }

    public static string RelativeSharePath(this string path) => path
        .Replace(path.SharePath(), "", StringComparison.InvariantCultureIgnoreCase)
        .RemoveLeadingAndTrailingSeparators()
        .Replace("/", @"\");

    public static string GetParentPath(this string path)
    {
        var pathUri = new Uri(path);
        var parentUri = pathUri.AbsoluteUri.EndsWith('/') ? new(pathUri, "..") : new Uri(pathUri, ".");
        string? pathString = parentUri.IsUnc ? parentUri.LocalPath : Uri.UnescapeDataString(parentUri.AbsoluteUri);
        return pathString.RemoveTrailingSeparators();
    }

    /// <summary>
    /// Standardizes smb:// paths.
    /// </summary>
    public static string ForwardSlash(this string path) => path.Replace('\\', '/');

    /// <summary>
    /// Standardizes UNC \\ paths.
    /// </summary>
    public static string BackSlash(this string path) => path.Replace('/', '\\');

    public static string GetLastPathSegment(this string path) => Uri.UnescapeDataString(new Uri(path).Segments.Last());

    private static readonly char[] s_pathSeparators = ['\\', '/'];

    public static string RemoveLeadingAndTrailingSeparators(this string path) => path.Trim(s_pathSeparators);

    public static string RemoveTrailingSeparators(this string path) => path.TrimEnd(s_pathSeparators);

    public static string StandardizeSeparators(this string path)
    {
        if (path.IsUncPath())
            return path.BackSlash();
        
        return path.IsSmbUri() ? path.ForwardSlash() : path;
    }

    private static readonly string[] s_stringPathSeparators = [@"\", "/"];
    
    private static string RemoveAnySeparators(this string path) => s_stringPathSeparators.Aggregate(path, (current, pathSeparator) => current.Replace(pathSeparator, ""));
}
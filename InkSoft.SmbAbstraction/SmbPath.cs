using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;

namespace InkSoft.SmbAbstraction;

public class SmbPath(IFileSystem fileSystem) : PathWrapper(new FileSystem())
{
    /// <inheritdoc cref="SmbFileSystem"/>
    public new IFileSystem FileSystem => fileSystem;

    /// <summary>
    /// Overrides the default implementation because the local OS FileSystem might use a different separator than is expected by the remote server.
    /// </summary>
    public override string Combine(string path1, string path2) => path1.IsSharePath() ? base.Combine(path1.RemoveTrailingSeparators(), path2).StandardizeSeparators() : base.Combine(path1, path2);

    /// <inheritdoc cref="Combine(string,string)"/>
    public override string Combine(string path1, string path2, string path3) => path1.IsSharePath() ? base.Combine(path1.RemoveTrailingSeparators(), path2.RemoveTrailingSeparators(), path3).StandardizeSeparators() : base.Combine(path1, path2, path3);

    /// <inheritdoc cref="Combine(string,string)"/>
    public override string Combine(string path1, string path2, string path3, string path4) => path1.IsSharePath() ? base.Combine(path1.RemoveTrailingSeparators(), path2.RemoveTrailingSeparators(), path3.RemoveTrailingSeparators(), path4).StandardizeSeparators() : base.Combine(path1, path2, path3, path4);

    /// <inheritdoc cref="Combine(string,string)"/>
    public override string Combine(params string[] paths) => paths.Length > 0 && paths[0].IsSharePath() ? base.Combine(paths.Select(p => p.RemoveTrailingSeparators()).ToArray()).StandardizeSeparators() : base.Combine(paths);

    public override string? GetDirectoryName(string? path)
    {
        if (!path.IsSharePath())
            return base.GetDirectoryName(path);

        var uri = new Uri(path);
        bool isSmb = uri.Scheme == "smb";

        string shareRoot = isSmb ? $"smb://{uri.Host}/{uri.Segments[1].RemoveAnySeparators()}/" : @$"\\{uri.Host}\{uri.Segments[1].RemoveAnySeparators()}\";

        string? relativePath = path[shareRoot.Length..];
        
        if (path.Length <= shareRoot.Length)
            return null;

        string[]? segments = relativePath.Split(['\\', '/'], StringSplitOptions.None);

        return shareRoot + string.Join(isSmb ? "/" : "\\", segments.Take(segments.Length - 1));
    }

    [return: NotNullIfNotNull(nameof(path))]
    public override string? GetFileName(string? path) => path.IsSharePath() ? path.Split(['\\', '/'], StringSplitOptions.None).Last() : base.GetFileName(path);

    public override string? GetPathRoot(string? path) => path.IsSharePath() ? path.SharePath() : base.GetPathRoot(path);

    public override bool IsPathRooted([NotNullWhen(true)] string? path) => path.IsValidSharePath() || base.IsPathRooted(path);
}
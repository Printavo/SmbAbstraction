using System;
using System.IO.Abstractions;
using System.Linq;

namespace InkSoft.SmbAbstraction;

public class SmbPath(IFileSystem fileSystem) : PathWrapper(new FileSystem())
{
    /// <inheritdoc cref="SmbFileSystem"/>
    public new IFileSystem FileSystem => fileSystem;

    // TBD: Not sure if we need to override base path.Combine methods.

    //public override string Combine(string path1, string path2) => path1.IsSmbUri() ? base.Combine(path1, path2).StandardizeSeparators() : base.Combine(path1, path2);

    //public override string Combine(string path1, string path2, string path3) => path1.IsSmbUri() ? base.Combine(path1, path2, path3).StandardizeSeparators() : base.Combine(path1, path2, path3);

    //public override string Combine(string path1, string path2, string path3, string path4) => path1.IsSmbUri() ? base.Combine(path1, path2, path3, path4).StandardizeSeparators() : base.Combine(path1, path2, path3, path4);

    public override string? GetDirectoryName(string? path)
    {
        if (path == null)
            return null;

        if (!path.IsSharePath())
            return base.GetDirectoryName(path);

        string? relativePath = path.RelativeSharePath();
        string directoryName = "";

        if (string.IsNullOrEmpty(relativePath))
            return directoryName;

        string[]? segments = relativePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (HasExtension(segments.Last()))
        {
            if (path.IsSmbUri())
                directoryName = Combine(path.SharePath(), string.Join("/", segments.Take(segments.Length - 1)));

            if (path.IsUncPath())
                directoryName = Combine(path.SharePath(), string.Join("\\", segments.Take(segments.Length - 1)));
        }
        else
        {
            directoryName = relativePath;
        }
        
        return directoryName;
    }

    public override string? GetFileName(string? path)
    {
        if (path == null)
            return null;

        if (!path.IsSharePath())
            return base.GetFileName(path);

        string? relativePath = path.RelativeSharePath();
        return string.IsNullOrEmpty(relativePath) ? "" : relativePath.Split('\\').Last();
    }

    public override string GetPathRoot(string path) => path.IsSharePath() ? path.SharePath() : base.GetPathRoot(path);

    public override bool IsPathRooted(string? path)
    {
        if (path == null)
            return false;

        return path.IsValidSharePath() || base.IsPathRooted(path);
    }
}
using System;
using System.IO.Abstractions;
using System.Linq;

namespace InkSoft.SmbAbstraction;

public class SmbPath : PathWrapper, IPath
{
    public SmbPath(IFileSystem fileSystem) : base(new FileSystem()){}

    public override string Combine(string path1, string path2)
    {
        if (!path1.IsSharePath())
        {
            return base.Combine(path1, path2);
        }

        if (path1.IsSmbUri())
        {
            return $"{path1}/{path2}";
        }

        if (path1.IsUncPath())
        {
            return $@"{path1}\{path2}";
        }

        throw new InvalidOperationException();
    }

    public override string Combine(string path1, string path2, string path3)
    {
        if (!path1.IsSharePath())
        {
            return base.Combine(path1, path2, path3);
        }

        if (path1.IsSmbUri())
        {
            return $"{path1}/{path2}/{path3}";
        }

        if (path1.IsUncPath())
        {
            return $@"{path1}\{path2}\{path3}";
        }

        throw new InvalidOperationException();
    }

    public override string Combine(string path1, string path2, string path3, string path4)
    {
        if (!path1.IsSharePath())
        {
            return base.Combine(path1, path2, path3, path4);
        }

        if (path1.IsSmbUri())
        {
            return $"{path1}/{path2}/{path3}/{path4}";
        }

        if (path1.IsUncPath())
        {
            return $@"{path1}\{path2}\{path3}\{path4}";
        }

        throw new InvalidOperationException();
    }

    public override string? GetDirectoryName(string? path)
    {
        if (path == null)
        {
            return null;
        }

        if (!path.IsSharePath())
        {
            return base.GetDirectoryName(path);
        }

        string? relativePath = path.RelativeSharePath();
        string directoryName = "";

        if (string.IsNullOrEmpty(relativePath))
        {
            return directoryName;
        }

        string[]? segments = relativePath.Split(@"\");
        if (HasExtension(segments.Last()))
        {
            if (path.IsSmbUri())
            {
                directoryName = Combine(path.SharePath(), string.Join('/', segments.Take(segments.Length - 1)));
            }

            if (path.IsUncPath())
            {
                directoryName = Combine(path.SharePath(), string.Join('\\', segments.Take(segments.Length - 1)));
            }
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
        {
            return null;
        }

        if (!path.IsSharePath())
        {
            return base.GetFileName(path);
        }

        string? relativePath = path.RelativeSharePath();
        string fileName = "";

        if (string.IsNullOrEmpty(relativePath))
        {
            return fileName;
        }

        fileName = relativePath.Split(@"\").Last();

        return fileName;
    }

    public override string GetPathRoot(string path)
    {
        if (!path.IsSharePath())
        {
            return base.GetPathRoot(path);
        }
            
        return path.SharePath();
    }

    public override bool IsPathRooted(string? path)
    {
        if (path == null)
        {
            return false;
        }

        if (path.IsValidSharePath())
        {
            return true;
        }

        return base.IsPathRooted(path);
    }
}
using System;
using System.IO;
using System.IO.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace InkSoft.SmbAbstraction;

#if FEATURE_SERIALIZABLE
[Serializable]
#endif
public class SmbFileStreamFactory(IFileSystem fileSystem) : IFileStreamFactory
{
    /// <inheritdoc />
    public IFileSystem FileSystem { get; } = fileSystem;

    private SmbFile SmbFile => (SmbFile)FileSystem.File;

    /// <inheritdoc />
    public FileSystemStream New(SafeFileHandle handle, FileAccess access) => new SmbFileStreamWrapper(new(handle, access));

    /// <inheritdoc />
    public FileSystemStream New(SafeFileHandle handle, FileAccess access, int bufferSize) => new SmbFileStreamWrapper(new(handle, access, bufferSize));

    /// <inheritdoc />
    public FileSystemStream New(SafeFileHandle handle, FileAccess access, int bufferSize, bool isAsync) => new SmbFileStreamWrapper(new(handle, access, bufferSize, isAsync));
    
    /// <inheritdoc />
    public FileSystemStream New(string path, FileMode mode) => new SmbFileStreamWrapper(new(path, mode));
    //{
    //    if (path.IsSharePath())
    //    {
    //        return new FileStream(path, mode);
    //    }

    //    return FileSystem.File.Open(path, mode);
    //}
    


    /// <inheritdoc />
    public FileSystemStream New(string path, FileMode mode, FileAccess access)=> new SmbFileStreamWrapper(new(path, mode, access));
    //{
    //    if (path.IsSharePath())
    //    {
    //        return new FileStream(path, mode, access);
    //    }

    //    return FileSystem.File.Open(path, mode, access);
    //}

    /// <inheritdoc />
    public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share) => new SmbFileStreamWrapper(new(path, mode, access, share));
    //{
    //    if (path.IsSharePath())
    //    {
    //        return new FileStream(path, mode, access, share);
    //    }

    //    return FileSystem.File.Open(path, mode, access, share);
    //}

    /// <inheritdoc />
    public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize) => new SmbFileStreamWrapper(new(path, mode, access, share, bufferSize));
    //{
    //    if (path.IsSharePath())
    //    {
    //        return new FileStream(path, mode, access, share, bufferSize);
    //    }

    //    return new BufferedStream(FileSystem.File.Open(path, mode, access, share), bufferSize);
    //}

    /// <inheritdoc />
    public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync) => new SmbFileStreamWrapper(new(path, mode, access, share, bufferSize, useAsync));
    //{
    //    if (path.IsSharePath())
    //    {
    //        return new FileStream(path, mode, access, share, bufferSize, useAsync);
    //    }

    //    if (useAsync == false)
    //    {
    //        return new BufferedStream(FileSystem.File.Open(path, mode, access, share), bufferSize);
    //    }
    //    else
    //    {
    //        throw new NotSupportedException();
    //    }
    //}

    /// <inheritdoc />
    public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options) => new SmbFileStreamWrapper(new(path, mode, access, share, bufferSize, options));
    //{
    //    if (path.IsSharePath())
    //    {
    //        return new FileSystemStream(path, mode, access, share, bufferSize, options);
    //    }

    //    return new BufferedStream(SmbFile.Open(path, mode, access, share, options, null), bufferSize);
    //}

#if FEATURE_FILESTREAM_OPTIONS
    /// <inheritdoc />
    public FileSystemStream New(string path, FileStreamOptions options)
        => new SmbFileStreamWrapper(new(path, options));
#endif

    /// <inheritdoc />
    public FileSystemStream Wrap(FileStream fileStream)
        => new SmbFileStreamWrapper(fileStream);
}
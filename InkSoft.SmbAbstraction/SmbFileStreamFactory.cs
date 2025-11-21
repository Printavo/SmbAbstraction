using System;
using System.IO;
using System.IO.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace InkSoft.SmbAbstraction;

#if FEATURE_SERIALIZABLE
[Serializable]
#endif
public class SmbFileStreamFactory(SmbFileSystem smbFileSystem) : IFileStreamFactory
{
    private SmbFile SmbFile => (SmbFile)smbFileSystem.File;

    /// <inheritdoc cref="SmbFileSystem"/>
    public IFileSystem FileSystem => smbFileSystem;

    /// <inheritdoc />
    public FileSystemStream New(SafeFileHandle handle, FileAccess access) => new SmbFileStreamWrapper(new(handle, access));

    /// <inheritdoc />
    public FileSystemStream New(SafeFileHandle handle, FileAccess access, int bufferSize) => new SmbFileStreamWrapper(new(handle, access, bufferSize));

    /// <inheritdoc />
    public FileSystemStream New(SafeFileHandle handle, FileAccess access, int bufferSize, bool isAsync) => new SmbFileStreamWrapper(new(handle, access, bufferSize, isAsync));

    /// <inheritdoc />
    public FileSystemStream New(string path, FileMode mode) => path.IsSharePath() ? SmbFile.OpenSmb(path, mode) : new SmbFileStreamWrapper(new(path, mode));

    /// <inheritdoc />
    public FileSystemStream New(string path, FileMode mode, FileAccess access) => path.IsSharePath() ? SmbFile.OpenSmb(path, mode, access) : new SmbFileStreamWrapper(new(path, mode, access));

    /// <inheritdoc />
    public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share) => path.IsSharePath() ? SmbFile.OpenSmb(path, mode, access, share) : new SmbFileStreamWrapper(new(path, mode, access, share));

    /// <inheritdoc />
    public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize) => path.IsSharePath() ? SmbFile.OpenSmb(path, mode, access, share) : new SmbFileStreamWrapper(new(path, mode, access, share, bufferSize));

    /// <inheritdoc />
    public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync) => path.IsSharePath() ? SmbFile.OpenSmb(path, mode, access, share) : new SmbFileStreamWrapper(new(path, mode, access, share, bufferSize, useAsync));

    /// <inheritdoc />
    public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options) => path.IsSharePath() ? SmbFile.OpenSmb(path, mode, access, share, options) : new SmbFileStreamWrapper(new(path, mode, access, share, bufferSize, options));

    #if FEATURE_FILESTREAM_OPTIONS
    /// <inheritdoc />
    public FileSystemStream New(string path, FileStreamOptions options) => path.IsSharePath() ? SmbFile.Open(path, options) : new SmbFileStreamWrapper(new(path, options));
    #endif

    /// <inheritdoc />
    public FileSystemStream Wrap(FileStream fileStream) => new SmbFileStreamWrapper(fileStream);
}
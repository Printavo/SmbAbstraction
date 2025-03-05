using System.IO.Abstractions;

namespace InkSoft.SmbAbstraction;

public class SmbFileSystemWatcherFactory(IFileSystem fileSystem) : FileSystemWatcherFactory(new FileSystem())
{
    /// <inheritdoc cref="SmbFileSystem"/>
    public new IFileSystem FileSystem => fileSystem;
}
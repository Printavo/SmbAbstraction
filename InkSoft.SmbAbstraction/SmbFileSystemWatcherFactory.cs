using System.IO.Abstractions;

namespace InkSoft.SmbAbstraction;

public class SmbFileSystemWatcherFactory(SmbFileSystem smbFileSystem): FileSystemWatcherFactory(smbFileSystem.NonSmbFileSystem)
{
    /// <inheritdoc cref="SmbFileSystem"/>
    public new IFileSystem FileSystem => smbFileSystem;
}
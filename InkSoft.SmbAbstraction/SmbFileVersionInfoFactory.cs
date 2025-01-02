using System;
using System.IO.Abstractions;

namespace InkSoft.SmbAbstraction;

/// <inheritdoc />
#if FEATURE_SERIALIZABLE
[Serializable]
#endif
public class SmbFileVersionInfoFactory(IFileSystem fileSystem): IFileVersionInfoFactory
{
    /// <inheritdoc />
    public IFileSystem FileSystem { get; } = fileSystem;

    /// <inheritdoc />
    public IFileVersionInfo GetVersionInfo(string fileName) => new FileVersionInfoWrapper(System.Diagnostics.FileVersionInfo.GetVersionInfo(fileName));
}
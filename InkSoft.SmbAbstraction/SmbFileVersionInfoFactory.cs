using System;
using System.IO.Abstractions;

namespace InkSoft.SmbAbstraction;

/// <inheritdoc />
#if FEATURE_SERIALIZABLE
[Serializable]
#endif
public class SmbFileVersionInfoFactory(SmbFileSystem smbFileSystem): IFileVersionInfoFactory
{
    /// <inheritdoc />
    public IFileSystem FileSystem => smbFileSystem;

    /// <inheritdoc />
    public IFileVersionInfo GetVersionInfo(string fileName) => new FileVersionInfoWrapper(System.Diagnostics.FileVersionInfo.GetVersionInfo(fileName));
}
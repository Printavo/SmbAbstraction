using System;
using System.IO;
using System.IO.Abstractions;
using System.Runtime.Versioning;
using System.Security.AccessControl;

namespace InkSoft.SmbAbstraction;

internal sealed class SmbFileStreamWrapper(FileStream fileStream) : FileSystemStream(fileStream, fileStream.Name, fileStream.IsAsync), IFileSystemAclSupport
{
    /// <inheritdoc cref="IFileSystemAclSupport.GetAccessControl()" />
    [SupportedOSPlatform("windows")]
    public object GetAccessControl() => fileStream.GetAccessControl();

    /// <inheritdoc cref="IFileSystemAclSupport.GetAccessControl(IFileSystemAclSupport.AccessControlSections)" />
    [SupportedOSPlatform("windows")]
    public object GetAccessControl(IFileSystemAclSupport.AccessControlSections includeSections) => throw new NotSupportedException("GetAccessControl with includeSections is not supported for FileStreams");

    /// <inheritdoc cref="IFileSystemAclSupport.SetAccessControl(object)" />
    [SupportedOSPlatform("windows")]
    public void SetAccessControl(object value)
    {
        if (value is FileSecurity fileSecurity)
        {
            fileStream.SetAccessControl(fileSecurity);
        }
        else
        {
            throw new ArgumentException("value must be of type `FileSecurity`");
        }
    }

    /// <inheritdoc />
    public override void Flush(bool flushToDisk)
        => fileStream.Flush(flushToDisk);
}
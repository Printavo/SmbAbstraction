#if NET
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace InkSoft.SmbAbstraction;

/// <summary>
/// <see cref="IRandomAccess"/> operates exclusively on OS-level <see cref="SafeFileHandle"/>s, which SMBLibrary connections don't produce, so every call is delegated to <see cref="SmbFileSystem.NonSmbFileSystem"/>. Use <see cref="IFileSystem.FileStream"/> with seeking for random access to files on SMB shares.
/// </summary>
public class SmbRandomAccess(SmbFileSystem smbFileSystem): IRandomAccess
{
    // Resolved per call because SmbFileSystem.NonSmbFileSystem is settable.
    private IRandomAccess NonSmbRandomAccess => smbFileSystem.NonSmbFileSystem.RandomAccess;

    /// <inheritdoc cref="SmbFileSystem"/>
    public IFileSystem FileSystem => smbFileSystem;

    /// <inheritdoc />
    public void FlushToDisk(SafeFileHandle handle) => NonSmbRandomAccess.FlushToDisk(handle);

    /// <inheritdoc />
    public long GetLength(SafeFileHandle handle) => NonSmbRandomAccess.GetLength(handle);

    /// <inheritdoc />
    public void SetLength(SafeFileHandle handle, long length) => NonSmbRandomAccess.SetLength(handle, length);

    /// <inheritdoc />
    public int Read(SafeFileHandle handle, Span<byte> buffer, long fileOffset) => NonSmbRandomAccess.Read(handle, buffer, fileOffset);

    /// <inheritdoc />
    public long Read(SafeFileHandle handle, IReadOnlyList<Memory<byte>> buffers, long fileOffset) => NonSmbRandomAccess.Read(handle, buffers, fileOffset);

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(SafeFileHandle handle, Memory<byte> buffer, long fileOffset, CancellationToken cancellationToken = default) => NonSmbRandomAccess.ReadAsync(handle, buffer, fileOffset, cancellationToken);

    /// <inheritdoc />
    public ValueTask<long> ReadAsync(SafeFileHandle handle, IReadOnlyList<Memory<byte>> buffers, long fileOffset, CancellationToken cancellationToken = default) => NonSmbRandomAccess.ReadAsync(handle, buffers, fileOffset, cancellationToken);

    /// <inheritdoc />
    public void Write(SafeFileHandle handle, ReadOnlySpan<byte> buffer, long fileOffset) => NonSmbRandomAccess.Write(handle, buffer, fileOffset);

    /// <inheritdoc />
    public void Write(SafeFileHandle handle, IReadOnlyList<ReadOnlyMemory<byte>> buffers, long fileOffset) => NonSmbRandomAccess.Write(handle, buffers, fileOffset);

    /// <inheritdoc />
    public ValueTask WriteAsync(SafeFileHandle handle, ReadOnlyMemory<byte> buffer, long fileOffset, CancellationToken cancellationToken = default) => NonSmbRandomAccess.WriteAsync(handle, buffer, fileOffset, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(SafeFileHandle handle, IReadOnlyList<ReadOnlyMemory<byte>> buffers, long fileOffset, CancellationToken cancellationToken = default) => NonSmbRandomAccess.WriteAsync(handle, buffers, fileOffset, cancellationToken);
}
#endif

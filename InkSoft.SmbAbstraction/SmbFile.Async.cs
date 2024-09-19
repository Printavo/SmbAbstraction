#if FEATURE_ASYNC_FILE

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace InkSoft.SmbAbstraction
{
    partial class SmbFile
    {
        /// <inheritdoc />
        public override Task AppendAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken) => path.IsSharePath() ? new(() => AppendAllLines(path, contents), cancellationToken) : base.AppendAllLinesAsync(path, contents, cancellationToken);

        /// <inheritdoc />
        public override Task AppendAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding, CancellationToken cancellationToken) => path.IsSharePath() ? new(() => AppendAllLines(path, contents, encoding), cancellationToken) : base.AppendAllLinesAsync(path, contents, encoding, cancellationToken);

        /// <inheritdoc />
        public override Task AppendAllTextAsync(string path, string contents, CancellationToken cancellationToken) => path.IsSharePath() ? new(() => AppendAllText(path, contents), cancellationToken) : base.AppendAllTextAsync(path, contents, cancellationToken);

        /// <inheritdoc />
        public override Task AppendAllTextAsync(string path, string contents, Encoding encoding, CancellationToken cancellationToken) => path.IsSharePath() ? new(() => AppendAllText(path, contents, encoding), cancellationToken) : base.AppendAllTextAsync(path, contents, encoding, cancellationToken);

        /// <inheritdoc />
        public override Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken) => path.IsSharePath() ? new(() => ReadAllBytes(path), cancellationToken) : base.ReadAllBytesAsync(path, cancellationToken);

        /// <inheritdoc />
        public override Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken) => path.IsSharePath() ? new(() => ReadAllLines(path), cancellationToken) : base.ReadAllLinesAsync(path, cancellationToken);

        /// <inheritdoc />
        public override Task<string[]> ReadAllLinesAsync(string path, Encoding encoding, CancellationToken cancellationToken) => path.IsSharePath() ? new(() => ReadAllLines(path, encoding), cancellationToken) : base.ReadAllLinesAsync(path, encoding, cancellationToken);

        /// <inheritdoc />
        public override Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) => path.IsSharePath() ? new(() => ReadAllText(path), cancellationToken) : base.ReadAllTextAsync(path, cancellationToken);

        /// <inheritdoc />
        public override Task<string> ReadAllTextAsync(string path, Encoding encoding, CancellationToken cancellationToken) => path.IsSharePath() ? new(() => ReadAllText(path, encoding), cancellationToken) : base.ReadAllTextAsync(path, encoding, cancellationToken);

#if FEATURE_READ_LINES_ASYNC
        /// <inheritdoc />
        public override IAsyncEnumerable<string> ReadLinesAsync(string path, CancellationToken cancellationToken = default) => path.IsSharePath() ? throw new NotImplementedException() : base.ReadLinesAsync(path, cancellationToken);

        /// <inheritdoc />
        public override IAsyncEnumerable<string> ReadLinesAsync(string path, Encoding encoding, CancellationToken cancellationToken = default) => path.IsSharePath() ? throw new NotImplementedException() : base.ReadLinesAsync(path, encoding, cancellationToken);
#endif

        /// <inheritdoc />
        public override Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken) => path.IsSharePath() ? new(() => WriteAllBytes(path, bytes), cancellationToken) : base.WriteAllBytesAsync(path, bytes, cancellationToken);

        /// <inheritdoc />
        public override Task WriteAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken) => path.IsSharePath() ? new(() => WriteAllLines(path, contents), cancellationToken) : base.WriteAllLinesAsync(path, contents, cancellationToken);

        /// <inheritdoc />
        public override Task WriteAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding, CancellationToken cancellationToken) => path.IsSharePath() ? new(() => WriteAllLines(path, contents, encoding), cancellationToken) : base.WriteAllLinesAsync(path, contents, encoding, cancellationToken);

        /// <inheritdoc />
        public override Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken) => path.IsSharePath() ? new(() => WriteAllText(path, contents), cancellationToken) : base.WriteAllTextAsync(path, contents, cancellationToken);

        /// <inheritdoc />
        public override Task WriteAllTextAsync(string path, string contents, Encoding encoding, CancellationToken cancellationToken) => !path.IsSharePath() ? base.WriteAllTextAsync(path, contents, encoding, cancellationToken) : new(() => WriteAllText(path, contents, encoding), cancellationToken);
    }
}
#endif

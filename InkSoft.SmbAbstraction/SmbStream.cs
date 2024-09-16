using System;
using System.Diagnostics;
using System.IO;
using SMBLibrary;
using SMBLibrary.Client;
using System.IO.Abstractions;

namespace InkSoft.SmbAbstraction;

public class SmbFsStream(ISMBFileStore fileStore, object fileHandle, SmbConnection connection, long fileLength, ISmbFileSystemSettings? smbFileSystemSettings, string path, bool isAsync) : FileSystemStream(new SmbStream(fileStore, fileHandle, connection, fileLength, smbFileSystemSettings), path, isAsync);

public class SmbStream : Stream
{
    private readonly ISmbFileSystemSettings _smbFileSystemSettings;
    private readonly ISMBFileStore _fileStore;
    private readonly object _fileHandle;
    private readonly SmbConnection _connection;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    private readonly int _maxReadSize;
    private readonly int _maxWriteSize;
    private int MaxBufferSize => Math.Min(_maxReadSize, _maxWriteSize);

    private long _length;

    public override long Length => _length;

    private long _position;

    public override long Position { get => _position;
        set => _position = value;
    }

    public SmbStream(ISMBFileStore fileStore, object fileHandle, SmbConnection connection, long fileLength, ISmbFileSystemSettings? smbFileSystemSettings)
    {
        _smbFileSystemSettings = smbFileSystemSettings ?? new SmbFileSystemSettings();
        _fileStore = fileStore;
        _fileHandle = fileHandle;
        _connection = connection;
        _maxReadSize = Convert.ToInt32(_connection.SmbClient.MaxReadSize);
        _maxWriteSize = Convert.ToInt32(_connection.SmbClient.MaxWriteSize);
        _length = fileLength;
    }

    public override void Flush(){}

    public override int Read(byte[] buffer, int offset, int count)
    {
        NTStatus status;
        var stopwatch = new Stopwatch();
            
        stopwatch.Start();

        do
        {
            status = _fileStore.ReadFile(out byte[] data, _fileHandle, _position, count);

            switch (status)
            {
                case NTStatus.STATUS_SUCCESS:
                    for (int i = offset, i2 = 0; i2 < data.Length; i++, i2++)
                    {
                        buffer[i] = data[i2];
                    }
                    _position += data.Length;
                    return data.Length;
                case NTStatus.STATUS_END_OF_FILE:
                    return 0;
                case NTStatus.STATUS_PENDING:
                    break;
                default:
                    throw new SmbException($"Unable to read file; Status: {status}");
            }
        }
        while (status == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= _smbFileSystemSettings.ClientSessionTimeout);

        stopwatch.Stop();

        throw new SmbException($"Unable to read file; Status: {status}");
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                _position = 0;
                break;
            case SeekOrigin.Current:
                break;
            case SeekOrigin.End:
                var status = _fileStore.GetFileInformation(out var result, _fileHandle, FileInformationClass.FileStreamInformation);

                status.HandleStatus();

                var fileStreamInformation = (FileStreamInformation)result;
                _position += fileStreamInformation.Entries[0].StreamSize;

                return _position;
        }
        _position += offset;
        return _position;
    }

    public override void SetLength(long value) => _length = value;

    public override void Write(byte[] buffer, int offset, int count)
    {
        byte[] data = new byte[count];

        for (int i = offset, i2 = 0; i < count; i++, i2++)
        {
            data[i2] = buffer[i];
        }

        NTStatus status;
        int bytesWritten;

        var stopwatch = new Stopwatch();

        stopwatch.Start();
        do
        {
            status = _fileStore.WriteFile(out bytesWritten, _fileHandle, _position, data);
        }
        while (status == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= _smbFileSystemSettings.ClientSessionTimeout);
        stopwatch.Stop();
            
        status.HandleStatus();

        _position += bytesWritten;
    }

    protected override void Dispose(bool disposing)
    {
        // TODO: This try/catch wasn't necessary in the original code.
        try
        {
            _fileStore.CloseFile(_fileHandle);
        }
        catch
        {
        }
        
        _connection.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc cref="Stream.CopyTo(Stream, int)" />
#if NETSTANDARD2_0 || NET462
	public new virtual void CopyTo(Stream destination, int bufferSize)
#else
    public override void CopyTo(Stream destination, int bufferSize)
#endif
    {
        if(bufferSize == 0 || bufferSize > MaxBufferSize)
        {
            bufferSize = MaxBufferSize;
        }

        int count;
        byte[] buffer = new byte[bufferSize];

        while ((count = this.Read(buffer, 0, buffer.Length)) != 0)
        {
            destination.Write(buffer, 0, count);
        }
    }
}
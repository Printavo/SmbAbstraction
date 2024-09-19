using System;
using System.Diagnostics;
using System.IO;
using SMBLibrary;
using SMBLibrary.Client;
using System.IO.Abstractions;

namespace InkSoft.SmbAbstraction;

public class SmbFsStream(ISMBFileStore fileStore, object fileHandle, SmbConnection connection, long fileLength, SmbFileSystemOptions? smbFileSystemOptions, string path, bool isAsync) : FileSystemStream(new SmbStream(fileStore, fileHandle, connection, fileLength, smbFileSystemOptions), path, isAsync);

public class SmbStream : Stream
{
    private readonly SmbFileSystemOptions _smbFileSystemOptions;
    
    private readonly ISMBFileStore _fileStore;
    
    private readonly object _fileHandle;
    
    private readonly SmbConnection _connection;

    public override bool CanRead => true;
    
    public override bool CanSeek => true;
    
    public override bool CanWrite => true;
    
    private readonly int _maxReadSize;
    
    private readonly int _maxWriteSize;
    
    /// <remarks>
    /// TODO: Should we get this value from _smbFileSystemOptions?
    /// </remarks>
    private int MaxBufferSize => Math.Min(_maxReadSize, _maxWriteSize);

    private long _length;

    public override long Length => _length;

    private long _position;

    public override long Position { get => _position;
        set => _position = value;
    }

    public SmbStream(ISMBFileStore fileStore, object fileHandle, SmbConnection connection, long fileLength, SmbFileSystemOptions? smbFileSystemOptions)
    {
        _smbFileSystemOptions = smbFileSystemOptions ?? new();
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
        NTStatus ntStatus;
        var stopwatch = new Stopwatch();
            
        stopwatch.Start();

        do
        {
            ntStatus = _fileStore.ReadFile(out byte[] data, _fileHandle, _position, count);

            switch (ntStatus)
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
                case NTStatus.STATUS_NOTIFY_CLEANUP:
                case NTStatus.STATUS_NOTIFY_ENUM_DIR:
                case NTStatus.SEC_I_CONTINUE_NEEDED:
                case NTStatus.STATUS_OBJECT_NAME_EXISTS:
                case NTStatus.STATUS_BUFFER_OVERFLOW:
                case NTStatus.STATUS_NO_MORE_FILES:
                case NTStatus.SEC_E_SECPKG_NOT_FOUND:
                case NTStatus.SEC_E_INVALID_TOKEN:
                case NTStatus.STATUS_NOT_IMPLEMENTED:
                case NTStatus.STATUS_INVALID_INFO_CLASS:
                case NTStatus.STATUS_INFO_LENGTH_MISMATCH:
                case NTStatus.STATUS_INVALID_HANDLE:
                case NTStatus.STATUS_INVALID_PARAMETER:
                case NTStatus.STATUS_NO_SUCH_DEVICE:
                case NTStatus.STATUS_NO_SUCH_FILE:
                case NTStatus.STATUS_INVALID_DEVICE_REQUEST:
                case NTStatus.STATUS_MORE_PROCESSING_REQUIRED:
                case NTStatus.STATUS_ACCESS_DENIED:
                case NTStatus.STATUS_BUFFER_TOO_SMALL:
                case NTStatus.STATUS_OBJECT_NAME_INVALID:
                case NTStatus.STATUS_OBJECT_NAME_NOT_FOUND:
                case NTStatus.STATUS_OBJECT_NAME_COLLISION:
                case NTStatus.STATUS_OBJECT_PATH_INVALID:
                case NTStatus.STATUS_OBJECT_PATH_NOT_FOUND:
                case NTStatus.STATUS_OBJECT_PATH_SYNTAX_BAD:
                case NTStatus.STATUS_DATA_ERROR:
                case NTStatus.STATUS_SHARING_VIOLATION:
                case NTStatus.STATUS_FILE_LOCK_CONFLICT:
                case NTStatus.STATUS_LOCK_NOT_GRANTED:
                case NTStatus.STATUS_DELETE_PENDING:
                case NTStatus.STATUS_IO_TIMEOUT:
                case NTStatus.STATUS_PRIVILEGE_NOT_HELD:
                case NTStatus.STATUS_WRONG_PASSWORD:
                case NTStatus.STATUS_LOGON_FAILURE:
                case NTStatus.STATUS_ACCOUNT_RESTRICTION:
                case NTStatus.STATUS_INVALID_LOGON_HOURS:
                case NTStatus.STATUS_INVALID_WORKSTATION:
                case NTStatus.STATUS_PASSWORD_EXPIRED:
                case NTStatus.STATUS_ACCOUNT_DISABLED:
                case NTStatus.STATUS_RANGE_NOT_LOCKED:
                case NTStatus.STATUS_DISK_FULL:
                case NTStatus.STATUS_INSUFFICIENT_RESOURCES:
                case NTStatus.STATUS_MEDIA_WRITE_PROTECTED:
                case NTStatus.STATUS_FILE_IS_A_DIRECTORY:
                case NTStatus.STATUS_NOT_SUPPORTED:
                case NTStatus.STATUS_NETWORK_NAME_DELETED:
                case NTStatus.STATUS_BAD_DEVICE_TYPE:
                case NTStatus.STATUS_BAD_NETWORK_NAME:
                case NTStatus.STATUS_TOO_MANY_SESSIONS:
                case NTStatus.STATUS_REQUEST_NOT_ACCEPTED:
                case NTStatus.STATUS_DIRECTORY_NOT_EMPTY:
                case NTStatus.STATUS_NOT_A_DIRECTORY:
                case NTStatus.STATUS_TOO_MANY_OPENED_FILES:
                case NTStatus.STATUS_CANCELLED:
                case NTStatus.STATUS_CANNOT_DELETE:
                case NTStatus.STATUS_FILE_CLOSED:
                case NTStatus.STATUS_LOGON_TYPE_NOT_GRANTED:
                case NTStatus.STATUS_ACCOUNT_EXPIRED:
                case NTStatus.STATUS_FS_DRIVER_REQUIRED:
                case NTStatus.STATUS_USER_SESSION_DELETED:
                case NTStatus.STATUS_INSUFF_SERVER_RESOURCES:
                case NTStatus.STATUS_PASSWORD_MUST_CHANGE:
                case NTStatus.STATUS_NOT_FOUND:
                case NTStatus.STATUS_ACCOUNT_LOCKED_OUT:
                case NTStatus.STATUS_PATH_NOT_COVERED:
                case NTStatus.STATUS_NOT_A_REPARSE_POINT:
                case NTStatus.STATUS_INVALID_SMB:
                case NTStatus.STATUS_SMB_BAD_COMMAND:
                case NTStatus.STATUS_SMB_BAD_FID:
                case NTStatus.STATUS_SMB_BAD_TID:
                case NTStatus.STATUS_OS2_INVALID_ACCESS:
                case NTStatus.STATUS_OS2_NO_MORE_SIDS:
                case NTStatus.STATUS_OS2_INVALID_LEVEL:
                default:
                    throw new SmbException($"Unable to read file; Status: {ntStatus}");
            }
        }

        while (ntStatus == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= _smbFileSystemOptions.ClientSessionTimeout);

        stopwatch.Stop();

        throw new SmbException($"Unable to read file; Status: {ntStatus}");
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
                status.AssertSuccess();
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
        while (status == NTStatus.STATUS_PENDING && stopwatch.Elapsed.TotalSeconds <= _smbFileSystemOptions.ClientSessionTimeout);
        stopwatch.Stop();
            
        status.AssertSuccess();

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
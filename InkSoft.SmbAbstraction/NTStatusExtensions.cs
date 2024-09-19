using System;
using System.IO;
using System.Security.Authentication;
using SMBLibrary;

namespace InkSoft.SmbAbstraction;

public static class NtStatusExtensions
{
    /// <summary>
    /// If the file or directory is confirmed to be absent (as opposed to uncertainty due to IO or authorization issues).
    /// </summary>
    public static bool IsAbsent(this NTStatus ntStatus) => ntStatus
        is NTStatus.STATUS_NOT_FOUND
        or NTStatus.STATUS_NO_SUCH_FILE
        or NTStatus.STATUS_NO_SUCH_DEVICE
        or NTStatus.STATUS_OBJECT_NAME_NOT_FOUND
        or NTStatus.STATUS_OBJECT_NAME_INVALID
        or NTStatus.STATUS_NOT_A_REPARSE_POINT
        or NTStatus.STATUS_NOT_A_DIRECTORY
        or NTStatus.STATUS_OBJECT_PATH_INVALID
        or NTStatus.STATUS_OBJECT_PATH_NOT_FOUND
    ;

    public static void AssertSuccess(this NTStatus ntStatus)
    {
        switch (ntStatus)
        {
            // ERRDOS Class 0x01
            case NTStatus.STATUS_NOT_IMPLEMENTED:
                throw new NotImplementedException($"{ntStatus.ToString()}: {c_errBadFunc}");
            case NTStatus.STATUS_INVALID_DEVICE_REQUEST:
                throw new InvalidOperationException($"{ntStatus.ToString()}: {c_errBadFunc}");
            case NTStatus.STATUS_NOT_FOUND:
            case NTStatus.STATUS_NO_SUCH_FILE:
            case NTStatus.STATUS_NO_SUCH_DEVICE:
            case NTStatus.STATUS_OBJECT_NAME_NOT_FOUND:
            case NTStatus.STATUS_FILE_IS_A_DIRECTORY:
                throw new FileNotFoundException($"{ntStatus.ToString()}: {c_errBadFile}");
            case NTStatus.STATUS_NOT_A_REPARSE_POINT:
            case NTStatus.STATUS_NOT_A_DIRECTORY:
            case NTStatus.STATUS_PATH_NOT_COVERED:
            case NTStatus.STATUS_OBJECT_PATH_INVALID:
            case NTStatus.STATUS_OBJECT_PATH_NOT_FOUND:
            case NTStatus.STATUS_OBJECT_PATH_SYNTAX_BAD:
                throw new DirectoryNotFoundException($"{ntStatus.ToString()}: {c_errBadPath}");
            case NTStatus.STATUS_TOO_MANY_OPENED_FILES:
                throw new IOException($"{ntStatus.ToString()}: {c_errNoFids}");
            case NTStatus.STATUS_ACCESS_DENIED:
            case NTStatus.STATUS_DELETE_PENDING:
            case NTStatus.STATUS_PRIVILEGE_NOT_HELD:
            case NTStatus.STATUS_LOGON_FAILURE:
            case NTStatus.STATUS_CANNOT_DELETE:
                throw new UnauthorizedAccessException($"{ntStatus.ToString()}: {c_errNoAccess}");
            case NTStatus.STATUS_SMB_BAD_FID:
            case NTStatus.STATUS_INVALID_HANDLE:
            case NTStatus.STATUS_FILE_CLOSED:
                throw new ArgumentException($"{ntStatus.ToString()}: {c_errBadFid}");
            case NTStatus.STATUS_INSUFF_SERVER_RESOURCES:
                throw new OutOfMemoryException($"{ntStatus.ToString()}:{c_errNoMem}");
            case NTStatus.STATUS_OS2_INVALID_ACCESS:
                throw new UnauthorizedAccessException($"{ntStatus.ToString()}: {c_errBadAccess}");
            case NTStatus.STATUS_DATA_ERROR:
                throw new InvalidDataException($"{ntStatus.ToString()}: {c_errBadData}");
            case NTStatus.STATUS_DIRECTORY_NOT_EMPTY:
                throw new IOException($"{ntStatus.ToString()}: {c_errRemCd}");
            case NTStatus.STATUS_NO_MORE_FILES:
                throw new IOException($"{ntStatus.ToString()}: {c_errNoFiles}");
            case NTStatus.STATUS_END_OF_FILE:
                throw new IOException($"{ntStatus.ToString()}: {c_errEof}");
            case NTStatus.STATUS_NOT_SUPPORTED:
                throw new NotSupportedException($"{ntStatus.ToString()}: {c_errUnsup}");
            case NTStatus.STATUS_OBJECT_NAME_COLLISION:
                throw new IOException($"{ntStatus.ToString()}: {c_errFileExists}");
            case NTStatus.STATUS_INVALID_PARAMETER:
                throw new ArgumentException($"{ntStatus.ToString()}: {c_errInvalidParam}");
            case NTStatus.STATUS_OS2_INVALID_LEVEL:
                throw new UnsupportedInformationLevelException($"{ntStatus.ToString()}: {c_errUnknownLevel}");
            case NTStatus.STATUS_RANGE_NOT_LOCKED:
                throw new AccessViolationException($"{ntStatus.ToString()}: {c_errorNotLocked}");
            case NTStatus.STATUS_OS2_NO_MORE_SIDS:
                throw new InvalidOperationException($"{ntStatus.ToString()}: {c_errorNoMoreSearchHandles}");
            case NTStatus.STATUS_INVALID_INFO_CLASS:
                throw new UnsupportedInformationLevelException($"{ntStatus.ToString()}: {c_errBadPipe}");
            case NTStatus.STATUS_BUFFER_OVERFLOW:
            case NTStatus.STATUS_MORE_PROCESSING_REQUIRED:
                throw new InvalidOperationException($"{ntStatus.ToString()}: {c_errMoreData}");
            case NTStatus.STATUS_NOTIFY_ENUM_DIR:
                throw new AccessViolationException($"{ntStatus.ToString()}: {c_errNotifyEnumDir}");

            // ERRSRV Class 0x02
            case NTStatus.STATUS_INVALID_SMB:
                throw new ArgumentException($"{ntStatus.ToString()}: {c_errInvSmb}"); //Is there a better exception for this?
            case NTStatus.STATUS_NETWORK_NAME_DELETED:
            case NTStatus.STATUS_SMB_BAD_TID:
                throw new ArgumentException($"{ntStatus.ToString()}: {c_errInvTid}");
            case NTStatus.STATUS_BAD_NETWORK_NAME:
                throw new ArgumentException($"{ntStatus.ToString()}: {c_errInvNetName}");
            case NTStatus.STATUS_SMB_BAD_COMMAND:
                throw new NotImplementedException($"{ntStatus.ToString()}: {c_errBadCmd}");
            case NTStatus.STATUS_TOO_MANY_SESSIONS:
                throw new ApplicationException($"{ntStatus.ToString()}: {c_errTooManyUids}");
            case NTStatus.STATUS_ACCOUNT_DISABLED:
            case NTStatus.STATUS_ACCOUNT_EXPIRED:
                throw new AuthenticationException($"{ntStatus.ToString()}: {c_errAccountExpired}");
            case NTStatus.STATUS_INVALID_WORKSTATION:
                throw new UnauthorizedAccessException($"{ntStatus.ToString()}: {c_errBadClient}");
            case NTStatus.STATUS_INVALID_LOGON_HOURS:
                throw new UnauthorizedAccessException($"{ntStatus.ToString()}: {c_errBadLogonTime}");
            case NTStatus.STATUS_PASSWORD_EXPIRED:
            case NTStatus.STATUS_PASSWORD_MUST_CHANGE:
                throw new InvalidCredentialException($"{ntStatus.ToString()}: {c_errPasswordExpired}");

            // ERRHRD Class 0x03
            case NTStatus.STATUS_MEDIA_WRITE_PROTECTED:
                throw new AccessViolationException($"{ntStatus.ToString()}: {c_errNoWrite}");
            case NTStatus.STATUS_SHARING_VIOLATION:
                throw new InvalidOperationException($"{ntStatus.ToString()}: {c_errBadShare}");
            case NTStatus.STATUS_FILE_LOCK_CONFLICT:
                throw new InvalidOperationException($"{ntStatus.ToString()}: {c_errLock}");

            // Others
            case NTStatus.STATUS_DISK_FULL:
                throw new IOException($"{ ntStatus.ToString() }: Disk is full.");
            case NTStatus.STATUS_LOGON_TYPE_NOT_GRANTED:
                throw new UnauthorizedAccessException($"{ntStatus.ToString()}: {c_ntStatusStatusLogonTypeNotGranted}"); 
            case NTStatus.STATUS_ACCOUNT_LOCKED_OUT:
                throw new UnauthorizedAccessException($"{ntStatus.ToString()}: {c_ntStatusStatusAccountLockedOut}");
            case NTStatus.STATUS_ACCOUNT_RESTRICTION:
                throw new UnauthorizedAccessException($"{ntStatus.ToString()}: {c_ntStatusStatusAccountRestriction}");
            case NTStatus.SEC_E_INVALID_TOKEN:
                throw new UnauthorizedAccessException($"{ntStatus.ToString()}");
            case NTStatus.SEC_E_SECPKG_NOT_FOUND:
                throw new InvalidCredentialException($"{ntStatus.ToString()}");
            case NTStatus.STATUS_OBJECT_NAME_INVALID:
                throw new MemberAccessException($"{ntStatus.ToString()}: {c_ntStatusStatusObjectNameInvalid}");
            case NTStatus.STATUS_OBJECT_NAME_EXISTS:
                throw new InvalidOperationException($"{ntStatus.ToString()}: {c_ntStatusStatusObjectNameExists}");
            case NTStatus.STATUS_LOCK_NOT_GRANTED:
                throw new IOException($"{ntStatus.ToString()}: {c_ntStatusStatusLockNotGranted}");
            case NTStatus.STATUS_BUFFER_TOO_SMALL:
                throw new ArgumentException($"{ntStatus.ToString()}: {c_ntStatusStatusBufferTooSmall}");
            case NTStatus.STATUS_BAD_DEVICE_TYPE:
                throw new InvalidOperationException($"{ntStatus.ToString()}: {c_ntStatusStatusBadDeviceType}");
            case NTStatus.STATUS_FS_DRIVER_REQUIRED:
                throw new FileLoadException($"{ntStatus.ToString()}: {c_ntStatusStatusFsDriverRequired}");
            case NTStatus.STATUS_USER_SESSION_DELETED:
                throw new UnauthorizedAccessException($"{ntStatus.ToString()}: {c_ntStatusStatusUserSessionDeleted}");
            case NTStatus.SEC_I_CONTINUE_NEEDED:
                throw new InvalidOperationException($"{ntStatus.ToString()}");
            case NTStatus.STATUS_CANCELLED:
                throw new IOException($"{ntStatus.ToString()}: {c_ntStatusStatusCancelled}");
            case NTStatus.STATUS_PENDING:
                throw new InvalidOperationException($"{ntStatus.ToString()}: {c_ntStatusStatusPending}");
            case (NTStatus)3221225566:
                throw new UnauthorizedAccessException("No logon servers are currently available to service the logon request.");  
            case NTStatus.STATUS_IO_TIMEOUT:
            case NTStatus.STATUS_INFO_LENGTH_MISMATCH:
            case NTStatus.STATUS_INSUFFICIENT_RESOURCES:
            case NTStatus.STATUS_REQUEST_NOT_ACCEPTED:
                throw new IOException(ntStatus.ToString());
            case NTStatus.STATUS_WRONG_PASSWORD:
                throw new UnauthorizedAccessException(ntStatus.ToString());
            
            case NTStatus.STATUS_NOTIFY_CLEANUP: // Indicates that a notify change request has been completed due to closing the handle that made the notify change request.
            case NTStatus.STATUS_SUCCESS:
            default:
                break;
        }
    }


    // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-smb/6ab6ca20-b404-41fd-b91a-2ed39e3762ea
    // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-cifs/8f11e0f3-d545-46cc-97e6-f00569e3e1bc
    // Status codes and messages
    // Format:
    // "{Error code} - {POSIX code (if applicable) - {Description}"

    // ERRDOS Class 0x01
    private const string c_errBadFunc = "ERRbadfunc(0x0001) - EINVAL - Invalid Function";
    private const string c_errBadFile = "ERRbadFile(0x0002) - EOENT - File Not Found";
    private const string c_errBadPath = "ERRbadpath(0x0003) - ENOENT - A component in the path prefix is not a directory";
    private const string c_errNoFids = "ERRnofids(0x0004) - EMFILE - Too many open files. No FIDs are available";
    private const string c_errNoAccess = "ERRnoaccess(0x0005) - EPERM - Access denied";
    private const string c_errBadFid = "ERRbadfid(0x0006) - EBADF - Invalid FID";
    private const string c_errNoMem = "ERRnomem(0x0008) - ENOMEM - Insufficient server memory to perform the requested operation";
    private const string c_errBadAccess = "ERRbadaccess(0x000C) - Invalid open mode";
    private const string c_errBadData = "ERRbaddata(0x000D) - E2BIG - Bad data (May be generated by IOCTL calls on the server.)";
    private const string c_errRemCd = "ERRremcd(0x0010) - Remove of directory failed because it was not empty";
    private const string c_errNoFiles = "ERRnofiles(0x0012) - No (more) files found following a file search command";
    private const string c_errEof = "ERReof(0x0026) - EEOF - Attempted to read beyond the end of the file";
    private const string c_errUnsup = "ERRunsup(0x0032) - This command is not supported by the server";
    private const string c_errFileExists = "ERRfilexists(0x0050) - EEXIST - An attempt to create a file or directory failed because an object with the same pathname already exists";
    private const string c_errInvalidParam = "ERRinvalidparam(0x0057) - A parameter supplied with the message is invalid";
    private const string c_errUnknownLevel = "ERRunknownlevel(0x007C) - Invalid information level";
    private const string c_errorNotLocked = "ERROR_NOT_LOCKED(0x009E) - The byte range specified in an unlock request was not locked";
    private const string c_errorNoMoreSearchHandles = "ERROR_NO_MORE_SEARCH_HANDLES(0x0071) - Maximum number of searches has been exhausted.";
    private const string c_errBadPipe = "ERRbadpipe(0x00E6) - Invalid named pipe";
    private const string c_errMoreData = "ERRmoredata(0x00EA) - There is more data available to read on the designated named pipe. {Still Busy} The specified I/O request packet (IRP) cannot be disposed of because the I/O operation is not complete.";
    private const string c_errNotifyEnumDir = "ERR_NOTIFY_ENUM_DIR(0x03FE) - More changes have occurred within the directory than will fit within the specified Change Notify response buffer";

    // ERRSRV Class 0x02
    private const string c_errInvSmb = "ERRError(0x0001) - An invalid SMB client request is received by the server";
    private const string c_errInvTid = "ERRinvtid(0x0005) - The client request received by the server contains an invalid TID value";
    private const string c_errInvNetName = "ERRinvnetname(0x0006) - Invalid server name in Tree Connect";
    private const string c_errBadCmd = "ERRbadcmd(0x0016) - An unknown SMB command code was received by the server";
    private const string c_errTooManyUids = "ERRtoomanyuids(0x005A) - Too many UIDs active for this SMB connection";
    private const string c_errAccountExpired = "ERRaccountExpired(0x08BF) - User account on the target machine is disabled or has expired";
    private const string c_errBadClient = "ERRbadClient(0x08C0) - The client does not have permission to access this server";
    private const string c_errBadLogonTime = "ERRbadLogonTime(0x08C0) - Access to the server is not permitted at this time";
    private const string c_errPasswordExpired = "ERRpasswordExpired(0x08C2) - The user's password has expired";

    // ERRHRD Class 0x03
    private const string c_errNoWrite = "ERRnowrite(0x0013) - EROFS - Attempt to modify a read-only file system";
    private const string c_errBadShare = "ERRbadshare(0x0020) - ETXTBSY - An attempted open operation conflicts with an existing open";
    private const string c_errLock = "ERRlock(0x0021) - EDEADLOCK - A lock request specified an invalid locking mode, or conflicted with an existing file lock";

    // Regular NTStatus Values 
    // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/596a1078-e883-4972-9bbc-49e60bebca55

    private const string c_ntStatusStatusObjectNameInvalid = "The object name is invalid. (0xC0000033)";
    private const string c_ntStatusStatusAccountRestriction = "Indicates a referenced user name and authentication information are valid, but some user account restriction has prevented successful authentication (such as time-of-day restrictions). (0xC000006E)";
    private const string c_ntStatusStatusAccountLockedOut = "The user account has been automatically locked because too many invalid logon attempts or password change attempts have been requested. (0xC0000234)";
    private const string c_ntStatusStatusObjectNameExists = "{Object Exists} An attempt was made to create an object but the object name already exists.(0x40000000)";
    private const string c_ntStatusStatusLogonTypeNotGranted = "A user has requested a type of logon (for example, interactive or network) that has not been granted. An administrator has control over who can logon interactively and through the network. (0xC000015B)";
    private const string c_ntStatusStatusLockNotGranted = "A requested file lock cannot be granted due to other existing locks. (0xC0000055)";
    private const string c_ntStatusStatusBufferTooSmall = "{Buffer Too Small} The buffer is too small to contain the entry. No information has been written to the buffer. (0xC0000023)";
    private const string c_ntStatusStatusBadDeviceType = "{Incorrect Network Resource Type} The specified device type (LPT, for example) conflicts with the actual device type on the remote resource. (0xC00000CB)";
    private const string c_ntStatusStatusFsDriverRequired = "A volume has been accessed for which a file system driver is required that has not yet been loaded. (0xC000019C)";
    private const string c_ntStatusStatusUserSessionDeleted = "The remote user session has been deleted. (0xC0000203)";
    private const string c_ntStatusStatusCancelled = "The I/O request was canceled. (0xC0000120)";
    private const string c_ntStatusStatusPending = "The operation that was requested is pending completion. (0x00000103)";
}
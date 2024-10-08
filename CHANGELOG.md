# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project tries to adhere to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] 2024-09-16

### Changed
- Initial version after fork.
- MANY breaking changes from original project.
- New Namespace for all classes.
- Case changes of class names, e.g. SMBFileSystem is now SmbFileSystem.
- Parameter order / signature changes for many class constructors.
- .Net Standard 2.0 support.
- System.IO.Abstractions 21.0.29 support.
- SMBLibrary 1.5.3.5 support.
- Partial null static analysis support.
- Removed public accessibility for most PathExtensions methods.
- Added SmbFileSystemExtensions extension methods.

## [1.1.15] 2020-06-19

### Changed
- Merge [Update SMBDirectory.Exists to call SMBFile.Exists - by jordanlytle](https://github.com/jordanlytle/SmbAbstraction/pull/38)
    - Update SMBDirectory.Exists to call SMBFile.Exists. This fixes a bug where SMBDirectory.Exists returns true for both files
and Directories.

## [1.1.14] 2020-06-13

### Changed 
- Merge [Create non-shared SMBConnection for SMBStream - by wgv-jxiao](https://github.com/jordanlytle/SmbAbstraction/pull/35)
	- When opening a file stream create a new non-shared `SMBConnection` for `SMBStream` to avoid conflicts on the stream during read and write operations
- Update SMBLibraryLite to 1.4.3.2
	- Revert changes in SMB2Client and SMB2FileStore to not use tree identifier (TID) when calling `WaitOnCommand`

## [1.1.13] 2020-06-09

### Changed
- Update SMBLibraryLite to 1.4.3.1
	- Check and set SMB2Client connection status based on client socket status when reusing a connection
- Added tests for `IDriveInfo` for cases where a directory name and a file name are used.

## Fixed 
- `RootDirectory` in `SMBDriveInfo`
- How credentials are retrieved if a share path is passed in for `SMBDriveInfoFactory.FromDriveName()`
	

## [1.1.12] 2020-06-07

### Changed
- Update SMBLibraryLite to 1.4.3
	- Prevents STATUS_PENDING from being returned from SMB2Client.WaitOnCommand()
	- Temporary fix for `Not Enough Credits`
	- SMB2Client and SMB2FileStore use tree identifier (TID) when calling `WaitOnCommand`

## [1.1.11] 2020-06-07

### Changed 
- Merge [Release unusable SMBConnection - by wgv-jxiao](https://github.com/jordanlytle/SmbAbstraction/pull/31)
	- In `SMBConnection.CreateSMBConnection()`, check if client connection is still alive for the current thread, if not reestablish connection.
- `SMBFile.Open()` dispose `SMBConnection` on failure
- `SMBDriveInfo` null check on `AttributeInformation` for `SMBFileSystemInformation`

## [1.1.10] 2020-05-29

### Changed
- Merge [Ensure File Closeure in Error Scenarios - by wgv-cwoods](https://github.com/jordanlytle/SmbAbstraction/pull/30)
	- `FileStoreUtilities.CloseFile(ISMBFileStore, ref object)` added
		- Now called where all `fileStore.CloseFile` were previously called and on `finally` of `try catch`
- `InvalidCredentialException` and `SMBException` now have a default constructor and are now `Serializable`


## [1.1.10-beta] 2020-05-15

### Changed
- Update SMBLibraryLite to 1.4.3-beta
	- Prevents STATUS_PENDING from being returned from SMB2Client.WaitOnCommand().

## [1.1.10-alpha-01] 2020-05-15

### Changed
- Close any potential `fileStore` and `handles` in `SMBDirectory.Exists` and `SMBFile.Exists` when an exception is caught before returning false
- Added `SMBDirectory` and `SMBDirectoryInfo` tests that check if operations succeed with trailing separators

### Fixed
- `PathExtensions.RemoveLeadingAndTrailingSeparators()` was calling `RemoveLeadingSeparators` twice and not calling `RemoveTrailingSeparators`


## [1.1.10-alpha] 2020-05-12

### Changed
- Update SMBLibraryLite to 1.4.3-alpha
	- Temporaily Fixes "Not enough credits" exception when reading large files


## [1.1.9] - 2020-05-07

### Changed
- Updated SmbLibraryLite to 1.4.2
	- Version number is reflective of TalAloni/SMBLibrary changeset
		- All NetBIOS and SMB Client code is up to date

## [1.1.8] - 2020-05-06

### Changed
- Add `ISmbFileSystemSettings` and `SmbFileSystemOptions` to allow configuring `ClientSessionTimeout` which defaults to `45` seconds
	- `ClientSessionTimeout` is used to set the max time to wait for a `STATUS_PENDING` status to clear. [As per spec, it seemed like the most appropriate timeout name to use.](https://docs.microsoft.com/en-us/archive/blogs/openspecification/cifs-and-smb-timeouts-in-windows)
	- Replaced all old `STATUS_PENDING` (and others) retry logic to use the configured `ClientSessionTimeout`
- Handle `STATUS_PENDING` in `SMBStream.Read()` 

### Fixed
- Return `SmbException` in `SMBStream.Read()`

## [1.1.7] - 2020-04-30

### Changed
- Apply `AccessMask.SYNCHRONIZE` and `CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT` to all `fileStore.CreateFile` calls to ensure synchrony on operations.
	- Left retry logic in place for now, `STATUS_PENDING` seems to no longer occur
- Set explicit `ShareAccess.Read`  and `ShareAccess.Write` to `fileStore.CreateFile` calls for better Samba compabitility.
	- Windows Share ACL was fine with how we were defining `ShareAccess` on calls. Samba shares seem to be picky about it.
- Remove calls to `ToUniversalTime()`  from `Set*Time` in SMBFile
- Add tests in `FileTests` for `IFile.Open` operations  
### Fixed 
- Fix `Set*TimeUtc` calls to use `ToUniversalTime()`  in SMBFile
- Match `DirectoryName` behavior in `SMBFIleInfo` with `base.FileInfo`

## [1.1.6] - 2020-04-26

### Fixed
- `SMBFile.Move()` would leave the file specified in `sourceFileName`.
	- Added test cases to `FileTests` for expected behavior

## [1.1.5] - 2020-04-20

### Changed
- Implemented `SMBPath.IsPathRooted` for share paths
### Fixed
- Handle `null` input for `SMBPath.GetDirectoryName()` and `SMBPath.GetFileName`

## [1.1.4] - 2020-04-18

### Changed 
- Check if input `driveName` is a share path or share name and operate on input, instead of trying to determine if the input is not a share path or share name
	- Too difficult to try to determine if input is for host's file system or for a share
- Add logging output to `dotnet test` in `dockerfile`
### Fixed
- `PathExtension.IsValidSharePath` throwing when input is invalid, instead of catching `Uri` exception and returning `false

## [1.1.2] - 2020-04-17

### Changed
- Add `RemoveLeadingAndTrailingSeperators()` to `PathExtensions`
- Replace calls to `RemoveAnySeperators()` to `RemoveLeadingAndTrailingSeperators()`
- Make `RemoveAnySeperators()` private

### Fixed
- Fix implementaion of `SMBPath.GetDirectoryName()` where the returning string was not the fill path of the parent directory of input path
- Fix call to `SMBPath.GetDirectoryName` in `SMBFileInfo`

## [1.1.0] - 2020-04-17

### Changed 
- Add `ILoggerFactory` as optional parameter to `SMBFileSystem`
- Add loging to `SMBFile`
- Add loging to `SMBDirectory`
- Add loging to `SMBDirectoryInfoFactory`
- Add loging to `SMBFileInfoFactory`
- Add loging to `SMBDriveInfoFactory`
- Add `ITestOutputHelper` to unit tests
- Wrap exceptions in `SMBExcpetion`

## [1.0.11] - 2020-04-16

### Fixed 
- Unescaped `Uri` strings from `PathExtensions` were causing issues on calls to `CreateFile` due to lingering `Uri` escaped symbols

## [1.0.10] - 2020-04-11

### Fixed
- Logic for setting `Parent` and `Root` for `SMBDirectoryInfo` that was causing `SackOverflow` and `null` reference exceptions

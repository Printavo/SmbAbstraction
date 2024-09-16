using SMBLibrary;
using SMBLibrary.Client;

namespace InkSoft.SmbAbstraction;

public class SmbFileSystemInformation
{
    public FileFsVolumeInformation VolumeInformation { get; }
    public FileFsDeviceInformation DeviceInformation { get; }
    public FileFsFullSizeInformation SizeInformation { get; }
    public FileFsAttributeInformation AttributeInformation { get; }
    public FileFsControlInformation ControlInformation { get; }
    public FileFsSectorSizeInformation SectorSizeInformation { get; }

    public SmbFileSystemInformation(ISMBFileStore fileStore, string path, NTStatus status)
    {
        string? shareName = path.ShareName();
        string? relativePath = path.RelativeSharePath();

        if (status == NTStatus.STATUS_SUCCESS)
        {
            fileStore.GetFileSystemInformation(out var fileFsVolumeInformation, FileSystemInformationClass.FileFsVolumeInformation);
            fileStore.GetFileSystemInformation(out var fileFsDeviceInformation, FileSystemInformationClass.FileFsDeviceInformation);
            fileStore.GetFileSystemInformation(out var fileFsFullSizeInformation, FileSystemInformationClass.FileFsFullSizeInformation);
            fileStore.GetFileSystemInformation(out var fileFsAttributeInformation, FileSystemInformationClass.FileFsAttributeInformation);
            fileStore.GetFileSystemInformation(out var fileFsControlInformation, FileSystemInformationClass.FileFsControlInformation);
            fileStore.GetFileSystemInformation(out var fileFsSectorSizeInformation, FileSystemInformationClass.FileFsSectorSizeInformation);

            VolumeInformation = (FileFsVolumeInformation)fileFsVolumeInformation;
            DeviceInformation = (FileFsDeviceInformation)fileFsDeviceInformation;
            SizeInformation = (FileFsFullSizeInformation)fileFsFullSizeInformation;
            AttributeInformation = (FileFsAttributeInformation)fileFsAttributeInformation;
            ControlInformation = (FileFsControlInformation)fileFsControlInformation;
            SectorSizeInformation = (FileFsSectorSizeInformation)fileFsSectorSizeInformation;
        }
    }
}
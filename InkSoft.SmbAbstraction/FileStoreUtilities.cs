using SMBLibrary.Client;

namespace InkSoft.SmbAbstraction.Utilities;

internal static class FileStoreUtilities
{
    public static void CloseFile(ISMBFileStore? fileStore, ref object? fileHandle)
    {
        if (fileStore == null || fileHandle == null)
            return;

        // TODO: In the original library, this wasn't necessary. What's up here?
        try
        {
            fileStore.CloseFile(fileHandle);
        }
        catch
        {
        }
        fileHandle = null;
    }
}
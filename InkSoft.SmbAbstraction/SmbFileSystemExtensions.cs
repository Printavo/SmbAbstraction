using Microsoft.Extensions.Logging;
using System;
using System.IO.Abstractions;
using System.Linq;

namespace InkSoft.SmbAbstraction;

public static class SmbFileSystemExtensions
{
    /// <summary>
    /// Copies all subfolders and files from <paramref name="sourcePath"/> to <paramref name="destinationPath"/>, optionally <paramref name="overwriteExistingFiles"/> if already present at destination.
    /// </summary>
    public static void Copy(this IDirectory directory, string sourcePath, string destinationPath, bool overwriteExistingFiles, ILogger? logger = null)
    {
        var fileSystem = directory.FileSystem;
        logger ??= (fileSystem as SmbFileSystem)?.LoggerFactory?.CreateLogger(nameof(SmbFileSystemExtensions));
        
        // TBD: Should we ensure path format here by calling Path.GetFullPath()? It may or may not do what we want with UNC on Linux.
        sourcePath = sourcePath.RemoveTrailingSeparators();
        destinationPath = destinationPath.RemoveTrailingSeparators();
        logger?.LogTrace("Copying {sourcePath} contents to {destinationPath}; overwriteExistingFiles: {overwriteExistingFiles}...", sourcePath, destinationPath, overwriteExistingFiles);
        
        if (!directory.Exists(sourcePath))
        {
            logger?.LogError("Missing source folder: {sourcePath}", sourcePath);
            return;
        }

        // In the case of multiple nested folders, we only need to check the furthest nested folders with CreateDirectory because checking any intermediate parents is redundant.
        var furthestNestedFolders = directory.GetDirectories(sourcePath, "*", System.IO.SearchOption.AllDirectories).Select(p => p[(sourcePath.Length+1)..]).ToList();
        
        // Need to cache with ToArray so we don't modify the collection we're iterating over.
        foreach (string subFolder in furthestNestedFolders.ToArray())
        {
            // Remove subFolder from the check-list if it has further nested subfolders. Doesn't work quite the same with '/' paths, but it's probably okay.
            if (furthestNestedFolders.Any(f => f.StartsWith(subFolder+"\\")))
                furthestNestedFolders.Remove(subFolder);
        }
        
        foreach (string furthestNestedPath in furthestNestedFolders.Select(p => fileSystem.Path.Combine(destinationPath, p)))
        {
            try
            {
                logger?.LogTrace("Ensuring Destination: {furthestNestedPath}", furthestNestedPath);
                directory.CreateDirectory(furthestNestedPath);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error checking source ({sourcePath}) and destination ({destinationPath}) folders.", sourcePath, furthestNestedPath);
            }
        }

        foreach (string sourceFileSubPath in directory.GetFiles(sourcePath, "*", System.IO.SearchOption.AllDirectories).Select(p => p[(sourcePath.Length+1)..]))
        {
            string destinationFilePath = fileSystem.Path.Combine(destinationPath, sourceFileSubPath);

            try
            {
                if (overwriteExistingFiles || !fileSystem.File.Exists(destinationFilePath))
                {
                    logger?.LogTrace("Copying: {sourceFileSubPath} from {sourcePath} to {destinationPath}", sourceFileSubPath, sourcePath, destinationPath);
                    fileSystem.File.Copy(fileSystem.Path.Combine(sourcePath, sourceFileSubPath), destinationFilePath, overwriteExistingFiles);
                }
                else
                {
                    logger?.LogTrace("Skipping: {sourceFileSubPath} from {sourcePath} to {destinationPath}", sourceFileSubPath, sourcePath, destinationPath);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error copying: {sourceFileSubPath} from {sourcePath} to {destinationPath}", sourceFileSubPath, sourcePath, destinationPath);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.Models
{
    public class FileOperations
    {
        public FileOperations()
        {
            // No longer need AppendToConsoleOutput - using static ConsoleMessenger
        }

        public async Task<bool> SmartDeleteDirectory(string path)
        {
            try
            {
                // First attempt: try standard deletion
                ConsoleMessenger.AddInfoMessage("Attempting standard directory deletion...");
                Directory.Delete(path, true);
                ConsoleMessenger.AddSuccessMessage("Standard deletion successful.");
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                ConsoleMessenger.AddWarningMessage("Access denied. Attempting permission-aware deletion...");
                return await DeleteWithPermissionHandling(path);
            }
            catch (DirectoryNotFoundException)
            {
                // Directory doesn't exist, consider it successfully deleted
                ConsoleMessenger.AddInfoMessage("Directory not found - already deleted.");
                return true;
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddWarningMessageF("Standard deletion failed: {0}", ex.Message);
                ConsoleMessenger.AddInfoMessage("Attempting permission-aware deletion...");
                return await DeleteWithPermissionHandling(path);
            }
        }

        private async Task<bool> DeleteWithPermissionHandling(string rootPath)
        {
            return await Task.Run(() =>
            {
                var problematicFiles = new List<string>();
                var problematicDirs = new List<string>();

                try
                {
                    // First pass: identify and fix permission issues
                    IdentifyAndFixPermissionIssues(rootPath, problematicFiles, problematicDirs);

                    // Second pass: attempt deletion
                    DeleteDirectoryContents(rootPath);

                    // Finally delete the root directory
                    var rootDir = new DirectoryInfo(rootPath);
                    if (rootDir.Exists)
                    {
                        try
                        {
                            rootDir.Attributes = FileAttributes.Normal;
                            rootDir.Delete(false);
                        }
                        catch (Exception ex)
                        {
                            ConsoleMessenger.AddErrorMessageF("Could not delete root directory {0}: {1}", rootPath, ex.Message);
                            return false;
                        }
                    }

                    ConsoleMessenger.AddSuccessMessageF("Successfully deleted directory. Fixed permissions on {0} files and {1} directories.", problematicFiles.Count, problematicDirs.Count);
                    return true;
                }
                catch (Exception ex)
                {
                    ConsoleMessenger.AddErrorMessageF("Permission-aware deletion failed: {0}", ex.Message);
                    return false;
                }
            });
        }

        private void IdentifyAndFixPermissionIssues(string path, List<string> problematicFiles, List<string> problematicDirs)
        {
            if (!Directory.Exists(path))
                return;

            var directory = new DirectoryInfo(path);

            // Check and fix directory permissions
            try
            {
                if (HasRestrictiveAttributes(directory.Attributes))
                {
                    ConsoleMessenger.AddDebugMessageF("Fixing permissions on directory: {0}", path);
                    directory.Attributes = FileAttributes.Normal;
                    problematicDirs.Add(path);
                }
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddWarningMessageF("Could not fix directory permissions for {0}: {1}", path, ex.Message);
            }

            // Process files in this directory
            try
            {
                foreach (var file in directory.GetFiles())
                {
                    try
                    {
                        if (HasRestrictiveAttributes(file.Attributes))
                        {
                            ConsoleMessenger.AddDebugMessageF("Fixing permissions on file: {0}", file.FullName);
                            file.Attributes = FileAttributes.Normal;
                            problematicFiles.Add(file.FullName);
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleMessenger.AddWarningMessageF("Could not fix file permissions for {0}: {1}", file.FullName, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddWarningMessageF("Could not enumerate files in {0}: {1}", path, ex.Message);
            }

            // Recursively process subdirectories
            try
            {
                foreach (var subDir in directory.GetDirectories())
                {
                    IdentifyAndFixPermissionIssues(subDir.FullName, problematicFiles, problematicDirs);
                }
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddWarningMessageF("Could not enumerate subdirectories in {0}: {1}", path, ex.Message);
            }
        }

        private void DeleteDirectoryContents(string path)
        {
            if (!Directory.Exists(path))
                return;

            var directory = new DirectoryInfo(path);

            // Delete all files first
            try
            {
                foreach (var file in directory.GetFiles())
                {
                    try
                    {
                        // Ensure file is deletable
                        if (file.Exists)
                        {
                            file.Attributes = FileAttributes.Normal;
                            file.Delete();
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleMessenger.AddWarningMessageF("Could not delete file {0}: {1}", file.FullName, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddWarningMessageF("Error processing files in {0}: {1}", path, ex.Message);
            }

            // Then delete subdirectories recursively
            try
            {
                foreach (var subDir in directory.GetDirectories())
                {
                    DeleteDirectoryContents(subDir.FullName);

                    // Delete the subdirectory itself
                    try
                    {
                        if (subDir.Exists)
                        {
                            subDir.Attributes = FileAttributes.Normal;
                            subDir.Delete(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleMessenger.AddWarningMessageF("Could not delete directory {0}: {1}", subDir.FullName, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddWarningMessageF("Error processing subdirectories in {0}: {1}", path, ex.Message);
            }
        }

        private static bool HasRestrictiveAttributes(FileAttributes attributes)
        {
            // Check for attributes that might prevent deletion
            return (attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly ||
                   (attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                   (attributes & FileAttributes.System) == FileAttributes.System;
        }
    }
}
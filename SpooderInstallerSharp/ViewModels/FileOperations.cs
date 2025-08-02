using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.ViewModels
{
    public class FileOperations
    {
        private readonly Action<string> AppendToConsoleOutput;

        public FileOperations(Action<string> appendToConsoleOutput)
        {
            AppendToConsoleOutput = appendToConsoleOutput;
        }

        public async Task<bool> SmartDeleteDirectory(string path)
        {
            try
            {
                // First attempt: try standard deletion
                AppendToConsoleOutput("Attempting standard directory deletion...");
                Directory.Delete(path, true);
                AppendToConsoleOutput("Standard deletion successful.");
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                AppendToConsoleOutput("Access denied. Attempting permission-aware deletion...");
                return await DeleteWithPermissionHandling(path);
            }
            catch (DirectoryNotFoundException)
            {
                // Directory doesn't exist, consider it successfully deleted
                AppendToConsoleOutput("Directory not found - already deleted.");
                return true;
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Standard deletion failed: {ex.Message}");
                AppendToConsoleOutput("Attempting permission-aware deletion...");
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
                            AppendToConsoleOutput($"Could not delete root directory {rootPath}: {ex.Message}");
                            return false;
                        }
                    }

                    AppendToConsoleOutput($"Successfully deleted directory. Fixed permissions on {problematicFiles.Count} files and {problematicDirs.Count} directories.");
                    return true;
                }
                catch (Exception ex)
                {
                    AppendToConsoleOutput($"Permission-aware deletion failed: {ex.Message}");
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
                    AppendToConsoleOutput($"Fixing permissions on directory: {path}");
                    directory.Attributes = FileAttributes.Normal;
                    problematicDirs.Add(path);
                }
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Warning: Could not fix directory permissions for {path}: {ex.Message}");
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
                            AppendToConsoleOutput($"Fixing permissions on file: {file.FullName}");
                            file.Attributes = FileAttributes.Normal;
                            problematicFiles.Add(file.FullName);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendToConsoleOutput($"Warning: Could not fix file permissions for {file.FullName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Warning: Could not enumerate files in {path}: {ex.Message}");
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
                AppendToConsoleOutput($"Warning: Could not enumerate subdirectories in {path}: {ex.Message}");
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
                        AppendToConsoleOutput($"Warning: Could not delete file {file.FullName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Warning: Error processing files in {path}: {ex.Message}");
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
                        AppendToConsoleOutput($"Warning: Could not delete directory {subDir.FullName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Warning: Error processing subdirectories in {path}: {ex.Message}");
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
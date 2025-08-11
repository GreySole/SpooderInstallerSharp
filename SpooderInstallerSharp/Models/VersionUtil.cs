using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Velopack;
using Velopack.Sources;

namespace SpooderInstallerSharp.Models
{
    public static class VersionUtil
    {
        /// <summary>
        /// Gets the current application version, preferring Velopack information when available
        /// </summary>
        /// <returns>A tuple containing (version, isVelopackInstalled, versionSource)</returns>
        public static (string Version, bool IsVelopackInstalled, string VersionSource) GetApplicationVersion()
        {
            string version = "Unknown";
            string versionSource = "Unknown";
            bool isVelopackInstalled = false;

            try
            {
                // Check if we're running as a Velopack-installed app (Windows only)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        var mgr = new UpdateManager(new GithubSource("https://github.com/GreySole/SpooderInstallerSharp", "", false));
                        isVelopackInstalled = mgr.IsInstalled;
                        
                        if (isVelopackInstalled)
                        {
                            versionSource = "Velopack (Installed)";
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Velopack check failed: {ex.Message}");
                    }
                }

                // Get version from assembly (works for both Velopack and non-Velopack installations)
                var assembly = Assembly.GetExecutingAssembly();
                var assemblyName = assembly.GetName();
                
                // Get the version from AssemblyVersion first
                var assemblyVersion = assemblyName.Version?.ToString() ?? "Unknown";
                
                // Get the file version if available (which includes pre-release info like "-test")
                var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
                var fileVersion = fileVersionInfo.FileVersion ?? assemblyVersion;
                
                version = fileVersion;
                
                if (versionSource == "Unknown")
                {
                    versionSource = "Assembly";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Version retrieval failed: {ex.Message}");
                version = "Unknown";
                versionSource = $"Error: {ex.Message}";
            }

            return (version, isVelopackInstalled, versionSource);
        }

        /// <summary>
        /// Gets just the version string (simplified version of GetApplicationVersion)
        /// </summary>
        /// <returns>The current application version as a string</returns>
        public static string GetVersionString()
        {
            var (version, _, _) = GetApplicationVersion();
            return version;
        }

        /// <summary>
        /// Gets a formatted version display string suitable for UI display
        /// </summary>
        /// <param name="applicationName">The name of the application to include in the display</param>
        /// <returns>A formatted string like "=== Application Name v1.0.0 (Velopack) ==="</returns>
        public static string GetFormattedVersionDisplay(string applicationName = "Spooder Manager")
        {
            var (version, isVelopackInstalled, _) = GetApplicationVersion();
            
            return isVelopackInstalled 
                ? $"=== {applicationName} v{version} (Velopack) ===" 
                : $"=== {applicationName} v{version} ===";
        }
    }
}
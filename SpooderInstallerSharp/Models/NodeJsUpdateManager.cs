using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.Models
{
    /// <summary>
    /// Checks the Node.js version bundled next to the app against the latest patch/minor
    /// release on the same major line (the major line the app was built against, via the
    /// NodeJSVersion AssemblyMetadata set in the Desktop csproj), and updates it in place.
    /// </summary>
    public class NodeJsUpdateManager
    {
        private readonly ProcessManager _processManager;

        public NodeJsUpdateManager(ProcessManager processManager)
        {
            _processManager = processManager;
        }

        public async Task CheckAndUpdateAsync()
        {
            if (!File.Exists(_processManager.nodePath))
            {
                return;
            }

            var bundledVersionString = GetBundledNodeJSVersion();
            if (bundledVersionString == null || !Version.TryParse(bundledVersionString, out var bundledVersion))
            {
                ConsoleMessenger.AddWarningMessage("Could not determine bundled Node.js version metadata; skipping Node.js update check.");
                return;
            }

            var installedVersion = await GetInstalledNodeVersionAsync();
            if (installedVersion == null)
            {
                ConsoleMessenger.AddWarningMessage("Could not determine installed Node.js version.");
                return;
            }

            ConsoleMessenger.AddInfoMessageF("Checking for Node.js updates (installed: {0})...", installedVersion);

            var latestVersion = await GetLatestPatchVersionAsync(bundledVersion.Major);
            if (latestVersion == null)
            {
                ConsoleMessenger.AddWarningMessage("Could not check latest Node.js version.");
                return;
            }

            if (latestVersion > installedVersion)
            {
                ConsoleMessenger.AddWarningMessageF("Node.js update available: {0} -> {1}. Updating...", installedVersion, latestVersion);
                await DownloadAndInstallAsync(latestVersion);
            }
            else
            {
                ConsoleMessenger.AddSuccessMessageF("Node.js is up to date ({0}).", installedVersion);
            }
        }

        private static string? GetBundledNodeJSVersion()
        {
            return Assembly.GetEntryAssembly()?
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "NodeJSVersion")?.Value;
        }

        private async Task<Version?> GetInstalledNodeVersionAsync()
        {
            try
            {
                var processStartInfo = new ProcessStartInfo(_processManager.nodePath, "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                output = output.Trim().TrimStart('v');
                return Version.TryParse(output, out var version) ? version : null;
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddErrorMessageF("Error checking installed Node.js version: {0}", ex.Message);
                return null;
            }
        }

        private async Task<Version?> GetLatestPatchVersionAsync(int major)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var response = await httpClient.GetAsync("https://nodejs.org/dist/index.json");
                if (!response.IsSuccessStatusCode)
                {
                    ConsoleMessenger.AddErrorMessageF("Failed to fetch Node.js release list: {0}", response.StatusCode);
                    return null;
                }

                string content = await response.Content.ReadAsStringAsync();
                JArray releases = JArray.Parse(content);

                Version? latest = null;
                foreach (var release in releases)
                {
                    var versionString = release["version"]?.ToString()?.TrimStart('v');
                    if (Version.TryParse(versionString, out var version) && version.Major == major)
                    {
                        if (latest == null || version > latest)
                        {
                            latest = version;
                        }
                    }
                }

                return latest;
            }
            catch (HttpRequestException ex)
            {
                ConsoleMessenger.AddErrorMessageF("Network error checking Node.js releases: {0}", ex.Message);
                return null;
            }
            catch (TaskCanceledException)
            {
                ConsoleMessenger.AddWarningMessage("Request timed out while checking Node.js releases");
                return null;
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddErrorMessageF("Error checking Node.js releases: {0}", ex.Message);
                return null;
            }
        }

        private async Task DownloadAndInstallAsync(Version newVersion)
        {
            if (!OperatingSystem.IsWindows())
            {
                ConsoleMessenger.AddWarningMessage("Automatic Node.js updates are currently only supported on Windows.");
                return;
            }

            if (IsSpooderProcessRunning())
            {
                ConsoleMessenger.AddWarningMessage("Skipping Node.js update because Spooder is currently running.");
                return;
            }

            var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
            var nodeDir = Path.Combine(exeDir, "nodejs");
            var tempDir = Path.Combine(Path.GetTempPath(), $"spooder_nodejs_update_{Guid.NewGuid():N}");
            var archivePath = Path.Combine(tempDir, $"node-v{newVersion}-win-x64.zip");
            var extractPath = Path.Combine(tempDir, "extracted");

            try
            {
                Directory.CreateDirectory(tempDir);

                var url = $"https://nodejs.org/dist/v{newVersion}/node-v{newVersion}-win-x64.zip";
                ConsoleMessenger.AddInfoMessageF("Downloading Node.js {0}...", newVersion);

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(5);
                    using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    using var fileStream = File.Create(archivePath);
                    await response.Content.CopyToAsync(fileStream);
                }

                ConsoleMessenger.AddInfoMessage("Extracting Node.js update...");
                ZipFile.ExtractToDirectory(archivePath, extractPath);

                var extractedNodeFolder = Path.Combine(extractPath, $"node-v{newVersion}-win-x64");
                if (!Directory.Exists(extractedNodeFolder))
                {
                    ConsoleMessenger.AddErrorMessage("Node.js update extraction failed: expected folder not found.");
                    return;
                }

                if (IsSpooderProcessRunning())
                {
                    ConsoleMessenger.AddWarningMessage("Skipping Node.js update because Spooder started running during the download.");
                    return;
                }

                foreach (var filePath in Directory.GetFiles(extractedNodeFolder, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(extractedNodeFolder, filePath);
                    var destPath = Path.Combine(nodeDir, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    File.Copy(filePath, destPath, overwrite: true);
                }

                _processManager.CheckPaths();
                ConsoleMessenger.AddSuccessMessageF("Node.js updated to {0}.", newVersion);
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddErrorMessageF("Error updating Node.js: {0}", ex.Message);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch
                {
                    // Best-effort cleanup of the temp download; leftovers are harmless.
                }
            }
        }

        private bool IsSpooderProcessRunning()
        {
            var process = _processManager.spooderProcess;
            return process != null && !process.HasExited;
        }
    }
}

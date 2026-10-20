using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.Models
{
    /// <summary>
    /// Bootstraps and drives fnm (Fast Node Manager) so the user picks a Node.js version from
    /// Settings and fnm owns installing/switching it, instead of the app bundling and
    /// self-updating its own private copy of Node in place (that in-place update was fragile -
    /// it merged new files over old ones without removing stale ones, which produced runtime
    /// errors like "Class extends value undefined" when npm's internals ended up mismatched).
    /// </summary>
    public class FnmManager
    {
        private static readonly string? exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        private static readonly string fnmDir = Path.Combine(exeDir ?? "", "fnm");
        private static readonly string fnmDataDir = Path.Combine(exeDir ?? "", "fnm-data");
        private static readonly string fnmExeName = OperatingSystem.IsWindows() ? "fnm.exe" : "fnm";
        private static readonly string fnmExePath = Path.Combine(fnmDir, fnmExeName);

        /// <summary>
        /// Ensures fnm itself is installed, ensures the given Node.js version is installed via
        /// fnm, and points the given ProcessManager at that version's node/npm executables.
        /// </summary>
        public async Task<bool> EnsureNodeVersionReadyAsync(ProcessManager processManager, string version)
        {
            try
            {
                if (!await EnsureFnmInstalledAsync())
                {
                    return false;
                }

                Directory.CreateDirectory(fnmDataDir);

                ConsoleMessenger.AddInfoMessageF("Setting up Node.js {0} via fnm...", version);

                var installResult = await RunFnmAsync("install", version);
                if (installResult.ExitCode != 0)
                {
                    ConsoleMessenger.AddErrorMessageF("fnm failed to install Node.js {0}.", version);
                    return false;
                }

                var execResult = await RunFnmAsync("exec", "--using", version, "--", "node", "-e", "console.log(process.execPath)");
                var nodeExePath = execResult.Output.Trim();
                if (execResult.ExitCode != 0 || string.IsNullOrEmpty(nodeExePath) || !File.Exists(nodeExePath))
                {
                    ConsoleMessenger.AddErrorMessageF("Could not resolve the Node.js {0} executable via fnm.", version);
                    return false;
                }

                var nodeBinDir = Path.GetDirectoryName(nodeExePath) ?? "";
                var npmExeName = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
                var npmExePath = Path.Combine(nodeBinDir, npmExeName);

                processManager.nodePath = nodeExePath;
                processManager.npmPath = npmExePath;

                ConsoleMessenger.AddSuccessMessageF("Using Node.js {0} via fnm ({1}).", version, nodeExePath);
                return true;
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddErrorMessageF("Error setting up Node.js via fnm: {0}", ex.Message);
                return false;
            }
        }

        private async Task<bool> EnsureFnmInstalledAsync()
        {
            if (File.Exists(fnmExePath))
            {
                return true;
            }

            var assetName = GetFnmAssetNameForPlatform();
            if (assetName == null)
            {
                ConsoleMessenger.AddErrorMessage("fnm is not available for this platform.");
                return false;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), $"spooder_fnm_install_{Guid.NewGuid():N}");
            var archivePath = Path.Combine(tempDir, assetName);

            try
            {
                Directory.CreateDirectory(tempDir);
                ConsoleMessenger.AddInfoMessage("Downloading fnm (Node.js version manager)...");

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SpooderInstallerSharp");
                    httpClient.Timeout = TimeSpan.FromMinutes(2);

                    var releaseJson = await httpClient.GetStringAsync("https://api.github.com/repos/Schniz/fnm/releases/latest");
                    var release = JObject.Parse(releaseJson);
                    var assets = release["assets"] as JArray;
                    var asset = assets?.FirstOrDefault(a => string.Equals(a["name"]?.ToString(), assetName, StringComparison.OrdinalIgnoreCase));
                    var downloadUrl = asset?["browser_download_url"]?.ToString();

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        ConsoleMessenger.AddErrorMessageF("Could not find fnm release asset '{0}'.", assetName);
                        return false;
                    }

                    using var response = await httpClient.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();
                    using var fileStream = File.Create(archivePath);
                    await response.Content.CopyToAsync(fileStream);
                }

                Directory.CreateDirectory(fnmDir);
                ZipFile.ExtractToDirectory(archivePath, fnmDir, overwriteFiles: true);

                if (!OperatingSystem.IsWindows() && File.Exists(fnmExePath))
                {
                    // The extracted binary needs the executable bit set on Unix.
                    File.SetUnixFileMode(fnmExePath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }

                if (!File.Exists(fnmExePath))
                {
                    ConsoleMessenger.AddErrorMessage("fnm download succeeded but the executable was not found after extraction.");
                    return false;
                }

                ConsoleMessenger.AddSuccessMessage("fnm installed successfully.");
                return true;
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddErrorMessageF("Error downloading fnm: {0}", ex.Message);
                return false;
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

        private static string? GetFnmAssetNameForPlatform()
        {
            if (OperatingSystem.IsWindows())
            {
                return "fnm-windows.zip";
            }
            if (OperatingSystem.IsLinux())
            {
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? "fnm-linux-arm64.zip"
                    : "fnm-linux.zip";
            }
            if (OperatingSystem.IsMacOS())
            {
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? "fnm-macos-arm64.zip"
                    : "fnm-macos.zip";
            }
            return null;
        }

        private async Task<(int ExitCode, string Output)> RunFnmAsync(params string[] args)
        {
            var processStartInfo = new ProcessStartInfo(fnmExePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in args)
            {
                processStartInfo.ArgumentList.Add(arg);
            }
            processStartInfo.EnvironmentVariables["FNM_DIR"] = fnmDataDir;

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            {
                ConsoleMessenger.AddPlainMessage(error.Trim());
            }

            return (process.ExitCode, output);
        }
    }
}

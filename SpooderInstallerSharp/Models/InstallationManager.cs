using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.Models
{
    public class InstallationManager
    {
        private readonly GitOperations _gitOperations;
        private readonly ProcessManager _processManager;
        private readonly FileOperations _fileOperations;
        private readonly Action OnSpooderInstallStart;
        private readonly Action OnSpooderInstallComplete;
        private readonly Action OnSpooderUninstalled;
        private readonly Action OnSpooderCleaned;

        public InstallationManager(GitOperations gitOperations, ProcessManager processManager, FileOperations fileOperations,
                                 Action onSpooderInstallStart, Action onSpooderInstallComplete,
                                 Action onSpooderUninstalled, Action onSpooderCleaned)
        {
            _gitOperations = gitOperations;
            _processManager = processManager;
            _fileOperations = fileOperations;
            OnSpooderInstallStart = onSpooderInstallStart;
            OnSpooderInstallComplete = onSpooderInstallComplete;
            OnSpooderUninstalled = onSpooderUninstalled;
            OnSpooderCleaned = onSpooderCleaned;
        }

        public async Task<bool> InstallSpooder()
        {
            var appSettings = SettingsManager.LoadSettings();
            var scriptPath = appSettings.SpooderInstallationPath;
            var selectedBranch = appSettings.SelectedBranch;
            
            ConsoleMessenger.AddInfoMessageF("Installing Spooder to {0} on branch {1}", scriptPath, selectedBranch);
            Logger.LogInfo($"Installing Spooder to {scriptPath} on branch {selectedBranch}");
            OnSpooderInstallStart();
            
            _gitOperations.CloneRepository("https://github.com/GreySole/Spooder.git", scriptPath, branch: selectedBranch);

            _processManager.CheckPaths();

            ConsoleMessenger.AddInfoMessage("Installing dependencies with npm...");

            var processStartInfo = new ProcessStartInfo(_processManager.npmPath, "install")
            {
                WorkingDirectory = scriptPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            _processManager.PrependNodeBinToPath(processStartInfo);

            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // Use plain message for real-time npm output to avoid CSS formatting
                        ConsoleMessenger.AddPlainMessage(e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // Use plain message for real-time npm output to avoid CSS formatting
                        ConsoleMessenger.AddPlainMessage(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    ConsoleMessenger.AddSuccessMessage("Dependencies installed successfully.");

                    // After npm install, run the build command if it exists
                    bool buildSuccess = await RunBuildCommand(scriptPath);

                    if (buildSuccess)
                    {
                        ConsoleMessenger.AddSuccessMessage("Installation and build completed successfully.");
                        OnSpooderInstallComplete();
                        return true;
                    }
                    else
                    {
                        ConsoleMessenger.AddWarningMessage("Installation succeeded but build failed. You can still run Spooder in Dev mode.");
                        OnSpooderInstallComplete();
                        return true;
                    }
                }
                else
                {
                    ConsoleMessenger.AddErrorMessage("Installation failed.");
                    OnSpooderInstallComplete();
                    return false;
                }
            }
        }

        public async Task<bool> UninstallSpooder()
        {
            try
            {
                var appSettings = SettingsManager.LoadSettings();
                // First, stop any running Spooder process
                if (_processManager.spooderProcess != null && !_processManager.spooderProcess.HasExited)
                {
                    ConsoleMessenger.AddInfoMessage("Stopping Spooder process before uninstallation...");
                    _processManager.StopSpooder();
                }

                if (Directory.Exists(appSettings.SpooderInstallationPath))
                {
                    ConsoleMessenger.AddInfoMessageF("Removing Spooder installation from {0}...", appSettings.SpooderInstallationPath);

                    // Wait a moment to ensure all file handles are closed
                    await Task.Delay(1000);

                    // Try smart deletion with permission handling
                    bool success = await _fileOperations.SmartDeleteDirectory(appSettings.SpooderInstallationPath);

                    if (success)
                    {
                        OnSpooderUninstalled();
                        ConsoleMessenger.AddSuccessMessage("Spooder has been successfully uninstalled.");
                        return true;
                    }
                    else
                    {
                        ConsoleMessenger.AddErrorMessage("Failed to completely remove Spooder installation directory.");
                        return false;
                    }
                }
                else
                {
                    ConsoleMessenger.AddWarningMessage("Spooder installation directory not found. Nothing to uninstall.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddErrorMessageF("Error uninstalling Spooder: {0}", ex.Message);
                return false;
            }
        }

        public async Task<bool> CleanSpooder()
        {
            try
            {
                var appSettings = SettingsManager.LoadSettings();
                // First, stop any running Spooder process
                if (_processManager.spooderProcess != null && !_processManager.spooderProcess.HasExited)
                {
                    ConsoleMessenger.AddInfoMessage("Stopping Spooder process before cleaning...");
                    _processManager.StopSpooder();
                }

                var userDataPath = Path.Combine(appSettings.SpooderInstallationPath, "user");

                if (Directory.Exists(userDataPath))
                {
                    ConsoleMessenger.AddInfoMessageF("Removing Spooder User data from {0}...", appSettings.SpooderInstallationPath);

                    // Wait a moment to ensure all file handles are closed
                    await Task.Delay(1000);

                    // Try smart deletion with permission handling
                    bool success = await _fileOperations.SmartDeleteDirectory(userDataPath);

                    if (success)
                    {
                        OnSpooderCleaned();
                        ConsoleMessenger.AddSuccessMessage("Spooder has been successfully cleaned.");
                        return true;
                    }
                    else
                    {
                        ConsoleMessenger.AddErrorMessage("Failed to clean Spooder entirely.");
                        return false;
                    }
                }
                else
                {
                    ConsoleMessenger.AddWarningMessage("Spooder installation directory not found. Nothing to clean.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddErrorMessageF("Error cleaning Spooder: {0}", ex.Message);
                return false;
            }
        }

        public async Task<bool> RunNpmInstall(string workingDirectory)
        {
            ConsoleMessenger.AddInfoMessage("Running npm install to update dependencies...");
            _processManager.CheckPaths();

            var processStartInfo = new ProcessStartInfo(_processManager.npmPath, "install --verbose")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            _processManager.PrependNodeBinToPath(processStartInfo);

            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // Use plain message for real-time npm output to avoid CSS formatting
                        ConsoleMessenger.AddPlainMessage(e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // Use plain message for real-time npm output to avoid CSS formatting
                        ConsoleMessenger.AddPlainMessage(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    ConsoleMessenger.AddSuccessMessage("npm install completed successfully.");
                }
                else
                {
                    ConsoleMessenger.AddErrorMessageF("npm install failed with exit code {0}.", process.ExitCode);
                }

                return process.ExitCode == 0;
            }
        }

        public async Task<bool> RunBuildCommand(string workingDirectory)
        {
            try
            {
                ProcessStartInfo processStartInfo;

                // Use npm to run the build script
                _processManager.CheckPaths();

                processStartInfo = new ProcessStartInfo(_processManager.npmPath, "run build")
                {
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                _processManager.PrependNodeBinToPath(processStartInfo);

                ConsoleMessenger.AddInfoMessage("Building with npm: npm run build");

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            // Use plain message for real-time build output to avoid CSS formatting
                            ConsoleMessenger.AddPlainMessage(e.Data);
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            // Use plain message for real-time build output to avoid CSS formatting
                            ConsoleMessenger.AddPlainMessage(e.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        ConsoleMessenger.AddSuccessMessage("Build completed successfully.");
                        return true;
                    }
                    else
                    {
                        ConsoleMessenger.AddErrorMessageF("Build failed with exit code {0}.", process.ExitCode);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddErrorMessageF("Error running build command: {0}", ex.Message);
                return false;
            }
        }
    }
}
using SpooderInstallerSharp.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.ViewModels
{
    public class InstallationManager
    {
        private readonly Action<string> AppendToConsoleOutput;
        private readonly GitOperations _gitOperations;
        private readonly ProcessManager _processManager;
        private readonly FileOperations _fileOperations;
        private readonly Action OnSpooderInstallStart;
        private readonly Action OnSpooderInstallComplete;
        private readonly Action OnSpooderUninstalled;
        private readonly Action OnSpooderCleaned;

        public InstallationManager(Action<string> appendToConsoleOutput, GitOperations gitOperations, 
                                 ProcessManager processManager, FileOperations fileOperations,
                                 Action onSpooderInstallStart, Action onSpooderInstallComplete,
                                 Action onSpooderUninstalled, Action onSpooderCleaned)
        {
            AppendToConsoleOutput = appendToConsoleOutput;
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
            Logger.LogInfo($"Installing Spooder to {scriptPath} on branch {selectedBranch}");
            OnSpooderInstallStart();
            _gitOperations.CloneRepository("https://github.com/GreySole/Spooder.git", scriptPath, branch: selectedBranch);

            _processManager.CheckPaths();

            var processStartInfo = new ProcessStartInfo(_processManager.npmPath, "install")
            {
                WorkingDirectory = scriptPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        AppendToConsoleOutput(e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        AppendToConsoleOutput(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    AppendToConsoleOutput("Dependencies installed successfully.");

                    // After npm install, run the build command if it exists
                    bool buildSuccess = await RunBuildCommand(scriptPath);

                    if (buildSuccess)
                    {
                        AppendToConsoleOutput("Installation and build completed successfully.");
                        OnSpooderInstallComplete();
                        return true;
                    }
                    else
                    {
                        AppendToConsoleOutput("Installation succeeded but build failed. You can still run Spooder in Dev mode.");
                        OnSpooderInstallComplete();
                        return true;
                    }
                }
                else
                {
                    AppendToConsoleOutput("Installation failed.");
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
                    AppendToConsoleOutput("Stopping Spooder process before uninstallation...");
                    _processManager.StopSpooder();
                }

                if (Directory.Exists(appSettings.SpooderInstallationPath))
                {
                    AppendToConsoleOutput($"Removing Spooder installation from {appSettings.SpooderInstallationPath}...");

                    // Wait a moment to ensure all file handles are closed
                    await Task.Delay(1000);

                    // Try smart deletion with permission handling
                    bool success = await _fileOperations.SmartDeleteDirectory(appSettings.SpooderInstallationPath);

                    if (success)
                    {
                        OnSpooderUninstalled();
                        AppendToConsoleOutput("Spooder has been successfully uninstalled.");
                        return true;
                    }
                    else
                    {
                        AppendToConsoleOutput("Failed to completely remove Spooder installation directory.");
                        return false;
                    }
                }
                else
                {
                    AppendToConsoleOutput("Spooder installation directory not found. Nothing to uninstall.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Error uninstalling Spooder: {ex.Message}");
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
                    AppendToConsoleOutput("Stopping Spooder process before cleaning...");
                    _processManager.StopSpooder();
                }

                var userDataPath = Path.Combine(appSettings.SpooderInstallationPath, "user");

                if (Directory.Exists(userDataPath))
                {
                    AppendToConsoleOutput($"Removing Spooder User data from {appSettings.SpooderInstallationPath}...");

                    // Wait a moment to ensure all file handles are closed
                    await Task.Delay(1000);

                    // Try smart deletion with permission handling
                    bool success = await _fileOperations.SmartDeleteDirectory(userDataPath);

                    if (success)
                    {
                        OnSpooderCleaned();
                        AppendToConsoleOutput("Spooder has been successfully cleaned.");
                        return true;
                    }
                    else
                    {
                        AppendToConsoleOutput("Failed to clean Spooder entirely.");
                        return false;
                    }
                }
                else
                {
                    AppendToConsoleOutput("Spooder installation directory not found. Nothing to clean.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Error cleaning Spooder: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RunNpmInstall(string workingDirectory)
        {
            _processManager.CheckPaths();

            var processStartInfo = new ProcessStartInfo(_processManager.npmPath, "install --verbose")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        AppendToConsoleOutput(e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        AppendToConsoleOutput(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

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

                AppendToConsoleOutput($"Building with npm: npm run build");

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            AppendToConsoleOutput(e.Data);
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            AppendToConsoleOutput(e.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        AppendToConsoleOutput("Build completed successfully.");
                        return true;
                    }
                    else
                    {
                        AppendToConsoleOutput($"Build failed with exit code {process.ExitCode}.");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Error running build command: {ex.Message}");
                return false;
            }
        }
    }
}
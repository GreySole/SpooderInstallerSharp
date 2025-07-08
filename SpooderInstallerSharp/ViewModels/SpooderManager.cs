using Avalonia.Threading;
using LibGit2Sharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SpooderInstallerSharp.JsonTypes;
using SpooderInstallerSharp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.ViewModels
{
    public class SpooderManager
    {
        // Define the event  
        public event EventHandler SpooderInstallStart;
        public event EventHandler SpooderInstallComplete;
        public event EventHandler SpooderUninstallComplete;
        public event EventHandler SpooderRunStart;
        public event EventHandler SpooderRunStop;
        public event EventHandler DefaultDirectorySetNeeded;
        public event EventHandler SpooderThemeChanged;

        private IPC _ipc;

        // Add event for receiving IPC messages
        public event EventHandler<string> MessageReceived;

        protected virtual void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, message);
        }

        public void SendMessageToSpooder(string message)
        {
            _ipc?.SendMessageToSpooder(message);
        }

        // Method to raise the event  
        protected virtual void OnSpooderInstallStart()
        {
            SpooderInstallStart?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSpooderInstallComplete()
        {
            SpooderInstallComplete?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSpooderUninstalled()
        {
            SpooderUninstallComplete?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSpooderRunStart()
        {
            SpooderRunStart?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSpooderRunStop()
        {
            SpooderRunStop?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnDefaultDirectorySetNeeded()
        {
            DefaultDirectorySetNeeded?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSpooderThemeChanged()
        {
            SpooderThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        private readonly Action<string> AppendToConsoleOutput;
        public Process spooderProcess;
        public string nodePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nodejs", "node.exe");
        public string npmPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nodejs", "npm.cmd");
        public string mingwPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mingw");
        //public string defaultLocalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spooder");
        public SpooderInfo spooderInfo { get; set; }

        private int spooderProcessId = -1;

        public SpooderManager(Action<string> appendToConsoleOutput)
        {
            Debug.WriteLine($"SpooderManager created");
            AppendToConsoleOutput = appendToConsoleOutput;

            _ipc = new IPC(appendToConsoleOutput);
            _ipc.MessageReceived += (sender, message) => OnMessageReceived(message);

            refreshSpooderInfo();
        }

        public void refreshSpooderInfo()
        {
            var appSettings = SettingsManager.LoadSettings();
            var packageJsonPath = Path.Combine(appSettings.SpooderInstallationPath, "package.json");

            var spooderInstalled = File.Exists(packageJsonPath);

            if (spooderInstalled)
            {
                string packageJsonContent = File.ReadAllText(packageJsonPath);
                JObject packageJson = JObject.Parse(packageJsonContent);
                spooderInfo = new SpooderInfo();
                var spooderVersion = packageJson["version"]?.ToString();
                if (System.Version.TryParse(spooderVersion, out var installedVersion))
                {
                    spooderInfo.version = installedVersion.ToString();
                    if (installedVersion < new System.Version(0, 5, 0))
                    {
                        AppendToConsoleOutput($"Spooder version {installedVersion} is outdated. Please update to the latest version.");
                    }
                    else
                    {
                        var spooderConfigPath = Path.Combine(appSettings.SpooderInstallationPath, "user", "settings", "config.json");
                        var spooderThemePath = Path.Combine(appSettings.SpooderInstallationPath, "user", "settings", "themes.json");
                        if(!File.Exists(spooderConfigPath) || !File.Exists(spooderThemePath))
                        {
                            AppendToConsoleOutput($"Spooder configuration files not found. Please ensure Spooder is properly installed.");
                            return;
                        }
                        var spooderConfig = JObject.Parse(File.ReadAllText(spooderConfigPath));
                        var spooderTheme = JObject.Parse(File.ReadAllText(spooderThemePath));

                        var botNameToken = spooderConfig["bot"]["bot_name"];
                        spooderInfo.name = botNameToken != null ? botNameToken.ToString() : "Unnamed";

                        spooderInfo.themeVariables = new SpooderTheme();

                        var hueToken = spooderTheme["webui"]?["hue"];
                        var satToken = spooderTheme["webui"]?["saturation"];
                        var isDarkToken = spooderTheme["webui"]?["isDarkTheme"];
                        spooderInfo.themeVariables.hue = (float)(hueToken != null ? hueToken.Value<float>() : 0.0);
                        spooderInfo.themeVariables.saturation = (float)(satToken != null ? satToken.Value<float>() : 0.0);

                        spooderInfo.customSpooder = new CustomSpooder();
                        var spooderParts = new CustomSpooderParts();
                        spooderParts.longlegleft = spooderTheme["spooderpet"]?["parts"]?["longlegleft"]?.ToString() ?? "/╲";
                        spooderParts.shortlegleft = spooderTheme["spooderpet"]?["parts"]?["shortlegleft"]?.ToString() ?? "/\\";
                        spooderParts.bodyleft = spooderTheme["spooderpet"]?["parts"]?["bodyleft"]?.ToString() ?? "(";
                        spooderParts.littleeyeleft = spooderTheme["spooderpet"]?["parts"]?["littleeyeleft"]?.ToString() ?? "º";
                        spooderParts.bigeyeleft = spooderTheme["spooderpet"]?["parts"]?["bigeyeleft"]?.ToString() ?? "o";
                        spooderParts.fangleft = spooderTheme["spooderpet"]?["parts"]?["fangleft"]?.ToString() ?? " ";
                        spooderParts.mouth = spooderTheme["spooderpet"]?["parts"]?["mouth"]?.ToString() ?? "ω";
                        spooderParts.fangright = spooderTheme["spooderpet"]?["parts"]?["fangright"]?.ToString() ?? " ";
                        spooderParts.bigeyeright = spooderTheme["spooderpet"]?["parts"]?["bigeyeright"]?.ToString() ?? "o";
                        spooderParts.littleeyeright = spooderTheme["spooderpet"]?["parts"]?["littleeyeright"]?.ToString() ?? "º";
                        spooderParts.bodyright = spooderTheme["spooderpet"]?["parts"]?["bodyright"]?.ToString() ?? ")";
                        spooderParts.shortlegright = spooderTheme["spooderpet"]?["parts"]?["shortlegright"]?.ToString() ?? "/\\";
                        spooderParts.longlegright = spooderTheme["spooderpet"]?["parts"]?["longlegright"]?.ToString() ?? "╱\\";

                        var spooderColors = new CustomSpooderParts();
                        spooderColors.longlegleft = spooderTheme["spooderpet"]?["colors"]?["longlegleft"]?.ToString() ?? "#FFFFFF";
                        spooderColors.shortlegleft = spooderTheme["spooderpet"]?["colors"]?["shortlegleft"]?.ToString() ?? "#FFFFFF";
                        spooderColors.bodyleft = spooderTheme["spooderpet"]?["colors"]?["bodyleft"]?.ToString() ?? "#FFFFFF";
                        spooderColors.littleeyeleft = spooderTheme["spooderpet"]?["colors"]?["littleeyeleft"]?.ToString() ?? "#FFFFFF";
                        spooderColors.bigeyeleft = spooderTheme["spooderpet"]?["colors"]?["bigeyeleft"]?.ToString() ?? "#FFFFFF";
                        spooderColors.fangleft = spooderTheme["spooderpet"]?["colors"]?["fangleft"]?.ToString() ?? "#FFFFFF";
                        spooderColors.mouth = spooderTheme["spooderpet"]?["colors"]?["mouth"]?.ToString() ?? "#FFFFFF";
                        spooderColors.fangright = spooderTheme["spooderpet"]?["colors"]?["fangright"]?.ToString() ?? "#FFFFFF";
                        spooderColors.bigeyeright = spooderTheme["spooderpet"]?["colors"]?["bigeyeright"]?.ToString() ?? "#FFFFFF";
                        spooderColors.littleeyeright = spooderTheme["spooderpet"]?["colors"]?["littleeyeright"]?.ToString() ?? "#FFFFFF";
                        spooderColors.bodyright = spooderTheme["spooderpet"]?["colors"]?["bodyright"]?.ToString() ?? "#FFFFFF";
                        spooderColors.shortlegright = spooderTheme["spooderpet"]?["colors"]?["shortlegright"]?.ToString() ?? "#FFFFFF";
                        spooderColors.longlegright = spooderTheme["spooderpet"]?["colors"]?["longlegright"]?.ToString() ?? "#FFFFFF";


                        spooderInfo.customSpooder.parts = spooderParts;
                        spooderInfo.customSpooder.colors = spooderColors;


                        OnSpooderThemeChanged();
                    }



                    AppendToConsoleOutput($"Spooder is installed at {appSettings.SpooderInstallationPath}");
                }
            }
            else
            {
                AppendToConsoleOutput($"Spooder is not installed. Click the install button on the top right.");
            }
        }

        public SpooderInfo getSpooderInfo()
        {
            return spooderInfo;
        }

        public bool StartSpooder()
        {
            var appSettings = SettingsManager.LoadSettings();
            var scriptPath = appSettings.SpooderInstallationPath;
            CheckPaths();

            // Read the package.json to find the start script
            string packageJsonPath = Path.Combine(scriptPath, "package.json");
            string startScript = "index.js"; // Default fallback

            try
            {
                if (File.Exists(packageJsonPath))
                {
                    string packageJsonContent = File.ReadAllText(packageJsonPath);
                    JObject packageJson = JObject.Parse(packageJsonContent);

                    // Get the start script from package.json
                    var scripts = packageJson["scripts"];
                    if (scripts != null && scripts["start"] != null)
                    {
                        string npmStartCommand = scripts["start"].ToString();

                        // Usually npm start scripts are like "node index.js" or similar
                        // Extract just the JS file name
                        if (npmStartCommand.StartsWith("node "))
                        {
                            startScript = npmStartCommand.Substring(5).Trim();
                        }
                        else
                        {
                            // If it's not a direct node command, use the command as is
                            startScript = npmStartCommand;
                        }
                    }
                }

                AppendToConsoleOutput($"Using start script: {startScript}");
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Error reading package.json: {ex.Message}. Using default start script.");
            }

            // Determine if we need to use node directly or if the script is another command
            bool useNodeDirectly = startScript.EndsWith(".js") || startScript.StartsWith("node ");
            bool useTsx = startScript.StartsWith("tsx ") || startScript.Contains(".ts");

            // Prepare the process start info
            ProcessStartInfo processStartInfo;

            if (useTsx)
            {
                // Handle tsx TypeScript execution
                string tsxExecutable = Path.Combine(scriptPath, "node_modules", ".bin", "tsx.cmd");
                
                // Fallback to global tsx if local not found
                if (!File.Exists(tsxExecutable))
                {
                    tsxExecutable = "tsx"; // Assume global installation
                }

                // Extract the TypeScript file path from the command
                string tsFile = startScript.StartsWith("tsx ") ? startScript.Substring(4).Trim() : startScript;
                
                processStartInfo = new ProcessStartInfo(tsxExecutable, tsFile)
                {
                    WorkingDirectory = scriptPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Add node_modules/.bin to PATH so tsx can find dependencies
                string nodeModulesBin = Path.Combine(scriptPath, "node_modules", ".bin");
                if (Directory.Exists(nodeModulesBin))
                {
                    string currentPath = processStartInfo.EnvironmentVariables.ContainsKey("PATH") 
                        ? processStartInfo.EnvironmentVariables["PATH"] 
                        : Environment.GetEnvironmentVariable("PATH");
                    processStartInfo.EnvironmentVariables["PATH"] = $"{nodeModulesBin};{currentPath}";
                }
                
                AppendToConsoleOutput($"Starting with tsx: {tsxExecutable} {tsFile}");
            }
            else if (useNodeDirectly)
            {
                // Run with node directly for .js files
                string fullScriptPath = Path.Combine(scriptPath, startScript);
                processStartInfo = new ProcessStartInfo(nodePath, fullScriptPath)
                {
                    WorkingDirectory = scriptPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                AppendToConsoleOutput($"Starting Node.js directly: {nodePath} {fullScriptPath}");
            }
            else
            {
                // For more complex commands, we might still need npm
                processStartInfo = new ProcessStartInfo(npmPath, $"run start")
                {
                    WorkingDirectory = scriptPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                AppendToConsoleOutput($"Using npm to run start script: {startScript}");
            }

            // Create and start the process
            spooderProcess = new Process
            {
                StartInfo = processStartInfo,
                EnableRaisingEvents = true
            };

            // Set up event handlers
            spooderProcess.Exited += (sender, e) =>
            {
                AppendToConsoleOutput("Spooder has exited.");
                _ipc.Cleanup();
                OnSpooderRunStop();
            };

            spooderProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    AppendToConsoleOutput(e.Data);
                }
            };

            // Start the process
            spooderProcess.Start();
            _ipc.SetupIpcCommunication(spooderProcess);
            if (useTsx)
            {
                var pipeName = _ipc.GetPipeName();
                if (!string.IsNullOrEmpty(pipeName))
                {
                    // Send the pipe name to the process so it can connect
                    spooderProcess.StandardInput.WriteLine($"SPOODER_IPC_PIPE={pipeName}");
                    spooderProcess.StandardInput.Flush();
                }
            }
            spooderProcess.BeginErrorReadLine();
            spooderProcessId = spooderProcess.Id;
            OnSpooderRunStart();

            return true;
        }

        public bool StopSpooder()
        {
            Debug.WriteLine($"Cleaning up {Environment.NewLine} Process: {spooderProcessId}");
            
            if (spooderProcess == null || spooderProcess.HasExited)
            {
                return false;
            }
            
            try
            {
                // Cancel any ongoing output reading
                spooderProcess.CancelErrorRead();
                Debug.WriteLine("Cancelled output reading");
                
                // Send SIGINT (CTRL+C) signal to Node.js
                spooderProcess.StandardInput.Write("\u0003");
                spooderProcess.StandardInput.Flush();

                spooderProcess.Kill(true);
                
                // Give Node.js a chance to shut down gracefully
                bool exited = spooderProcess.WaitForExit(5000);
                
                Debug.WriteLine($"Process exited: {spooderProcess.HasExited}");
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Error stopping process: {ex.Message}");
            }
            finally
            {
                _ipc?.Cleanup();
                spooderProcess.Dispose();
                spooderProcess = null;
                OnSpooderRunStop();
                Dispatcher.UIThread.Post(() => refreshSpooderInfo());
            }
            
            return true;
        }

        private void CloneRepository(string repoUrl, string localPath, string branch = "main")
        {
            AppendToConsoleOutput($"Cloning Spooder repository on {branch}...");
            var cloneOptions = new CloneOptions
            {
                BranchName = branch,
                OnCheckoutProgress = (path, completedSteps, totalSteps) =>
                {
                    AppendToConsoleOutput($"Checked out {completedSteps} of {totalSteps} steps.");
                }
            };
            try
            {
                Repository.Clone(repoUrl, localPath, cloneOptions);
                AppendToConsoleOutput($"Repository cloned to {localPath}");
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Error cloning repository: {ex.Message}");
            }
        }

        public void CheckPaths()
        {
            if (!File.Exists(nodePath))
            {
                throw new FileNotFoundException("Node.js executable not found.", nodePath);
            }

            if (!File.Exists(npmPath))
            {
                throw new FileNotFoundException("npm script not found.", npmPath);
            }
        }

        public async Task<bool> InstallSpooder()
        {
            var appSettings = SettingsManager.LoadSettings();
            var scriptPath = appSettings.SpooderInstallationPath;
            var selectedBranch = appSettings.SelectedBranch;
            Debug.WriteLine($"Installing Spooder to {scriptPath} on branch {selectedBranch}");
            OnSpooderInstallStart();
            CloneRepository("https://github.com/GreySole/Spooder.git", scriptPath, branch:selectedBranch);

            CheckPaths();

            var processStartInfo = new ProcessStartInfo(npmPath, "install --verbose")
            {
                WorkingDirectory = scriptPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Set environment variables for MinGW
            //processStartInfo.EnvironmentVariables["PATH"] = $"{mingwPath};{processStartInfo.EnvironmentVariables["PATH"]}";

            var output = new StringWriter();
            var error = new StringWriter();

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

                // Analyze the output and error streams to determine success
                string outputResult = output.ToString();
                string errorResult = error.ToString();

                if (process.ExitCode == 0 && string.IsNullOrEmpty(errorResult))
                {
                    AppendToConsoleOutput("Installation successful.");
                    OnSpooderInstallComplete();
                    return true;
                }
                else
                {
                    AppendToConsoleOutput("Installation failed.");
                    AppendToConsoleOutput($"Output: {outputResult}");
                    AppendToConsoleOutput($"Error: {errorResult}");
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
                if (spooderProcess != null && !spooderProcess.HasExited)
                {
                    AppendToConsoleOutput("Stopping Spooder process before uninstallation...");
                    StopSpooder();
                }

                if (Directory.Exists(appSettings.SpooderInstallationPath))
                {
                    AppendToConsoleOutput($"Removing Spooder installation from {appSettings.SpooderInstallationPath}...");

                    // Wait a moment to ensure all file handles are closed
                    await Task.Delay(1000);

                    // Try smart deletion with permission handling
                    bool success = await SmartDeleteDirectory(appSettings.SpooderInstallationPath);

                    if (success)
                    {
                        spooderInfo = null;
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

        public async Task<bool> UpdateSpooder(string targetBranch = null)
        {
            var appSettings = SettingsManager.LoadSettings();
            var spooderPath = appSettings.SpooderInstallationPath;

            if (!Directory.Exists(spooderPath))
            {
                AppendToConsoleOutput("Spooder installation not found. Please install first.");
                return false;
            }

            // Stop Spooder if it's running
            if (spooderProcess != null && !spooderProcess.HasExited)
            {
                AppendToConsoleOutput("Stopping Spooder before update...");
                StopSpooder();
                await Task.Delay(2000); // Wait for clean shutdown
            }

            try
            {
                using (var repo = new Repository(spooderPath))
                {
                    AppendToConsoleOutput("Updating Spooder repository...");

                    // Ensure user folder is properly ignored
                    await EnsureUserFolderIgnored(spooderPath);

                    // Stash any local changes (including untracked files in user folder)
                    AppendToConsoleOutput("Stashing local changes...");
                    var signature = new Signature("SpooderInstaller", "installer@spooder.local", DateTimeOffset.Now);

                    Stash stashResult = null;
                    try
                    {
                        stashResult = repo.Stashes.Add(signature, "Auto-stash before update", StashModifiers.IncludeUntracked);
                        if (stashResult != null)
                        {
                            AppendToConsoleOutput("Local changes stashed successfully.");
                        }
                    }
                    catch (LibGit2SharpException ex) when (ex.Message.Contains("no changes"))
                    {
                        AppendToConsoleOutput("No local changes to stash.");
                    }

                    // Fetch latest changes from remote
                    var remote = repo.Network.Remotes["origin"];
                    var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);

                    AppendToConsoleOutput("Fetching latest changes...");
                    Commands.Fetch(repo, remote.Name, refSpecs, null, "Fetching updates");

                    // Determine target branch
                    string branchToUpdate = targetBranch ?? appSettings.SelectedBranch ?? "main";

                    // Check if we need to switch branches
                    var currentBranch = repo.Head.FriendlyName;
                    if (currentBranch != branchToUpdate)
                    {
                        AppendToConsoleOutput($"Switching from branch '{currentBranch}' to '{branchToUpdate}'");

                        // Try to find the branch locally first
                        var localBranch = repo.Branches[branchToUpdate];
                        if (localBranch == null)
                        {
                            // Create local branch tracking remote
                            var remoteBranchToTrack = repo.Branches[$"origin/{branchToUpdate}"];
                            if (remoteBranchToTrack == null)
                            {
                                AppendToConsoleOutput($"Branch '{branchToUpdate}' not found on remote.");
                                return false;
                            }

                            localBranch = repo.CreateBranch(branchToUpdate, remoteBranchToTrack.Tip);
                            repo.Branches.Update(localBranch, b => b.TrackedBranch = remoteBranchToTrack.CanonicalName);
                        }

                        // Checkout the target branch
                        var checkoutOptions = new CheckoutOptions()
                        {
                            CheckoutModifiers = CheckoutModifiers.Force // Force checkout to avoid conflicts
                        };
                        Commands.Checkout(repo, localBranch, checkoutOptions);
                    }

                    // Reset to latest remote commit (hard reset)
                    var remoteBranchName = $"origin/{branchToUpdate}";
                    var remoteBranch = repo.Branches[remoteBranchName];
                    if (remoteBranch != null)
                    {
                        AppendToConsoleOutput($"Resetting to latest {remoteBranchName}...");
                        repo.Reset(ResetMode.Hard, remoteBranch.Tip);
                        AppendToConsoleOutput("Repository updated successfully.");
                    }
                    else
                    {
                        AppendToConsoleOutput($"Remote branch {remoteBranchName} not found.");
                        return false;
                    }

                    // Restore stashed changes (this will restore user folder contents)
                    if (stashResult != null)
                    {
                        try
                        {
                            AppendToConsoleOutput("Restoring local changes...");
                            repo.Stashes.Pop(0, new StashApplyOptions()
                            {
                                ApplyModifiers = StashApplyModifiers.ReinstateIndex
                            });
                            AppendToConsoleOutput("Local changes restored successfully.");
                        }
                        catch (Exception ex)
                        {
                            AppendToConsoleOutput($"Warning: Could not restore all local changes: {ex.Message}");
                            AppendToConsoleOutput("User folder should still be intact due to .gitignore.");
                        }
                    }

                    // Update the selected branch in settings if we switched
                    if (targetBranch != null && targetBranch != appSettings.SelectedBranch)
                    {
                        appSettings.SelectedBranch = targetBranch;
                        SettingsManager.SaveSettings(appSettings);
                    }

                    // Run npm install to update dependencies
                    AppendToConsoleOutput("Updating dependencies...");
                    await RunNpmInstall(spooderPath);

                    AppendToConsoleOutput("Spooder update completed successfully!");

                    // Refresh spooder info to reflect changes
                    refreshSpooderInfo();

                    return true;
                }
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Error updating Spooder: {ex.Message}");
                return false;
            }
        }

        private async Task EnsureUserFolderIgnored(string repoPath)
        {
            var gitignorePath = Path.Combine(repoPath, ".gitignore");

            try
            {
                // Check if .gitignore exists and contains user folder entry
                var gitignoreContent = new List<string>();

                if (File.Exists(gitignorePath))
                {
                    gitignoreContent.AddRange(await File.ReadAllLinesAsync(gitignorePath));
                }

                // Check if user folder is already ignored
                bool userFolderIgnored = gitignoreContent.Any(line =>
                    line.Trim().Equals("user/", StringComparison.OrdinalIgnoreCase) ||
                    line.Trim().Equals("user", StringComparison.OrdinalIgnoreCase) ||
                    line.Trim().Equals("/user/", StringComparison.OrdinalIgnoreCase) ||
                    line.Trim().Equals("/user", StringComparison.OrdinalIgnoreCase));

                if (!userFolderIgnored)
                {
                    AppendToConsoleOutput("Adding user folder to .gitignore...");
                    gitignoreContent.Add("");
                    gitignoreContent.Add("# User configuration and data");
                    gitignoreContent.Add("user/");

                    await File.WriteAllLinesAsync(gitignorePath, gitignoreContent);
                    AppendToConsoleOutput("User folder added to .gitignore.");
                }
                else
                {
                    AppendToConsoleOutput("User folder is already in .gitignore.");
                }
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Warning: Could not update .gitignore: {ex.Message}");
            }
        }

        private async Task<bool> RunNpmInstall(string workingDirectory)
        {
            CheckPaths();

            var processStartInfo = new ProcessStartInfo(npmPath, "install --verbose")
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

        private async Task<bool> SmartDeleteDirectory(string path)
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

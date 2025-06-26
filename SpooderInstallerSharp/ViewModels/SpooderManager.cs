using LibGit2Sharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SpooderInstallerSharp.JsonTypes;
using Newtonsoft.Json.Linq;
using SpooderInstallerSharp.Models;

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

        private readonly Action<string> AppendToConsoleOutput;
        public Process spooderProcess;
        public string nodePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nodejs", "node.exe");
        public string npmPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nodejs", "npm.cmd");
        public string mingwPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mingw");
        public string defaultLocalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spooder");
        public SpooderInfo spooderInfo { get; set; }

        private int spooderProcessId = -1;

        public SpooderManager(Action<string> appendToConsoleOutput)
        {
            Debug.WriteLine($"SpooderManager created");
            AppendToConsoleOutput = appendToConsoleOutput;

            refreshSpooderInfo();
        }

        public void refreshSpooderInfo()
        {
            var packageJsonPath = Path.Combine(defaultLocalPath, "package.json");

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
                        var spooderConfigPath = Path.Combine(defaultLocalPath, "user", "settings", "config.json");
                        var spooderThemePath = Path.Combine(defaultLocalPath, "user", "settings", "themes.json");
                        var spooderConfig = JObject.Parse(File.ReadAllText(spooderConfigPath));
                        var spooderTheme = JObject.Parse(File.ReadAllText(spooderThemePath));

                        var botNameToken = spooderConfig["bot_name"];
                        //spooderInfo.name = botNameToken != null ? botNameToken.ToString() : "Unnamed";

                        var hueToken = spooderTheme["webui"]?["hue"];
                        var satToken = spooderTheme["webui"]?["saturation"];
                        var isDarkToken = spooderTheme["webui"]?["isDarkTheme"];
                        //spooderInfo.themeVariables.hue = (float)(hueToken != null ? hueToken.Value<float>() : 0.0);
                        //spooderInfo.themeVariables.saturation = (float)(satToken != null ? satToken.Value<float>() : 0.0);
                    }



                    AppendToConsoleOutput($"Spooder is installed at {defaultLocalPath}");
                }
                else
                {
                    AppendToConsoleOutput($"Spooder is not installed. Please install it first.");
                }
            }
        }

        public SpooderInfo getSpooderInfo()
        {
            return spooderInfo;
        }

        public bool StartSpooder()
        {
            var appSettings = SettingsManager.LoadSettings();
            var scriptPath = appSettings.DefaultSpooderPath;
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
                //spooderRunning = false;
                OnSpooderRunStop();
            };

            spooderProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    AppendToConsoleOutput(e.Data);
                }
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
            spooderProcess.BeginOutputReadLine();
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
                spooderProcess.CancelOutputRead();
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
                spooderProcess.Dispose();
                spooderProcess = null;
                OnSpooderRunStop();
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
            var scriptPath = appSettings.DefaultSpooderPath;
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
                // First, stop any running Spooder process
                if (spooderProcess != null && !spooderProcess.HasExited)
                {
                    AppendToConsoleOutput("Stopping Spooder process before uninstallation...");
                    StopSpooder();
                }

                if (Directory.Exists(defaultLocalPath))
                {
                    AppendToConsoleOutput($"Removing Spooder installation from {defaultLocalPath}...");

                    // Wait a moment to ensure all file handles are closed
                    await Task.Delay(1000);

                    // Delete all contents recursively
                    DirectoryInfo directory = new DirectoryInfo(defaultLocalPath);

                    // Set all files to non read-only to avoid permission issues
                    foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
                    {
                        file.Attributes = FileAttributes.Normal;
                    }

                    // Delete all contents
                    Directory.Delete(defaultLocalPath, true);

                    spooderInfo = null;

                    OnSpooderUninstalled();

                    AppendToConsoleOutput("Spooder has been successfully uninstalled.");
                    return true;
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
    }
}

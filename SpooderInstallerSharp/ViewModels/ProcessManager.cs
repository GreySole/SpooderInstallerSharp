using Avalonia.Threading;
using Newtonsoft.Json.Linq;
using SpooderInstallerSharp.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SpooderInstallerSharp.ViewModels
{
    public class ProcessManager
    {
        private readonly Action<string> AppendToConsoleOutput;
        private readonly IPC _ipc;
        private readonly Action OnSpooderRunStart;
        private readonly Action OnSpooderRunStop;
        private readonly Action RefreshSpooderInfo;

        public Process? spooderProcess;
        private int spooderProcessId = -1;

        static readonly string? exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        public string nodePath = Path.Combine(exeDir ?? "", "nodejs", "node.exe");
        public string npmPath = Path.Combine(exeDir ?? "", "nodejs", "npm.cmd");

        public ProcessManager(Action<string> appendToConsoleOutput, IPC ipc, 
                            Action onSpooderRunStart, Action onSpooderRunStop, Action refreshSpooderInfo)
        {
            AppendToConsoleOutput = appendToConsoleOutput;
            _ipc = ipc;
            OnSpooderRunStart = onSpooderRunStart;
            OnSpooderRunStop = onSpooderRunStop;
            RefreshSpooderInfo = refreshSpooderInfo;
        }

        public void CheckPaths()
        {
            var baseDir = Path.Combine(exeDir ?? "", "nodejs");
            if (File.Exists(Path.Combine(baseDir, "node.exe")))
            {
                nodePath = Path.Combine(baseDir, "node.exe");
                npmPath = Path.Combine(baseDir, "npm.cmd");
                return;
            }
            try
            {
                var nodeVersionDirs = Directory.GetDirectories(baseDir);
                if (nodeVersionDirs.Length > 0)
                {
                    AppendToConsoleOutput($"Path: {nodeVersionDirs[0]}");
                    nodePath = Path.Combine(nodeVersionDirs[0], "node.exe");
                    npmPath = Path.Combine(nodeVersionDirs[0], "npm.cmd");
                }
                if (!File.Exists(nodePath))
                {
                    AppendToConsoleOutput("Node.js executable not found.");
                }

                if (!File.Exists(npmPath))
                {
                    AppendToConsoleOutput("npm script not found.");
                }
            }
            catch(Exception ex)
            {
                AppendToConsoleOutput($"Error checking paths: {ex.Message}");
            }
            
        }

        public bool StartSpooder()
        {
            AppendToConsoleOutput($"Attempting to start Spooder...");
            var appSettings = SettingsManager.LoadSettings();
            var scriptPath = appSettings.SpooderInstallationPath;
            CheckPaths();

            // Read the package.json to find the start script
            string packageJsonPath = Path.Combine(scriptPath, "package.json");
            string startScript = "index.js"; // Default fallback
            string nodeArgs = ""; // Store any additional node arguments

            try
            {
                if (File.Exists(packageJsonPath))
                {
                    string packageJsonContent = File.ReadAllText(packageJsonPath);
                    JObject packageJson = JObject.Parse(packageJsonContent);

                    // Get the scripts from package.json
                    var scripts = packageJson["scripts"];
                    if (scripts != null)
                    {
                        // First check for start-build command, then fallback to start
                        string? npmStartCommand = null;

                        if (appSettings.SelectedMode == "Normal")
                        {
                            if (scripts["start"] != null)
                            {
                                npmStartCommand = scripts["start"]?.ToString();
                                AppendToConsoleOutput("Found start script, using it for startup.");
                            }
                            else
                            {
                                AppendToConsoleOutput("No start script found! Aborting...");
                                return false;
                            }
                        }
                        else if (appSettings.SelectedMode == "Dev")
                        {
                            if (scripts["dev"] != null)
                            {
                                npmStartCommand = scripts["dev"]?.ToString();
                                AppendToConsoleOutput("Using start script for startup.");
                            }
                            else
                            {
                                AppendToConsoleOutput("No dev script found! Aborting...");
                                return false;
                            }
                        }else if(appSettings.SelectedMode == "Safe")
                        {
                            if (scripts["safe"] != null)
                            {
                                npmStartCommand = scripts["safe"]?.ToString();
                                AppendToConsoleOutput("Using safe script for startup.");
                            }
                            else
                            {
                                AppendToConsoleOutput("No safe script found! Aborting...");
                                return false;
                            }
                        }
                        else if (appSettings.SelectedMode == "Init")
                        {
                            if (scripts["init"] != null)
                            {
                                npmStartCommand = scripts["init"]?.ToString();
                                AppendToConsoleOutput("Using init script for startup.");
                            }
                            else
                            {
                                AppendToConsoleOutput("No init script found! Aborting...");
                                return false;
                            }
                        }

                        if (!string.IsNullOrEmpty(npmStartCommand))
                        {
                            // Parse the command to extract node arguments and script file
                            if (npmStartCommand.StartsWith("node "))
                            {
                                var commandParts = npmStartCommand.Substring(5).Trim();
                                var parts = commandParts.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                                if (parts.Length > 0)
                                {
                                    // Find the script file (first argument that doesn't start with -)
                                    var scriptIndex = -1;
                                    for (int i = 0; i < parts.Length; i++)
                                    {
                                        if (!parts[i].StartsWith("-"))
                                        {
                                            scriptIndex = i;
                                            break;
                                        }
                                    }

                                    if (scriptIndex >= 0)
                                    {
                                        // Extract node arguments (everything before the script)
                                        if (scriptIndex > 0)
                                        {
                                            nodeArgs = string.Join(" ", parts.Take(scriptIndex));
                                        }

                                        // Extract script file and any script arguments
                                        startScript = string.Join(" ", parts.Skip(scriptIndex));
                                    }
                                    else
                                    {
                                        // No script file found, treat everything as arguments
                                        nodeArgs = commandParts;
                                        startScript = "index.js"; // fallback
                                    }
                                }
                            }
                            else
                            {
                                // If it's not a direct node command, use the command as is
                                startScript = npmStartCommand;
                            }
                        }
                    }
                }

                AppendToConsoleOutput($"Using start script: {startScript}");
                if (!string.IsNullOrEmpty(nodeArgs))
                {
                    AppendToConsoleOutput($"Using node arguments: {nodeArgs}");
                }
            }
            catch (Exception ex)
            {
                AppendToConsoleOutput($"Error reading package.json: {ex.Message}. Using default start script.");
            }

            // Determine if we need to use node directly or if the script is another command
            bool useNodeDirectly = startScript.EndsWith(".js") || startScript.StartsWith("node ") || !string.IsNullOrEmpty(nodeArgs);
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
                    string? currentPath = processStartInfo.EnvironmentVariables.ContainsKey("PATH")
                        ? processStartInfo.EnvironmentVariables["PATH"]
                        : Environment.GetEnvironmentVariable("PATH");
                    processStartInfo.EnvironmentVariables["PATH"] = $"{nodeModulesBin};{currentPath}";
                }

                AppendToConsoleOutput($"Starting with tsx: {tsxExecutable} {tsFile}");
            }
            else if (useNodeDirectly)
            {
                // Run with node directly for .js files
                // Combine node arguments with script path
                string arguments = "";
                if (!string.IsNullOrEmpty(nodeArgs))
                {
                    arguments = $"{nodeArgs} ";
                }

                // If startScript contains spaces (script + args), use it as is
                // Otherwise, build the full path to the script
                if (startScript.Contains(" ") || Path.IsPathRooted(startScript) || startScript.Contains("/") || startScript.Contains("\\"))
                {
                    arguments += startScript;
                }
                else
                {
                    string fullScriptPath = Path.Combine(scriptPath, startScript);
                    arguments += $"\"{fullScriptPath}\"";
                }

                processStartInfo = new ProcessStartInfo(nodePath, arguments)
                {
                    WorkingDirectory = scriptPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                AppendToConsoleOutput($"Starting Node.js directly: {nodePath} {arguments}");
            }
            else
            {
                // For more complex commands, use npm to run the appropriate script
                string scriptName = "start";
                if (!string.IsNullOrEmpty(nodeArgs) || startScript != "index.js")
                {
                    // Check if we found start-build earlier
                    try
                    {
                        if (File.Exists(packageJsonPath))
                        {
                            string packageJsonContent = File.ReadAllText(packageJsonPath);
                            JObject packageJson = JObject.Parse(packageJsonContent);
                            var scripts = packageJson["scripts"];
                            if (scripts != null && scripts["start-build"] != null)
                            {
                                scriptName = "start-build";
                            }
                        }
                    }
                    catch
                    {
                        // Ignore errors, use default
                    }
                }

                processStartInfo = new ProcessStartInfo(npmPath, $"run {scriptName}")
                {
                    WorkingDirectory = scriptPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                AppendToConsoleOutput($"Using npm to run {scriptName} script: {startScript}");
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
            var pipeName = _ipc.GetPipeName();
            if (!string.IsNullOrEmpty(pipeName))
            {
                // Send the pipe name to the process so it can connect
                spooderProcess.StandardInput.WriteLine($"SPOODER_IPC_PIPE={pipeName}");
                spooderProcess.StandardInput.Flush();
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
                spooderProcess?.Dispose();
                spooderProcess = null;
                OnSpooderRunStop();
                Dispatcher.UIThread.Post(() => RefreshSpooderInfo());
            }
            
            return true;
        }
    }
}
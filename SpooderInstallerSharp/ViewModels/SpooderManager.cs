using LibGit2Sharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.ViewModels
{
    public class SpooderManager
    {
        // Define the event  
        public event EventHandler SpooderInstallStart;
        public event EventHandler SpooderInstallComplete;
        public event EventHandler SpooderRunStart;
        public event EventHandler SpooderRunStop;

        // Method to raise the event  
        protected virtual void OnSpooderInstallStart()
        {
            SpooderInstallStart?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSpooderInstallComplete()
        {
            SpooderInstallComplete?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSpooderRunStart()
        {
            SpooderRunStart?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSpooderRunStop()
        {
            SpooderRunStop?.Invoke(this, EventArgs.Empty);
        }

        private readonly Action<string> AppendToConsoleOutput;
        public Process spooderProcess;
        public string nodePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nodejs", "node.exe");
        public string npmPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nodejs", "npm.cmd");
        public string mingwPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mingw");
        public string defaultLocalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spooder");

        public bool spooderInstalled = false;
        public bool spooderRunning = false;

        private int spooderProcessId = -1;

        public SpooderManager(Action<string> appendToConsoleOutput)
        {
            Debug.WriteLine($"SpooderManager created");
            AppendToConsoleOutput = appendToConsoleOutput;

            spooderInstalled = File.Exists(Path.Combine(defaultLocalPath, "package.json"));
        }

        public void KillProcessAndChildren(int pid)
        {
            if (pid == -1)
            {
                return;
            }

            ManagementObjectSearcher searcher = new($"Select * From Win32_Process Where ParentProcessID={pid}");
            ManagementObjectCollection moc = searcher.Get();

            foreach (ManagementObject mo in moc)
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }

            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
        }
        public bool Cleanup()
        {
            Debug.WriteLine($"Cleaning up {Environment.NewLine} Process: {spooderProcessId}");

            if (spooderProcess == null)
            {
                KillProcessAndChildren(spooderProcessId);
                return false;
            }

            if (!spooderProcess.HasExited)
            {
                spooderProcess.CancelOutputRead();
                spooderProcess.CancelErrorRead();

                Debug.WriteLine($"Cancelled {Environment.NewLine}");

                KillProcessAndChildren(spooderProcessId);

                Debug.WriteLine($"Killed {Environment.NewLine}");
                spooderProcess.WaitForExit();
                Debug.WriteLine($"Exited {Environment.NewLine} Exited?: {spooderProcess.HasExited} {spooderProcess.ExitCode}");
                spooderProcess.Dispose();
                Debug.WriteLine($"Disposed {Environment.NewLine}");
                spooderProcess = null;

            }

            return false;
        }
        public void CloneRepository(string repoUrl, string localPath, string branch = "main")
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

            /*if (!Directory.Exists(mingwPath))
            {
                throw new DirectoryNotFoundException("MinGW bin directory not found.");
            }*/
        }

        public async Task<bool> InstallSpooderNode(string scriptPath)
        {
            OnSpooderInstallStart();
            CloneRepository("https://github.com/GreySole/Spooder.git", defaultLocalPath);

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

        public bool RunSpooderNode(string scriptPath)
        {
            CheckPaths();

            AppendToConsoleOutput($"Starting Spooder from {scriptPath}");

            var processStartInfo = new ProcessStartInfo(npmPath, "run start")
            {
                WorkingDirectory = scriptPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            // Set environment variables for MinGW
            //processStartInfo.EnvironmentVariables["PATH"] = $"{mingwPath};{processStartInfo.EnvironmentVariables["PATH"]}";

            spooderProcess = new Process
            {
                StartInfo = processStartInfo,
                EnableRaisingEvents = true
            };

            spooderProcess.Exited += (sender, e) =>
            {
                AppendToConsoleOutput("Spooder has exited.");

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

            spooderProcess.Start();
            spooderProcess.BeginOutputReadLine();
            spooderProcess.BeginErrorReadLine();
            spooderProcessId = spooderProcess.Id;
            //spooderRunning = true;

            return true;
        }
    }
}

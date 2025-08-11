using Avalonia.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SpooderInstallerSharp.JsonTypes;
using SpooderInstallerSharp.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.ViewModels
{
    public class SpooderManager
    {
        // Define the event  
        public event EventHandler? SpooderInstallStart;
        public event EventHandler? SpooderInstallComplete;
        public event EventHandler? SpooderUninstallComplete;
        public event EventHandler? SpooderCleanComplete;
        public event EventHandler? SpooderRunStart;
        public event EventHandler? SpooderRunStop;
        public event EventHandler? DefaultDirectorySetNeeded;
        public event EventHandler? SpooderThemeChanged;
        public event EventHandler<UpdateAvailableEventArgs>? UpdateAvailable;

        // Add event for receiving IPC messages
        public event EventHandler<string>? MessageReceived;

        // Component dependencies
        private readonly IPC _ipc;
        private readonly GitOperations _gitOperations;
        private readonly ProcessManager _processManager;
        private readonly InstallationManager _installationManager;
        private readonly FileOperations _fileOperations;
        
        public SpooderInfo? spooderInfo { get; set; }

        public SpooderManager()
        {
            Debug.WriteLine($"SpooderManager created");

            // Initialize components - no longer need AppendToConsoleOutput
            _ipc = new IPC();
            _ipc.MessageReceived += (sender, message) => OnMessageReceived(message);

            _gitOperations = new GitOperations();
            _fileOperations = new FileOperations();
            _processManager = new ProcessManager(_ipc, OnSpooderRunStart, OnSpooderRunStop, refreshSpooderInfo);
            _installationManager = new InstallationManager(_gitOperations, _processManager, _fileOperations,
                                                         OnSpooderInstallStart, OnSpooderInstallComplete, 
                                                         OnSpooderUninstalled, OnSpooderCleaned);

            // Perform initialization checks
            PerformInitialChecks();
        }

        private void PerformInitialChecks()
        {
            _processManager.CheckPaths();

            var nodeExists = File.Exists(_processManager.nodePath);
            var npmExists = File.Exists(_processManager.npmPath);

            ConsoleMessenger.AddInfoMessageIf(nodeExists, "Node.js found: OK");
            ConsoleMessenger.AddWarningMessageIf(!nodeExists, "Node.js: NOT FOUND");
            ConsoleMessenger.AddInfoMessageIf(npmExists, "NPM found: OK");
            ConsoleMessenger.AddWarningMessageIf(!npmExists, "NPM: NOT FOUND");

            refreshSpooderInfo();
        }

        /// <summary>
        /// Semantic message methods for SpooderManager - using static ConsoleMessenger directly
        /// </summary>
        public void AddErrorMessage(string message) => ConsoleMessenger.AddErrorMessage(message);
        public void AddWarningMessage(string message) => ConsoleMessenger.AddWarningMessage(message);
        public void AddSuccessMessage(string message) => ConsoleMessenger.AddSuccessMessage(message);
        public void AddInfoMessage(string message) => ConsoleMessenger.AddInfoMessage(message);
        public void AddDebugMessage(string message) => ConsoleMessenger.AddDebugMessage(message);

        protected virtual void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, message);
        }

        public void SendMessageToSpooder(string message)
        {
            _ipc?.SendMessageToSpooder(message);
        }

        // Method to raise the events  
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
            spooderInfo = null;
            SpooderUninstallComplete?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSpooderCleaned()
        {
            spooderInfo = null;
            SpooderCleanComplete?.Invoke(this, EventArgs.Empty);
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

        protected virtual void OnUpdateAvailable(string currentVersion, string newVersion, string branch)
        {
            UpdateAvailable?.Invoke(this, new UpdateAvailableEventArgs(currentVersion, newVersion, branch));
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

                    // Only check for updates if auto-check is enabled
                    if (appSettings.AutoCheckUpdates)
                    {
                        _ = CheckRemoteVersionAsync(appSettings.SelectedBranch, installedVersion);
                    }

                    if (installedVersion < new System.Version(0, 5, 0))
                    {
                        ConsoleMessenger.AddWarningMessageF("Don't use the legacy {0} version of Spooder. Switch to one of the 0.5.x branches!", installedVersion);
                    }
                    else
                    {
                        var spooderConfigPath = Path.Combine(appSettings.SpooderInstallationPath, "user", "settings", "config.json");
                        var spooderThemePath = Path.Combine(appSettings.SpooderInstallationPath, "user", "settings", "themes.json");
                        if(!File.Exists(spooderConfigPath) || !File.Exists(spooderThemePath))
                        {
                            ConsoleMessenger.AddErrorMessage("Spooder configuration files not found. Please ensure Spooder is properly installed.");
                            return;
                        }
                        var spooderConfig = JObject.Parse(File.ReadAllText(spooderConfigPath));
                        var spooderTheme = JObject.Parse(File.ReadAllText(spooderThemePath));

                        var hostPortToken = spooderConfig["network"]?["host_port"];
                        spooderInfo.host_port = hostPortToken != null ? hostPortToken.Value<int>() : 3000;

                        var botNameToken = spooderConfig["bot"]?["bot_name"];
                        spooderInfo.name = botNameToken != null ? botNameToken.ToString() : "Unnamed";

                        spooderInfo.themeVariables = new SpooderTheme();

                        var hueToken = spooderTheme["webui"]?["hue"];
                        var satToken = spooderTheme["webui"]?["saturation"];
                        var monoSpaceToken = spooderTheme["webui"]?["isMonospacedFont"];
                        var fontWeightToken = spooderTheme["webui"]?["fontWeight"];
                        var letterSpacingToken = spooderTheme["webui"]?["letterSpacing"];
                        spooderInfo.themeVariables.hue = (float)(hueToken != null ? hueToken.Value<float>() : 0.0);
                        spooderInfo.themeVariables.saturation = (float)(satToken != null ? satToken.Value<float>() : 0.0);
                        spooderInfo.themeVariables.isDarkTheme = spooderTheme["webui"]?["isDarkTheme"]?.Value<bool>() ?? true;
                        spooderInfo.themeVariables.isMonospacedFont = monoSpaceToken != null ? monoSpaceToken.Value<bool>() : false;
                        spooderInfo.themeVariables.fontWeight = fontWeightToken != null ? fontWeightToken.Value<int>() : 500;
                        spooderInfo.themeVariables.letterSpacing = letterSpacingToken != null ? letterSpacingToken.Value<int>() : 0;

                        spooderInfo.customSpooder = new CustomSpooder();
                        
                        // Check if spooderpet exists and is an array
                        var spooderPetToken = spooderTheme["spooderpet"];
                        if (spooderPetToken != null && spooderPetToken.Type == JTokenType.Array)
                        {
                            ConsoleMessenger.AddSuccessMessage("Spooder pet found, loading custom parts...");
                            var spooderPetArray = (JArray)spooderPetToken;
                            
                            // Iterate through the array of spooder part objects
                            foreach (var partToken in spooderPetArray)
                            {
                                if (partToken.Type == JTokenType.Object)
                                {
                                    var partObject = (JObject)partToken;
                                    var partString = partObject["partString"]?.ToString() ?? "";
                                    var partColor = partObject["partColor"]?.ToString() ?? "#FFFFFF";
                                    
                                    spooderInfo.customSpooder.Parts.Add(new SpooderPart
                                    {
                                        partString = partString,
                                        partColor = partColor
                                    });
                                }
                            }
                        }
                        else
                        {
                            ConsoleMessenger.AddWarningMessage("Spooder pet not found or not an array, using default parts.");
                            // Fallback: If spooderpet is not an array, create default parts
                            var defaultParts = new[]
                            {
                                new { partString = "/╲", partColor = "#FFFFFF" },
                                new { partString = "/\\", partColor = "#FFFFFF" },
                                new { partString = "(", partColor = "#FFFFFF" },
                                new { partString = "º", partColor = "#FFFFFF" },
                                new { partString = "o", partColor = "#FFFFFF" },
                                new { partString = " ", partColor = "#FFFFFF" },
                                new { partString = "ω", partColor = "#FFFFFF" },
                                new { partString = " ", partColor = "#FFFFFF" },
                                new { partString = "o", partColor = "#FFFFFF" },
                                new { partString = "º", partColor = "#FFFFFF" },
                                new { partString = ")", partColor = "#FFFFFF" },
                                new { partString = "/\\", partColor = "#FFFFFF" },
                                new { partString = "╱\\", partColor = "#FFFFFF" }
                            };
                            
                            foreach (var defaultPart in defaultParts)
                            {
                                spooderInfo.customSpooder.Parts.Add(new SpooderPart
                                {
                                    partString = defaultPart.partString,
                                    partColor = defaultPart.partColor
                                });
                            }
                        }

                        OnSpooderThemeChanged();
                    }

                    ConsoleMessenger.AddSuccessMessageF("Spooder is installed at {0}", appSettings.SpooderInstallationPath);
                }
            }
            else
            {
                ConsoleMessenger.AddWarningMessage("Spooder is not installed. Click the install button on the top right.");
            }
        }

        private async Task CheckRemoteVersionAsync(string? branch, System.Version installedVersion)
        {
            try
            {
                ConsoleMessenger.AddInfoMessageF("Checking remote Spooder version on branch '{0}'...", branch);

                // Download package.json directly from GitHub's raw content API
                string packageJsonUrl = $"https://raw.githubusercontent.com/GreySole/Spooder/{branch}/package.json";

                using (var httpClient = new HttpClient())
                {
                    // Set a reasonable timeout
                    httpClient.Timeout = TimeSpan.FromSeconds(10);

                    var response = await httpClient.GetAsync(packageJsonUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string remotePackageContent = await response.Content.ReadAsStringAsync();
                        JObject remotePackageJson = JObject.Parse(remotePackageContent);
                        var remoteVersionString = remotePackageJson["version"]?.ToString();

                        if (System.Version.TryParse(remoteVersionString, out var remoteVersion))
                        {
                            if (remoteVersion > installedVersion)
                            {
                                ConsoleMessenger.AddWarningMessageF("Update available: Remote version {0} is newer than installed version {1}", remoteVersion, installedVersion);
                                OnUpdateAvailable(installedVersion.ToString(), remoteVersion.ToString(), branch ?? "unknown");
                            }
                            else if (remoteVersion == installedVersion)
                            {
                                ConsoleMessenger.AddSuccessMessageF("You have the latest Spooder version ({0}) from branch '{1}'", installedVersion, branch);
                            }
                            else
                            {
                                ConsoleMessenger.AddInfoMessageF("Your Spooder version ({0}) is newer than remote version ({1}) on branch '{2}'", installedVersion, remoteVersion, branch);
                            }
                        }
                        else
                        {
                            ConsoleMessenger.AddErrorMessageF("Could not parse remote Spooder version: {0}", remoteVersionString);
                        }
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        ConsoleMessenger.AddErrorMessageF("Branch '{0}' not found or package.json doesn't exist on this branch", branch);
                    }
                    else
                    {
                        ConsoleMessenger.AddErrorMessageF("Failed to fetch remote package.json: {0}", response.StatusCode);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                ConsoleMessenger.AddErrorMessageF("Network error checking remote version: {0}", ex.Message);
            }
            catch (TaskCanceledException)
            {
                ConsoleMessenger.AddWarningMessage("Request timed out while checking remote version");
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddErrorMessageF("Error checking remote version: {0}", ex.Message);
            }
        }

        public SpooderInfo? getSpooderInfo()
        {
            return spooderInfo;
        }

        // Delegate to component methods
        public bool StartSpooder() => _processManager.StartSpooder();
        public bool StopSpooder() => _processManager.StopSpooder();
        public void CheckPaths() => _processManager.CheckPaths();
        public async Task<bool> InstallSpooder() => await _installationManager.InstallSpooder();
        public async Task<bool> UninstallSpooder() => await _installationManager.UninstallSpooder();
        public async Task<bool> CleanSpooder() => await _installationManager.CleanSpooder();

        public async Task<bool> UpdateSpooder(string? targetBranch = null)
        {
            var appSettings = SettingsManager.LoadSettings();
            var spooderPath = appSettings.SpooderInstallationPath;

            if (!Directory.Exists(spooderPath))
            {
                ConsoleMessenger.AddErrorMessage("Spooder installation not found. Please install first.");
                return false;
            }

            // Stop Spooder if it's running
            if (_processManager.spooderProcess != null && !_processManager.spooderProcess.HasExited)
            {
                ConsoleMessenger.AddInfoMessage("Stopping Spooder before update...");
                _processManager.StopSpooder();
                await Task.Delay(2000); // Wait for clean shutdown
            }

            try
            {
                // Update the repository using GitOperations
                bool gitUpdateSuccess = await _gitOperations.UpdateRepository(spooderPath, targetBranch);
                if (!gitUpdateSuccess)
                {
                    return false;
                }

                // Run npm install to update dependencies
                ConsoleMessenger.AddInfoMessage("Updating dependencies...");
                await _installationManager.RunNpmInstall(spooderPath);
                await _installationManager.RunBuildCommand(spooderPath);

                ConsoleMessenger.AddSuccessMessage("Spooder update completed successfully!");

                return true;
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddErrorMessageF("Error updating Spooder: {0}", ex.Message);
                return false;
            }
        }
    }
}

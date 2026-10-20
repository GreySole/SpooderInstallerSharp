using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using SpooderInstallerSharp.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SpooderInstallerSharp.ViewModels;

public class MainViewModel : ReactiveObject
{

    public AppSettings appSettings;

    public event EventHandler? ReturnToConsole;
    protected virtual void OnReturnToConsole()
    {
        ReturnToConsole?.Invoke(this, EventArgs.Empty);
    }
   

    public SpooderManager _spooder;

    public ICommand InstallSpooder { get; }
    public ICommand UninstallSpooder { get; }
    public ICommand ReinstallSpooder { get; }
    public ICommand StartSpooder { get; }
    public ICommand RestartSpooder { get; }
    public ICommand StopSpooder { get; }
    public ICommand CleanSpooder { get; }
    public ICommand OpenSpooder { get; }
    public ICommand BrowseSpooder { get; }
    public ICommand ShowWindowCommand { get; }
    public ICommand ToggleRun { get; }
    public ICommand ExitCommand { get; }
    public ICommand OpenSpooderLog { get; }

    private bool _IsSpooderInstalled;

    public bool IsSpooderInstalled
    {
        get => _IsSpooderInstalled;
        set
        {
            this.RaiseAndSetIfChanged(ref _IsSpooderInstalled, value);
            this.RaisePropertyChanged(nameof(IsSpooderNotInstalled));
            this.RaisePropertyChanged(nameof(IsSpooderRunnable));
        }
    }

    private bool _IsSpooderRunning;

    public bool IsSpooderRunning
    {
        get => _IsSpooderRunning;
        set
        {
            this.RaiseAndSetIfChanged(ref _IsSpooderRunning, value);
            this.RaisePropertyChanged(nameof(IsSpooderNotRunning));
            this.RaisePropertyChanged(nameof(IsSpooderRunnable));
            this.RaisePropertyChanged(nameof(ToggleRunMenuText));
        }
    }

    public bool IsSpooderNotRunning => !IsSpooderRunning;
    public bool IsSpooderNotInstalled => !IsSpooderInstalled;
    public bool IsSpooderRunnable => !IsSpooderRunning && IsSpooderInstalled;
    public string ToggleRunMenuText => IsSpooderRunning ? "Stop" : "Start";

    private bool _isLoading;

    /// <summary>
    /// Drives the spinning loading icon. Backed by a ref-count so overlapping sources
    /// (a manager command plus an IPC-triggered loading state, say) don't clear each other out.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    private int _loadingRefCount;
    private readonly object _loadingLock = new object();

    private void BeginLoading()
    {
        bool shouldShow = false;
        lock (_loadingLock)
        {
            _loadingRefCount++;
            shouldShow = _loadingRefCount == 1;
        }
        if (shouldShow)
        {
            Dispatcher.UIThread.Post(() => IsLoading = true);
        }
    }

    private void EndLoading()
    {
        bool shouldHide = false;
        lock (_loadingLock)
        {
            if (_loadingRefCount > 0)
            {
                _loadingRefCount--;
            }
            shouldHide = _loadingRefCount == 0;
        }
        if (shouldHide)
        {
            Dispatcher.UIThread.Post(() => IsLoading = false);
        }
    }

    /// <summary>
    /// Resets the loading indicator regardless of how many sources requested it.
    /// Called when Spooder exits so a crash mid-operation can't leave the icon spinning forever.
    /// </summary>
    private void ForceLoadingOff()
    {
        lock (_loadingLock)
        {
            _loadingRefCount = 0;
        }
        Dispatcher.UIThread.Post(() => IsLoading = false);
    }

    private bool _isSpooderBusy;

    /// <summary>
    /// Drives the dot inside the loading icon. The ring alone means the manager itself is
    /// busy; the ring plus this dot means Spooder (the Node process) reported it's busy over IPC.
    /// </summary>
    public bool IsSpooderBusy
    {
        get => _isSpooderBusy;
        private set => this.RaiseAndSetIfChanged(ref _isSpooderBusy, value);
    }

    private int _spooderBusyRefCount;
    private readonly object _spooderBusyLock = new object();

    private void BeginSpooderBusy()
    {
        bool shouldShow = false;
        lock (_spooderBusyLock)
        {
            _spooderBusyRefCount++;
            shouldShow = _spooderBusyRefCount == 1;
        }
        if (shouldShow)
        {
            Dispatcher.UIThread.Post(() => IsSpooderBusy = true);
        }
    }

    private void EndSpooderBusy()
    {
        bool shouldHide = false;
        lock (_spooderBusyLock)
        {
            if (_spooderBusyRefCount > 0)
            {
                _spooderBusyRefCount--;
            }
            shouldHide = _spooderBusyRefCount == 0;
        }
        if (shouldHide)
        {
            Dispatcher.UIThread.Post(() => IsSpooderBusy = false);
        }
    }

    /// <summary>
    /// Resets the Spooder-busy dot regardless of how many "loading_start" messages came in
    /// without a matching "loading_stop". Called when Spooder exits so a crash can't leave it stuck on.
    /// </summary>
    private void ForceSpooderBusyOff()
    {
        lock (_spooderBusyLock)
        {
            _spooderBusyRefCount = 0;
        }
        Dispatcher.UIThread.Post(() => IsSpooderBusy = false);
    }

    public ObservableCollection<string> ConsoleOutput { get; } = new ObservableCollection<string>();

    private async Task OnUpdateAvailableAsync(object? sender, UpdateAvailableEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                // Check if user wants to see update prompts
                if (!appSettings.ShowUpdatePrompts)
                {
                    ConsoleMessenger.AddInfoMessageF("Update available (v{0})", e.NewVersion);
                    return;
                }

                var messageBox = MessageBoxManager.GetMessageBoxStandard(
                    "Spooder Update Available",
                    $"A new version of Spooder is available!\n\n" +
                    $"Current Version: {e.CurrentVersion}\n" +
                    $"New Version: {e.NewVersion}\n" +
                    $"Branch: {e.Branch}\n\n" +
                    $"Would you like to update now?",
                    ButtonEnum.YesNo,
                    Icon.Question
                );

                var result = await messageBox.ShowAsync();

                if (result == ButtonResult.Yes)
                {
                    ConsoleMessenger.AddInfoMessage("User chose to update Spooder...");
                    OnReturnToConsole();
                    
                    bool success = await Task.Run(() => _spooder.UpdateSpooder());
                    if (success)
                    {
                        ConsoleMessenger.AddSuccessMessage("Spooder updated successfully!");
                    }
                    else
                    {
                        ConsoleMessenger.AddErrorMessage("Spooder update failed.");
                    }
                }
                else
                {
                    ConsoleMessenger.AddInfoMessage("User declined to update Spooder.");
                }
            }
            catch (Exception ex)
            {
                ConsoleMessenger.AddErrorMessageF("Error showing update dialog: {0}", ex.Message);
            }
        });
    }

    public MainViewModel()
    {
        Debug.WriteLine($"MainViewModel created");
        appSettings = SettingsManager.LoadSettings();
        
        _spooder = new SpooderManager();

        IsSpooderInstalled = _spooder.spooderInfo != null;

        // refreshSpooderInfo() runs asynchronously after Node setup completes, so spooderInfo is
        // still null at the line above on a fresh launch. Re-check once it actually finishes.
        _spooder.SpooderInfoRefreshed += (sender, e) =>
        {
            Dispatcher.UIThread.Post(() => IsSpooderInstalled = _spooder.spooderInfo != null);
        };

        _spooder.SpooderRunStart += (sender, e) =>
        {
            Debug.WriteLine("SpooderRunStart event received");
            Dispatcher.UIThread.Post(() => IsSpooderRunning = true);
        };

        _spooder.SpooderRunStop += (sender, e) =>
        {
            Debug.WriteLine("SpooderRunStop event received");
            Dispatcher.UIThread.Post(() => IsSpooderRunning = false);
            // Spooder may have crashed mid-operation; don't let the loading icon spin (or the busy dot) forever.
            ForceLoadingOff();
            ForceSpooderBusyOff();
        };

        _spooder.NodeSetupStart += (sender, e) => BeginLoading();
        _spooder.NodeSetupComplete += (sender, e) => EndLoading();

        _spooder.MessageReceived += (sender, message) =>
        {
            // Handle the IPC message from the tsx app
            try
            {
                Logger.LogInfo($"Received IPC message: {message}");
                JObject messageJson = JObject.Parse(message);
                Debug.WriteLine($"Received IPC message: {message} {messageJson["action"]?.ToString()}");
                if (messageJson["action"]?.ToString() == "restart")
                {
                    Debug.WriteLine("Restart action received from IPC message");
                    Dispatcher.UIThread.Post(async () => await RestartSpooderTask());
                }else if (messageJson["action"]?.ToString() == "refresh_info")
                {
                    Dispatcher.UIThread.Post(() => _spooder.refreshSpooderInfo());
                }else if (messageJson["action"]?.ToString() == "loading_start")
                {
                    // Spooder itself is busy: show the general ring plus the dot that marks it as Spooder's work.
                    BeginLoading();
                    BeginSpooderBusy();
                }else if (messageJson["action"]?.ToString() == "loading_stop")
                {
                    EndLoading();
                    EndSpooderBusy();
                }else if (messageJson["action"]?.ToString() == "server_running")
                {
                    // Spooder's web server is actually ready; open the browser now instead of guessing with a delay.
                    var currentSettings = SettingsManager.LoadSettings();
                    if (currentSettings.OpenSpooderOnStartup)
                    {
                        Dispatcher.UIThread.Post(async () => await OpenSpooderTask());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error parsing IPC message: {message}", ex);
                Debug.WriteLine($"Error parsing IPC message: {ex.Message}");
            }
        };

        _spooder.UpdateAvailable += async (sender, e) => await OnUpdateAvailableAsync(sender, e);

        InstallSpooder = ReactiveCommand.CreateFromTask(InstallSpooderTask, this.WhenAnyValue(x => x.IsSpooderNotInstalled));
        UninstallSpooder = ReactiveCommand.CreateFromTask(UninstallSpooderTask, this.WhenAnyValue(x => x.IsSpooderInstalled));
        ReinstallSpooder = ReactiveCommand.CreateFromTask(ReinstallSpooderTask, this.WhenAnyValue(x => x.IsSpooderInstalled));
        CleanSpooder = ReactiveCommand.CreateFromTask(CleanSpooderTask, this.WhenAnyValue(x => x.IsSpooderInstalled));
        StartSpooder = ReactiveCommand.CreateFromTask(StartSpooderTask, this.WhenAnyValue(x => x.IsSpooderNotRunning));
        RestartSpooder = ReactiveCommand.CreateFromTask(RestartSpooderTask, this.WhenAnyValue(x => x.IsSpooderRunning));
        StopSpooder = ReactiveCommand.CreateFromTask(StopSpooderTask, this.WhenAnyValue(x => x.IsSpooderRunning));
        OpenSpooder = ReactiveCommand.CreateFromTask(OpenSpooderTask, this.WhenAnyValue(x => x.IsSpooderRunning));
        BrowseSpooder = ReactiveCommand.CreateFromTask(BrowseSpooderTask);
        ShowWindowCommand = ReactiveCommand.Create(ShowWindow);
        ToggleRun = ReactiveCommand.CreateFromTask(ToggleRunTask);
        ExitCommand = ReactiveCommand.Create(ExitApplication);
        OpenSpooderLog = ReactiveCommand.Create(OpenSpooderLogFile);
    }

    private void ShowWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Show();
            desktop.MainWindow?.Activate();
        }
    }

    private async Task ToggleRunTask()
    {
        if (IsSpooderRunning)
        {
            await StopSpooderTask();
        }
        else if (IsSpooderInstalled)
        {
            await StartSpooderTask();
        }
    }

    private void ExitApplication()
    {
        OnCloseAsync();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    public void OnCloseAsync()
    {
        Logger.LogInfo("Manager application shutting down...");
        _spooder.StopSpooder();
    }

    public void OpenSpooderLogFile()
    {
        Logger.OpenLogFile();
    }

    private async Task InstallSpooderTask()
    {
        OnReturnToConsole();
        BeginLoading();
        try
        {
            IsSpooderInstalled = await Task.Run(() => _spooder.InstallSpooder());
        }
        finally
        {
            EndLoading();
        }
    }

    private async Task UninstallSpooderTask()
    {
        OnReturnToConsole();
        BeginLoading();
        try
        {
            await Task.Run(() => _spooder.UninstallSpooder());
            IsSpooderInstalled = false;
        }
        finally
        {
            EndLoading();
        }
    }

    private async Task ReinstallSpooderTask()
    {
        OnReturnToConsole();
        BeginLoading();
        try
        {
            // No target branch: UpdateSpooder falls back to appSettings.SelectedBranch, so this
            // fetches and hard-resets to whatever branch is currently configured in Settings.
            bool success = await Task.Run(() => _spooder.UpdateSpooder());
            if (success)
            {
                ConsoleMessenger.AddSuccessMessage("Spooder reinstalled successfully!");
            }
            else
            {
                ConsoleMessenger.AddErrorMessage("Spooder reinstall failed.");
            }
        }
        finally
        {
            EndLoading();
        }
    }

    private async Task CleanSpooderTask()
    {
        OnReturnToConsole();
        BeginLoading();
        try
        {
            await Task.Run(() => _spooder.CleanSpooder());
        }
        finally
        {
            EndLoading();
        }
    }

    private async Task StartSpooderTask()
    {
        OnReturnToConsole();
        BeginLoading();
        try
        {
            await Task.Run(() => _spooder.StartSpooder());
        }
        finally
        {
            EndLoading();
        }
        // Browser auto-open (if enabled) now happens when Spooder's "server_running" IPC message arrives,
        // instead of guessing with a fixed delay here.
    }

    private async Task RestartSpooderTask()
    {
        OnReturnToConsole();
        BeginLoading();
        try
        {
            await Task.Run(() => _spooder.StopSpooder());
            await Task.Run(() => _spooder.StartSpooder());
        }
        finally
        {
            EndLoading();
        }
    }

    private async Task StopSpooderTask()
    {
        OnReturnToConsole();
        BeginLoading();
        try
        {
            await Task.Run(() => _spooder.StopSpooder());
        }
        finally
        {
            EndLoading();
        }
    }

    private async Task OpenSpooderTask()
    {
        try
        {
            var hostPort = _spooder.spooderInfo?.host_port ?? 3000;
            string url = $"http://localhost:{hostPort}";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };

            Process.Start(processStartInfo);
            ConsoleMessenger.AddInfoMessageF("Opening Spooder in default browser: {0}", url);
        }
        catch (Exception ex)
        {
            ConsoleMessenger.AddErrorMessageF("Error opening browser: {0}", ex.Message);
        }
        
        await Task.CompletedTask;
    }

    private async Task BrowseSpooderTask()
    {
        try
        {
            string installPath = appSettings.SpooderInstallationPath;

            if (!Directory.Exists(installPath))
            {
                ConsoleMessenger.AddErrorMessageF("Spooder installation folder not found: {0}", installPath);
                return;
            }

            ProcessStartInfo? processStartInfo = GetPlatformSpecificFileManagerProcess(installPath);

            if (processStartInfo != null)
            {
                Process.Start(processStartInfo);
                ConsoleMessenger.AddInfoMessageF("Opening Spooder installation folder: {0}", installPath);
            }
            else
            {
                ConsoleMessenger.AddErrorMessage("File manager not supported on this platform");
            }
        }
        catch (Exception ex)
        {
            ConsoleMessenger.AddErrorMessageF("Error opening folder: {0}", ex.Message);
        }
        
        await Task.CompletedTask;
    }

    private ProcessStartInfo? GetPlatformSpecificFileManagerProcess(string path)
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            return new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            };
        }
        else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
        {
            // Try common Linux file managers in order of preference
            string[] fileManagers = { "xdg-open", "nautilus", "dolphin", "thunar", "pcmanfm", "nemo" };

            foreach (string fileManager in fileManagers)
            {
                if (IsCommandAvailable(fileManager))
                {
                    return new ProcessStartInfo
                    {
                        FileName = fileManager,
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    };
                }
            }
        }
        else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
        {
            return new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            };
        }
        else
        {
            // Android or other platforms - try generic approach
            try
            {
                // For Android, we might need to use Intents through platform-specific code
                // For now, try xdg-open as a fallback
                if (IsCommandAvailable("xdg-open"))
                {
                    return new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    };
                }
            }
            catch
            {
                // Fallback failed
            }
        }

        return null;
    }

    private bool IsCommandAvailable(string command)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processStartInfo))
            {
                if (process != null)
                {
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Test method to verify CSS classes are working - you can call this for debugging
    /// </summary>
    public void TestCssClasses()
    {
        // Test using the static methods directly
        ConsoleMessenger.AddErrorMessage("This should be RED and BOLD (error message)");
        ConsoleMessenger.AddWarningMessage("This should be ORANGE and SEMI-BOLD (warning message)");
        ConsoleMessenger.AddSuccessMessage("This should be GREEN and SEMI-BOLD (success message)");
        ConsoleMessenger.AddInfoMessage("This should be BLUE (info message)");
        ConsoleMessenger.AddDebugMessage("This should be GRAY and ITALIC (debug message)");
        
        // Test custom styling via static methods
        ConsoleMessenger.AddStyledMessage("This should be RED with YELLOW background", "text-red", "bg-yellow");
        ConsoleMessenger.AddStyledMessage("This should be BLUE, BOLD and UNDERLINED", "text-blue", "text-bold", "text-underline");
        
        // Test conditional and formatted methods
        ConsoleMessenger.AddErrorMessageIf(true, "This conditional error message should appear");
        ConsoleMessenger.AddErrorMessageIf(false, "This conditional error message should NOT appear");
        ConsoleMessenger.AddInfoMessageF("This is a formatted message with {0} and {1}", "parameter1", "parameter2");
    }
}

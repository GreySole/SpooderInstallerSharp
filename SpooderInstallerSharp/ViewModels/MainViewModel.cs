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

        _spooder.SpooderRunStart += (sender, e) =>
        {
            Debug.WriteLine("SpooderRunStart event received");
            Dispatcher.UIThread.Post(() => IsSpooderRunning = true);
        };

        _spooder.SpooderRunStop += (sender, e) =>
        {
            Debug.WriteLine("SpooderRunStop event received");
            Dispatcher.UIThread.Post(() => IsSpooderRunning = false);
        };

        _spooder.MessageReceived += (sender, message) =>
        {
            // Handle the IPC message from the tsx app
            try
            {
                JObject messageJson = JObject.Parse(message);
                Debug.WriteLine($"Received IPC message: {message} {messageJson["action"]?.ToString()}");
                if (messageJson["action"]?.ToString() == "restart")
                {
                    Debug.WriteLine("Restart action received from IPC message");
                    Dispatcher.UIThread.Post(async () => await RestartSpooderTask());
                }else if (messageJson["action"]?.ToString() == "refresh_info")
                {
                    Dispatcher.UIThread.Post(() => _spooder.refreshSpooderInfo());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing IPC message: {ex.Message}");
            }
        };

        _spooder.UpdateAvailable += async (sender, e) => await OnUpdateAvailableAsync(sender, e);

        InstallSpooder = ReactiveCommand.CreateFromTask(InstallSpooderTask, this.WhenAnyValue(x => x.IsSpooderNotInstalled));
        UninstallSpooder = ReactiveCommand.CreateFromTask(UninstallSpooderTask, this.WhenAnyValue(x => x.IsSpooderInstalled));
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
        IsSpooderInstalled = await Task.Run(() => _spooder.InstallSpooder());
    }

    private async Task UninstallSpooderTask()
    {
        OnReturnToConsole();
        await Task.Run(() => _spooder.UninstallSpooder());
        IsSpooderInstalled = false;
    }

    private async Task CleanSpooderTask()
    {
        OnReturnToConsole();
        await Task.Run(() => _spooder.CleanSpooder());
    }

    private async Task StartSpooderTask()
    {
        OnReturnToConsole();
        await Task.Run(() => _spooder.StartSpooder());
        var appSettings = SettingsManager.LoadSettings();
        if (appSettings.OpenSpooderOnStartup)
        {
            await Task.Delay(5000);
            await OpenSpooderTask();
        }
    }

    private async Task RestartSpooderTask()
    {
        OnReturnToConsole();
        await Task.Run(() => _spooder.StopSpooder());
        await Task.Run(() => _spooder.StartSpooder());
    }

    private async Task StopSpooderTask()
    {
        OnReturnToConsole();
        await Task.Run(() => _spooder.StopSpooder());
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

using Avalonia.Controls;
using Avalonia.Threading;
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

    public event EventHandler ReturnToConsole;
    protected virtual void OnReturnToConsole()
    {
        ReturnToConsole?.Invoke(this, EventArgs.Empty);
    }
    private StackPanel _consoleOutputPanel;
    public StackPanel ConsoleOutputPanel
    {
        get => _consoleOutputPanel;
        set
        {
            _consoleOutputPanel = value;
        }
    }

    public SpooderManager _spooder;

    public ICommand InstallSpooder { get; }
    public ICommand UninstallSpooder { get; }
    public ICommand StartSpooder { get; }
    public ICommand RestartSpooder { get; }
    public ICommand StopSpooder { get; }
    public ICommand CleanSpooder { get; }
    public ICommand UpdateSpooder { get; }
    public ICommand OpenSpooder { get; }
    public ICommand BrowseSpooder { get; }

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
        }
    }

    public bool IsSpooderNotRunning => !IsSpooderRunning;
    public bool IsSpooderNotInstalled => !IsSpooderInstalled;
    public bool IsSpooderRunnable => !IsSpooderRunning && IsSpooderInstalled;

    public ObservableCollection<string> ConsoleOutput { get; } = new ObservableCollection<string>();


    public MainViewModel()
    {
        Debug.WriteLine($"MainViewModel created");
        appSettings = new AppSettings();
        
        _spooder = new SpooderManager(AppendToConsoleOutput);

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
            Debug.WriteLine($"Received IPC message: {message}");
        };

        InstallSpooder = ReactiveCommand.CreateFromTask(InstallSpooderTask, this.WhenAnyValue(x => x.IsSpooderNotInstalled));
        UninstallSpooder = ReactiveCommand.CreateFromTask(UninstallSpooderTask, this.WhenAnyValue(x => x.IsSpooderInstalled));
        StartSpooder = ReactiveCommand.CreateFromTask(StartSpooderTask, this.WhenAnyValue(x => x.IsSpooderNotRunning));
        RestartSpooder = ReactiveCommand.CreateFromTask(RestartSpooderTask, this.WhenAnyValue(x => x.IsSpooderRunning));
        StopSpooder = ReactiveCommand.CreateFromTask(StopSpooderTask, this.WhenAnyValue(x => x.IsSpooderRunning));
        OpenSpooder = ReactiveCommand.CreateFromTask(OpenSpooderTask, this.WhenAnyValue(x => x.IsSpooderRunning));
        BrowseSpooder = ReactiveCommand.CreateFromTask(BrowseSpooderTask);
    }

    public void OnCloseAsync()
    {
        _spooder.StopSpooder();
    }

    public void AppendToConsoleOutput(string text)
    {
        Debug.WriteLine(text);
        if (text != null)
        {
            ConsoleOutput.Add(text);
        }
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

    private async Task StartSpooderTask()
    {
        OnReturnToConsole();
        await Task.Run(() => _spooder.StartSpooder());
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
            string url = "http://localhost:3000";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };

            Process.Start(processStartInfo);
            AppendToConsoleOutput($"Opening Spooder in default browser: {url}");
        }
        catch (Exception ex)
        {
            AppendToConsoleOutput($"Error opening browser: {ex.Message}");
        }
    }

    private async Task BrowseSpooderTask()
    {
        try
        {
            string installPath = appSettings.SpooderInstallationPath;

            if (!Directory.Exists(installPath))
            {
                AppendToConsoleOutput($"Spooder installation folder not found: {installPath}");
                return;
            }

            ProcessStartInfo processStartInfo = GetPlatformSpecificFileManagerProcess(installPath);

            if (processStartInfo != null)
            {
                Process.Start(processStartInfo);
                AppendToConsoleOutput($"Opening Spooder installation folder: {installPath}");
            }
            else
            {
                AppendToConsoleOutput("File manager not supported on this platform");
            }
        }
        catch (Exception ex)
        {
            AppendToConsoleOutput($"Error opening folder: {ex.Message}");
        }
    }

    private ProcessStartInfo GetPlatformSpecificFileManagerProcess(string path)
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
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }
}

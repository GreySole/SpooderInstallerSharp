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
    public ICommand StopSpooder { get; }
    public ICommand CleanSpooder { get; }
    public ICommand UpdateSpooder { get; }

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

        InstallSpooder = ReactiveCommand.CreateFromTask(InstallSpooderTask, this.WhenAnyValue(x => x.IsSpooderNotInstalled));
        UninstallSpooder = ReactiveCommand.CreateFromTask(UninstallSpooderTask, this.WhenAnyValue(x => x.IsSpooderInstalled));
        StartSpooder = ReactiveCommand.CreateFromTask(StartSpooderTask, this.WhenAnyValue(x => x.IsSpooderNotRunning));
        StopSpooder = ReactiveCommand.CreateFromTask(StopSpooderTask, this.WhenAnyValue(x => x.IsSpooderRunning));
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

    private async Task StopSpooderTask()
    {
        OnReturnToConsole();
        await Task.Run(() => _spooder.StopSpooder());
    }
}

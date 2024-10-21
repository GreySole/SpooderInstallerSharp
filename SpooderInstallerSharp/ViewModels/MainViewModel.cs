using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SpooderInstallerSharp.ViewModels;

public class MainViewModel : ReactiveObject
{

    public string defaultLocalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spooder");

    private string _consoleOutput;
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
    public ICommand StartSpooder { get; }
    public ICommand StopSpooder { get; }
    public ICommand CleanSpooder { get; }
    public ICommand UpdateSpooder { get; }

    public bool IsSpooderInstalled
    {
        get => _spooder.spooderInstalled;
        set
        {
            this.RaiseAndSetIfChanged(ref _spooder.spooderInstalled, value);
            this.RaisePropertyChanged(nameof(IsSpooderNotInstalled));
            this.RaisePropertyChanged(nameof(IsSpooderRunnable));
        }
    }

    public bool IsSpooderRunning
    {
        get => _spooder.spooderRunning;
        set
        {

            this.RaiseAndSetIfChanged(ref _spooder.spooderRunning, value);
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
        defaultLocalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spooder");
        _spooder = new SpooderManager(AppendToConsoleOutput);

        InstallSpooder = ReactiveCommand.CreateFromTask(InstallSpooderTask, this.WhenAnyValue(x => x.IsSpooderNotInstalled));
        StartSpooder = ReactiveCommand.CreateFromTask(StartSpooderTask, this.WhenAnyValue(x => x.IsSpooderNotRunning));
        StopSpooder = ReactiveCommand.CreateFromTask(StopSpooderTask, this.WhenAnyValue(x => x.IsSpooderRunning));
    }

    public void OnCloseAsync()
    {
        _spooder.Cleanup();
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
        IsSpooderInstalled = await Task.Run(() => _spooder.InstallSpooderNode(defaultLocalPath));
    }

    private async Task StartSpooderTask()
    {
        string defaultLocalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spooder");
        IsSpooderRunning = await Task.Run(() => _spooder.RunSpooderNode(defaultLocalPath));

    }

    private async Task StopSpooderTask()
    {
        IsSpooderRunning = await Task.Run(() => _spooder.Cleanup());
    }
}

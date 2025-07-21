using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using SpooderInstallerSharp.ViewModels;
using SpooderInstallerSharp.Views;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SpooderInstallerSharp;

public partial class App : Application
{
    private MainViewModel? _mainViewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            File.AppendAllText("fatal.log", $"Unhandled: {e.ExceptionObject}\n");
        };
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            File.AppendAllText("fatal.log", $"Unobserved: {e.Exception}\n");
            e.SetObserved();
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _mainViewModel = new MainViewModel();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainView = new MainWindow
            {
                DataContext = _mainViewModel
            };

            desktop.MainWindow = new Window
            {
                Content = mainView,
                Title = "Spooder Installer",
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://SpooderInstallerSharp/Assets/favicon.ico"))),
                DataContext = _mainViewModel
            };

            desktop.MainWindow.Closing += (sender, e) =>
            {
                _mainViewModel.OnCloseAsync();
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainWindow
            {
                DataContext = _mainViewModel
            };
            singleViewPlatform.MainView.Unloaded += (sender, e) =>
            {
                _mainViewModel.OnCloseAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using SpooderInstallerSharp.Models;
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
            _mainViewModel?.OnCloseAsync();
            File.AppendAllText("fatal.log", $"Unhandled: {e.ExceptionObject}\n");
        };
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            _mainViewModel?.OnCloseAsync();
            File.AppendAllText("fatal.log", $"Unobserved: {e.Exception}\n");
            e.SetObserved();
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _mainViewModel = new MainViewModel();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            this.DataContext = _mainViewModel;

            var appSettings = SettingsManager.LoadSettings();

            var mainView = new MainWindow
            {
                DataContext = _mainViewModel
            };

            desktop.MainWindow = new Window
            {
                Content = mainView,
                Title = "Spooder Manager",
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://SpooderInstallerSharp/Assets/favicon.ico"))),
                DataContext = _mainViewModel,
                Width = appSettings.ScreenWidth,
                Height = appSettings.ScreenHeight,
            };

            desktop.MainWindow.Resized += (sender, e) =>
            {
                if (desktop.MainWindow is Window window)
                {
                    appSettings.ScreenWidth = (int)window.Width;
                    appSettings.ScreenHeight = (int)window.Height;
                    SettingsManager.SaveSettings(appSettings);
                }
            };

            desktop.MainWindow.Closing += (sender, e) =>
            {
                e.Cancel = true;
                if(sender is Window window)
                {
                    window.Hide();
                }
            };
            _mainViewModel._spooder.SpooderRunStart += (sender, e) =>
            {
                Dispatcher.UIThread.Post(() => UpdateTrayIcon("/Assets/StatusIcons/green_spooder_icon.ico", "Spooder Installer is running"));
            };
            _mainViewModel._spooder.SpooderRunStop += (sender, e) =>
            {
                Dispatcher.UIThread.Post(() => UpdateTrayIcon("/Assets/StatusIcons/red_spooder_icon.ico", "Spooder Installer is not running"));
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

    // Add these methods to access and modify the TrayIcon
    public void UpdateTrayIcon(string iconPath, string toolTipText)
    {
        if (TrayIcon.GetIcons(this)?.Count > 0)
        {
            var trayIcon = TrayIcon.GetIcons(this)?[0];
            if (trayIcon == null)
            {
                return;
            }

            trayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri($"avares://SpooderInstallerSharp{iconPath}")));
            trayIcon.ToolTipText = toolTipText;
        }
    }

    public static void UpdateTrayIconStatic(string iconPath, string toolTipText)
    {
        if (Application.Current is App app)
        {
            app.UpdateTrayIcon(iconPath, toolTipText);
        }
    }
}
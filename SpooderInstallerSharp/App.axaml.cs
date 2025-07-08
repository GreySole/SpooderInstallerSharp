using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SpooderInstallerSharp.ViewModels;
using SpooderInstallerSharp.Views;

namespace SpooderInstallerSharp;

public partial class App : Application
{
    private MainViewModel? _mainViewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
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
                Title = "SpooderInstallerSharp",
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
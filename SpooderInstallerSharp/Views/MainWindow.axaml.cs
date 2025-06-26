using Avalonia.Controls;
using SpooderInstallerSharp.Models;
using SpooderInstallerSharp.ViewModels;
using System;
using System.Diagnostics;

namespace SpooderInstallerSharp.Views;

public partial class MainWindow : Window
{
    private ConsoleOutput consoleOutput = new ConsoleOutput();
    private Settings settingsView = new Settings();
    private bool settingsOpened = false;
    public MainWindow()
    {
        InitializeComponent();
        Debug.WriteLine($"Need Initialization {SettingsManager.InitializationNeeded}");
        if (SettingsManager.InitializationNeeded)
        {
            Debug.WriteLine("Settings initialization needed, showing settings view.");
            ShowSettingsView();
        }
        else
        {
            ShowMainView();
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ReturnToConsole += (s, e) =>
            {
                Debug.WriteLine("Returning to console from settings view.");
                ShowMainView();
            };
        }
    }

    private void OnMainViewClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowMainView();
    }

    private void OnSettingsClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (settingsOpened)
        {
            ShowMainView();
        }
        else
        {
            ShowSettingsView();
        }
    }

    private void ShowMainView()
    {
        consoleOutput.DataContext = this.DataContext;
        var contentFrame = this.FindControl<ContentControl>("ContentFrame");
        contentFrame.Content = consoleOutput;
        settingsOpened = false;
    }

    private void ShowSettingsView()
    {
        settingsView.DataContext = this.DataContext;
        var contentFrame = this.FindControl<ContentControl>("ContentFrame");
        contentFrame.Content = settingsView;
        settingsOpened = true;
    }
}

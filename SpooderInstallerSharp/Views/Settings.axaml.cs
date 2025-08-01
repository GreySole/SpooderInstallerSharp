using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using SpooderInstallerSharp.Models;
using SpooderInstallerSharp.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.Views;

public partial class Settings : UserControl
{

    public event EventHandler? ReturnToConsole;

    protected virtual void OnReturnToConsole()
    {
        ReturnToConsole?.Invoke(this, EventArgs.Empty);
    }

    public Settings()
    {
        InitializeComponent();
        _ = PopulateBranchSelectAsync();
        LoadInstallationDirectory();
        LoadPreferences();
    }

    private void LoadPreferences()
    {
        var appSettings = SettingsManager.LoadSettings();
        var autoStartSpooderCheckBox = this.FindControl<CheckBox>("AutoStartSpooderCheckBox");
        var openSpooderOnStartupCheckBox = this.FindControl<CheckBox>("AutoOpenSpooderCheckbox");

        if(autoStartSpooderCheckBox != null)
        {
            autoStartSpooderCheckBox.IsChecked = appSettings.StartSpooderOnStartup;
        }

        if(openSpooderOnStartupCheckBox != null)
        {
            openSpooderOnStartupCheckBox.IsChecked = appSettings.OpenSpooderOnStartup;
        }
    }

    private void LoadInstallationDirectory()
    {
        var installationDirTextBox = this.FindControl<TextBox>("InstallationDirTextBox");
        if (installationDirTextBox != null)
        {
            var appSettings = SettingsManager.LoadSettings();
            // Load from settings or set a default path
            installationDirTextBox.Text = appSettings.SpooderInstallationPath; // Replace with your actual default or saved path
        }
    }

    private async void BrowseFolderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider != null)
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Installation Directory",
                AllowMultiple = false
            }).ConfigureAwait(false);

            if (folders.Count > 0)
            {
                var selectedFolder = folders.First();
                var installationDirTextBox = this.FindControl<TextBox>("InstallationDirTextBox");
                if (installationDirTextBox != null)
                {
                    var newPath = Path.Combine(selectedFolder.Path.LocalPath, "Spooder");
                    installationDirTextBox.Text = newPath;
                    SaveInstallationDirectory(newPath);
                }
            }
        }
    }

    private void SaveInstallationDirectory(string directory)
    {
        var appSettings = SettingsManager.LoadSettings();
        appSettings.SpooderInstallationPath = directory;
        SettingsManager.SaveSettings(appSettings);
    }

    private async Task PopulateBranchSelectAsync()
    {
        var branchSelect = this.FindControl<ComboBox>("BranchSelect");
        var items = await Branch.FetchBranchNamesAsync();

        if (branchSelect != null)
        {
            foreach (var item in items)
            {
                branchSelect.Items.Add(item);
            }

            var appSettings = SettingsManager.LoadSettings();

            if (!string.IsNullOrEmpty(appSettings.SelectedBranch))
            {
                branchSelect.SelectedItem = appSettings.SelectedBranch;
            }

            branchSelect.SelectionChanged += BranchSelect_SelectionChanged;
        }
        else
        {
            // Handle the case where the ComboBox is not found
            Debug.WriteLine("BranchSelect ComboBox not found.");
        }
    }

    private void BranchSelect_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var branchSelect = sender as ComboBox;
        if (branchSelect?.SelectedItem != null)
        {
            string? selectedBranch = branchSelect.SelectedItem.ToString();
            if (!string.IsNullOrEmpty(selectedBranch))
            {
                _ = SaveSelectedBranchAsync(selectedBranch);
            }
        }
    }

    private async Task SaveSelectedBranchAsync(string branch)
    {
        if (DataContext is not MainViewModel mainViewModel)
        {
            Debug.WriteLine("DataContext is not MainViewModel");
            return;
        }

        if (mainViewModel.IsSpooderInstalled)
        {
            var result = await MessageBoxManager.GetMessageBoxStandard("Switch Branch", $"Switching to {branch} will reinstall Spooder while preserving your data. Plugin dependencies may need to be reinstalled. Continue?", MsBox.Avalonia.Enums.ButtonEnum.YesNo).ShowAsync().ConfigureAwait(false);
            if (result == ButtonResult.Yes)
            {
                var appSettings = SettingsManager.LoadSettings();
                appSettings.SelectedBranch = branch;
                SettingsManager.SaveSettings(appSettings);
                OnReturnToConsole();
                _ = Task.Run(() => mainViewModel._spooder.UpdateSpooder(branch));
            }
        }
        else
        {
            var appSettings = SettingsManager.LoadSettings();
            appSettings.SelectedBranch = branch;
            SettingsManager.SaveSettings(appSettings);
        }

        Debug.WriteLine($"Selected branch saved: {branch}");
    }
}
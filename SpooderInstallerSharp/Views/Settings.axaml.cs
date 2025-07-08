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
using System.Linq;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.Views;

public partial class Settings : UserControl
{

    public event EventHandler ReturnToConsole;

    protected virtual void OnReturnToConsole()
    {
        ReturnToConsole?.Invoke(this, EventArgs.Empty);
    }

    public Settings()
    {
        InitializeComponent();
        PopulateBranchSelect();
        LoadInstallationDirectory();
    }

    private void LoadInstallationDirectory()
    {
        var installationDirTextBox = this.FindControl<TextBox>("InstallationDirTextBox");
        if (installationDirTextBox != null)
        {
            // Load from settings or set a default path
            installationDirTextBox.Text = @"C:\Program Files\Spooder"; // Replace with your actual default or saved path
        }
    }

    private async void BrowseFolderButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider != null)
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Installation Directory",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                var selectedFolder = folders.First();
                var installationDirTextBox = this.FindControl<TextBox>("InstallationDirTextBox");
                if (installationDirTextBox != null)
                {
                    installationDirTextBox.Text = selectedFolder.Path.LocalPath;
                    SaveInstallationDirectory(selectedFolder.Path.LocalPath);
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

    private async void PopulateBranchSelect()
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

    private void BranchSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var branchSelect = sender as ComboBox;
        if (branchSelect != null && branchSelect.SelectedItem != null)
        {
            string selectedBranch = branchSelect.SelectedItem.ToString();
            SaveSelectedBranch(selectedBranch);
        }
    }

    private async Task SaveSelectedBranch(string branch)
    {
        var mainViewModel = (MainViewModel)DataContext;
        if (mainViewModel.IsSpooderInstalled)
        {
            var result = await MessageBoxManager.GetMessageBoxStandard("Switch Branch", $"Switching to {branch} will reinstall Spooder while preserving your data. Plugin dependencies may need to be reinstalled. Continue?", MsBox.Avalonia.Enums.ButtonEnum.YesNo).ShowAsync();
            if (result == ButtonResult.Yes)
            {
                var appSettings = SettingsManager.LoadSettings();
                appSettings.SelectedBranch = branch;
                SettingsManager.SaveSettings(appSettings);
                OnReturnToConsole();
                mainViewModel._spooder.UpdateSpooder(branch);
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
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
        PopulateModes();
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
            });

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

    private void PopulateModes()
    {
        var modeSelect = this.FindControl<ComboBox>("ModeSelect");
        var items = new List<string>(["Normal", "Dev", "Safe", "Init"]);

        if (modeSelect != null)
        {
            foreach (var item in items)
            {
                modeSelect.Items.Add(item);
            }

            var appSettings = SettingsManager.LoadSettings();

            if (!string.IsNullOrEmpty(appSettings.SelectedMode))
            {
                modeSelect.SelectedItem = appSettings.SelectedMode;
            }

            modeSelect.SelectionChanged += ModeSelect_SelectionChanged;
        }
        else
        {
            // Handle the case where the ComboBox is not found
            Debug.WriteLine("ModeSelect ComboBox not found.");
        }

    }

    private async Task PopulateBranchSelectAsync()
    {
        var branchSelect = this.FindControl<ComboBox>("BranchSelect");
        var items = await Branch.FetchBranchNamesAsync();

        if (branchSelect != null)
        {
            var recommendedBranch = "";
            
            // Add all branches to the ComboBox
            foreach (var item in items)
            {
                branchSelect.Items.Add(item);
            }

            var appSettings = SettingsManager.LoadSettings();

            if (!string.IsNullOrEmpty(appSettings.SelectedBranch))
            {
                branchSelect.SelectedItem = appSettings.SelectedBranch;
            }
            else
            {
                var bestBranch = FindBestBranch(items);
                if (!string.IsNullOrEmpty(bestBranch))
                {
                    recommendedBranch = bestBranch;
                    appSettings.SelectedBranch = recommendedBranch;
                    SettingsManager.SaveSettings(appSettings);
                }
                branchSelect.SelectedItem = recommendedBranch;
            }

            branchSelect.SelectionChanged += BranchSelect_SelectionChanged;
        }
        else
        {
            // Handle the case where the ComboBox is not found
            Debug.WriteLine("BranchSelect ComboBox not found.");
        }
    }

    private string FindBestBranch(List<string> branches)
    {
        if (branches == null || branches.Count == 0)
            return string.Empty;

        string bestStable = string.Empty;
        string bestBeta = string.Empty;
        string bestDev = string.Empty;
        string bestOther = string.Empty;

        Version highestStableVersion = null;
        Version highestBetaVersion = null;
        Version highestDevVersion = null;
        Version highestOtherVersion = null;

        foreach (var branch in branches)
        {
            var version = ExtractVersion(branch);
            if (version == null) continue;

            if (branch.Contains("-stable", StringComparison.OrdinalIgnoreCase))
            {
                if (highestStableVersion == null || version > highestStableVersion)
                {
                    highestStableVersion = version;
                    bestStable = branch;
                }
            }
            else if (branch.Contains("-beta", StringComparison.OrdinalIgnoreCase))
            {
                if (highestBetaVersion == null || version > highestBetaVersion)
                {
                    highestBetaVersion = version;
                    bestBeta = branch;
                }
            }
            else if (branch.Contains("-dev", StringComparison.OrdinalIgnoreCase))
            {
                if (highestDevVersion == null || version > highestDevVersion)
                {
                    highestDevVersion = version;
                    bestDev = branch;
                }
            }
            else
            {
                if (highestOtherVersion == null || version > highestOtherVersion)
                {
                    highestOtherVersion = version;
                    bestOther = branch;
                }
            }
        }

        // Prioritize: stable > beta > dev > other
        if (!string.IsNullOrEmpty(bestStable))
            return bestStable;
        if (!string.IsNullOrEmpty(bestBeta))
            return bestBeta;
        if (!string.IsNullOrEmpty(bestDev))
            return bestDev;
        
        return bestOther;
    }

    private Version ExtractVersion(string branchName)
    {
        if (string.IsNullOrEmpty(branchName))
            return null;

        // Try to extract version number from branch name
        // Common patterns: v1.2.3, 1.2.3, v1.2.3-stable, etc.
        var patterns = new[]
        {
            @"v?(\d+\.\d+\.\d+)", // v1.2.3 or 1.2.3
            @"v?(\d+\.\d+)",      // v1.2 or 1.2
            @"v?(\d+)"            // v1 or 1
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(branchName, pattern);
            if (match.Success)
            {
                var versionString = match.Groups[1].Value;
                
                // Ensure we have at least major.minor.patch format
                var parts = versionString.Split('.');
                if (parts.Length == 1)
                    versionString += ".0.0";
                else if (parts.Length == 2)
                    versionString += ".0";

                if (Version.TryParse(versionString, out var version))
                {
                    return version;
                }
            }
        }

        return null;
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

    private void ModeSelect_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var modeSelect = sender as ComboBox;
        if (modeSelect?.SelectedItem != null)
        {
            string? selectedMode = modeSelect.SelectedItem.ToString();
            if (!string.IsNullOrEmpty(selectedMode))
            {
                var appSettings = SettingsManager.LoadSettings();
                appSettings.SelectedMode = selectedMode;
                SettingsManager.SaveSettings(appSettings);
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
            var result = await MessageBoxManager.GetMessageBoxStandard("Switch Branch", $"Switching to {branch} will reinstall Spooder while preserving your data. Plugin dependencies may need to be reinstalled. Continue?", MsBox.Avalonia.Enums.ButtonEnum.YesNo).ShowAsync();
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
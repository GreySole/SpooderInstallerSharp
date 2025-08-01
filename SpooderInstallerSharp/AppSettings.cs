using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;

namespace SpooderInstallerSharp.Models
{
    public class AppSettings
    {
        public string SpooderInstallationPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spooder");
        public string SelectedBranch { get; set; } = "main";
        public bool AutoCheckUpdates { get; set; } = true;
        public bool ShowUpdatePrompts { get; set; } = true;
        public int ScreenWidth { get; set; } = 800;
        public int ScreenHeight { get; set; } = 600;
    };

    public class UpdateAvailableEventArgs : EventArgs
    {
        public string CurrentVersion { get; }
        public string NewVersion { get; }
        public string Branch { get; }

        public UpdateAvailableEventArgs(string currentVersion, string newVersion, string branch)
        {
            CurrentVersion = currentVersion;
            NewVersion = newVersion;
            Branch = branch;
        }
    }

    public static class SettingsManager
    {
        
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SpooderInstaller",
            "settings.json"
        );

        public static bool InitializationNeeded { get; } = !File.Exists(SettingsPath);

        public static AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            return new AppSettings();
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }
}
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using SpooderInstallerSharp.Models;
using SpooderInstallerSharp.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace SpooderInstallerSharp.Models;

public class DesktopUpdateService : IUpdateService
{

    public DesktopUpdateService()
    {
    }

    public async Task CheckForUpdatesAsync()
    {
        // Only run on Windows Desktop
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var appSettings = new AppSettings();

        try
        {
            // Get the current version using the centralized version utility
            var currentVersion = VersionUtil.GetVersionString();

            // Extract channel from current version (e.g., "0.5.0-beta" -> "beta")
            var explicitChannel = "test"; // default fallback
            Debug.WriteLine($"Current version: {currentVersion}");
            
            var dashIndex = currentVersion.IndexOf('-');
            if (dashIndex >= 0 && dashIndex < currentVersion.Length - 1)
            {
                explicitChannel = $"win-{currentVersion.Substring(dashIndex + 1)}";
            }

            Debug.WriteLine($"Checking channel: {explicitChannel}");

            var mgr = new UpdateManager(new GithubSource("https://github.com/GreySole/SpooderInstallerSharp", "", false), new UpdateOptions
            {
                ExplicitChannel = explicitChannel
            });

            var newVersion = await mgr.CheckForUpdatesAsync();

            if (newVersion == null)
            {
                Debug.WriteLine("No updates available.");
                return;
            }

            Debug.WriteLine($"Update found! New version: {newVersion.TargetFullRelease.Version}");

            // Show the update dialog
            var result = await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var messageBox = MessageBoxManager.GetMessageBoxStandard(
                        "Manager Update Available",
                        $"A new version of Spooder Manager is available!\n\n" +
                        $"Current Version: {currentVersion}\n" +
                        $"New Version: {newVersion.TargetFullRelease.Version}\n" +
                        $"Would you like to update now?",
                        ButtonEnum.YesNo,
                        Icon.Question
                    );

                return await messageBox.ShowAsync();
            });

            if (result == ButtonResult.Yes)
            {
                await mgr.DownloadUpdatesAsync(newVersion);

                mgr.ApplyUpdatesAndRestart(newVersion);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Auto update failed: {ex.Message}");
        }
    }
}

public class AndroidUpdateService : IUpdateService
{
    public Task CheckForUpdatesAsync()
    {
        // Android updates would be handled through Google Play Store
        // or a different mechanism entirely
        Debug.WriteLine("Update check not supported on Android platform");
        return Task.CompletedTask;
    }
}

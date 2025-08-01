using Avalonia;
using Avalonia.ReactiveUI;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using SpooderInstallerSharp.Models;
using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
using Velopack.Windows;

namespace SpooderInstallerSharp.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Initialize Velopack first
        VelopackApp.Build()
            .Run();

        OnAppStart();


        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>();

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }

    private static async Task OnAppStart()
    {
        var appSettings = new AppSettings();
        var mgr = new UpdateManager(new GithubSource("https://github.com/GreySole/SpooderInstallerSharp", "", false), new UpdateOptions
        {
            ExplicitChannel = appSettings.SelectedBranch
        });

        var newVersion = await mgr.CheckForUpdatesAsync();

        if (newVersion == null)
        {
            return;
        }

        await mgr.DownloadUpdatesAsync(newVersion);

        mgr.ApplyUpdatesAndRestart(newVersion);
    }
}

using Avalonia;
using Avalonia.ReactiveUI;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Velopack;

namespace SpooderInstallerSharp.Desktop;

class Program
{
    private static Mutex? _instanceMutex;
    private static NamedPipeServerStream? _pipeServer;
    private static readonly string MutexName = "SpooderInstallerSharp_SingleInstance_Mutex";
    private static readonly string PipeName = "SpooderInstallerSharp_SingleInstance_Pipe";
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (!EnsureSingleInstance())
        {
            return; // Another instance is running, exit this one
        }

        try
        {
            // Initialize Velopack first
            VelopackApp.Build()
                .Run();

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // Cleanup single instance resources
            CleanupSingleInstance();
        }
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

    private static bool EnsureSingleInstance()
    {
        try
        {
            // Try to create or open the mutex
            _instanceMutex = new Mutex(true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is running, try to notify it to show its window
                NotifyExistingInstance();
                return false;
            }

            // This is the first instance, set up the pipe server to listen for other instances
            SetupPipeServer();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in EnsureSingleInstance: {ex.Message}");
            return true; // If we can't determine, allow the instance to run
        }
    }

    private static void NotifyExistingInstance()
    {
        try
        {
            using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            pipeClient.Connect(1000); // Wait up to 1 second

            using var writer = new StreamWriter(pipeClient);
            writer.WriteLine("SHOW_WINDOW");
            writer.Flush();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to notify existing instance: {ex.Message}");
        }
    }

    private static void SetupPipeServer()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    // Byte mode (not Message) so this works on Linux too; message framing isn't needed since we delimit with WriteLine/ReadLineAsync.
                    _pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte);
                    await _pipeServer.WaitForConnectionAsync();

                    using var reader = new StreamReader(_pipeServer);
                    string? message = await reader.ReadLineAsync();

                    if (message == "SHOW_WINDOW")
                    {
                        // Show and activate the main window
                        ShowMainWindow();
                    }

                    _pipeServer.Disconnect();
                    _pipeServer.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Pipe server error: {ex.Message}");
                    break;
                }
            }
        });
    }

    private static void ShowMainWindow()
    {
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow;
                    if (mainWindow != null)
                    {
                        // Show the window if it's hidden
                        if (!mainWindow.IsVisible)
                        {
                            mainWindow.Show();
                        }

                        // Bring window to front and activate it
                        mainWindow.Activate();
                        mainWindow.Topmost = true;
                        mainWindow.Topmost = false; // Reset topmost to allow normal window behavior
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing main window: {ex.Message}");
        }
    }

    private static void CleanupSingleInstance()
    {
        try
        {
            _pipeServer?.Dispose();
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during cleanup: {ex.Message}");
        }
    }
}
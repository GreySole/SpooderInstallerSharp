using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SpooderInstallerSharp.Models;
using SpooderInstallerSharp.ViewModels;
using SpooderInstallerSharp.Views.Controls;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;

namespace SpooderInstallerSharp.Views;

public partial class ConsoleOutput : UserControl
{
    private StackPanel? _consoleOutputPanel;

    public ConsoleOutput()
    {
        InitializeComponent();
        _consoleOutputPanel = this.FindControl<StackPanel>("ConsoleOutputPanel");
        Debug.WriteLine($"Initializing ConsoleOutput");
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        ConsoleMessenger.Initialize(AddConsoleItem);

        // Display app version information after ConsoleMessenger is initialized
        DisplayAppVersion();
    }

    private void DisplayAppVersion()
    {
        try
        {
            // Use the centralized version utility
            var versionDisplay = VersionUtil.GetFormattedVersionDisplay();
            ConsoleMessenger.AddStyledMessage(versionDisplay, "text-cyan", "text-bold");
            
            // Add debug info about version source in debug builds
            #if DEBUG
            var (_, _, versionSource) = VersionUtil.GetApplicationVersion();
            ConsoleMessenger.AddDebugMessageF("Version retrieved from: {0}", versionSource);
            #endif
        }
        catch (Exception ex)
        {
            // Fallback in case there's an issue getting version info
            ConsoleMessenger.AddWarningMessageF("Could not retrieve app version: {0}", ex.Message);
            ConsoleMessenger.AddInfoMessage("Spooder Manager - Version information unavailable");
        }
    }

    public void AddConsoleItem(string? text, string[]? cssClasses = null)
    {
        if (string.IsNullOrEmpty(text) || _consoleOutputPanel == null)
            return;

        // Ensure we're on the UI thread
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.InvokeAsync(() => AddConsoleItem(text, cssClasses));
            return;
        }

        // Debug: Log what's being passed in
        //Debug.WriteLine($"ConsoleOutput.AddConsoleItem called with text: '{text}' and CSS classes: [{(cssClasses != null ? string.Join(", ", cssClasses) : "null")}]");

        var (processedText, matchedKeys) = ProcessLogText(text);
        var textBlock = new ClickableTextBlock(processedText, cssClasses)
        {
            TextWrapping = TextWrapping.Wrap,
            MatchedKeys = matchedKeys
        };

        _consoleOutputPanel.Children.Add(textBlock);
    }

    private static (string processedText, List<string> matchedKeys) ProcessLogText(string originalText)
    {
        string processedText = originalText;
        var matchedKeys = new List<string>();

        foreach (var kvp in ColorUtil.LogEffects)
        {
            string key = kvp.Key;
            string? value = kvp.Value?.ToString();

            if (!string.IsNullOrEmpty(value) && processedText.Contains(value))
            {
                matchedKeys.Add(key);
                processedText = processedText.Replace(value, string.Empty);
            }
        }

        return (processedText, matchedKeys);
    }
}
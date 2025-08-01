using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SpooderInstallerSharp.ViewModels;
using SpooderInstallerSharp.Views.Controls;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;

namespace SpooderInstallerSharp.Views;

public partial class ConsoleOutput : UserControl
{
    private StackPanel? _consoleOutputPanel;

    public ConsoleOutput()
    {
        InitializeComponent();
        _consoleOutputPanel = this.FindControl<StackPanel>("ConsoleOutputPanel");
        Debug.WriteLine($"initializing ConsoleOutput");
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainViewModel viewModel)
        {
            // Subscribe to collection changes
            viewModel.ConsoleOutput.CollectionChanged += OnConsoleOutputChanged;

            // Process existing items
            ProcessExistingItems(viewModel.ConsoleOutput);
        }
    }

    private void OnConsoleOutputChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (var newItem in e.NewItems)
            {
                _ = Dispatcher.UIThread.InvokeAsync(() => AddConsoleItem(newItem?.ToString()));
            }
        }
    }

    private void ProcessExistingItems(IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            AddConsoleItem(item);
        }
    }

    private void AddConsoleItem(string? text)
    {
        if (string.IsNullOrEmpty(text) || _consoleOutputPanel == null)
            return;

        var (processedText, matchedKeys) = ProcessLogText(text);
        var textBlock = new ClickableTextBlock
        {
            Text = processedText,
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

    private void OnGoToSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var settingsView = new Settings();
        var contentFrame = this.FindControl<ContentControl>("ContentFrame");
        if (contentFrame != null)
        {
            contentFrame.Content = settingsView;
        }
        else
        {
            Debug.WriteLine("ContentFrame not found!");
        }
    }
}
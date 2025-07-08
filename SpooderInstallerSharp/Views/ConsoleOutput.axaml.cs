using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SpooderInstallerSharp.ViewModels;
using System.Collections.Generic;
using System.Diagnostics;
using Color = Avalonia.Media.Color;

namespace SpooderInstallerSharp.Views;

public partial class ConsoleOutput : UserControl
{

    public ConsoleOutput()
    {
        InitializeComponent();
        var ConsoleOutputPanel = this.FindControl<StackPanel>("ConsoleOutputPanel");

        this.DataContextChanged += (s, e) =>
        {
            
            var mainViewModel = DataContext as MainViewModel;
            if (mainViewModel == null)
            {
                Debug.WriteLine($"MainViewModel null!");
                return;
            }

            // Example of adding text dynamically
            mainViewModel.ConsoleOutput.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (var newItem in e.NewItems)
                    {
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            string originalText = newItem.ToString();
                            string processedText = originalText;
                            var matchedKeys = new List<string>();

                            foreach (var kvp in ColorUtil.LogEffects)
                            {
                                string key = kvp.Key;
                                string value = kvp.Value.ToString();

                                if (processedText.Contains(value))
                                {
                                    matchedKeys.Add(key);
                                    processedText = processedText.Replace(value, string.Empty);
                                }
                            }

                            var textBlock = new TextBlock { Text = processedText };
                            textBlock.TextWrapping = TextWrapping.Wrap;

                            ColorUtil.ApplyLogStyle(textBlock, matchedKeys);

                            // Assuming you have a reference to the ConsoleOutputPanel
                            if (ConsoleOutputPanel != null)
                            {
                                ConsoleOutputPanel.Children.Add(textBlock);
                            }
                        });
                    }
                }
            };

            if (ConsoleOutputPanel != null)
            {
                // Process each TextBlock in the consoleOutputPanel
                foreach (var child in ConsoleOutputPanel.Children)
                {
                    if (child is TextBlock textBlock)
                    {
                        string originalText = textBlock.Text;
                        string processedText = originalText;
                        var matchedKeys = new List<string>();

                        foreach (var kvp in ColorUtil.LogEffects)
                        {
                            string key = kvp.Key;
                            string kvalue = kvp.Value.ToString();

                            if (processedText.Contains(kvalue))
                            {
                                matchedKeys.Add(key);
                                processedText = processedText.Replace(kvalue, string.Empty);
                            }
                        }

                        // Update the TextBlock text
                        textBlock.Text = processedText;

                    }
                }
            }

        };


    }

    private void OnGoToSettingsClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var settingsView = new Settings();
        var contentFrame = this.FindControl<ContentControl>("ContentFrame");
        contentFrame.Content = settingsView;
    }
}

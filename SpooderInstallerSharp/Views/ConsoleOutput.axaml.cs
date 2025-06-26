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
            mainViewModel._spooder.SpooderInstallStart += (s, e) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateCustomSpooder(true);
                });
            };

            mainViewModel._spooder.SpooderInstallComplete += (s, e) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateCustomSpooder(false);
                });
            };

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

            UpdateCustomSpooder(false);
        };


    }

    private void OnGoToSettingsClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var settingsView = new Settings();
        var contentFrame = this.FindControl<ContentControl>("ContentFrame");
        contentFrame.Content = settingsView;
    }

    public void UpdateCustomSpooder(bool IsInstalling)
    {
        var SpooderStackPanel = this.FindControl<StackPanel>("SpooderStackPanel");
        Debug.WriteLine($"UPDATING CUSTOM SPOODER {IsInstalling}");
        return;
        foreach (var child in SpooderStackPanel.Children)
        {
            if (child is TextBlock textBlock)
            {
                if (CustomSpooder.Parts.ContainsKey(textBlock.Name))
                {
                    textBlock.Text = CustomSpooder.Parts[textBlock.Name];
                    textBlock.Foreground = new SolidColorBrush(Color.Parse(CustomSpooder.Colors[textBlock.Name]));
                }
            }
            else if (child is ContentControl contentControl)
            {
                //TODO: Implement rotating SVG for eyes when installing
                /*if (!IsInstalling)
                {
                    //<Svg Path="avares://SpooderInstallerSharp/Assets/DashedCircle.svg" Width="36" Height="36"/>
                    Debug.WriteLine("GENERATE SVG");
                    
                    var svgPath = new Uri("avares://SpooderInstallerSharp/Assets/DashedCircle.svg");
                    using (var stream = AssetLoader.Open(svgPath))
                    {
                        svg.Load(stream);
                    }
                    using (var surface = SKSurface.Create(new SKImageInfo(64, 64)))
                    {
                        Debug.WriteLine("APPLY SVG");
                        // Get the SKCanvas from the SKSurface
                        SKCanvas canvas = surface.Canvas;

                        // Draw the SVG onto the canvas
                        canvas.DrawPicture(svg.Picture);

                        contentControl.DataContext = canvas;
                    }

                }
                else
                {
                    var textBlockName = "";
                    Debug.WriteLine("CONTENT CONTROL " + contentControl.Name);
                    if (contentControl.Name == "bigeyeleftcontrol")
                    {
                        textBlockName = "bigeyeleft";
                    }
                    else
                    {
                        textBlockName = "bigeyeright";
                    }
                    var swapTextBlock = new TextBlock { Text = CustomSpooder.Parts[textBlockName] };
                    swapTextBlock.Name = textBlockName;
                    swapTextBlock.Foreground = new SolidColorBrush(Color.Parse(CustomSpooder.Colors[swapTextBlock.Name]));
                    contentControl.DataContext = swapTextBlock;
                }*/
            }
        }
    }
}

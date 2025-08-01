using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using SpooderInstallerSharp.JsonTypes;
using SpooderInstallerSharp.Models;
using SpooderInstallerSharp.ViewModels;
using System;
using System.Diagnostics;

namespace SpooderInstallerSharp.Views;

public partial class MainWindow : UserControl
{
    private ConsoleOutput consoleOutput = new ConsoleOutput();
    private Settings settingsView = new Settings();
    private bool settingsOpened = false;
    private bool _initialViewSet = false; // Add this flag

    public MainWindow()
    {
        InitializeComponent();
        Debug.WriteLine($"Need Initialization {SettingsManager.InitializationNeeded}");

        var settingsButton = this.FindControl<ToggleButton>("SettingsButton");

        // Don't set initial view here - DataContext isn't available yet
        // Just determine which view should be shown
        if (SettingsManager.InitializationNeeded)
        {
            Debug.WriteLine("Settings initialization needed, will show settings view.");
            settingsOpened = true; // Set flag but don't show view yet
        }
        else
        {
            Debug.WriteLine("Will show main view.");
            settingsOpened = false; // Set flag but don't show view yet
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ReturnToConsole += (s, e) =>
            {
                Debug.WriteLine("Returning to console from settings view.");
                ShowMainView();
            };

            viewModel._spooder.SpooderThemeChanged += (s, e) =>
            {
                ApplySpooderInfo();
            };

            ApplySpooderInfo();

            // Now that DataContext is set, show the initial view
            if (!_initialViewSet)
            {
                if (settingsOpened)
                {
                    ShowSettingsView();
                }
                else
                {
                    ShowMainView();
                }
                _initialViewSet = true;
            }
        }
    }

    private void OnMainViewClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowMainView();
    }

    private void OnSettingsClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (settingsOpened)
        {
            ShowMainView();
        }
        else
        {
            ShowSettingsView();
        }
    }

    private void ShowMainView()
    {
        Debug.WriteLine("ShowMainView called");
        consoleOutput.DataContext = this.DataContext;
        var contentFrame = this.FindControl<ContentControl>("ContentFrame");
        if (contentFrame != null)
        {
            contentFrame.Content = consoleOutput;
            Debug.WriteLine("ConsoleOutput set as content");
        }
        else
        {
            Debug.WriteLine("ContentFrame not found!");
        }
        settingsOpened = false;
    }

    private void ShowSettingsView()
    {
        Debug.WriteLine("ShowSettingsView called");
        settingsView.DataContext = this.DataContext;
        var contentFrame = this.FindControl<ContentControl>("ContentFrame");
        if (contentFrame != null)
        {
            contentFrame.Content = settingsView;
            Debug.WriteLine("Settings set as content");
        }
        else
        {
            Debug.WriteLine("ContentFrame not found!");
        }
        settingsOpened = true;
    }

    private void ApplySpooderInfo()
    {
        if (DataContext is MainViewModel viewModel)
        {
            if(viewModel._spooder?.spooderInfo?.themeVariables == null)
            {
                Debug.WriteLine("Spooder theme variables are null, cannot apply theme.");
                return;
            }
            var theme = viewModel._spooder.spooderInfo.themeVariables;
            var spooder = viewModel._spooder.spooderInfo;

            var spooderNameBlock = this.FindControl<TextBlock>("SpooderName");
            if (spooderNameBlock != null)
            {
                spooderNameBlock.Text = spooder.name;
            }

            var MainBorder = this.FindControl<Border>("MainBorder");
            var ContentBorder = this.FindControl<Border>("ContentBorder");

            var hue = theme.hue;
            var saturation = theme.saturation;

            if (MainBorder != null)
            {
                MainBorder.Background = BrushFromHsv(hue, saturation, 0.2f);
            }

            if (ContentBorder != null)
            {
                ContentBorder.BorderBrush = BrushFromHsv(hue, saturation, 1.0f);
            }

            ApplyCustomSpooder(spooder.customSpooder);
        }
    }

    private Brush BrushFromHsv(float hue, float saturation, float value)
    {
        // Convert HSV to RGB
        var hueInDegrees = hue * 360.0f;
        var saturationPercent = saturation * 100.0f;
        var valuePercent = value * 100.0f;
        // HSV to RGB conversion
        var h = hueInDegrees / 60.0f;
        var c = valuePercent / 100.0f * saturationPercent / 100.0f;
        var x = c * (1 - Math.Abs((h % 2) - 1));
        var m = valuePercent / 100.0f - c;
        float r, g, b;
        if (h >= 0 && h < 1)
        {
            r = c; g = x; b = 0;
        }
        else if (h >= 1 && h < 2)
        {
            r = x; g = c; b = 0;
        }
        else if (h >= 2 && h < 3)
        {
            r = 0; g = c; b = x;
        }
        else if (h >= 3 && h < 4)
        {
            r = 0; g = x; b = c;
        }
        else if (h >= 4 && h < 5)
        {
            r = x; g = 0; b = c;
        }
        else
        {
            r = c; g = 0; b = x;
        }
        // Add the m value and convert to 0-255 range
        var red = (byte)((r + m) * 255);
        var green = (byte)((g + m) * 255);
        var blue = (byte)((b + m) * 255);
        return new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(red, green, blue));
    }

    private void ApplyCustomSpooder(CustomSpooder customSpooder)
    {
        if (customSpooder?.Parts == null || customSpooder.Parts.Count == 0)
            return;

        // Define the control names in the display order (left to right in the UI)
        var controlNames = new[]
        {
            "longlegleft", "shortlegleft", "bodyleft", "littleeyeleft", "bigeyeleft", 
            "fangleft", "mouth", "fangright", "bigeyeright", "littleeyeright", 
            "bodyright", "shortlegright", "longlegright"
        };

        // Clear all controls first (set to defaults)
        foreach (var controlName in controlNames)
        {
            var textBlock = this.FindControl<TextBlock>(controlName);
            if (textBlock != null)
            {
                textBlock.Text = "";
                textBlock.Foreground = Avalonia.Media.Brush.Parse("#FFFFFF");
            }
        }

        // Apply each part from the array to the corresponding control
        for (int i = 0; i < Math.Min(customSpooder.Parts.Count, controlNames.Length); i++)
        {
            var part = customSpooder.Parts[i];
            var controlName = controlNames[i];
            
            var textBlock = this.FindControl<TextBlock>(controlName);
            if (textBlock != null)
            {
                // Set the part string (character/text)
                textBlock.Text = part.partString ?? "";
                
                // Set the part color
                if (!string.IsNullOrEmpty(part.partColor))
                {
                    try
                    {
                        textBlock.Foreground = Avalonia.Media.Brush.Parse(part.partColor);
                    }
                    catch (Exception ex)
                    {
                        // If color parsing fails, log the error and use default white
                        Debug.WriteLine($"Failed to parse color '{part.partColor}' for part {i}: {ex.Message}");
                        textBlock.Foreground = Avalonia.Media.Brush.Parse("#FFFFFF");
                    }
                }
                else
                {
                    // Default color if none specified
                    textBlock.Foreground = Avalonia.Media.Brush.Parse("#FFFFFF");
                }
            }
        }
    }
}

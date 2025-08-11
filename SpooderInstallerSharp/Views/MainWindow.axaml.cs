using Avalonia;
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
    private readonly ConsoleOutput consoleOutput = new ConsoleOutput();
    private readonly Settings settingsView = new Settings();
    private bool settingsOpened = false;
    private bool _initialViewSet = false;
    private const double BASE_NARROW_THRESHOLD = 400;

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

        if (settingsButton != null)
        {
            settingsButton.SetCurrentValue(ToggleButton.IsCheckedProperty, settingsOpened);
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateUniformGridLayout(e.NewSize.Width);
    }

    private void UpdateUniformGridLayout(double width)
    {
        var uniformGrid = this.FindControl<UniformGrid>("MainUniformGrid");
        var spooderNameBlock = this.FindControl<TextBlock>("SpooderName");
        
        var spooderPetControl = this.FindControl<StackPanel>("SpooderPet");
        var spooderPetWidth = 350;
        
        var processControlsControl = this.FindControl<ProcessControls>("ProcessControls");
        var processControlWidth = 250;

        var spooderNameWidth = 200;

        var totalWidth = spooderPetWidth + spooderNameWidth + processControlWidth + 50;

        if (uniformGrid == null) return;
        if(spooderNameBlock == null) return;

        if (width < totalWidth)
        {
            // Switch to rows for narrow windows
            uniformGrid.Rows = 3;
            uniformGrid.Columns = 0; // Setting to 0 disables column constraint

            // Optionally adjust alignment for vertical layout
            uniformGrid.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            spooderNameBlock.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        }
        else
        {
            // Use columns for wide windows (original layout)
            uniformGrid.Columns = 3;
            uniformGrid.Rows = 0; // Setting to 0 disables row constraint

            // Restore original alignment
            uniformGrid.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            spooderNameBlock.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ReturnToConsole += (s, eventArgs) =>
            {
                Debug.WriteLine("Returning to console from settings view.");
                ShowMainView();
            };

            viewModel._spooder.SpooderThemeChanged += (s, eventArgs) =>
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
                    var appSettings = SettingsManager.LoadSettings();
                    if (appSettings.StartSpooderOnStartup)
                    {
                        viewModel._spooder?.StartSpooder();
                    }
                }
                _initialViewSet = true;
            }
        }
    }

    private void OnMainViewClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowMainView();
    }

    private void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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
        var settingsButton = this.FindControl<ToggleButton>("SettingsButton");
        if (settingsButton != null)
        {
            settingsButton.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
        }
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

            var mainBorder = this.FindControl<Border>("MainBorder");
            var contentBorder = this.FindControl<Border>("ContentBorder");

            var hue = theme.hue;
            var saturation = theme.saturation;

            if (mainBorder != null)
            {
                mainBorder.Background = BrushFromHsv(hue, saturation, 0.2f);
            }

            if (contentBorder != null)
            {
                contentBorder.BorderBrush = BrushFromHsv(hue, saturation, 1.0f);
            }

            if (spooder.customSpooder != null)
            {
                ApplyCustomSpooder(spooder.customSpooder);
            }
        }
    }

    private static Brush BrushFromHsv(float hue, float saturation, float value)
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
        return new SolidColorBrush(Color.FromRgb(red, green, blue));
    }

    private void ApplyCustomSpooder(CustomSpooder customSpooder)
    {
        if (DataContext is MainViewModel viewModel)
        {
            if (viewModel._spooder?.spooderInfo?.themeVariables == null)
            {
                Debug.WriteLine("Spooder theme variables are null, cannot apply theme.");
                return;
            }
            var theme = viewModel._spooder.spooderInfo.themeVariables;
        
            if (customSpooder.Parts.Count == 0)
                return;

            // Define the control names in the display order (left to right in the UI)
            var controlNames = new[]
            {
                "longlegleft", "shortlegleft", "bodyleft", "littleeyeleft", "bigeyeleft",
                "fangleft", "mouth", "fangright", "bigeyeright", "littleeyeright",
                "bodyright", "shortlegright", "longlegright"
            };

            var spooderContainer = this.FindControl<StackPanel>("SpooderPet");
            spooderContainer?.Children.Clear();

            // Apply each part from the array to the corresponding control
            for (int i = 0; i < customSpooder.Parts.Count; i++)
            {
                var part = customSpooder.Parts[i];
                var controlName = controlNames[i];

                var textBlock = new TextBlock();
                textBlock.FontSize = 36;

                if (textBlock != null)
                {
                    textBlock.FontFamily = (FontFamily)Application.Current!.FindResource("RecursiveFont")!;
                    textBlock.FontWeight = (FontWeight)theme.fontWeight;
                    Debug.WriteLine($"Setting font spacing for {controlName} to {theme.letterSpacing}");
                    textBlock.LetterSpacing = theme.letterSpacing * 16;
                    var monoSpaceFeature = new FontFeature() { Tag = "MONO", Value = theme.isMonospacedFont ? 1 : 0 };
                    textBlock.FontFeatures?.Add(monoSpaceFeature);



                    // Set the part string (character/text)
                    textBlock.Text = part.partString ?? "";

                    // Set the part color
                    if (!string.IsNullOrEmpty(part.partColor))
                    {
                        try
                        {
                            textBlock.Foreground = Brush.Parse(part.partColor);
                        }
                        catch (Exception ex)
                        {
                            // If color parsing fails, log the error and use default white
                            Debug.WriteLine($"Failed to parse color '{part.partColor}' for part {i}: {ex.Message}");
                            textBlock.Foreground = Brush.Parse("#FFFFFF");
                        }
                    }
                    else
                    {
                        // Default color if none specified
                        textBlock.Foreground = Brush.Parse("#FFFFFF");
                    }
                    spooderContainer?.Children.Add(textBlock);
                }
            }
        }
    }
}

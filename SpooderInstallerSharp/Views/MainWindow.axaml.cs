using Avalonia.Controls;
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
    public MainWindow()
    {
        InitializeComponent();
        Debug.WriteLine($"Need Initialization {SettingsManager.InitializationNeeded}");
        if (SettingsManager.InitializationNeeded)
        {
            Debug.WriteLine("Settings initialization needed, showing settings view.");
            ShowSettingsView();
        }
        else
        {
            ShowMainView();
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
        consoleOutput.DataContext = this.DataContext;
        var contentFrame = this.FindControl<ContentControl>("ContentFrame");
        contentFrame.Content = consoleOutput;
        settingsOpened = false;
    }

    private void ShowSettingsView()
    {
        settingsView.DataContext = this.DataContext;
        var contentFrame = this.FindControl<ContentControl>("ContentFrame");
        contentFrame.Content = settingsView;
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
        var longlegleftBlock = this.FindControl<TextBlock>("longlegleft");
        if (longlegleftBlock != null)
        {
            longlegleftBlock.Text = customSpooder.parts.longlegleft;
            if (customSpooder.colors?.longlegleft != null)
                longlegleftBlock.Foreground = Avalonia.Media.Brush.Parse(customSpooder.colors.longlegleft);
        }

        var shortlegleftBlock = this.FindControl<TextBlock>("shortlegleft");
        if (shortlegleftBlock != null)
        {
            shortlegleftBlock.Text = customSpooder.parts.shortlegleft;
            if (customSpooder.colors?.shortlegleft != null)
                shortlegleftBlock.Foreground = Avalonia.Media.Brush.Parse(customSpooder.colors.shortlegleft);
        }

        var bodyleftBlock = this.FindControl<TextBlock>("bodyleft");
        if (bodyleftBlock != null)
        {
            bodyleftBlock.Text = customSpooder.parts.bodyleft;
            if (customSpooder.colors?.bodyleft != null)
                bodyleftBlock.Foreground = Avalonia.Media.Brush.Parse(customSpooder.colors.bodyleft);
        }

        var littleeyeleftBlock = this.FindControl<TextBlock>("littleeyeleft");
        if (littleeyeleftBlock != null)
        {
            littleeyeleftBlock.Text = customSpooder.parts.littleeyeleft;
            if (customSpooder.colors?.littleeyeleft != null)
                littleeyeleftBlock.Foreground = Avalonia.Media.Brush.Parse(customSpooder.colors.littleeyeleft);
        }

        var bigeyeleftBlock = this.FindControl<TextBlock>("bigeyeleft");
        if (bigeyeleftBlock != null)
        {
            bigeyeleftBlock.Text = customSpooder.parts.bigeyeleft;
            if (customSpooder.colors?.bigeyeleft != null)
                bigeyeleftBlock.Foreground = Avalonia.Media.Brush.Parse(customSpooder.colors.bigeyeleft);
        }

        var fangleftBlock = this.FindControl<TextBlock>("fangleft");
        if (fangleftBlock != null)
        {
            fangleftBlock.Text = customSpooder.parts.fangleft;
            if (customSpooder.colors?.fangleft != null)
                fangleftBlock.Foreground = Avalonia.Media.Brush.Parse(customSpooder.colors.fangleft);
        }

        var mouthBlock = this.FindControl<TextBlock>("mouth");
        if (mouthBlock != null)
        {
            mouthBlock.Text = customSpooder.parts.mouth;
            if (customSpooder.colors?.mouth != null)
                mouthBlock.Foreground = Avalonia.Media.Brush.Parse(customSpooder.colors.mouth);
        }

        var fangrightBlock = this.FindControl<TextBlock>("fangright");
        if (fangrightBlock != null)
        {
            fangrightBlock.Text = customSpooder.parts.fangright;
            if (customSpooder.colors?.fangright != null)
                fangrightBlock.Foreground = Avalonia.Media.Brush.Parse(customSpooder.colors.fangright);
        }

        var bigeyerightBlock = this.FindControl<TextBlock>("bigeyeright");
        if (bigeyerightBlock != null)
        {
            bigeyerightBlock.Text = customSpooder.parts.bigeyeright;
            if (customSpooder.colors?.bigeyeright != null)
                bigeyerightBlock.Foreground = Avalonia.Media.Brush.Parse(customSpooder.colors.bigeyeright);
        }

        var littleeyerightBlock = this.FindControl<TextBlock>("littleeyeright");
        if (littleeyerightBlock != null)
        {
            littleeyerightBlock.Text = customSpooder.parts.littleeyeright;
            if (customSpooder.colors?.littleeyeright != null)
                littleeyerightBlock.Foreground = Avalonia.Media.Brush.Parse(customSpooder.colors.littleeyeright);
        }

        var bodyrightBlock = this.FindControl<TextBlock>("bodyright");
        if (bodyrightBlock != null)
        {
            bodyrightBlock.Text = customSpooder.parts.bodyright;
            if (customSpooder.colors?.bodyright != null)
                bodyrightBlock.Foreground = Avalonia.Media.Brush.Parse(customSpooder.colors.bodyright);
        }

        var shortlegrightBlock = this.FindControl<TextBlock>("shortlegright");
        if (shortlegrightBlock != null)
        {
            shortlegrightBlock.Text = customSpooder.parts.shortlegright;
            if (customSpooder.colors?.shortlegright != null)
                shortlegrightBlock.Foreground = Avalonia.Media.Brush.Parse(customSpooder.colors.shortlegright);
        }

        var longlegrightBlock = this.FindControl<TextBlock>("longlegright");
        if (longlegrightBlock != null)
        {
            longlegrightBlock.Text = customSpooder.parts.longlegright;
            if (customSpooder.colors?.longlegright != null)
                longlegrightBlock.Foreground = Avalonia.Media.Brush.Parse(customSpooder.colors.longlegright);
        }
    }
}

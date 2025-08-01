using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SpooderInstallerSharp.Views.Controls
{
    public class ClickableTextBlock : Panel
    {
        private static readonly Regex UrlRegex = new Regex(
            @"https?://[^\s]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<ClickableTextBlock, string?>(nameof(Text));

        public static readonly StyledProperty<TextWrapping> TextWrappingProperty =
            AvaloniaProperty.Register<ClickableTextBlock, TextWrapping>(nameof(TextWrapping), TextWrapping.NoWrap);

        public static readonly StyledProperty<IBrush?> ForegroundProperty =
            AvaloniaProperty.Register<ClickableTextBlock, IBrush?>(nameof(Foreground));

        public static readonly StyledProperty<IBrush?> LinkForegroundProperty =
            AvaloniaProperty.Register<ClickableTextBlock, IBrush?>(nameof(LinkForeground), 
                new SolidColorBrush(Color.FromRgb(0, 123, 255)));

        public static readonly StyledProperty<List<string>?> MatchedKeysProperty =
            AvaloniaProperty.Register<ClickableTextBlock, List<string>?>(nameof(MatchedKeys));

        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public TextWrapping TextWrapping
        {
            get => GetValue(TextWrappingProperty);
            set => SetValue(TextWrappingProperty, value);
        }

        public IBrush? Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public IBrush? LinkForeground
        {
            get => GetValue(LinkForegroundProperty);
            set => SetValue(LinkForegroundProperty, value);
        }

        public List<string>? MatchedKeys
        {
            get => GetValue(MatchedKeysProperty);
            set => SetValue(MatchedKeysProperty, value);
        }

        static ClickableTextBlock()
        {
            TextProperty.Changed.AddClassHandler<ClickableTextBlock>((x, e) => x.OnContentChanged());
            ForegroundProperty.Changed.AddClassHandler<ClickableTextBlock>((x, e) => x.OnContentChanged());
            LinkForegroundProperty.Changed.AddClassHandler<ClickableTextBlock>((x, e) => x.OnContentChanged());
            MatchedKeysProperty.Changed.AddClassHandler<ClickableTextBlock>((x, e) => x.OnContentChanged());
        }

        public ClickableTextBlock()
        {
            UpdateContent();
        }

        private void OnContentChanged()
        {
            UpdateContent();
        }

        private void UpdateContent()
        {
            Children.Clear();

            if (string.IsNullOrEmpty(Text))
                return;

            // Apply log style colors first
            var computedForeground = GetStyledForeground();

            var matches = UrlRegex.Matches(Text);
            
            if (matches.Count == 0)
            {
                // No URLs found, create a single selectable text block
                var textBlock = new SelectableTextBlock
                {
                    Text = Text,
                    TextWrapping = TextWrapping,
                    Foreground = computedForeground
                };
                Children.Add(textBlock);
                return;
            }

            // Create a horizontal stack panel to hold mixed content
            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            int lastIndex = 0;
            foreach (Match match in matches)
            {
                // Add text before the URL
                if (match.Index > lastIndex)
                {
                    var beforeText = Text.Substring(lastIndex, match.Index - lastIndex);
                    if (!string.IsNullOrEmpty(beforeText))
                    {
                        var textBlock = new SelectableTextBlock
                        {
                            Text = beforeText,
                            TextWrapping = TextWrapping,
                            Foreground = computedForeground
                        };
                        stackPanel.Children.Add(textBlock);
                    }
                }

                // Add clickable URL
                var linkBlock = new TextBlock
                {
                    Text = match.Value,
                    Foreground = LinkForeground,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    TextDecorations = TextDecorations.Underline,
                    TextWrapping = TextWrapping
                };

                string url = match.Value;
                linkBlock.PointerPressed += (sender, e) =>
                {
                    if (e.GetCurrentPoint(linkBlock).Properties.IsLeftButtonPressed)
                    {
                        OpenUrl(url);
                        e.Handled = true;
                    }
                };

                // Add hover effects
                linkBlock.PointerEntered += (sender, e) =>
                {
                    linkBlock.Opacity = 0.8;
                };

                linkBlock.PointerExited += (sender, e) =>
                {
                    linkBlock.Opacity = 1.0;
                };

                stackPanel.Children.Add(linkBlock);
                lastIndex = match.Index + match.Length;
            }

            // Add remaining text after the last URL
            if (lastIndex < Text.Length)
            {
                var remainingText = Text.Substring(lastIndex);
                if (!string.IsNullOrEmpty(remainingText))
                {
                    var textBlock = new SelectableTextBlock
                    {
                        Text = remainingText,
                        TextWrapping = TextWrapping,
                        Foreground = computedForeground
                    };
                    stackPanel.Children.Add(textBlock);
                }
            }

            Children.Add(stackPanel);
        }

        private IBrush GetStyledForeground()
        {
            // Start with the base foreground
            var foreground = Foreground ?? Brushes.White;

            // Apply log style colors based on matched keys
            if (MatchedKeys != null)
            {
                foreach (var key in MatchedKeys)
                {
                    switch (key)
                    {
                        case "FgBlack":
                            foreground = Brushes.Black;
                            break;
                        case "FgRed":
                            foreground = Brushes.Red;
                            break;
                        case "FgGreen":
                            foreground = Brushes.Green;
                            break;
                        case "FgYellow":
                            foreground = Brushes.Yellow;
                            break;
                        case "FgBlue":
                            foreground = Brushes.Blue;
                            break;
                        case "FgMagenta":
                            foreground = Brushes.Magenta;
                            break;
                        case "FgCyan":
                            foreground = Brushes.Cyan;
                            break;
                        case "FgWhite":
                            foreground = Brushes.White;
                            break;
                        case "FgGray":
                            foreground = Brushes.Gray;
                            break;
                        // Background colors could be handled here as well
                        // by setting the Background property of the panel
                    }
                }
            }

            return foreground;
        }

        private void OpenUrl(string url)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening URL: {ex.Message}");
            }
        }
    }
}
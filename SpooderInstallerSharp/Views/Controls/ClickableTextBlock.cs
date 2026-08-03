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

        public static readonly StyledProperty<string[]?> CssClassesProperty =
            AvaloniaProperty.Register<ClickableTextBlock, string[]?>(nameof(CssClasses));

        // Define CSS class styles - can be extended or moved to a configuration file
        private static readonly Dictionary<string, Action<Control>> CssClassStyles = new Dictionary<string, Action<Control>>
        {
            // Text styling classes
            ["text-bold"] = control => {
                if (control is TextBlock tb) tb.FontWeight = FontWeight.Bold;
                if (control is SelectableTextBlock stb) stb.FontWeight = FontWeight.Bold;
            },
            ["text-italic"] = control => {
                if (control is TextBlock tb) tb.FontStyle = FontStyle.Italic;
                if (control is SelectableTextBlock stb) stb.FontStyle = FontStyle.Italic;
            },
            ["text-underline"] = control => {
                if (control is TextBlock tb) tb.TextDecorations = TextDecorations.Underline;
                if (control is SelectableTextBlock stb) stb.TextDecorations = TextDecorations.Underline;
            },

            // Color classes - using SetValue with appropriate properties
            ["text-red"] = control => {
                if (control is TextBlock tb) tb.Foreground = Brushes.Red;
                if (control is SelectableTextBlock stb) stb.Foreground = Brushes.Red;
            },
            ["text-green"] = control => {
                if (control is TextBlock tb) tb.Foreground = Brushes.Green;
                if (control is SelectableTextBlock stb) stb.Foreground = Brushes.Green;
            },
            ["text-blue"] = control => {
                if (control is TextBlock tb) tb.Foreground = Brushes.Blue;
                if (control is SelectableTextBlock stb) stb.Foreground = Brushes.Blue;
            },
            ["text-yellow"] = control => {
                if (control is TextBlock tb) tb.Foreground = Brushes.Yellow;
                if (control is SelectableTextBlock stb) stb.Foreground = Brushes.Yellow;
            },
            ["text-orange"] = control => {
                if (control is TextBlock tb) tb.Foreground = Brushes.Orange;
                if (control is SelectableTextBlock stb) stb.Foreground = Brushes.Orange;
            },
            ["text-purple"] = control => {
                if (control is TextBlock tb) tb.Foreground = Brushes.Purple;
                if (control is SelectableTextBlock stb) stb.Foreground = Brushes.Purple;
            },
            ["text-cyan"] = control => {
                if (control is TextBlock tb) tb.Foreground = Brushes.Cyan;
                if (control is SelectableTextBlock stb) stb.Foreground = Brushes.Cyan;
            },
            ["text-white"] = control => {
                if (control is TextBlock tb) tb.Foreground = Brushes.White;
                if (control is SelectableTextBlock stb) stb.Foreground = Brushes.White;
            },
            ["text-gray"] = control => {
                if (control is TextBlock tb) tb.Foreground = Brushes.Gray;
                if (control is SelectableTextBlock stb) stb.Foreground = Brushes.Gray;
            },
            ["text-black"] = control => {
                if (control is TextBlock tb) tb.Foreground = Brushes.Black;
                if (control is SelectableTextBlock stb) stb.Foreground = Brushes.Black;
            },

            // Background classes - using SetValue with appropriate properties
            ["bg-red"] = control => {
                if (control is TextBlock tb) tb.Background = Brushes.Red;
                if (control is SelectableTextBlock stb) stb.Background = Brushes.Red;
            },
            ["bg-green"] = control => {
                if (control is TextBlock tb) tb.Background = Brushes.Green;
                if (control is SelectableTextBlock stb) stb.Background = Brushes.Green;
            },
            ["bg-blue"] = control => {
                if (control is TextBlock tb) tb.Background = Brushes.Blue;
                if (control is SelectableTextBlock stb) stb.Background = Brushes.Blue;
            },
            ["bg-yellow"] = control => {
                if (control is TextBlock tb) tb.Background = Brushes.Yellow;
                if (control is SelectableTextBlock stb) stb.Background = Brushes.Yellow;
            },
            ["bg-orange"] = control => {
                if (control is TextBlock tb) tb.Background = Brushes.Orange;
                if (control is SelectableTextBlock stb) stb.Background = Brushes.Orange;
            },
            ["bg-purple"] = control => {
                if (control is TextBlock tb) tb.Background = Brushes.Purple;
                if (control is SelectableTextBlock stb) stb.Background = Brushes.Purple;
            },
            ["bg-cyan"] = control => {
                if (control is TextBlock tb) tb.Background = Brushes.Cyan;
                if (control is SelectableTextBlock stb) stb.Background = Brushes.Cyan;
            },
            ["bg-white"] = control => {
                if (control is TextBlock tb) tb.Background = Brushes.White;
                if (control is SelectableTextBlock stb) stb.Background = Brushes.White;
            },
            ["bg-gray"] = control => {
                if (control is TextBlock tb) tb.Background = Brushes.Gray;
                if (control is SelectableTextBlock stb) stb.Background = Brushes.Gray;
            },
            ["bg-black"] = control => {
                if (control is TextBlock tb) tb.Background = Brushes.Black;
                if (control is SelectableTextBlock stb) stb.Background = Brushes.Black;
            },

            // Semantic classes
            ["error"] = control => {
                if (control is TextBlock tb) {
                    tb.Foreground = Brushes.Red;
                    tb.FontWeight = FontWeight.Bold;
                }
                if (control is SelectableTextBlock stb) {
                    stb.Foreground = Brushes.Red;
                    stb.FontWeight = FontWeight.Bold;
                }
            },
            ["warning"] = control => {
                if (control is TextBlock tb) {
                    tb.Foreground = Brushes.Orange;
                    tb.FontWeight = FontWeight.SemiBold;
                }
                if (control is SelectableTextBlock stb) {
                    stb.Foreground = Brushes.Orange;
                    stb.FontWeight = FontWeight.SemiBold;
                }
            },
            ["success"] = control => {
                if (control is TextBlock tb) {
                    tb.Foreground = Brushes.Green;
                    tb.FontWeight = FontWeight.SemiBold;
                }
                if (control is SelectableTextBlock stb) {
                    stb.Foreground = Brushes.Green;
                    stb.FontWeight = FontWeight.SemiBold;
                }
            },
            ["info"] = control => {
                if (control is TextBlock tb) tb.Foreground = Brushes.CornflowerBlue;
                if (control is SelectableTextBlock stb) stb.Foreground = Brushes.CornflowerBlue;
            },
            ["debug"] = control => {
                if (control is TextBlock tb) {
                    tb.Foreground = Brushes.Gray;
                    tb.FontStyle = FontStyle.Italic;
                }
                if (control is SelectableTextBlock stb) {
                    stb.Foreground = Brushes.Gray;
                    stb.FontStyle = FontStyle.Italic;
                }
            }
        };

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

        public string[]? CssClasses
        {
            get => GetValue(CssClassesProperty);
            set => SetValue(CssClassesProperty, value);
        }

        static ClickableTextBlock()
        {
            TextProperty.Changed.AddClassHandler<ClickableTextBlock>((x, e) => x.OnContentChanged());
            ForegroundProperty.Changed.AddClassHandler<ClickableTextBlock>((x, e) => x.OnContentChanged());
            LinkForegroundProperty.Changed.AddClassHandler<ClickableTextBlock>((x, e) => x.OnContentChanged());
            MatchedKeysProperty.Changed.AddClassHandler<ClickableTextBlock>((x, e) => x.OnContentChanged());
            CssClassesProperty.Changed.AddClassHandler<ClickableTextBlock>((x, e) => x.OnContentChanged());
        }

        public ClickableTextBlock(string? initialText = null, string[]? cssClasses = null)
        {
            if (!string.IsNullOrEmpty(initialText))
            {
                Text = initialText;
            }
            
            if (cssClasses != null)
            {
                CssClasses = cssClasses;
            }
            
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

            // Debug: Log CSS classes being applied
            if (CssClasses != null && CssClasses.Length > 0)
            {
                Debug.WriteLine($"ClickableTextBlock applying CSS classes: [{string.Join(", ", CssClasses)}] to text: '{Text}'");
            }
            else
            {
                Debug.WriteLine($"ClickableTextBlock: No CSS classes for text: '{Text}'");
            }

            // Apply styles - CSS classes take precedence over ANSI colors
            var computedStyles = GetComputedStyles();

            // Debug: Log computed styles
            Debug.WriteLine($"ClickableTextBlock computed styles - Foreground: {computedStyles.Foreground}, FontWeight: {computedStyles.FontWeight}");

            var matches = UrlRegex.Matches(Text);
            
            if (matches.Count == 0)
            {
                // No URLs found, create a single selectable text block
                var textBlock = new SelectableTextBlock
                {
                    Text = Text,
                    TextWrapping = TextWrapping
                };
                
                ApplyStylesToControl(textBlock, computedStyles);
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
                            TextWrapping = TextWrapping
                        };
                        
                        ApplyStylesToControl(textBlock, computedStyles);
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
                        TextWrapping = TextWrapping
                    };
                    
                    ApplyStylesToControl(textBlock, computedStyles);
                    stackPanel.Children.Add(textBlock);
                }
            }

            Children.Add(stackPanel);
        }

        private ComputedStyles GetComputedStyles()
        {
            var styles = new ComputedStyles();

            // First apply ANSI color styles (lower priority)
            if (MatchedKeys != null)
            {
                foreach (var key in MatchedKeys)
                {
                    switch (key)
                    {
                        case "FgBlack":
                            styles.Foreground = Brushes.Black;
                            break;
                        case "FgRed":
                            styles.Foreground = Brushes.Red;
                            break;
                        case "FgGreen":
                            styles.Foreground = Brushes.Green;
                            break;
                        case "FgYellow":
                            styles.Foreground = Brushes.Yellow;
                            break;
                        case "FgBlue":
                            styles.Foreground = Brushes.Blue;
                            break;
                        case "FgMagenta":
                            styles.Foreground = Brushes.Magenta;
                            break;
                        case "FgCyan":
                            styles.Foreground = Brushes.Cyan;
                            break;
                        case "FgWhite":
                            styles.Foreground = Brushes.White;
                            break;
                        case "FgGray":
                            styles.Foreground = Brushes.Gray;
                            break;
                        case "BgBlack":
                            styles.Background = Brushes.Black;
                            break;
                        case "BgRed":
                            styles.Background = Brushes.Red;
                            break;
                        case "BgGreen":
                            styles.Background = Brushes.Green;
                            break;
                        case "BgYellow":
                            styles.Background = Brushes.Yellow;
                            break;
                        case "BgBlue":
                            styles.Background = Brushes.Blue;
                            break;
                        case "BgMagenta":
                            styles.Background = Brushes.Magenta;
                            break;
                        case "BgCyan":
                            styles.Background = Brushes.Cyan;
                            break;
                        case "BgWhite":
                            styles.Background = Brushes.White;
                            break;
                        case "BgGray":
                            styles.Background = Brushes.Gray;
                            break;
                        case "Bright":
                            styles.FontWeight = FontWeight.Bold;
                            break;
                        case "Underscore":
                            styles.TextDecorations = TextDecorations.Underline;
                            break;
                    }
                }
            }

            // Then apply CSS classes (higher priority - will override ANSI styles)
            if (CssClasses != null)
            {
                Debug.WriteLine($"GetComputedStyles: Processing {CssClasses.Length} CSS classes");
                foreach (var cssClass in CssClasses)
                {
                    Debug.WriteLine($"GetComputedStyles: Processing CSS class '{cssClass}'");
                    if (CssClassStyles.TryGetValue(cssClass, out var styleAction))
                    {
                        Debug.WriteLine($"GetComputedStyles: Found style action for '{cssClass}'");
                        // Apply styles to a temporary control to extract values
                        var tempControl = new SelectableTextBlock(); // Use SelectableTextBlock for consistency
                        styleAction(tempControl);
                        
                        // Extract the applied styles - check the actual control properties
                        if (tempControl.Foreground != null && tempControl.Foreground != Brushes.Black)
                        {
                            styles.Foreground = tempControl.Foreground;
                            Debug.WriteLine($"GetComputedStyles: Set foreground from '{cssClass}' to {tempControl.Foreground}");
                        }
                        if (tempControl.Background != null && tempControl.Foreground != Brushes.Black)
                        {
                            styles.Background = tempControl.Background;
                            Debug.WriteLine($"GetComputedStyles: Set background from '{cssClass}' to {tempControl.Background}");
                        }
                        
                        // Extract font properties
                        styles.FontWeight = tempControl.FontWeight;
                        styles.FontStyle = tempControl.FontStyle;
                        styles.TextDecorations = tempControl.TextDecorations;
                        
                        Debug.WriteLine($"GetComputedStyles: FontWeight: {styles.FontWeight}, FontStyle: {styles.FontStyle}");
                    }
                    else
                    {
                        Debug.WriteLine($"GetComputedStyles: No style action found for CSS class '{cssClass}'");
                    }
                }
            }

            // Apply base foreground if no other foreground was set
            if (styles.Foreground == null)
            {
                styles.Foreground = Foreground ?? Brushes.White;
                Debug.WriteLine($"GetComputedStyles: Using default foreground: {styles.Foreground}");
            }

            return styles;
        }

        private void ApplyStylesToControl(Control control, ComputedStyles styles)
        {
            Debug.WriteLine($"ApplyStylesToControl: Applying styles to {control.GetType().Name}");
            
            // Apply styles based on control type
            if (control is TextBlock textBlock)
            {
                if (styles.Foreground != null)
                {
                    textBlock.Foreground = styles.Foreground;
                    Debug.WriteLine($"ApplyStylesToControl: Set TextBlock foreground to {styles.Foreground}");
                }
                
                if (styles.Background != null)
                {
                    textBlock.Background = styles.Background;
                    Debug.WriteLine($"ApplyStylesToControl: Set TextBlock background to {styles.Background}");
                }

                if (styles.FontWeight.HasValue)
                {
                    textBlock.FontWeight = styles.FontWeight.Value;
                    Debug.WriteLine($"ApplyStylesToControl: Set TextBlock font weight to {styles.FontWeight.Value}");
                }
                if (styles.FontStyle.HasValue)
                {
                    textBlock.FontStyle = styles.FontStyle.Value;
                    Debug.WriteLine($"ApplyStylesToControl: Set TextBlock font style to {styles.FontStyle.Value}");
                }
                if (styles.TextDecorations != null)
                {
                    textBlock.TextDecorations = styles.TextDecorations;
                    Debug.WriteLine($"ApplyStylesToControl: Set TextBlock text decorations to {styles.TextDecorations}");
                }
            }
            else if (control is SelectableTextBlock selectableTextBlock)
            {
                if (styles.Foreground != null)
                {
                    selectableTextBlock.Foreground = styles.Foreground;
                    Debug.WriteLine($"ApplyStylesToControl: Set SelectableTextBlock foreground to {styles.Foreground}");
                }
                
                if (styles.Background != null)
                {
                    selectableTextBlock.Background = styles.Background;
                    Debug.WriteLine($"ApplyStylesToControl: Set SelectableTextBlock background to {styles.Background}");
                }

                if (styles.FontWeight.HasValue)
                {
                    selectableTextBlock.FontWeight = styles.FontWeight.Value;
                    Debug.WriteLine($"ApplyStylesToControl: Set SelectableTextBlock font weight to {styles.FontWeight.Value}");
                }
                if (styles.FontStyle.HasValue)
                {
                    selectableTextBlock.FontStyle = styles.FontStyle.Value;
                    Debug.WriteLine($"ApplyStylesToControl: Set SelectableTextBlock font style to {styles.FontStyle.Value}");
                }
                if (styles.TextDecorations != null)
                {
                    selectableTextBlock.TextDecorations = styles.TextDecorations;
                    Debug.WriteLine($"ApplyStylesToControl: Set SelectableTextBlock text decorations to {styles.TextDecorations}");
                }
            }
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

        // Helper class to store computed styles
        private class ComputedStyles
        {
            public IBrush? Foreground { get; set; }
            public IBrush? Background { get; set; }
            public FontWeight? FontWeight { get; set; }
            public FontStyle? FontStyle { get; set; }
            public TextDecorationCollection? TextDecorations { get; set; }
        }
    }
}
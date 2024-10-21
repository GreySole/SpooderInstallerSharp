using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;

namespace SpooderInstallerSharp.Views
{
    static class ColorUtil
    {
        public static JsonObject LogEffects { get; } = new JsonObject
        {
            { "Reset", "\x1b[0m" },
            { "Bright", "\x1b[1m" },
            { "Dim", "\x1b[2m" },
            { "Underscore", "\x1b[4m" },
            { "Blink", "\x1b[5m" },
            { "Reverse", "\x1b[7m" },
            { "Hidden", "\x1b[8m" },
            { "FgBlack", "\x1b[30m" },
            { "FgRed", "\x1b[31m" },
            { "FgGreen", "\x1b[32m" },
            { "FgYellow", "\x1b[33m" },
            { "FgBlue", "\x1b[34m" },
            { "FgMagenta", "\x1b[35m" },
            { "FgCyan", "\x1b[36m" },
            { "FgWhite", "\x1b[37m" },
            { "FgGray", "\x1b[90m" },
            { "BgBlack", "\x1b[40m" },
            { "BgRed", "\x1b[41m" },
            { "BgGreen", "\x1b[42m" },
            { "BgYellow", "\x1b[43m" },
            { "BgBlue", "\x1b[44m" },
            { "BgMagenta", "\x1b[45m" },
            { "BgCyan", "\x1b[46m" },
            { "BgWhite", "\x1b[47m" },
            { "BgGray", "\x1b[100m" }
        };

        public static void ApplyLogStyle(TextBlock textBlock, List<string> matchedKeys)
        {
            // Set colors based on matched keys
            foreach (var key in matchedKeys)
            {
                switch (key)
                {
                    case "FgBlack":
                        textBlock.Foreground = Brushes.Black;
                        break;
                    case "FgRed":
                        textBlock.Foreground = Brushes.Red;
                        break;
                    case "FgGreen":
                        textBlock.Foreground = Brushes.Green;
                        break;
                    case "FgYellow":
                        textBlock.Foreground = Brushes.Yellow;
                        break;
                    case "FgBlue":
                        textBlock.Foreground = Brushes.Blue;
                        break;
                    case "FgMagenta":
                        textBlock.Foreground = Brushes.Magenta;
                        break;
                    case "FgCyan":
                        textBlock.Foreground = Brushes.Cyan;
                        break;
                    case "FgWhite":
                        textBlock.Foreground = Brushes.White;
                        break;
                    case "FgGray":
                        textBlock.Foreground = Brushes.Gray;
                        break;
                    case "BgBlack":
                        textBlock.Background = Brushes.Black;
                        break;
                    case "BgRed":
                        textBlock.Background = Brushes.Red;
                        break;
                    case "BgGreen":
                        textBlock.Background = Brushes.Green;
                        break;
                    case "BgYellow":
                        textBlock.Background = Brushes.Yellow;
                        break;
                    case "BgBlue":
                        textBlock.Background = Brushes.Blue;
                        break;
                    case "BgMagenta":
                        textBlock.Background = Brushes.Magenta;
                        break;
                    case "BgCyan":
                        textBlock.Background = Brushes.Cyan;
                        break;
                    case "BgWhite":
                        textBlock.Background = Brushes.White;
                        break;
                    case "BgGray":
                        textBlock.Background = Brushes.Gray;
                        break;
                }
            }
        }

        public static uint ConvertHexToUInt(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor) || hexColor[0] != '#' || hexColor.Length != 7)
            {
                throw new ArgumentException("Invalid color code format. Expected format is #RRGGBB.");
            }

            return uint.Parse(hexColor.Substring(1), NumberStyles.HexNumber);
        }
    }
}

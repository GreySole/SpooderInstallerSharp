using System.Collections.Generic;

namespace SpooderInstallerSharp.Views
{
    static class CustomSpooder
    {
        public static Dictionary<string, string> Parts { get; } = new()
            {
                { "bigeyeleft", "o" },
                { "bigeyeright", "o" },
                { "littleeyeleft", "º" },
                { "littleeyeright", "º" },
                { "fangleft", " " },
                { "fangright", " " },
                { "mouth", "ω" },
                { "bodyleft", "(" },
                { "bodyright", ")" },
                { "shortlegleft", "/\\" },
                { "longlegleft", "/╲" },
                { "shortlegright", "/\\" },
                { "longlegright", "╱\\" }
        };

        public static Dictionary<string, string> Colors = new()
            {
                { "bigeyeleft", "#ffff00" },
                { "bigeyeright", "#ffff00" },
                { "littleeyeleft", "#969900" },
                { "littleeyeright", "#969900" },
                { "fangleft", "#ffffff" },
                { "fangright", "#ffffff" },
                { "mouth", "#ffffb8" },
                { "shortlegleft", "#615400" },
                { "shortlegright", "#615400" },
                { "longlegleft", "#fff700" },
                { "longlegright", "#fff700" },
                { "bodyleft", "#feffb8" },
                { "bodyright", "#feffb8" }
            };
    }
}

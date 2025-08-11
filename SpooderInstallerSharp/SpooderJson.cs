using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.JsonTypes
{
    public class SpooderInfo
    {
        public string? name { get; set; }
        public string? version { get; set; }
        public CustomSpooder? customSpooder { get; set; }
        public SpooderTheme? themeVariables { get; set; }
        public int? host_port { get; set; }
    }

    public class PackageJson
    {
        public string? name { get; set; }
        public string? version { get; set; }
        // Add other properties as needed
    }

    public class SpooderTheme
    {
        public float hue { get; set; }
        public float saturation { get; set; }
        public bool isDarkTheme { get; set; }
    }

    public class CustomSpooder
    {
        public List<SpooderPart> Parts { get; set; } = new List<SpooderPart>();
    }

    public class SpooderPart
    {
        public string partString { get; set; } = string.Empty;
        public string partColor { get; set; } = string.Empty;
    }
}

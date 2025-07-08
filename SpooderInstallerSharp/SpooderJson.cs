using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.JsonTypes
{
    public class SpooderInfo
    {
        public string name { get; set; }
        public string version { get; set; }
        public CustomSpooder customSpooder { get; set; }
        public SpooderTheme themeVariables { get; set; }
    }

    public class PackageJson
    {
        public string name { get; set; }
        public string version { get; set; }
        // Add other properties as needed
    }

    public class SpooderTheme
    {
        public float hue { get; set; }
        public float saturation { get; set; }
        public Boolean isDarkTheme { get; set; }
    }

    public class CustomSpooder
    {
        public CustomSpooderParts parts { get; set; }
        public CustomSpooderParts colors { get; set; }
    }

    public class CustomSpooderParts
    {
        public string bigeyeleft { get; set; }
        public string bigeyeright { get; set; }
        public string littleeyeleft { get; set; }
        public string littleeyeright { get; set; }
        public string fangleft { get; set; }
        public string fangright { get; set; }
        public string mouth { get; set; }
        public string bodyleft { get; set; }
        public string bodyright { get; set; }
        public string shortlegleft { get; set; }
        public string shortlegright { get; set; }
        public string longlegleft { get; set; }
        public string longlegright { get; set; }
    }
}

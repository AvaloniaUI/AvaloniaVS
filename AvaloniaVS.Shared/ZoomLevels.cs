using System.Globalization;

namespace AvaloniaVS;

internal static class ZoomLevels
{
    public static string FmtZoomLevel(double v) => $"{v.ToString(CultureInfo.InvariantCulture)}%";

    public static readonly string[] Levels = new string[]
        {
            FmtZoomLevel(800), FmtZoomLevel(400), FmtZoomLevel(200), FmtZoomLevel(150), FmtZoomLevel(100),
            FmtZoomLevel(66.67), FmtZoomLevel(50), FmtZoomLevel(33.33), FmtZoomLevel(25), FmtZoomLevel(12.5),
            "Fit to Width",
            "Fit All",
        };
}

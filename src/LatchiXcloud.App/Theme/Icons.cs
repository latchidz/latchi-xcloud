using System.Windows.Media;

namespace LatchiXcloud.App.Theme;

/// <summary>Monochrome vector icons (24×24, stroke-based) — no emoji, no bitmaps.</summary>
public static class Icons
{
    private static Geometry G(string data)
    {
        var g = Geometry.Parse(data);
        g.Freeze();
        return g;
    }

    public static readonly Geometry Cloud = G("M7 18.5h10a4 4 0 0 0 .6-7.95A6 6 0 0 0 6.1 9.6 4.2 4.2 0 0 0 7 18.5z");
    public static readonly Geometry Play = G("M8 5.2l11.5 6.8L8 18.8z");
    public static readonly Geometry Home = G("M4 11l8-7.5L20 11 M6.5 10v9.5h4v-5.5h3v5.5h4V10");
    public static readonly Geometry Reload = G("M20 12a8 8 0 1 1-2.5-5.8 M18.2 2.8v4h-4");
    public static readonly Geometry Fullscreen = G("M4 9.5V4h5.5 M20 9.5V4h-5.5 M4 14.5V20h5.5 M20 14.5V20h-5.5");
    public static readonly Geometry ExitFullscreen = G("M9.5 4v5.5H4 M14.5 4v5.5H20 M9.5 20v-5.5H4 M14.5 20v-5.5H20");
    public static readonly Geometry Settings = G("M4 8h9 M17 8h3 M15 5.8v4.4 M4 16h3 M11 16h9 M9 13.8v4.4");
    public static readonly Geometry Pulse = G("M3 12h4l2.5-6 4 12 2.5-6h5");
    public static readonly Geometry Gamepad = G("M6.5 8.5h11a4.5 4.5 0 0 1 0 9c-1.6 0-2.4-.9-3.4-.9H9.9c-1 0-1.8.9-3.4.9a4.5 4.5 0 0 1 0-9z M9 11.5v2.5 M7.75 12.75h2.5 M15.5 12.75v.01 M17.5 12.75v.01");
    public static readonly Geometry Update = G("M20 12a8 8 0 1 1-2.5-5.8 M18.2 2.8v4h-4");
    public static readonly Geometry Folder = G("M3.5 6.5h6l2 2.5h9v10h-17z");
    public static readonly Geometry Trash = G("M5 7h14 M9.5 7V5h5v2 M7 7l1 13h8l1-13");
    public static readonly Geometry External = G("M14 4h6v6 M20 4l-9 9 M19 13v6.5H4.5V5H11");
    public static readonly Geometry Check = G("M5 12.5l4.5 4.5L19 7");
    public static readonly Geometry Warn = G("M12 4l9 16H3z M12 10.5v4.5 M12 17.6v.01");
    public static readonly Geometry Info = G("M12 3.5a8.5 8.5 0 1 0 0 17 8.5 8.5 0 0 0 0-17z M12 11v5.5 M12 7.8v.01");
    public static readonly Geometry Close = G("M6 6l12 12 M18 6L6 18");
    public static readonly Geometry Minimize = G("M5 12h14");
    public static readonly Geometry Maximize = G("M5.5 5.5h13v13h-13z");
    public static readonly Geometry Restore = G("M5.5 8.5h10v10h-10z M8.5 8.5V5.5h10v10h-3");
    public static readonly Geometry Globe = G("M12 3.5a8.5 8.5 0 1 0 0 17 8.5 8.5 0 0 0 0-17z M3.5 12h17 M12 3.5c-4 4.7-4 12.3 0 17 M12 3.5c4 4.7 4 12.3 0 17");
    public static readonly Geometry Back = G("M14.5 5.5L8 12l6.5 6.5");
}

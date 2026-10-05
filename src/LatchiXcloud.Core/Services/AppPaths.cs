namespace LatchiXcloud.Core.Services;

/// <summary>
/// All LATCHI xCLOUD data lives under %LOCALAPPDATA%\LATCHI\xCLOUD — a dedicated, isolated
/// profile that never touches the user's Chrome/Edge data.
/// </summary>
public static class AppPaths
{
    public static string DataDir =>
        Environment.GetEnvironmentVariable("LATCHI_XCLOUD_DATA")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LATCHI", "xCLOUD");

    public static string BrowserProfileDir => Path.Combine(DataDir, "BrowserProfile");
    public static string BetterXcloudDir => Path.Combine(DataDir, "BetterXCloud");
    public static string ActiveScriptDir => Path.Combine(BetterXcloudDir, "active");
    public static string PreviousScriptDir => Path.Combine(BetterXcloudDir, "previous");
    public static string UpdatesDir => Path.Combine(DataDir, "Updates");
    public static string SettingsDir => Path.Combine(DataDir, "Settings");
    public static string LogsDir => Path.Combine(DataDir, "Logs");

    public static string ActiveScriptPath => Path.Combine(ActiveScriptDir, "better-xcloud.user.js");
    public static string ActiveManifestPath => Path.Combine(ActiveScriptDir, "manifest.json");

    public static void EnsureDataDir()
    {
        foreach (var d in new[] { DataDir, BrowserProfileDir, ActiveScriptDir, PreviousScriptDir, UpdatesDir, SettingsDir, LogsDir })
            Directory.CreateDirectory(d);
    }
}

namespace LatchiXcloud.Core.Models;

/// <summary>Host application settings (Better xCloud manages its own settings inside the page).</summary>
public sealed class AppSettings
{
    // Application
    public bool StartMaximized { get; set; } = true;
    public bool StartFullscreen { get; set; } = false;     // v1.0.1: windowed-maximized WITH the standard buttons, by default
    public string Language { get; set; } = "ar";           // "ar" | "en"
    public bool StartOnHome { get; set; } = true;          // always open xCloud home

    // v1.0 first-run wizard: language → Microsoft sign-in → cloud
    public bool FirstRunComplete { get; set; } = false;
    public string KeepSessionOnExit { get; set; } = "ask"; // "ask" | "keep" | "signout"

    // Performance
    public bool LowEndMode { get; set; } = false;          // 4 GB RAM profile
    public bool BackgroundThrottling { get; set; } = true; // let Chromium throttle when hidden

    // Better xCloud
    public bool AutoUpdateBetterXcloud { get; set; } = true;

    // Window persistence
    public double WindowLeft { get; set; } = 0;
    public double WindowTop { get; set; } = 0;
    public double WindowWidth { get; set; } = 0;
    public double WindowHeight { get; set; } = 0;
    public bool WindowMaximized { get; set; }
}

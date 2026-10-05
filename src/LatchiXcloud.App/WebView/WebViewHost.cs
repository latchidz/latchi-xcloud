using System.Text.Json;
using LatchiXcloud.Core.Security;
using LatchiXcloud.Core.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LatchiXcloud.App.WebView;

/// <summary>
/// Owns the single WebView2 instance (one browser instance for the whole app lifetime —
/// the session, Better xCloud settings and xCloud state live as long as the app runs).
/// Implements the host security model:
///   • top-level navigation restricted to the Microsoft/Xbox allowlist (§31)
///   • popups never spawn extra browser windows (§32)
///   • downloads always cancelled (§33)
///   • permissions denied by default; microphone allowed only for xbox.com (§34)
///   • no host objects are ever exposed to remote content (§35)
/// </summary>
public sealed class WebViewHost
{
    private readonly WebView2 _view;
    private bool _initialized;

    /// <summary>Raised for every validated web message from the loader bridge.</summary>
    public event Action<string, string?>? MessageReceived; // (type, detail)

    /// <summary>Raised when a top-level navigation was blocked by the policy.</summary>
    public event Action<string>? NavigationBlocked;

    /// <summary>Raised when the browser process died (renderer/browser crash).</summary>
    public event Action? ProcessFailed;

    public CoreWebView2? Core => _initialized ? _view.CoreWebView2 : null;
    public string RuntimeVersion { get; private set; } = "";

    public WebViewHost(WebView2 view) => _view = view;

    /// <summary>Creates the environment with the dedicated profile and wires all events.</summary>
    public async Task InitializeAsync(string loaderJs)
    {
        if (_initialized) return;

        AppPaths.EnsureDataDir();
        // dedicated, isolated profile — never the user's Chrome/Edge data (§14)
        var env = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: AppPaths.BrowserProfileDir,
            options: null);
        RuntimeVersion = env.BrowserVersionString;
        Logger.Info("WebView2 environment created — runtime " + RuntimeVersion +
                    ", profile " + AppPaths.BrowserProfileDir);

        await _view.EnsureCoreWebView2Async(env);
        var core = _view.CoreWebView2;
        _initialized = true;

        // ── host app behaviour, NOT browser features ──
        core.Settings.AreDevToolsEnabled = false;                 // no dev console in a player app
        core.Settings.IsBuiltInErrorPageEnabled = false;          // the host error screen replaces it
        core.Settings.IsStatusBarEnabled = false;                 // no browser chrome
        core.Settings.AreDefaultContextMenusEnabled = true;       // keeps right-click Paste on MS login fields
        core.Settings.IsZoomControlEnabled = true;                // Ctrl+wheel helps readability on login
        core.Settings.AreBrowserAcceleratorKeysEnabled = true;    // F5/Ctrl+R behave as expected
        core.Settings.IsNonClientRegionSupportEnabled = false;    // don't let pages fake titlebar regions

        // ── document-start injection: loader + Better xCloud (§17) ──
        await core.AddScriptToExecuteOnDocumentCreatedAsync(loaderJs);
        Logger.Info("Loader script registered (bridge + Better xCloud, document-start)");

        // ── security events ──
        core.NavigationStarting += (s, e) =>
        {
            if (!NavigationPolicy.IsAllowed(e.Uri))
            {
                e.Cancel = true;
                Logger.Warn("Blocked navigation: " + Logger.SafeUrl(e.Uri));
                NavigationBlocked?.Invoke(e.Uri);
            }
        };

        core.NewWindowRequested += (s, e) =>
        {
            // never allow extra browser windows; allowlisted popups become
            // same-window navigations so Microsoft auth stays inside the session
            e.Handled = true;
            if (NavigationPolicy.IsAllowed(e.Uri))
            {
                Logger.Info("Popup routed in-app: " + Logger.SafeUrl(e.Uri));
                core.Navigate(e.Uri);
            }
            else
            {
                Logger.Warn("Blocked popup: " + Logger.SafeUrl(e.Uri));
            }
        };

        core.DownloadStarting += (s, e) =>
        {
            e.Cancel = true; // this app never downloads arbitrary files (§33)
            Logger.Warn("Blocked download: " + (e.DownloadOperation?.ResultFilePath is { } p ? Path.GetFileName(p) : "(unknown)"));
        };

        core.PermissionRequested += (s, e) =>
        {
            var allowed = e.PermissionKind == CoreWebView2PermissionKind.Microphone
                          && e.Uri is not null
                          && NavigationPolicyIsXbox(e.Uri);
            e.State = allowed ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
            if (!allowed) Logger.Warn("Denied permission " + e.PermissionKind + " for " + Logger.SafeUrl(e.Uri));
        };

        core.WebMessageReceived += (s, e) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                if (!doc.RootElement.TryGetProperty("type", out var t)) return;
                var type = t.GetString();
                if (type is null) return;
                string? detail = doc.RootElement.TryGetProperty("detail", out var d) ? d.GetString() : null;
                // the loader bridge sends {type:'hotkey', key:'F11'} — 'key' is the hotkey payload
                if (detail is null && doc.RootElement.TryGetProperty("key", out var k)) detail = k.GetString();
                if (type == "bxc-error") detail = doc.RootElement.TryGetProperty("error", out var err) ? err.GetString() : null;
                MessageReceived?.Invoke(type, detail);
            }
            catch (Exception ex)
            {
                Logger.Warn("Invalid web message ignored: " + ex.Message);
            }
        };

        core.ProcessFailed += (s, e) =>
        {
            Logger.Error("WebView2 process failed: " + e.ProcessFailedKind);
            ProcessFailed?.Invoke();
        };

        core.NavigationCompleted += (s, e) =>
        {
            if (!e.IsSuccess)
                Logger.Warn("Navigation failed (hr=0x" + e.WebErrorStatus + "): " + Logger.SafeUrl(core.Source?.ToString()));
        };
    }

    private static bool NavigationPolicyIsXbox(string uri)
    {
        try
        {
            var host = new Uri(uri).Host.ToLowerInvariant();
            return host == "xbox.com" || host.EndsWith(".xbox.com", StringComparison.Ordinal);
        }
        catch { return false; }
    }

    /// <summary>Navigate to the Xbox Cloud Gaming home.</summary>
    public void GoHome()
    {
        if (Core is { } core) core.Navigate("https://www.xbox.com/play");
    }

    public void Reload()
    {
        if (Core is { } core) core.Reload();
    }

    /// <summary>Runs the loader's probe: is Better xCloud present in the current document?</summary>
    public async Task<bool> ProbeBetterXcloudAsync()
    {
        if (Core is not { } core) return false;
        try
        {
            var raw = await core.ExecuteScriptAsync("window.__LATCHI_PROBE ? window.__LATCHI_PROBE() : '{\"bxc\":false}'");
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.TryGetProperty("bxc", out var b) && b.GetBoolean();
        }
        catch (Exception ex)
        {
            Logger.Warn("Probe failed: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Signs the user out of the Microsoft session (v1.0 "sign me out on exit") by deleting the
    /// authentication cookies — nothing else: Better xCloud's local settings are untouched.
    /// </summary>
    public async Task ClearLoginCookiesAsync()
    {
        if (Core is not { } core) return;
        try
        {
            var mgr = core.Profile.CookieManager;
            var uris = new[]
            {
                "https://www.xbox.com", "https://xbox.com",
                "https://login.live.com", "https://login.microsoftonline.com", "https://account.live.com",
            };
            foreach (var uri in uris)
            {
                var cookies = await mgr.GetCookiesAsync(uri);
                foreach (var c in cookies)
                    mgr.DeleteCookies(c.Name, uri);
            }
            Logger.Info("Login session cookies cleared (user chose sign-out)");
        }
        catch (Exception ex)
        {
            Logger.Error("Could not clear login cookies: " + ex.Message);
        }
    }

    /// <summary>URLs that should be checked for Better xCloud (they match its own @match rules).</summary>
    public bool IsBetterXcloudPage(string? url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        return url.Contains("xbox.com/", StringComparison.Ordinal)
               && (url.Contains("/play", StringComparison.Ordinal) || url.Contains("/auth/msa", StringComparison.Ordinal));
    }
}

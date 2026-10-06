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

    /// <summary>v1.2 loading-state events — the host shows/hides the loading overlay
    /// and detects a permanently-empty page (never a silent dark screen again).</summary>
    public event Action<string>? NavigationStarted;   // sanitized url
    public event Action? ContentLoaded;               // DOM ready
    public event Action<bool>? NavigationDone;        // NavigationCompleted(isSuccess)

    /// <summary>Sanitized navigation history (last 60 events, domains/status only —
    /// never query strings or tokens) — shown in Diagnostics.</summary>
    public readonly List<string> NavigationLog = new();

    private void LogNav(string kind, string? url, string extra = "")
    {
        var line = $"{DateTime.Now:HH:mm:ss} {kind} {Logger.SafeUrl(url)}" + (extra.Length > 0 ? " — " + extra : "");
        lock (NavigationLog)
        {
            NavigationLog.Add(line);
            while (NavigationLog.Count > 60) NavigationLog.RemoveAt(0);
        }
        Logger.Info("nav: " + line);
    }

    /// <summary>Last N sanitized navigation events (newest last), for Diagnostics.</summary>
    public string[] SnapshotNavigationLog()
    {
        lock (NavigationLog) return NavigationLog.ToArray();
    }

    /// <summary>Is the current document really empty (no body text, no elements)?
    /// Used to catch the "blank dark screen" state instead of leaving the user
    /// staring at nothing. Returns null when it cannot be determined.</summary>
    public async Task<bool?> IsPageEmptyAsync()
    {
        if (Core is not { } core) return null;
        try
        {
            var raw = await core.ExecuteScriptAsync(
                "(function(){try{var b=document.body;return JSON.stringify({ok:!!b,txt:(b?(b.innerText||''):'').trim().length,n:(b?b.childElementCount:0)});}catch(e){return '{\"ok\":false}';}})()");
            using var doc = JsonDocument.Parse(raw);
            var ok = doc.RootElement.TryGetProperty("ok", out var o) && o.GetBoolean();
            var txt = doc.RootElement.TryGetProperty("txt", out var t) ? t.GetInt32() : -1;
            var n = doc.RootElement.TryGetProperty("n", out var c) ? c.GetInt32() : -1;
            return ok && txt == 0 && n == 0;
        }
        catch (Exception ex)
        {
            Logger.Warn("Empty-page probe failed: " + ex.Message);
            return null;
        }
    }

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
            LogNav("→", e.Uri, "top-level");
            if (!NavigationPolicy.IsAllowed(e.Uri))
            {
                e.Cancel = true;
                Logger.Warn("Blocked navigation: " + Logger.SafeUrl(e.Uri));
                LogNav("✗ blocked", e.Uri);
                NavigationBlocked?.Invoke(e.Uri);
                return;
            }
            NavigationStarted?.Invoke(e.Uri);
        };

        core.NavigationCompleted += (s, e) =>
        {
            LogNav(e.IsSuccess ? "✓ done" : "✗ failed", core.Source?.ToString(), e.IsSuccess ? "" : "hr=0x" + e.WebErrorStatus);
            if (!e.IsSuccess)
                Logger.Warn("Navigation failed (hr=0x" + e.WebErrorStatus + "): " + Logger.SafeUrl(core.Source?.ToString()));
            NavigationDone?.Invoke(e.IsSuccess);
        };

        core.ContentLoading += (s, e) =>
        {
            LogNav("· content", core.Source?.ToString());
            ContentLoaded?.Invoke();
        };

        // JS error tap: WebView2 has no console event — we register a document-created
        // script that forwards window.onerror / unhandledrejection through our own
        // WebMessage channel (type 'js-error'), sanitized to 160 chars, no query strings
        try
        {
            _ = core.AddScriptToExecuteOnDocumentCreatedAsync(ConsoleTapJs);
        }
        catch (Exception ex) { Logger.Info("JS error tap unavailable: " + ex.Message); }

        core.NewWindowRequested += (s, e) =>
        {
            // never allow extra browser windows; allowlisted popups become
            // same-window navigations so Microsoft auth stays inside the session
            e.Handled = true;
            LogNav("popup", e.Uri);
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
                if (type == "js-error")
                {
                    // page-side error tap (§19): LOG ONLY — never shown, never exported
                    if (!string.IsNullOrEmpty(detail)) Logger.Warn("js-error: " + Sanitize(detail));
                    return;
                }
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

    /// <summary>Console-message sanitizer: keep the first 160 chars, strip anything
    /// that looks like a URL with a query string, strip long tokens. Never logs
    /// credentials/tokens — the source of console errors is code locations.</summary>
    private static string Sanitize(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";
        var m = System.Text.RegularExpressions.Regex.Replace(message, @"https?://[^\s""']+",
            mm => mm.Value.Contains('?') ? mm.Value[..Math.Max(0, mm.Value.IndexOf('?'))] + "?…" : mm.Value);
        if (m.Length > 160) m = m[..160] + "…";
        return m;
    }

    /// <summary>Page-side JS error tap (§19): forwards window.onerror and
    /// unhandledrejection through the app's WebMessage channel as type 'js-error'.
    /// Message text only — no URLs with query strings, no stack frames, capped.</summary>
    private const string ConsoleTapJs = @"
        (function(){
          if (window.__LATCHI_JS_TAP) return; window.__LATCHI_JS_TAP = true;
          function cap(m){ m = String(m || '').replace(/[?&][^\s""' ]{4,}/g, '?…'); return m.slice(0, 160); }
          window.addEventListener('error', function(e){
            try { window.chrome.webview.postMessage({ type: 'js-error', detail: cap(e.message) }); } catch (_) {}
          }, true);
          window.addEventListener('unhandledrejection', function(e){
            try { window.chrome.webview.postMessage({ type: 'js-error', detail: cap('promise: ' + (e.reason && e.reason.message ? e.reason.message : e.reason)) }); } catch (_) {}
          });
        })();";

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

    /* ── Better xCloud settings bridge (v1.1) ───────────────────────────
       One effective configuration: LATCHI UI writes THE SAME values Better xCloud
       reads (localStorage["BetterXcloud"] of the shared profile). The onboarding seed
       runs once per profile via document-created script BEFORE the first navigation;
       later changes are applied live and the page is reloaded by the caller. */

    /// <summary>Registers the ONE-SHOT onboarding seed (runs on document creation —
    /// i.e. before any page script, exactly like a document-start userscript — but only
    /// executes its payload once per profile, guarded by a localStorage flag).</summary>
    public async Task SeedBxcSettingsAsync(string quality, string gameLocale, string? region = null)
    {
        if (Core is not { } core) throw new InvalidOperationException("webview not initialized");
        await core.AddScriptToExecuteOnDocumentCreatedAsync(
            global::LatchiXcloud.Core.Services.BxcSettings.BuildSeedScript(quality, gameLocale, region));
        Logger.Info($"BxC settings seed registered (quality={quality}, gameLang={gameLocale}, region={region ?? "default"}) — applies before first navigation");
    }

    /// <summary>Writes stream settings into localStorage["BetterXcloud"] on the live page
    /// and returns true when the page confirmed. The caller should reload the page so
    /// Better xCloud re-reads the values at document-start.</summary>
    public async Task<bool> ApplyBxcSettingsAsync(string? quality, string? gameLocale, string? region = null)
    {
        if (Core is not { } core) return false;
        var raw = await core.ExecuteScriptAsync(global::LatchiXcloud.Core.Services.BxcSettings.BuildApplyScript(quality, gameLocale, region));
        var ok = raw.Contains("applied");
        Logger.Info($"BxC settings applied live (quality={quality ?? "-"}, gameLang={gameLocale ?? "-"}, region={region ?? "-"}) → {raw.Trim()}");
        return ok;
    }

    /// <summary>Reads the CURRENT effective stream settings straight from
    /// localStorage["BetterXcloud"] — the exact same source Better xCloud reads.</summary>
    public async Task<(string? Quality, string? GameLang, string? Region)> ReadBxcSettingsAsync()
    {
        if (Core is not { } core) return (null, null, null);
        try
        {
            var raw = await core.ExecuteScriptAsync(global::LatchiXcloud.Core.Services.BxcSettings.BuildReadScript());
            // ExecuteScriptAsync wraps the returned string literal in JSON quotes
            var json = System.Text.Json.JsonSerializer.Deserialize<string>(raw) ?? "{}";
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return (doc.RootElement.TryGetProperty("q", out var q) ? q.GetString() : null,
                    doc.RootElement.TryGetProperty("l", out var l) ? l.GetString() : null,
                    doc.RootElement.TryGetProperty("r", out var r) ? r.GetString() : null);
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not read BxC settings: " + ex.Message);
            return (null, null, null);
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

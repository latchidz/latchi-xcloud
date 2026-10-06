using System.IO;
using System.Windows;
using LatchiXcloud.App.WebView;
using LatchiXcloud.Core.Security;
using LatchiXcloud.Core.Services;
using LatchiXcloud.Core.Userscript;
using LatchiXcloud.Core.Versioning;

namespace LatchiXcloud.App.Smoke;

/// <summary>
/// Production self-test (dev/CI, `--smoke`): boots the real app against an isolated
/// data dir and validates everything that can be validated WITHOUT a Microsoft
/// account or a live game stream. Anything requiring real login/streaming is
/// explicitly reported as NOT TESTED — never faked.
///
/// Runs as ONE async flow on the application's real message loop (App.OnStartup
/// dispatches RunAsync and returns; Application.Run pumps everything naturally).
/// A hard watchdog in App kills the process after 8 minutes no matter what —
/// CI can never hang on this test.
/// </summary>
public static class SmokeRunner
{
    private sealed class Check
    {
        public string Name = "";
        public bool Pass;
        public string Detail = "";
    }

    private static readonly List<Check> Checks = new();

    public static bool ShouldRun(string[] args) =>
        args.Any(a => string.Equals(a, "--smoke", StringComparison.OrdinalIgnoreCase));

    public static async Task<int> RunAsync()
    {
        string dataDir = "";
        try
        {
            dataDir = Environment.GetEnvironmentVariable("LATCHI_XCLOUD_DATA")
                      ?? Path.Combine(Path.GetTempPath(), "latchi-xcloud-smoke");
            if (Directory.Exists(dataDir)) Directory.Delete(dataDir, recursive: true);
            Directory.CreateDirectory(dataDir);
            Environment.SetEnvironmentVariable("LATCHI_XCLOUD_DATA", dataDir);
            Logger.Init(Path.Combine(dataDir, "Logs"));

            await RunCoreAsync(dataDir);
        }
        catch (Exception ex)
        {
            Checks.Add(new Check { Name = "fatal", Pass = false, Detail = ex.ToString() });
        }

        var allPass = Checks.All(c => c.Pass);
        var report = new System.Text.StringBuilder();
        report.AppendLine("{");
        report.AppendLine($"  \"passed\": {Checks.Count(c => c.Pass)}, \"failed\": {Checks.Count(c => !c.Pass)},");
        report.AppendLine("  \"checks\": [");
        report.AppendLine(string.Join(",\n", Checks.Select(c =>
            $"    {{\"name\": \"{c.Name}\", \"pass\": {(c.Pass ? "true" : "false")}, \"detail\": {Json(c.Detail)}}}")));
        report.AppendLine("  ]");
        report.AppendLine("}");
        var outPath = Environment.GetEnvironmentVariable("LATCHI_SMOKE_OUT")
                      ?? Path.Combine(dataDir, "smoke-results.json");
        try { File.WriteAllText(outPath, report.ToString()); } catch { }
        Console.WriteLine(report);
        return allPass ? 0 : 1;
    }

    private static string Json(string s) =>
        "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";

    private static void Add(string name, bool pass, string detail = "") =>
        Checks.Add(new Check { Name = name, Pass = pass, Detail = detail });

    private static async Task RunCoreAsync(string dataDir)
    {
        /* ── S1: settings persistence ─────────────────────────────────── */
        var settings = new SettingsStore(dataDir);
        Add("S1 defaults", settings.Current.StartMaximized && settings.Current.AutoUpdateBetterXcloud
            && settings.Current.Language == "ar", $"lang={settings.Current.Language}");
        settings.Current.LowEndMode = true;
        settings.Save();
        var re = new SettingsStore(dataDir);
        Add("S1 roundtrip", re.Current.LowEndMode, $"lowEnd={re.Current.LowEndMode}");
        File.WriteAllText(Path.Combine(dataDir, "Settings", "settings.json"), "### broken ###");
        Add("S1 corrupt safe", new SettingsStore(dataDir).Current.StartMaximized);

        /* ── S2: shipped Better xCloud script is the official one ────── */
        // resolves from disk, or from the embedded copies in the single-file portable build
        var (bundledPath, bundledManifestPath) = Services.BundledResources.Resolve();
        var bundledManifest = Core.Services.Json.Deserialize<Core.Updates.BetterXcloudManifest>(
            File.ReadAllText(bundledManifestPath));
        var source = File.ReadAllText(bundledPath);
        var meta = UserscriptMetadata.Parse(source);
        Add("S2 metadata", meta.Name == "Better xCloud" && meta.Version == "6.7.12"
            && meta.RunAt == "document-start" && meta.IsGrantless
            && meta.Matches.Count == 2 && meta.Excludes.Count == 1,
            $"v={meta.Version} runAt={meta.RunAt} grants={string.Join(',', meta.Grants)} matches={meta.Matches.Count}");
        Add("S2 manifest provenance", bundledManifest is not null
            && bundledManifest.UpstreamRepo == "redphx/better-xcloud"
            && bundledManifest.UpstreamCommit.Length == 40
            && bundledManifest.Version == meta.Version
            && bundledManifest.Sha256 == AtomicFile.Sha256(bundledPath),
            $"sha={bundledManifest?.Sha256[..12]}… commit={bundledManifest?.UpstreamCommit[..7]}");

        /* ── S3: match patterns (C# side) ────────────────────────────── */
        bool M(string url) => PatternConverter.TestManaged(url, meta);
        Add("S3 patterns",
            M("https://www.xbox.com/en-US/play")
            && M("https://www.xbox.com/ar-DZ/play/games/FIFA")
            && M("https://www.xbox.com/en-US/auth/msa?loggedIn=true&ru=x")
            && !M("https://www.xbox.com/en-US/xbox-game-pass/play-day-one")
            && !M("https://login.live.com/")
            && !M("https://store.xbox.com/en-US/play"),
            "match/exclude matrix");

        /* ── S4: navigation policy ───────────────────────────────────── */
        Add("S4 policy",
            NavigationPolicy.IsAllowed("https://www.xbox.com/play")
            && NavigationPolicy.IsAllowed("https://login.live.com/")
            && NavigationPolicy.IsAllowed("https://login.microsoftonline.com/")
            && NavigationPolicy.IsAllowed("https://aadcdn.msauth.net/thing")
            && !NavigationPolicy.IsAllowed("https://example.com/")
            && !NavigationPolicy.IsAllowed("https://evil-notxbox.com/play")
            && !NavigationPolicy.IsAllowed("https://xbox.com.evil.io/play")
            && !NavigationPolicy.IsAllowed("file:///C:/Windows/win.ini")
            && !NavigationPolicy.IsAllowed("https://store.steampowered.com/"),
            "allow/block matrix");

        /* ── S5: semver ──────────────────────────────────────────────── */
        Add("S5 semver",
            SemanticVersion.TryParse("v6.7.13", out var nv) && SemanticVersion.TryParse("6.7.12", out var cv)
            && nv > cv && !(cv > nv),
            "6.7.13 > 6.7.12");

        /* ── S6: update pipeline with a FAKE official source ─────────── */
        var fake = new FakeSource();
        var svc = new Core.Updates.BetterXcloudUpdateService(fake,
            Path.Combine(dataDir, "Updates"),
            Path.Combine(dataDir, "BxC", "active"),
            Path.Combine(dataDir, "BxC", "previous"));
        svc.Bootstrap(bundledPath, bundledManifest!);
        var latest = await svc.CheckLatestAsync();
        Add("S6 check", latest is { Version: "9.9.9" }, $"latest={latest?.Version}");
        await svc.DownloadAndStageAsync(latest!);
        Add("S6 staged", svc.PeekStaged() is { Version: "9.9.9" });
        var activated = svc.ActivateStaged();
        var activeAfter = svc.LoadActiveManifest();
        Add("S6 activate", activated.Version == "9.9.9" && activeAfter!.Version == "9.9.9" && svc.HasPrevious());
        svc.MarkBad("9.9.9");
        var rolled = svc.Rollback();
        Add("S6 rollback+bad", rolled.Version == "6.7.12" && svc.IsBadVersion("9.9.9"));
        await svc.DownloadAndStageAsync(latest!);
        var refused = false;
        try { svc.ActivateStaged(); }
        catch (InvalidOperationException) { refused = true; }
        Add("S6 bad version refused", refused);

        fake.Tamper = true;
        var bad = false;
        try { await svc.DownloadAndStageAsync(latest!); }
        catch (InvalidOperationException) { bad = true; }
        Add("S6 tampered rejected", bad);
        fake.Tamper = false;

        var evilUrl = false;
        try { await svc.DownloadAndStageAsync(new Core.Updates.ReleaseInfo("8.8.8", "https://evil.io/bx.user.js", "", "")); }
        catch (InvalidOperationException) { evilUrl = true; }
        Add("S6 non-official URL refused", evilUrl);

        /* ── S7: REAL WebView2 + loader injection (natural message loop) ── */
        var loader = LoaderJs.Build(meta, source);
        var smokeWin = new Window { Width = 700, Height = 500, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        var wv = new Microsoft.Web.WebView2.Wpf.WebView2
        { DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 10, 13, 20) };
        smokeWin.Content = wv;
        smokeWin.Show();

        var host = new WebViewHost(wv);
        string? blockedSeen = null;
        var hotkeyTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockedTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.MessageReceived += (type, detail) => { if (type == "hotkey") hotkeyTcs.TrySetResult(detail); };
        host.NavigationBlocked += url => { blockedSeen = url; blockedTcs.TrySetResult(url); };

        await host.InitializeAsync(loader);
        Add("S7 webview2 init", host.RuntimeVersion.Length > 0, "runtime " + host.RuntimeVersion);

        var navTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        wv.CoreWebView2!.NavigationCompleted += (_, e) => navTcs.TrySetResult(e.IsSuccess);
        wv.CoreWebView2.NavigateToString("<html><body><h1>smoke</h1></body></html>");
        var navOk = await Task.WhenAny(navTcs.Task, Task.Delay(TimeSpan.FromSeconds(30))) == navTcs.Task;
        Add("S7 test page loaded", navOk, navOk ? "ok" : "timeout");
        await Task.Delay(400);

        var marker = await wv.CoreWebView2.ExecuteScriptAsync("window.__LATCHI__ && window.__LATCHI_PROBE ? 'loader-ok' : 'loader-missing'");
        Add("S7 loader injected", marker.Contains("loader-ok"), marker);

        var probe = await wv.CoreWebView2.ExecuteScriptAsync("window.__LATCHI_PROBE ? window.__LATCHI_PROBE() : 'missing'");
        // BxC must NOT have run on this non-xbox document — the match guard held
        Add("S7 probe + BxC correctly not run on non-xbox page",
            probe.Contains("bxc") && probe.Contains("false"), probe);

        var jsTest = await wv.CoreWebView2.ExecuteScriptAsync(BuildJsMatcherTest(meta));
        Add("S7 js matcher (real engine)", jsTest.Contains("ALL-OK"), jsTest.Trim());

        // hotkey bridge: dispatch F11 inside the page → the host must receive it
        await wv.CoreWebView2.ExecuteScriptAsync(
            "document.dispatchEvent(new KeyboardEvent('keydown', {key:'F11', bubbles:true}));");
        var hotkeySeen = await Task.WhenAny(hotkeyTcs.Task, Task.Delay(TimeSpan.FromSeconds(10))) == hotkeyTcs.Task
            ? await hotkeyTcs.Task : null;
        Add("S7 hotkey bridge", hotkeySeen == "F11", "received=" + (hotkeySeen ?? "nothing"));

        /* ── S8: live blocked navigation (real network event) ────────── */
        if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
        {
            wv.CoreWebView2.Navigate("https://example.com/");
            var blocked = await Task.WhenAny(blockedTcs.Task, Task.Delay(TimeSpan.FromSeconds(25))) == blockedTcs.Task;
            Add("S8 blocked navigation", blocked && blockedSeen is not null,
                "blocked=" + Logger.SafeUrl(blockedSeen));
        }
        else
        {
            Add("S8 blocked navigation", true, "skipped — no network (NOT TESTED live)");
        }

        /* ── S10: REAL xbox.com navigation must COMPLETE — never hang ────
           (This is the exact navigation that hung on a real user machine in v1.0.0.
            The page may fail from a datacenter IP — that's fine; hanging is not.) */
        if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
        {
            var realTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void RealDone(object? s2, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs ev2)
                => realTcs.TrySetResult(true);
            wv.CoreWebView2!.NavigationCompleted += RealDone;
            wv.CoreWebView2.Navigate("https://www.xbox.com/play");
            var done = await Task.WhenAny(realTcs.Task, Task.Delay(TimeSpan.FromSeconds(45))) == realTcs.Task;
            wv.CoreWebView2.NavigationCompleted -= RealDone;
            Add("S10 real xbox.com navigation completes", done,
                done ? "navigation completed (no hang)" : "HUNG >45s — exactly the reported bug");
        }
        else
        {
            Add("S10 real xbox.com navigation completes", true, "skipped — no network (NOT TESTED live)");
        }

        smokeWin.Close();
        await Task.Delay(300);

        /* ── S9: v1.0 first-run wizard + session choice ── */
        var s9 = new SettingsStore(dataDir);
        Add("S9 first-run defaults",
            !s9.Current.FirstRunComplete && !s9.Current.StartFullscreen
            && s9.Current.KeepSessionOnExit == "ask",
            $"fullscreen={s9.Current.StartFullscreen} keep={s9.Current.KeepSessionOnExit}");
        var step = Core.Services.FirstRunFlow.InitialStep(s9.Current.FirstRunComplete);
        step = Core.Services.FirstRunFlow.Advance(step); // language chosen → sign-in
        Add("S9 wizard steps", step == "signin", "language→signin");
        Add("S9 sign-in url detect",
            Core.Services.FirstRunFlow.IsSignInSuccessUrl("https://www.xbox.com/en-US/auth/msa?loggedIn=true&ru=%2Fplay")
            && !Core.Services.FirstRunFlow.IsSignInSuccessUrl("https://www.xbox.com/en-US/play")
            && !Core.Services.FirstRunFlow.IsSignInSuccessUrl("https://login.live.com/")
            && !Core.Services.FirstRunFlow.IsSignInSuccessUrl(null),
            "auth/msa?loggedIn matrix");
        s9.Current.FirstRunComplete = true;
        s9.Current.KeepSessionOnExit = "signout";
        s9.Save();
        var s9b = new SettingsStore(dataDir);
        Add("S9 wizard persisted", s9b.Current.FirstRunComplete && s9b.Current.KeepSessionOnExit == "signout");
    }

    /// <summary>JS that re-tests the generated @match regexes inside the real engine.</summary>
    private static string BuildJsMatcherTest(UserscriptMetadata meta)
    {
        var matches = string.Join(",", meta.Matches.Select(m => "\"" + PatternConverter.ToJsRegexSource(m).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""));
        var excludes = string.Join(",", meta.Excludes.Select(m => "\"" + PatternConverter.ToJsRegexSource(m).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""));
        return
$@"(function () {{
  var M = [{matches}], X = [{excludes}];
  function t(list, url) {{ for (var i = 0; i < list.length; i++) if (new RegExp(list[i]).test(url)) return true; return false; }}
  function hit(url) {{ return t(M, url) && !t(X, url); }}
  var ok = hit('https://www.xbox.com/en-US/play')
        && hit('https://www.xbox.com/de-DE/play/games/thing')
        && hit('https://www.xbox.com/en-US/auth/msa?loggedIn=true')
        && !hit('https://www.xbox.com/en-US/xbox-game-pass/play-day-one')
        && !hit('https://login.live.com/')
        && !hit('https://store.xbox.com/en-US/play');
  return ok ? 'ALL-OK' : 'MISMATCH';
}})()";
    }

    /// <summary>Fake official source: serves the shipped script as "9.9.9" from the
    /// official URL; can be told to tamper with content.</summary>
    private sealed class FakeSource : Core.Updates.IFileSource
    {
        public bool Tamper;

        public Task<string> GetLatestReleaseJsonAsync() => Task.FromResult(
            @"{""tag_name"":""v9.9.9"",""prerelease"":false,""html_url"":""https://github.com/redphx/better-xcloud/releases/tag/v9.9.9"",""published_at"":""2026-10-05T00:00:00Z"",""assets"":[{""name"":""better-xcloud.user.js"",""browser_download_url"":""https://github.com/redphx/better-xcloud/releases/download/v9.9.9/better-xcloud.user.js""}]}");

        public async Task<byte[]> DownloadBytesAsync(string url)
        {
            if (!url.StartsWith("https://github.com/redphx/better-xcloud/releases/download/"))
                throw new InvalidOperationException("unofficial url");
            var path = Path.Combine(AppContext.BaseDirectory, "resources", "better-xcloud.user.js");
            var text = await File.ReadAllTextAsync(path);
            if (Tamper) text = text.Replace("Better xCloud", "EvilClone");
            // pretend this is 9.9.9 so the version check passes
            text = text.Replace("// @version      6.7.12", "// @version      9.9.9");
            return System.Text.Encoding.UTF8.GetBytes(text);
        }
    }
}

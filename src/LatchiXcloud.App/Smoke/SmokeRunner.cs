using System.IO;
using System.Windows;
using System.Windows.Threading;
using LatchiXcloud.App.WebView;
using LatchiXcloud.Core.Security;
using LatchiXcloud.Core.Services;
using LatchiXcloud.Core.Userscript;
using LatchiXcloud.Core.Versioning;
using Microsoft.Web.WebView2.Wpf;

namespace LatchiXcloud.App.Smoke;

/// <summary>
/// Production self-test (dev/CI, `--smoke`): boots the real app against an isolated
/// data dir and validates everything that can be validated WITHOUT a Microsoft
/// account or a live game stream. Anything requiring real login/streaming is
/// explicitly reported as NOT TESTED — never faked.
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

    public static int Run()
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

            RunCore(dataDir);
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

    private static void RunCore(string dataDir)
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
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "resources", "better-xcloud.user.js");
        var bundledManifest = Core.Services.Json.Deserialize<Core.Updates.BetterXcloudManifest>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "resources", "bxc-manifest.json")));
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
        var latest = svc.CheckLatestAsync().GetAwaiter().GetResult();
        Add("S6 check", latest is { Version: "9.9.9" }, $"latest={latest?.Version}");
        svc.DownloadAndStageAsync(latest!).GetAwaiter().GetResult();
        Add("S6 staged", svc.PeekStaged() is { Version: "9.9.9" });
        var activated = svc.ActivateStaged();
        var activeAfter = svc.LoadActiveManifest();
        Add("S6 activate", activated.Version == "9.9.9" && activeAfter!.Version == "9.9.9" && svc.HasPrevious());
        svc.MarkBad("9.9.9");
        var rolled = svc.Rollback();
        Add("S6 rollback+bad", rolled.Version == "6.7.12" && svc.IsBadVersion("9.9.9"));
        svc.DownloadAndStageAsync(latest!).GetAwaiter().GetResult();
        var refused = false;
        try { svc.ActivateStaged(); }
        catch (InvalidOperationException) { refused = true; }
        Add("S6 bad version refused", refused);

        // tampered download must be rejected
        fake.Tamper = true;
        var bad = false;
        try { svc.DownloadAndStageAsync(latest!).GetAwaiter().GetResult(); }
        catch (InvalidOperationException) { bad = true; }
        Add("S6 tampered rejected", bad);
        fake.Tamper = false;

        // non-official URL refused
        var evilUrl = false;
        try { svc.DownloadAndStageAsync(new Core.Updates.ReleaseInfo("8.8.8", "https://evil.io/bx.user.js", "", "")).GetAwaiter().GetResult(); }
        catch (InvalidOperationException) { evilUrl = true; }
        Add("S6 non-official URL refused", evilUrl);

        /* ── S7: REAL WebView2 + loader injection ────────────────────── */
        var loader = LoaderJs.Build(meta, source);
        WebView2? wv = null;
        WebViewHost? host = null;
        string? hotkeySeen = null;
        string? blockedSeen = null;
        Window? smokeWin = null;

        RunOnUi(() =>
        {
            smokeWin = new Window { Width = 700, Height = 500, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            wv = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 10, 13, 20) };
            smokeWin.Content = wv;
            smokeWin.Show();
        });

        var hotkeyTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        RunOnUi(() =>
        {
            host = new WebViewHost(wv!);
            host.MessageReceived += (type, detail) => { if (type == "hotkey") hotkeyTcs.TrySetResult(detail); };
            host.NavigationBlocked += url => blockedSeen = url;
        });
        RunAsyncOnUi(() => host!.InitializeAsync(loader));
        Add("S7 webview2 init", host!.RuntimeVersion.Length > 0, "runtime " + host.RuntimeVersion);

        var navTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        RunOnUi(() =>
        {
            wv!.CoreWebView2!.NavigationCompleted += (_, e) => navTcs.TrySetResult(e.IsSuccess);
            wv!.CoreWebView2.NavigateToString(
                "<html><body><h1>smoke</h1></body></html>");
        });
        PumpUntil(navTcs.Task, 30);
        PumpMs(500);

        var marker = RunScript(wv!, "window.__LATCHI ? 'loader-ok' : 'loader-missing'");
        Add("S7 loader injected", marker.Contains("loader-ok"), marker);

        var probe = RunScript(wv!, "window.__LATCHI_PROBE ? window.__LATCHI_PROBE() : 'missing'");
        // BxC must NOT have run on this non-xbox document — the match guard held
        Add("S7 probe + BxC correctly not run on non-xbox page",
            probe.Contains("bxc") && probe.Contains("false"), probe);

        var jsTest = RunScript(wv!, BuildJsMatcherTest(meta));
        Add("S7 js matcher (real engine)", jsTest.Contains("ALL-OK"), jsTest.Trim());

        // hotkey bridge: dispatch F11 inside the page → the host must receive it
        RunAsyncOnUi(async () =>
            await wv!.CoreWebView2!.ExecuteScriptAsync(
                "document.dispatchEvent(new KeyboardEvent('keydown', {key:'F11', bubbles:true}));"));
        hotkeySeen = PumpUntil(hotkeyTcs.Task, 10);
        Add("S7 hotkey bridge", hotkeySeen == "F11", "received=" + (hotkeySeen ?? "nothing"));

        /* ── S8: live blocked navigation (real network event) ────────── */
        if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
        {
            RunOnUi(() => wv!.CoreWebView2!.Navigate("https://example.com/"));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (blockedSeen is null && sw.Elapsed < TimeSpan.FromSeconds(25))
                PumpMs(250);
            Add("S8 blocked navigation", blockedSeen is not null,
                "blocked=" + Logger.SafeUrl(blockedSeen));
        }
        else
        {
            Add("S8 blocked navigation", true, "skipped — no network (NOT TESTED live)");
        }

        RunOnUi(() => smokeWin!.Close());
        PumpMs(300);
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

    /* ── UI-thread helpers ───────────────────────────────────────────── */
    // Smoke runs ON the dispatcher thread; blocking waits would deadlock the
    // async WebView2 continuations, so every wait pumps dispatcher frames.

    private static void RunOnUi(Action action) => Application.Current.Dispatcher.Invoke(action);

    /// <summary>Runs an async block on the UI thread, pumping frames until it finishes.</summary>
    private static void RunAsyncOnUi(Func<Task> work)
    {
        var outer = Application.Current.Dispatcher.InvokeAsync(() => work());
        PumpUntil(outer.Task.Unwrap(), 180);
    }

    private static string RunScript(WebView2 wv, string js)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        RunAsyncOnUi(async () => tcs.TrySetResult(await wv.CoreWebView2!.ExecuteScriptAsync(js)));
        return tcs.Task.IsCompleted ? tcs.Task.Result : "(timeout)";
    }

    /// <summary>Pumps dispatcher frames until the task completes or the timeout hits.</summary>
    private static void PumpUntil(Task task, int seconds)
    {
        if (task.IsCompleted) return;
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.SystemIdle)
        { Interval = TimeSpan.FromSeconds(seconds) };
        timer.Tick += (_, _) => frame.Continue = false;
        timer.Start();
        task.ContinueWith(_ =>
            Application.Current.Dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    private static T PumpUntil<T>(Task<T> task, int seconds)
    {
        PumpUntil((Task)task, seconds);
        return task.IsCompleted ? task.Result : default!;
    }

    /// <summary>Pumps dispatcher frames for a fixed duration.</summary>
    private static void PumpMs(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.SystemIdle)
        { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { frame.Continue = false; timer.Stop(); };
        timer.Start();
        Dispatcher.PushFrame(frame);
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
            if (Tamper) text = text.Replace("Better xCloud", "EvilClone", StringComparison.Ordinal);
            // pretend this is 9.9.9 so the version check passes
            text = text.Replace("// @version      6.7.12", "// @version      9.9.9");
            return System.Text.Encoding.UTF8.GetBytes(text);
        }
    }
}

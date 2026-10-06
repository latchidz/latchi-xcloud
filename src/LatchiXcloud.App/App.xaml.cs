using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using LatchiXcloud.App.Smoke;
using LatchiXcloud.App.Views;
using LatchiXcloud.Core.Services;

namespace LatchiXcloud.App;

public partial class App : Application
{
    public static bool IsSmokeMode { get; private set; }
    public static string AppVersion => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.1.0";

    /// <summary>Hard ceiling for the smoke run — CI must NEVER hang on us (8 min).</summary>
    private System.Threading.Timer? _smokeWatchdog;
    private volatile bool _smokeDone;

    /// <summary>One instance at a time — a second launch (e.g. portable while installed runs)
    /// would collide on the shared WebView2 profile and misbehave.</summary>
    private Mutex? _singleInstance;

    /// <summary>Startup crash-loop guard: written on boot, cleared on a clean exit.
    /// ≥3 consecutive crash boots → offer help (logs / reset) instead of looping silently.</summary>
    private const string CrashStreakFile = "crash-streak.count";

    protected override void OnStartup(StartupEventArgs e)
    {
        // global exception capture: dispatcher (UI thread), any thread, unobserved tasks
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += App_DomainUnhandledException;
        TaskScheduler.UnobservedTaskException += App_UnobservedTaskException;

        if (SmokeRunner.ShouldRun(e.Args))
        {
            IsSmokeMode = true;
            StartSmokeWatchdog();
            base.OnStartup(e);
            return;
        }

        base.OnStartup(e);

        // single instance — the data dir (and the WebView2 profile inside it) is shared,
        // two running copies would fight over it
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\LATCHI-xCLOUD-single-instance", out var first);
        if (!first)
        {
            MessageBox.Show(
                "LATCHI xCLOUD يعمل بالفعل. أغلق النسخة المفتوحة أولاً ثم أعد المحاولة.",
                "LATCHI xCLOUD", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        AppPaths.EnsureDataDir();
        Logger.Init(AppPaths.LogsDir);
        Logger.Info($"LATCHI xCLOUD {AppVersion} starting — data dir: {AppPaths.DataDir}");

        HandleCrashStreak();

        try
        {
            // plain, light startup: straight into the main window — no splash, no sound.
            // If MainWindow.xaml (or any resource it pulls) is malformed, the detailed
            // crash report lands in the log and the user gets an actionable dialog.
            var main = new MainWindow();
            MainWindow = main;
            main.Closed += (_, _) => Shutdown();
            main.Show();
        }
        catch (Exception ex)
        {
            FatalStartup(ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // a clean exit clears the crash streak — the loop guard only counts consecutive failures
        if (!IsSmokeMode)
        {
            try { File.WriteAllText(Path.Combine(AppPaths.DataDir, CrashStreakFile), "0"); } catch { }
        }
        base.OnExit(e);
    }

    /* ── crash-loop guard ─────────────────────────────────────────── */

    private void HandleCrashStreak()
    {
        try
        {
            var path = Path.Combine(AppPaths.DataDir, CrashStreakFile);
            var n = int.TryParse(File.Exists(path) ? File.ReadAllText(path).Trim() : "0", out var v) ? v : 0;
            File.WriteAllText(path, (n + 1).ToString());
            if (n >= 3)
            {
                Logger.Error($"Startup crash-loop detected ({n} consecutive failed boots) — offering recovery");
                var choice = MessageBox.Show(
                    "التطبيق توقف عن العمل عدة مرات متتالية عند الإقلاع.\n\n" +
                    "«نعم» = إعادة تعيين الإعدادات فقط (تسجيل الدخول وبيانات Better xCloud تبقى كما هي)\n" +
                    "«لا» = متابعة الإقلاع كما هو\n\n" +
                    $"السجلات الكاملة: {Path.Combine(AppPaths.LogsDir, "app.log")}",
                    "LATCHI xCLOUD — إصلاح الإقلاع", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (choice == MessageBoxResult.Yes)
                {
                    var settingsFile = Path.Combine(AppPaths.DataDir, "Settings", "settings.json");
                    if (File.Exists(settingsFile)) File.Delete(settingsFile);
                    Logger.Info("User chose settings reset after crash loop — settings restored to defaults");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Crash-streak guard failed (non-fatal): " + ex.Message);
        }
    }

    /* ── detailed crash reporting (§2 of the fix spec) ────────────── */

    /// <summary>Full crash report: type, message, complete inner-exception chain, stack
    /// traces, and — for XamlParseException — the exact XAML file/URI, line and column.
    /// NEVER logs secrets: only exception metadata (type/message/stack), never page
    /// contents, cookies, tokens or credentials.</summary>
    private static string CrashReport(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        var depth = 0;
        for (var cur = ex; cur is not null && depth < 10; cur = cur.InnerException, depth++)
        {
            var prefix = depth == 0 ? "EXCEPTION" : $"INNER [{depth}]";
            sb.AppendLine($"{prefix}: {cur.GetType().FullName}");
            sb.AppendLine($"{prefix} MESSAGE: {cur.Message}");
            if (cur is System.Windows.Markup.XamlParseException xpe)
            {
                sb.AppendLine($"{prefix} XAML BASE URI: {xpe.BaseUri}");
                sb.AppendLine($"{prefix} XAML LINE: {xpe.LineNumber} POS: {xpe.LinePosition}");
            }
            if (cur.StackTrace is { } st)
                sb.AppendLine($"{prefix} STACK:\n{st}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Logger.Error("Unhandled UI exception:\n" + CrashReport(e.Exception));
        if (IsSmokeMode)
        {
            _smokeDone = true;
            Shutdown(2);
            return;
        }

        var innerMost = e.Exception.GetBaseException();
        var xamlInfo = innerMost is System.Windows.Markup.XamlParseException xpe
            ? $"\n\nXAML: {xpe.BaseUri} (سطر {xpe.LineNumber}، عمود {xpe.LinePosition})"
            : "";
        MessageBox.Show(
            "حدث خطأ غير متوقع وأُغلق الجزء المعني.\n" +
            $"السبب: {innerMost.GetType().Name} — {innerMost.Message}{xamlInfo}\n\n" +
            $"التفاصيل الكاملة في: {Path.Combine(AppPaths.LogsDir, "app.log")}",
            "LATCHI xCLOUD", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void App_DomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        // last chance — nothing else will run after this; log everything we can
        try { Logger.Error($"FATAL (non-UI thread, terminating={e.IsTerminating}):\n" + (ex is null ? e.ExceptionObject?.ToString() ?? "?" : CrashReport(ex))); }
        catch { }
    }

    private void App_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // observed so the process doesn't die from a background fault we already logged
        e.SetObserved();
        try { Logger.Warn("Unobserved task exception:\n" + CrashReport(e.Exception)); } catch { }
    }

    /// <summary>Startup itself failed — the window never appeared. Show the real cause
    /// (innermost exception + XAML position when available) and end cleanly.</summary>
    private void FatalStartup(Exception ex)
    {
        Logger.Error("FATAL STARTUP FAILURE:\n" + CrashReport(ex));
        var baseEx = ex.GetBaseException();
        var xamlInfo = baseEx is System.Windows.Markup.XamlParseException xpe
            ? $"\n\nالملف/الموضع: {xpe.BaseUri} (سطر {xpe.LineNumber}، عمود {xpe.LinePosition})"
            : "";
        MessageBox.Show(
            "تعذّر تشغيل التطبيق عند الإقلاع.\n" +
            $"السبب: {baseEx.GetType().Name} — {baseEx.Message}{xamlInfo}\n\n" +
            $"التفاصيل الكاملة في: {Path.Combine(AppPaths.LogsDir, "app.log")}",
            "LATCHI xCLOUD — فشل الإقلاع", MessageBoxButton.OK, MessageBoxImage.Error);
        try { Shutdown(); } catch { }
    }

    /* ── smoke mode (unchanged mechanics) ──────────────────────────── */

    private void StartSmokeWatchdog()
    {
        // watchdog on a background thread: if the smoke flow ever stalls,
        // kill the process with a failure code instead of hanging the runner
        _smokeWatchdog = new System.Threading.Timer(_ =>
        {
            if (_smokeDone) return;
            try
            {
                var outPath = Environment.GetEnvironmentVariable("LATCHI_SMOKE_OUT");
                if (!string.IsNullOrEmpty(outPath))
                    File.AppendAllText(outPath, "\n{\"watchdog\": \"smoke exceeded 8 minutes — killed\"}\n");
            }
            catch { }
            Environment.Exit(1);
        }, null, TimeSpan.FromMinutes(8), System.Threading.Timeout.InfiniteTimeSpan);

        // Run the smoke as ONE async flow on the REAL application message loop:
        // OnStartup returns, Application.Run pumps, every await resumes naturally.
        // (Do NOT block or call Shutdown inside OnStartup in smoke mode.)
        Dispatcher.InvokeAsync(async () =>
        {
            var exitCode = 1;
            try { exitCode = await SmokeRunner.RunAsync(); }
            catch (Exception ex) { Logger.Error("Smoke crashed:\n" + CrashReport(ex)); exitCode = 2; }
            finally
            {
                _smokeDone = true;
                _smokeWatchdog?.Dispose();
                Shutdown(exitCode);
            }
        });
    }
}

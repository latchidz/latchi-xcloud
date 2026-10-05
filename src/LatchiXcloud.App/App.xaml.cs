using System.IO;
using System.Windows;
using System.Windows.Threading;
using LatchiXcloud.App.Smoke;
using LatchiXcloud.App.Views;
using LatchiXcloud.Core.Services;

namespace LatchiXcloud.App;

public partial class App : Application
{
    public static bool IsSmokeMode { get; private set; }
    public static string AppVersion => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    /// <summary>Hard ceiling for the smoke run — CI must NEVER hang on us (8 min).</summary>
    private System.Threading.Timer? _smokeWatchdog;
    private volatile bool _smokeDone;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        if (SmokeRunner.ShouldRun(e.Args))
        {
            IsSmokeMode = true;

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
                catch { exitCode = 2; }
                finally
                {
                    _smokeDone = true;
                    _smokeWatchdog?.Dispose();
                    Shutdown(exitCode);
                }
            });

            base.OnStartup(e); // no StartupUri in XAML → nothing else opens
            return;
        }

        base.OnStartup(e);
        AppPaths.EnsureDataDir();
        Logger.Init(AppPaths.LogsDir);
        Logger.Info($"LATCHI xCLOUD {AppVersion} starting — data dir: {AppPaths.DataDir}");

        // v1.0: animated splash shows instantly while the heavy WebView2 initializes
        var splash = new SplashWindow();
        if (new SettingsStore(AppPaths.DataDir).Current.PlayStartupSound)
            splash.PlayChime();
        splash.Show();

        var main = new MainWindow(splash);
        MainWindow = main;
        main.Closed += (_, _) => { splash.ForceClose(); Shutdown(); };
        main.Show();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Logger.Error("Unhandled exception: " + e.Exception);
        if (IsSmokeMode)
        {
            _smokeDone = true;
            Shutdown(2);
            return;
        }
        MessageBox.Show(
            "حدث خطأ غير متوقع. راجع مجلد السجلات للمزيد من التفاصيل.\n\n" + e.Exception.Message,
            "LATCHI xCLOUD", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

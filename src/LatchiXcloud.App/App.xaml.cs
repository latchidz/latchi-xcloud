using System.Windows;
using LatchiXcloud.App.Smoke;
using LatchiXcloud.App.Views;
using LatchiXcloud.Core.Services;

namespace LatchiXcloud.App;

public partial class App : Application
{
    public static bool IsSmokeMode { get; private set; }
    public static string AppVersion => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        if (SmokeRunner.ShouldRun(e.Args))
        {
            // No window is ever created in smoke mode: Shutdown(exitCode) is the last
            // word on the process exit code (lesson from the teleprompter project —
            // never combine this with StartupUri).
            IsSmokeMode = true;
            var exitCode = SmokeRunner.Run();
            Shutdown(exitCode);
            return;
        }

        base.OnStartup(e);
        AppPaths.EnsureDataDir();
        Logger.Init(AppPaths.LogsDir);
        Logger.Info($"LATCHI xCLOUD {AppVersion} starting — data dir: {AppPaths.DataDir}");

        var main = new MainWindow();
        MainWindow = main;
        main.Closed += (_, _) => Shutdown();
        if (MainWindow is { } win) win.Show();
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Logger.Error("Unhandled exception: " + e.Exception);
        if (IsSmokeMode)
        {
            Shutdown(2);
            return;
        }
        MessageBox.Show(
            "حدث خطأ غير متوقع. راجع مجلد السجلات للمزيد من التفاصيل.\n\n" + e.Exception.Message,
            "LATCHI xCLOUD", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

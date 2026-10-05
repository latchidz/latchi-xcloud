using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
using System.Windows.Media;
using System.Windows.Threading;
using LatchiXcloud.App.Services;
using LatchiXcloud.App.Theme;
using LatchiXcloud.App.WebView;
using LatchiXcloud.Core.Services;

namespace LatchiXcloud.App.Views;

/// <summary>
/// The app shell: one window, one WebView2 instance, custom chrome. The shell becomes
/// almost invisible once the game runs — no polling loops, no overlays, no timers
/// except a one-shot probe after each navigation.
/// </summary>
public partial class MainWindow : Window
{
    private SettingsStore _settings = null!;
    private BetterXcloudRuntime _bxc = null!;
    private WebViewHost _host = null!;

    private DispatcherTimer? _probeTimer;
    private DispatcherTimer? _toastTimer;
    private int _probeAttempt;
    private bool _firstNavigationDone;
    private bool _hostFullscreen;
    private WindowState _preFullscreenState = WindowState.Normal;
    private bool _bxcFailedThisVersion;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += OnStateChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AppPaths.EnsureDataDir();

        // pending full app reset (from settings, with strong confirmation)
        var resetFlag = Path.Combine(AppPaths.DataDir, "pending-app-reset.flag");
        if (File.Exists(resetFlag))
        {
            try
            {
                Web.Visibility = Visibility.Collapsed; // nothing holds the profile yet
                Directory.Delete(AppPaths.DataDir, recursive: true);
                AppPaths.EnsureDataDir();
                Logger.Info("Application data reset performed");
            }
            catch (Exception ex)
            {
                Logger.Error("App reset failed: " + ex.Message);
            }
        }

        _settings = new SettingsStore(AppPaths.DataDir);
        RestoreWindowBounds();

        _bxc = new BetterXcloudRuntime();
        SetSplash("Starting xCloud…");
        _bxc.Initialize(); // bundled/staged/rollback — always before any page loads
        SetSplash("Loading Better xCloud…");

        _host = new WebViewHost(Web);
        _host.MessageReceived += OnWebMessage;
        _host.NavigationBlocked += url => ShowToast("تم حظر فتح موقع خارج نطاق Xbox/Microsoft");
        _host.ProcessFailed += OnWebProcessFailed;

        try
        {
            var loader = LoaderJs.Build(_bxc.Metadata, _bxc.ActiveSource);
            await _host.InitializeAsync(loader);
        }
        catch (Exception ex)
        {
            Logger.Error("WebView2 init failed: " + ex);
            ShowError("تعذّر تشغيل محرك العرض (WebView2).",
                "ثبّت Microsoft Edge WebView2 Runtime ثم أعد المحاولة.\n" + ex.Message);
            return;
        }

        Web.CoreWebView2!.NavigationCompleted += OnNavigationCompleted;
        SetChip(ChipState.Pending, "Better xCloud: v" + _bxc.ActiveVersion);

        if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
        {
            _host.GoHome();
        }
        else
        {
            ShowError("لا يوجد اتصال بالإنترنت.", "التطبيق يحتاج إنترنت لبث ألعاب Xbox Cloud.\nاضغط «إعادة المحاولة» بعد استعادة الاتصال.");
        }

        if (_settings.Current.StartFullscreen)
            EnterHostFullscreen();
    }

    /* ── navigation + Better xCloud state ────────────────────────────── */

    private void OnNavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        CancelProbe();

        if (!_firstNavigationDone)
        {
            _firstNavigationDone = true;
            if (e.IsSuccess) Splash.Visibility = Visibility.Collapsed;
            else if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                ShowError("Xbox Cloud Gaming could not be loaded.", "تعذّر الوصول إلى الخدمة. قد يكون هناك انقطاع مؤقت أو حجب شبكي.");
            else
                ShowError("لا يوجد اتصال بالإنترنت.", "اضغط «إعادة المحاولة» بعد استعادة الاتصال.");
            _ = QuietUpdateCheckAsync();
        }

        if (!e.IsSuccess) return;

        var url = Web.CoreWebView2!.Source?.ToString();
        if (_host.IsBetterXcloudPage(url))
        {
            // one-shot probes — never a polling loop that could disturb the stream
            _probeAttempt = 0;
            _probeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            _probeTimer.Tick += async (_, _) => await ProbeTickAsync();
            _probeTimer.Start();
        }
        else
        {
            SetChip(ChipState.Inactive, "Better xCloud: v" + _bxc.ActiveVersion);
        }
    }

    private async Task ProbeTickAsync()
    {
        _probeTimer?.Stop();
        var ok = await _host.ProbeBetterXcloudAsync();
        _probeAttempt++;

        if (ok)
        {
            _bxcFailedThisVersion = false;
            SetChip(ChipState.Ready, "Better xCloud: v" + _bxc.ActiveVersion + " ✓");
            Logger.Info("Better xCloud initialized (v" + _bxc.ActiveVersion + ")");
            return;
        }

        if (_probeAttempt == 1)
        {
            _probeTimer!.Interval = TimeSpan.FromMilliseconds(2300); // second and final try
            _probeTimer.Start();
            return;
        }

        // failed twice on a page where the script SHOULD have run
        SetChip(ChipState.Failed, "Better xCloud تعذّر التشغيل");
        Logger.Error("Better xCloud failed to initialize on " + Logger.SafeUrl(Web.CoreWebView2?.Source?.ToString()));
        if (!_bxcFailedThisVersion)
        {
            _bxcFailedThisVersion = true;
            if (_bxc.Updates.HasPrevious())
            {
                if (_bxc.TryMarkBadAndRollback(_bxc.ActiveVersion))
                {
                    ShowToast("تعذّرت تهيئة النسخة الجديدة — استعدنا النسخة السابقة وأعدنا التحميل");
                    _host.Reload();
                }
            }
            else
            {
                ShowToast("Better xCloud لم يبدأ — أعد تحميل الصفحة أو راجع التشخيصات");
            }
        }
    }

    private void CancelProbe()
    {
        _probeTimer?.Stop();
        _probeTimer = null;
        _probeAttempt = 0;
    }

    /* ── web bridge messages (hotkeys from the page) ─────────────────── */

    private void OnWebMessage(string type, string? detail)
    {
        Dispatcher.Invoke(() =>
        {
            switch (type)
            {
                case "hotkey":
                    switch (detail)
                    {
                        case "F11": ToggleHostFullscreen(); break;
                        case "Esc": if (_hostFullscreen) ExitHostFullscreen(); break;
                        case "CtrlShiftR": _host.Reload(); break;
                        case "CtrlShiftU": OpenSettings(updateTab: true); break;
                        case "CtrlShiftB": ShowBxcStatus(); break;
                    }
                    break;
                case "bxc-error":
                    Logger.Error("Better xCloud threw during init: " + (detail ?? "?"));
                    SetChip(ChipState.Failed, "Better xCloud تعذّر التشغيل");
                    ShowToast("Better xCloud أبلغ عن خطأ أثناء البدء");
                    break;
            }
        });
    }

    private void OnWebProcessFailed()
    {
        Dispatcher.Invoke(() =>
        {
            ShowError("توقّف محرك العرض.", "أغلق التطبيق وأعد فتحه. إذا تكرر الأمر، راجع التشخيصات.");
        });
    }

    /* ── host fullscreen (F11) — independent from the page's own fullscreen ── */

    private void EnterHostFullscreen()
    {
        if (_hostFullscreen) return;
        _hostFullscreen = true;
        _preFullscreenState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
        TitleBar.Visibility = Visibility.Collapsed;
        WindowState = WindowState.Maximized;
        var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
                  if (chrome is not null) chrome.ResizeBorderThickness = new Thickness(0);
        OuterBorder.BorderThickness = new Thickness(0);
        OuterBorder.Margin = new Thickness(0);
        OuterBorder.CornerRadius = new CornerRadius(0);
        BtnFullscreen.Tag = Icons.ExitFullscreen;
    }

    private void ExitHostFullscreen()
    {
        if (!_hostFullscreen) return;
        _hostFullscreen = false;
        TitleBar.Visibility = Visibility.Visible;
        WindowState = _preFullscreenState;
        var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
                  if (chrome is not null) chrome.ResizeBorderThickness = new Thickness(6);
        OuterBorder.BorderThickness = new Thickness(1);
        ApplyWindowChromePadding();
        OuterBorder.CornerRadius = new CornerRadius(8);
        BtnFullscreen.Tag = Icons.Fullscreen;
    }

    private void ToggleHostFullscreen()
    {
        if (_hostFullscreen) ExitHostFullscreen();
        else EnterHostFullscreen();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        ApplyWindowChromePadding();
        BtnMax.Tag = WindowState == WindowState.Maximized ? Icons.Restore : Icons.Maximize;
    }

    private void ApplyWindowChromePadding()
    {
        // maximized borderless windows bleed off-screen — pad the root
        OuterBorder.Margin = WindowState == WindowState.Maximized
            ? new Thickness(7)
            : new Thickness(0);
    }

    /* ── toolbar / window buttons ────────────────────────────────────── */

    private void BtnHome_Click(object sender, RoutedEventArgs e) => _host.GoHome();
    private void BtnReload_Click(object sender, RoutedEventArgs e) => _host.Reload();
    private void BtnFullscreen_Click(object sender, RoutedEventArgs e) => ToggleHostFullscreen();
    private void BtnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnMax_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // only reached when focus is OUTSIDE the webview (titlebar etc.) —
        // inside the page the loader bridge posts the same actions
        if (e.Key == Key.F11) { ToggleHostFullscreen(); e.Handled = true; }
        else if (e.Key == Key.R && e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        { _host.Reload(); e.Handled = true; }
        else if (e.Key == Key.U && e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        { OpenSettings(updateTab: true); e.Handled = true; }
        else if (e.Key == Key.B && e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        { ShowBxcStatus(); e.Handled = true; }
        else if (e.Key == Key.Escape && _hostFullscreen) { ExitHostFullscreen(); e.Handled = true; }
    }

    /* ── error screen actions ────────────────────────────────────────── */

    private void BtnRetry_Click(object sender, RoutedEventArgs e)
    {
        ErrorScreen.Visibility = Visibility.Collapsed;
        Splash.Visibility = Visibility.Visible;
        SetSplash("Starting xCloud…");
        _host.GoHome();
    }

    private void BtnReloadPage_Click(object sender, RoutedEventArgs e)
    {
        ErrorScreen.Visibility = Visibility.Collapsed;
        Splash.Visibility = Visibility.Visible;
        _host.Reload();
    }

    private void BtnOpenExternal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.xbox.com/play",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not open system browser: " + ex.Message);
        }
    }

    /* ── settings / diagnostics / status ─────────────────────────────── */

    private void BtnSettings_Click(object sender, RoutedEventArgs e) => OpenSettings(updateTab: false);
    private void BtnDiag_Click(object sender, RoutedEventArgs e) => OpenDiagnostics();

    private void OpenSettings(bool updateTab)
    {
        var win = new SettingsWindow(this, _settings, _bxc, _host) { Owner = this };
        if (updateTab) win.FocusUpdateSection();
        win.ShowDialog();
        if (_settings.Current.StartFullscreen && !_hostFullscreen) EnterHostFullscreen();
    }

    private void OpenDiagnostics()
    {
        var win = new DiagnosticsWindow(this, _host, _bxc) { Owner = this };
        win.ShowDialog();
    }

    private void ChipBxc_Click(object sender, MouseButtonEventArgs e) => ShowBxcStatus();

    private void ShowBxcStatus()
    {
        var m = _bxc.ActiveManifest;
        var msg = $"Better xCloud: v{_bxc.ActiveVersion}\n" +
                  $"المصدر الرسمي: {m.UpstreamRepo}\n" +
                  (m.UpstreamCommit.Length > 0 ? $"الالتزام المدمج: {m.UpstreamCommit}\n" : "") +
                  $"طريقة التشغيل: حقن document-start عبر WebView2 (بلا Tampermonkey)";
        Dialogs.Alert(this, msg, "حالة Better xCloud");
    }

    /* ── quiet auto-update (never applies mid-session) ───────────────── */

    private async Task QuietUpdateCheckAsync()
    {
        if (!_settings.Current.AutoUpdateBetterXcloud) return;
        try
        {
            var latest = await _bxc.Updates.CheckLatestAsync();
            if (latest is null) return;
            if (Core.Versioning.SemanticVersion.TryParse(latest.Version, out var newV)
                && Core.Versioning.SemanticVersion.TryParse(_bxc.ActiveVersion, out var curV)
                && newV > curV)
            {
                await _bxc.Updates.DownloadAndStageAsync(latest);
                Logger.Info($"Better xCloud v{latest.Version} staged — activates on next restart");
                ShowToast($"تحديث Better xCloud إلى v{latest.Version} جاهز — يُفعّل عند إعادة تشغيل التطبيق");
            }
        }
        catch (Exception ex)
        {
            Logger.Info("Update check skipped: " + ex.Message); // offline etc. — never nag
        }
    }

    /* ── small UI helpers ────────────────────────────────────────────── */

    private enum ChipState { Pending, Ready, Failed, Inactive }

    private void SetChip(ChipState state, string text)
    {
        ChipText.Text = text;
        ChipDot.Fill = state switch
        {
            ChipState.Ready => (Brush)FindResource("BrushGreen"),
            ChipState.Failed => (Brush)FindResource("BrushRed"),
            ChipState.Inactive => (Brush)FindResource("BrushMuted"),
            _ => (Brush)FindResource("BrushGold"),
        };
    }

    private void SetSplash(string status)
    {
        SplashStatus.Text = status;
        Splash.Visibility = Visibility.Visible;
    }

    private void ShowError(string title, string detail)
    {
        Splash.Visibility = Visibility.Collapsed;
        ErrorTitle.Text = title;
        ErrorDetail.Text = detail;
        ErrorScreen.Visibility = Visibility.Visible;
        Logger.Warn("Error screen: " + title);
    }

    public void ShowToast(string text)
    {
        ToastText.Text = text;
        Toast.Visibility = Visibility.Visible;
        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) => { Toast.Visibility = Visibility.Collapsed; _toastTimer.Stop(); };
        _toastTimer.Start();
    }

    /* ── window persistence ──────────────────────────────────────────── */

    private void RestoreWindowBounds()
    {
        var s = _settings.Current;
        if (s.WindowWidth >= MinWidth && s.WindowHeight >= MinHeight)
        {
            var left = SystemParameters.VirtualScreenLeft;
            var top = SystemParameters.VirtualScreenTop;
            var right = left + SystemParameters.VirtualScreenWidth;
            var bottom = top + SystemParameters.VirtualScreenHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = Math.Clamp(s.WindowLeft, left - 40, right - 200);
            Top = Math.Clamp(s.WindowTop, top, bottom - 120);
            Width = s.WindowWidth;
            Height = s.WindowHeight;
        }
        if (s.StartMaximized || s.WindowMaximized) WindowState = WindowState.Maximized;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        CancelProbe();
        _toastTimer?.Stop();
        try
        {
            var s = _settings.Current;
            s.WindowMaximized = WindowState == WindowState.Maximized;
            if (WindowState == WindowState.Normal)
            {
                s.WindowLeft = Left; s.WindowTop = Top;
                s.WindowWidth = Width; s.WindowHeight = Height;
            }
            _settings.Save();
            Logger.Info("Shutdown — settings saved");
        }
        catch (Exception ex)
        {
            Logger.Error("Could not save settings on close: " + ex.Message);
        }
    }
}

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LatchiXcloud.App.Services;
using LatchiXcloud.App.Theme;
using LatchiXcloud.App.WebView;
using LatchiXcloud.Core.Services;

namespace LatchiXcloud.App.Views;

/// <summary>
/// The app shell: one window, one WebView2 instance, custom chrome. The shell becomes
/// almost invisible once the game runs — no polling loops, no overlays over the stream;
/// transient UI lives in Popups (own HWND — the WebView2 is an HwndHost and plain WPF
/// elements would never render above it).
/// v1.0: first-run wizard (language → Microsoft sign-in), keep-session exit question,
/// startup splash window, fullscreen-by-default with a floating exit chip.
/// </summary>
public partial class MainWindow : Window
{
    private SettingsStore _settings = null!;
    private BetterXcloudRuntime _bxc = null!;
    private WebViewHost _host = null!;

    private DispatcherTimer? _probeTimer;
    private DispatcherTimer? _toastTimer;
    private DispatcherTimer? _exitFsTimer;
    private DispatcherTimer? _navWatchdog;
    private int _probeAttempt;
    private bool _firstNavigationDone;
    private bool _hostFullscreen;
    private WindowState _preFullscreenState = WindowState.Normal;
    private bool _bxcFailedThisVersion;

    private string _lang = "ar";
    private string _firstRunStep = Core.Services.FirstRunFlow.StepDone;
    private bool _awaitingSignIn;
    private bool _sessionPromptDone;

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
        _lang = _settings.Current.Language;
        FlowDirection = _lang == "en" ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        ApplyLanguage();
        RestoreWindowBounds();

        _bxc = new BetterXcloudRuntime();
        Logger.Info("Better xCloud runtime ready (v" + _bxc.ActiveVersion + ")");
        _bxc.Initialize(); // bundled/staged/rollback — always before any page loads

        _host = new WebViewHost(Web);
        _host.MessageReceived += OnWebMessage;
        _host.NavigationBlocked += _ => ShowToast(Loc.S(_lang, "errBlockedNav"));
        _host.ProcessFailed += OnWebProcessFailed;

        try
        {
            var loader = LoaderJs.Build(_bxc.Metadata, _bxc.ActiveSource);
            await _host.InitializeAsync(loader);
        }
        catch (Exception ex)
        {
            Logger.Error("WebView2 init failed: " + ex);
            ShowError(Loc.S(_lang, "errWebviewTitle"),
                "Microsoft Edge WebView2 Runtime is required.\n" + ex.Message);
            return;
        }

        Web.CoreWebView2!.NavigationCompleted += OnNavigationCompleted;
        SetChip(ChipState.Pending, "Better xCloud: v" + _bxc.ActiveVersion);

        var online = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
        if (online)
        {
            Logger.Info("Navigating to Xbox Cloud Gaming home…");
            _host.GoHome();
            StartNavWatchdog();
        }
        else
        {
            ShowError(Loc.S(_lang, "errOfflineTitle"), Loc.S(_lang, "errOfflineDetail"));
        }

        // v1.0 first-run: language → Microsoft sign-in.
        // DispatcherPriority.NORMAL — never Background: a continuously rendering page
        // (xbox.com) starves Background-priority callbacks forever (the v1.0.0 hang).
        _firstRunStep = Core.Services.FirstRunFlow.InitialStep(_settings.Current.FirstRunComplete);
        if (_firstRunStep != Core.Services.FirstRunFlow.StepDone)
            _ = Dispatcher.InvokeAsync(RunFirstRunWizard, DispatcherPriority.Normal);
        else if (_settings.Current.StartFullscreen)
            EnterHostFullscreen();
    }

    /* ── navigation watchdog: NEVER hang silently ────────────────────── */

    /// <summary>If the first navigation hasn't finished within 30s, surface an actionable
    /// error instead of sitting on a dead page forever (the v1.0.0 "stuck" symptom).</summary>
    private void StartNavWatchdog()
    {
        _navWatchdog?.Stop();
        _navWatchdog = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(30) };
        _navWatchdog.Tick += (_, _) =>
        {
            _navWatchdog?.Stop();
            if (_firstNavigationDone) return;
            Logger.Error("First navigation did not complete within 30 seconds — showing retry screen");
            ShowError(Loc.S(_lang, "errLoadTitle"), Loc.S(_lang, "errOfflineDetail"));
        };
        _navWatchdog.Start();
    }

    /* ── first-run wizard (v1.0) ─────────────────────────────────────── */

    private void RunFirstRunWizard()
    {
        _firstRunStep = Core.Services.FirstRunFlow.Advance(_firstRunStep); // language step → sign-in
        var wizard = new FirstRunWindow { Owner = this };
        wizard.ShowDialog();
        var s = _settings.Current;
        s.Language = wizard.SelectedLanguage;
        if (s.Language != _lang)
        {
            _lang = s.Language;
            FlowDirection = _lang == "en" ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            ApplyLanguage();
        }
        _settings.Save();

        if (wizard.Result == "signin")
        {
            _awaitingSignIn = true;
            if (Web.CoreWebView2?.Source?.ToString().Contains("login") != true)
                _host.GoHome(); // make sure the sign-in page is in front of the user
        }
        else
        {
            CompleteFirstRun(signedIn: false);
        }
    }

    private void CompleteFirstRun(bool signedIn)
    {
        _awaitingSignIn = false;
        _firstRunStep = Core.Services.FirstRunFlow.StepDone;
        var s = _settings.Current;
        s.FirstRunComplete = true;
        _settings.Save();
        Logger.Info("First-run wizard completed" + (signedIn ? " — signed in" : " — skipped sign-in"));
        if (signedIn) ShowToast(Loc.S(_lang, "welcomeSaved"));
        if (s.StartFullscreen) EnterHostFullscreen();
    }

    /* ── navigation + Better xCloud state ────────────────────────────── */

    private void OnNavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        CancelProbe();

        if (!_firstNavigationDone)
        {
            _firstNavigationDone = true;
            _navWatchdog?.Stop();
            if (!e.IsSuccess)
            {
                if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                    ShowError(Loc.S(_lang, "errLoadTitle"), Loc.S(_lang, "errOfflineDetail"));
                else
                    ShowError(Loc.S(_lang, "errOfflineTitle"), Loc.S(_lang, "errOfflineDetail"));
            }
            _ = QuietUpdateCheckAsync();
        }

        if (!e.IsSuccess) return;

        var url = Web.CoreWebView2!.Source?.ToString();

        // v1.0: the Microsoft sign-in finished — welcome + straight into the cloud
        if (_awaitingSignIn && Core.Services.FirstRunFlow.IsSignInSuccessUrl(url))
            CompleteFirstRun(signedIn: true);

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
        SetChip(ChipState.Failed, "Better xCloud ✗");
        Logger.Error("Better xCloud failed to initialize on " + Logger.SafeUrl(Web.CoreWebView2?.Source?.ToString()));
        if (!_bxcFailedThisVersion)
        {
            _bxcFailedThisVersion = true;
            if (_bxc.Updates.HasPrevious())
            {
                if (_bxc.TryMarkBadAndRollback(_bxc.ActiveVersion))
                {
                    ShowToast(_lang == "en"
                        ? "The new version failed — restored the previous one and reloaded"
                        : "تعذّرت تهيئة النسخة الجديدة — استعدنا النسخة السابقة وأعدنا التحميل");
                    _host.Reload();
                }
            }
            else
            {
                ShowToast(_lang == "en"
                    ? "Better xCloud did not start — reload the page or check Diagnostics"
                    : "Better xCloud لم يبدأ — أعد تحميل الصفحة أو راجع التشخيصات");
            }
        }
    }

    private void CancelProbe()
    {
        _probeTimer?.Stop();
        _probeTimer = null;
        _probeAttempt = 0;
    }

    /* ── web bridge messages (hotkeys + top-edge reporter from the page) ── */

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
                case "mouse-top":
                    if (_hostFullscreen) ShowExitFullscreenChip();
                    break;
                case "bxc-error":
                    Logger.Error("Better xCloud threw during init: " + (detail ?? "?"));
                    SetChip(ChipState.Failed, "Better xCloud ✗");
                    ShowToast(_lang == "en"
                        ? "Better xCloud reported an error during startup"
                        : "Better xCloud أبلغ عن خطأ أثناء البدء");
                    break;
            }
        });
    }

    private void OnWebProcessFailed()
    {
        Dispatcher.Invoke(() =>
        {
            ShowError(_lang == "en" ? "The rendering engine stopped." : "توقّف محرك العرض.",
                _lang == "en"
                    ? "Close and reopen the app. If it keeps happening, check Diagnostics."
                    : "أغلق التطبيق وأعد فتحه. إذا تكرر الأمر، راجع التشخيصات.");
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
        HideExitFullscreenChip();
    }

    private void ToggleHostFullscreen()
    {
        if (_hostFullscreen) ExitHostFullscreen();
        else EnterHostFullscreen();
    }

    /// <summary>Floating chip that appears when the mouse touches the top edge in fullscreen
    /// (its Popup has its own HWND — always visible above the WebView2).</summary>
    private void ShowExitFullscreenChip()
    {
        ExitFsPopup.HorizontalOffset = Math.Max(8, Web.ActualWidth - 56);
        ExitFsPopup.VerticalOffset = 10;
        ExitFsPopup.IsOpen = true;
        _exitFsTimer?.Stop();
        _exitFsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _exitFsTimer.Tick += (_, _) => { _exitFsTimer.Stop(); ExitFsPopup.IsOpen = false; };
        _exitFsTimer.Start();
    }

    private void HideExitFullscreenChip()
    {
        _exitFsTimer?.Stop();
        ExitFsPopup.IsOpen = false;
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
    private void BtnExitFs_Click(object sender, RoutedEventArgs e) => ExitHostFullscreen();
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

    /* ── error screen actions (ErrorWindow dialog, own HWND above the WebView2) ── */

    private void ShowError(string title, string detail)
    {
        Logger.Warn("Error screen: " + title);
        var win = new ErrorWindow(this, title, detail, _lang) { Owner = this };
        win.ShowDialog();
        switch (win.Chosen)
        {
            case ErrorWindow.Action.Retry:
                _host.GoHome();
                break;
            case ErrorWindow.Action.Reload:
                _host.Reload();
                break;
            case ErrorWindow.Action.OpenBrowser:
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = "https://www.xbox.com/play", UseShellExecute = true });
                }
                catch (Exception ex) { Logger.Warn("Could not open system browser: " + ex.Message); }
                break;
            case ErrorWindow.Action.Diagnostics:
                OpenDiagnostics();
                break;
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
        var m = $"Better xCloud: v{_bxc.ActiveVersion}\n" +
                $"redphx/better-xcloud (official)\n" +
                (_bxc.ActiveManifest.UpstreamCommit.Length > 0 ? $"@ {_bxc.ActiveManifest.UpstreamCommit[..7]}\n" : "") +
                (_lang == "en"
                    ? "Runs via document-start injection through WebView2 (no Tampermonkey)"
                    : "الحقن: document-start عبر WebView2 (بلا Tampermonkey)");
        Dialogs.Alert(this, m, "Better xCloud");
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
                ShowToast(_lang == "en"
                    ? $"Better xCloud v{latest.Version} is ready — activates on next restart"
                    : $"تحديث Better xCloud إلى v{latest.Version} جاهز — يُفعّل عند إعادة تشغيل التطبيق");
            }
        }
        catch (Exception ex)
        {
            Logger.Info("Update check skipped: " + ex.Message); // offline etc. — never nag
        }
    }

    /* ── small UI helpers ────────────────────────────────────────────── */

    private enum ChipState { Pending, Ready, Failed, Inactive }

    private void ApplyLanguage()
    {
        BtnHome.ToolTip = Loc.S(_lang, "tipHome");
        BtnReload.ToolTip = Loc.S(_lang, "tipReload");
        BtnFullscreen.ToolTip = Loc.S(_lang, "tipFullscreen");
        BtnSettings.ToolTip = Loc.S(_lang, "tipSettings");
        BtnDiag.ToolTip = Loc.S(_lang, "tipDiag");
        BtnExitFs.ToolTip = Loc.S(_lang, "tipExitFs");
        BtnMin.ToolTip = Loc.S(_lang, "tipMin");
        BtnMax.ToolTip = Loc.S(_lang, "tipMax");
        BtnClose.ToolTip = Loc.S(_lang, "tipClose");
        ChipBxc.ToolTip = Loc.S(_lang, "tipDiag") is { } ? "Better xCloud" : "";
        ChipBxc.ToolTip = "Better xCloud — " + (_lang == "en" ? "click for details" : "اضغط للتفاصيل");
    }

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

    public void ShowToast(string text)
    {
        ToastText.Text = text;
        ToastPopup.HorizontalOffset = Math.Max(0, (Web.ActualWidth - 420) / 2);
        ToastPopup.VerticalOffset = Math.Max(8, Web.ActualHeight - 70);
        ToastPopup.IsOpen = true;
        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) => { ToastPopup.IsOpen = false; _toastTimer.Stop(); };
        _toastTimer.Start();
    }

    /* ── keep-session question on exit (v1.0) ────────────────────────── */

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // ask once per run, only when a live webview exists and the wizard isn't mid-sign-in
        if (!_sessionPromptDone
            && _host is not null
            && _host.Core is not null
            && !_awaitingSignIn
            && FirstRunFlow.NormalizeKeepChoice(_settings.Current.KeepSessionOnExit) == "ask")
        {
            e.Cancel = true;
            _sessionPromptDone = true;
            Dispatcher.InvokeAsync(() => PromptKeepSessionThenClose());
            return;
        }

        CancelProbe();
        _toastTimer?.Stop();
        _exitFsTimer?.Stop();
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

    private async void PromptKeepSessionThenClose()
    {
        try
        {
            var choice = Dialogs.KeepSession(this, _lang);
            var s = _settings.Current;
            if (choice is { Keep: false })
            {
                await _host.ClearLoginCookiesAsync(); // sign out on request — nothing else
                if (choice.Value.Remember) s.KeepSessionOnExit = "signout";
            }
            else if (choice is { Keep: true } && choice.Value.Remember)
            {
                s.KeepSessionOnExit = "keep";
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Keep-session prompt failed: " + ex.Message);
        }
        finally
        {
            Close(); // second pass: the prompt is done, normal shutdown continues
        }
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
}

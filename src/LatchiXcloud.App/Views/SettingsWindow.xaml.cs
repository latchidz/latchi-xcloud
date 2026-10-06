using System.Windows;
using LatchiXcloud.App.Services;
using LatchiXcloud.App.WebView;
using LatchiXcloud.Core.Models;
using LatchiXcloud.Core.Services;

namespace LatchiXcloud.App.Views;

/// <summary>Host-app settings. Better xCloud's own settings live inside the page and stay untouched.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private readonly BetterXcloudRuntime _bxc;
    private readonly WebViewHost _host;
    private Core.Updates.ReleaseInfo? _pendingUpdate;

    public SettingsWindow(Window owner, SettingsStore store, BetterXcloudRuntime bxc, WebViewHost host)
    {
        _store = store;
        _bxc = bxc;
        _host = host;
        InitializeComponent();
        Owner = owner;

        var s = store.Current;
        ChkMaximized.IsChecked = s.StartMaximized;
        ChkFullscreen.IsChecked = s.StartFullscreen;
        ChkLowEnd.IsChecked = s.LowEndMode;
        ChkAutoUpdate.IsChecked = s.AutoUpdateBetterXcloud;
        CmbLang.SelectedIndex = s.Language == "en" ? 1 : 0;
        CmbKeepSession.SelectedIndex = Core.Services.FirstRunFlow.NormalizeKeepChoice(s.KeepSessionOnExit) switch
        {
            "keep" => 1, "signout" => 2, _ => 0
        };
        TxtBxcVersion.Text = "v" + _bxc.ActiveVersion;
        if (_bxc.ActiveManifest.UpstreamCommit.Length >= 7)
            TxtBxcSource.Text = "المصدر الرسمي: redphx/better-xcloud @ " + _bxc.ActiveManifest.UpstreamCommit[..7];
        if (_bxc.Updates.HasPrevious()) BtnRollback.Visibility = Visibility.Visible;
    }

    public void FocusUpdateSection() => CardBxc.BringIntoView();

    private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        TxtUpdateStatus.Text = "جارٍ فحص المصدر الرسمي…";
        try
        {
            var latest = await _bxc.Updates.CheckLatestAsync();
            if (latest is null)
            {
                TxtUpdateStatus.Text = "تعذّر جلب معلومات الإصدار من المصدر الرسمي.";
                return;
            }
            var newer = Core.Versioning.SemanticVersion.TryParse(latest.Version, out var nv)
                        && Core.Versioning.SemanticVersion.TryParse(_bxc.ActiveVersion, out var cv)
                        && nv > cv;
            if (newer)
            {
                _pendingUpdate = latest;
                TxtUpdateStatus.Text = $"متاح: v{latest.Version} (الحالية v{_bxc.ActiveVersion}).";
                BtnUpdateNow.Visibility = Visibility.Visible;
            }
            else
            {
                TxtUpdateStatus.Text = $"أنت على أحدث نسخة رسمية (v{_bxc.ActiveVersion}).";
            }
        }
        catch (Exception ex)
        {
            TxtUpdateStatus.Text = "فشل الفحص (تحقق من الإنترنت): " + ex.Message;
        }
    }

    private async void BtnUpdateNow_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate is null) return;
        TxtUpdateStatus.Text = $"جارٍ تنزيل v{_pendingUpdate.Version} من الإصدار الرسمي والتحقق منه…";
        try
        {
            await _bxc.Updates.DownloadAndStageAsync(_pendingUpdate);
            TxtUpdateStatus.Text = $"تم تجهيز v{_pendingUpdate.Version} والتحقق منه. سيُفعَّل عند إعادة تشغيل التطبيق.";
            BtnUpdateNow.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            TxtUpdateStatus.Text = "رُفض التحديث: " + ex.Message;
        }
    }

    private void BtnRollback_Click(object sender, RoutedEventArgs e)
    {
        if (!Dialogs.Confirm(this,
                $"سيتم استرجاع النسخة السابقة من Better xCloud بدل v{_bxc.ActiveVersion}.",
                "استرجاع النسخة السابقة؟"))
            return;
        try
        {
            var restored = _bxc.Updates.Rollback();
            TxtBxcVersion.Text = "v" + restored.Version;
            TxtUpdateStatus.Text = "تم الاسترجاع. أعد تشغيل التطبيق لتشغيل النسخة المستعادة.";
        }
        catch (Exception ex)
        {
            Dialogs.Alert(this, "تعذّر الاسترجاع: " + ex.Message, "استرجاع");
        }
    }

    private void BtnOpenData_Click(object sender, RoutedEventArgs e)
    {
        AppPaths.EnsureDataDir();
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppPaths.DataDir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Dialogs.Alert(this, "تعذّر فتح المجلد: " + ex.Message, "مجلد البيانات");
        }
    }

    private async void BtnClearBxc_Click(object sender, RoutedEventArgs e)
    {
        var url = _host.Core?.Source?.ToString() ?? "";
        if (!url.Contains("xbox.com", StringComparison.Ordinal))
        {
            Dialogs.Alert(this, "افتح الصفحة الرئيسية لـ xCloud أولاً (يجب أن تكون على نطاق xbox.com لمسح بيانات Better xCloud).", "مسح بيانات Better xCloud");
            return;
        }
        if (!Dialogs.Confirm(this,
                "سيتم مسح إعدادات Better xCloud وبياناته المخزنة محلياً. تسجيل الدخول لا يتأثر.",
                "مسح بيانات Better xCloud؟"))
            return;
        try
        {
            var raw = await _host.Core!.ExecuteScriptAsync("window.__LATCHI_CLEAR_BXC ? window.__LATCHI_CLEAR_BXC() : '{\"ok\":false}'");
            var ok = raw.Contains("\"ok\":true");
            Dialogs.Alert(this, ok ? "تم مسح بيانات Better xCloud. أعد تحميل الصفحة." : "تعذّر المسح — أعد تحميل الصفحة وحاول مجدداً.", "مسح بيانات Better xCloud");
        }
        catch (Exception ex)
        {
            Dialogs.Alert(this, "تعذّر المسح: " + ex.Message, "مسح بيانات Better xCloud");
        }
    }

    private void BtnResetApp_Click(object sender, RoutedEventArgs e)
    {
        if (!Dialogs.Confirm(this,
                "سيتم حذف كل شيء: ملف تعريف المتصفح المخصص، تسجيل الدخول، إعدادات التطبيق، وإعدادات Better xCloud.",
                "إعادة تعيين كاملة لبيانات التطبيق؟"))
            return;
        if (!Dialogs.Confirm(this,
                "هذا سيُخرجك من حساب Microsoft وسيعود التطبيق كأنه أول تثبيت. لا رجوع بعدها.",
                "متأكد تماماً؟"))
            return;

        _bxc.ScheduleBetterXcloudDataReset();
        File.WriteAllText(Path.Combine(AppPaths.DataDir, "pending-app-reset.flag"), "reset");
        Dialogs.Alert(this, "سيتم التنفيذ عند إغلاق التطبيق وإعادة فتحه.", "إعادة التعيين");
        Save();
        Close();
        Application.Current.MainWindow?.Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => SaveAndClose();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        Save();
        base.OnClosing(e);
    }

    private void SaveAndClose() { Save(); Close(); }

    private void Save()
    {
        var s = _store.Current;
        s.StartMaximized = ChkMaximized.IsChecked == true;
        s.StartFullscreen = ChkFullscreen.IsChecked == true;
        s.LowEndMode = ChkLowEnd.IsChecked == true;
        s.AutoUpdateBetterXcloud = ChkAutoUpdate.IsChecked == true;
        s.Language = CmbLang.SelectedIndex == 1 ? "en" : "ar";
        s.KeepSessionOnExit = CmbKeepSession.SelectedIndex switch
        {
            1 => "keep", 2 => "signout", _ => "ask"
        };
        _store.Save(s);
    }

}

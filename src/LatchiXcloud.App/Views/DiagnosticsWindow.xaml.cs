using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LatchiXcloud.App.Services;
using LatchiXcloud.App.WebView;
using LatchiXcloud.Core.Services;

namespace LatchiXcloud.App.Views;

/// <summary>
/// Lightweight diagnostics (§42): versions, runtime, GPU/WebGL evidence, gamepads,
/// network. Never shows or exports credentials — the browser profile stays sealed.
/// </summary>
public partial class DiagnosticsWindow : Window
{
    private readonly WebViewHost _host;
    private readonly BetterXcloudRuntime _bxc;
    private readonly List<(string Key, string Value)> _rows = new();

    public DiagnosticsWindow(Window owner, WebViewHost host, BetterXcloudRuntime bxc)
    {
        _host = host;
        _bxc = bxc;
        InitializeComponent();
        Owner = owner;
        Loaded += async (_, _) => await GatherAsync();
    }

    private async Task GatherAsync()
    {
        Add("LATCHI xCLOUD", App.AppVersion);
        Add("نظام التشغيل", Environment.OSVersion.VersionString + (Environment.Is64BitOperatingSystem ? " (x64)" : ""));
        Add("معمارية العملية", Environment.Is64BitProcess ? "x64" : "x86");
        Add("محرك العرض (WebView2)", _host.RuntimeVersion.Length > 0 ? _host.RuntimeVersion : "غير مهيأ");
        Add("Better xCloud", "v" + _bxc.ActiveVersion +
            (_bxc.ActiveManifest.UpstreamCommit.Length >= 7 ? "  @ " + _bxc.ActiveManifest.UpstreamCommit[..7] : ""));
        Add("مصدر Better xCloud", "github.com/" + _bxc.ActiveManifest.UpstreamRepo + " (رسمي)");
        Add("الشبكة", System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable() ? "متاحة" : "غير متاحة");
        Add("وضع الجهاز الضعيف", "غير مفعّل / مفعّل (لا يؤثر على تسريع العتاد)");

        // v1.2: sanitized navigation history — see exactly where the auth flow went
        var navLog = _host.SnapshotNavigationLog();
        if (navLog.Length > 0)
        {
            Add("آخر عمليات التنقل (منقَّحة — بلا رموز دخول)",
                string.Join("\n", navLog.TakeLast(14)));
        }

        // GPU / WebGL / gamepads — probed inside the live webview document
        try
        {
            var raw = await _host.Core!.ExecuteScriptAsync(GpuProbeJs);
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            var s = doc.RootElement.GetString();
            var inner = System.Text.Json.JsonDocument.Parse(s ?? "{}").RootElement;
            string gpu = inner.TryGetProperty("gpu", out var g) ? g.GetString() ?? "" : "";
            string webgl = inner.TryGetProperty("webgl", out var w) ? w.GetString() ?? "" : "";
            int pads = inner.TryGetProperty("gamepads", out var p) ? p.GetInt32() : 0;

            Add("WebGL", webgl == "ok" ? "مدعوم ✓" : "غير متاح ✗");
            Add("معالج الرسوميات (WebGL)", gpu.Length > 0 ? gpu : "غير معروف");
            Add("تسريع العتاد", gpu.Length > 0 && webgl == "ok"
                ? "نشط (عبر معالج الرسوميات أعلاه)"
                : "NOT VERIFIED — لم يمكن التحقق");
            Add("يد التحكم (Gamepad)", pads > 0 ? $"{pads} متصلة ✓" : "غير متصلة (وصلها ثم أعد الفتح)");
        }
        catch (Exception ex)
        {
            Add("فحص WebGL/Gamepad", "تعذّر: " + ex.Message);
        }

        Add("User Agent", _host.Core?.Settings?.UserAgent is { } ua && ua.Length > 0 ? ua : "(افتراضي Edge/WebView2)");
        Render();
    }

    private const string GpuProbeJs =
"""
(function () {
  try {
    var c = document.createElement('canvas');
    var gl = c.getContext('webgl') || c.getContext('experimental-webgl');
    if (!gl) return JSON.stringify({ webgl: 'no' });
    var dbg = gl.getExtension('WEBGL_debug_renderer_info');
    var gpu = dbg ? gl.getParameter(dbg.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
    var pads = 0;
    try { (navigator.getGamepads() || []).forEach(function (p) { if (p) pads++; }); } catch (e) {}
    return JSON.stringify({ webgl: 'ok', gpu: String(gpu), gamepads: pads });
  } catch (err) { return JSON.stringify({ webgl: 'err', gpu: '', gamepads: 0 }); }
})()
""";

    private void Add(string key, string value) => _rows.Add((key, value));

    private void Render()
    {
        Rows.Children.Clear();
        foreach (var (key, value) in _rows)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(185) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var k = new TextBlock { Text = key, Foreground = (Brush)FindResource("BrushMuted"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
            var v = new TextBlock { Text = value, Foreground = (Brush)FindResource("BrushText"), FontSize = 12, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(k, 0); Grid.SetColumn(v, 1);
            grid.Children.Add(k); grid.Children.Add(v);
            Rows.Children.Add(grid);
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = "LATCHI xCLOUD diagnostics\n" + string.Join("\n", _rows.Select(r => r.Key + ": " + r.Value));
            Clipboard.SetText(text);
            ((Button)sender).Content = "تم النسخ ✓";
        }
        catch { /* clipboard may be locked */ }
    }

    private void BtnLogs_Click(object sender, RoutedEventArgs e)
    {
        AppPaths.EnsureDataDir();
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            { FileName = AppPaths.LogsDir, UseShellExecute = true });
        }
        catch { }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}

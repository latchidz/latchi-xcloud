using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LatchiXcloud.App.Theme;
using WPath = System.Windows.Shapes.Path;

namespace LatchiXcloud.App.Views;

/// <summary>
/// The error / offline screen — a dialog with its own HWND so it is always visible above the
/// WebView2 (airspace). Actions: retry, reload the page, open in the system browser, diagnostics.
/// </summary>
public class ErrorWindow : Window
{
    public enum Action { Retry, Reload, OpenBrowser, Diagnostics }

    public Action Chosen { get; private set; } = Action.Retry;

    public ErrorWindow(Window owner, string title, string detail, string lang)
    {
        Title = "LATCHI xCLOUD";
        Width = 540;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Background = Res("BrushBg");
        FlowDirection = lang == "en" ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        FontFamily = new FontFamily("Segoe UI");
        Owner = owner;

        var sp = new StackPanel { Margin = new Thickness(26) };

        sp.Children.Add(new WPath
        {
            Data = Icons.Warn,
            Stroke = Res("BrushGold"), StrokeThickness = 1.5, Fill = Brushes.Transparent,
            Width = 42, Height = 42, Stretch = Stretch.Uniform,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        sp.Children.Add(new TextBlock
        {
            Text = title, Foreground = Brushes.White, FontSize = 16.5, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 6),
            TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
        });
        sp.Children.Add(new TextBlock
        {
            Text = detail, Foreground = Res("BrushMuted"), FontSize = 12.5,
            HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        });

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 22, 0, 0),
        };
        row.Children.Add(MkBtn(lang == "en" ? "Retry" : "إعادة المحاولة", Action.Retry, primary: true));
        row.Children.Add(MkBtn(lang == "en" ? "Reload page" : "إعادة تحميل الصفحة", Action.Reload));
        row.Children.Add(MkBtn(lang == "en" ? "Open in browser" : "فتح في المتصفح", Action.OpenBrowser));
        row.Children.Add(MkBtn(lang == "en" ? "Diagnostics" : "التشخيصات", Action.Diagnostics));
        sp.Children.Add(row);

        Content = sp;
    }

    private static Brush Res(string key) => (Brush)App.Current.FindResource(key);

    private Button MkBtn(string text, Action action, bool primary = false)
    {
        var btn = new Button
        {
            Content = text,
            Height = 34,
            Margin = new Thickness(5, 0, 5, 0),
            Cursor = Cursors.Hand,
            Focusable = false,
            Background = primary ? Res("BrushGold") : Brushes.Transparent,
            Foreground = primary ? new SolidColorBrush(Color.FromRgb(0x0A, 0x0D, 0x14)) : Res("BrushMuted"),
            FontWeight = primary ? FontWeights.Bold : FontWeights.Normal,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 0, 12, 0),
            Template = Dialogs.ButtonTemplate(),
        };
        btn.Click += (_, _) => { Chosen = action; Close(); };
        return btn;
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LatchiXcloud.App.Theme;

namespace LatchiXcloud.App.Views;

/// <summary>Small dark dialogs built in code — Alert / Confirm / KeepSession (navy+gold).</summary>
public static class Dialogs
{
    private static Window OwnerOf(Window? owner) => owner ?? Application.Current?.MainWindow ?? new Window();

    /// <summary>The exit question: "keep your account signed in?" (v1.0).
    /// Returns (keep, remember) — or null when dismissed without a choice (treated as keep: we
    /// never wipe a login session without an explicit request).</summary>
    public static (bool Keep, bool Remember)? KeepSession(Window? owner, string lang)
    {
        (bool Keep, bool Remember)? result = null;
        var win = Base(500, Loc.S(lang, "keepTitle"));
        var sp = new StackPanel { Margin = new Thickness(24) };

        sp.Children.Add(new TextBlock
        {
            Text = Loc.S(lang, "keepMessage"),
            Foreground = Brushes.White, FontSize = 13.5, TextWrapping = TextWrapping.Wrap,
        });

        var remember = new CheckBox
        {
            Content = Loc.S(lang, "keepRemember"),
            Foreground = Res("BrushMuted"), FontSize = 12, Margin = new Thickness(0, 14, 0, 4),
            Cursor = Cursors.Hand, Focusable = false,
        };
        sp.Children.Add(remember);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0),
        };
        row.Children.Add(MkBtn(Loc.S(lang, "keepYes"), () => { result = (true, remember.IsChecked == true); win.Close(); }, primary: true, w: 200));
        row.Children.Add(MkBtn(Loc.S(lang, "keepNo"), 200, 34, () => { result = (false, remember.IsChecked == true); win.Close(); }));
        sp.Children.Add(row);

        win.Content = sp;
        win.Owner = OwnerOf(owner);
        win.ShowDialog();
        return result;
    }

    public static ControlTemplate ButtonTemplate() => MkBtnTemplate();

    public static void Alert(Window? owner, string message, string title)
    {
        var win = Base(520, title);
        var sp = new StackPanel { Margin = new Thickness(24) };
        sp.Children.Add(new TextBlock
        {
            Text = message, Foreground = Brushes.White, FontSize = 13.5,
            TextWrapping = TextWrapping.Wrap,
        });
        var ok = MkBtn("حسناً", 104, 34, win.Close, primary: true);
        ok.HorizontalAlignment = HorizontalAlignment.Center;
        ok.Margin = new Thickness(0, 20, 0, 0);
        sp.Children.Add(ok);
        win.Content = sp;
        win.Owner = OwnerOf(owner);
        win.ShowDialog();
    }

    public static bool Confirm(Window? owner, string message, string question)
    {
        bool? result = null;
        var win = Base(520, "تأكيد");
        var sp = new StackPanel { Margin = new Thickness(24) };
        sp.Children.Add(new TextBlock
        {
            Text = message, Foreground = Brushes.White, FontSize = 13.5,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6),
        });
        sp.Children.Add(new TextBlock
        {
            Text = question, Foreground = Res("BrushGold"),
            FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 18),
        });
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        row.Children.Add(MkBtn("نعم", () => { result = true; win.Close(); }, primary: true));
        row.Children.Add(MkBtn("لا", 76, 34, () => { result = false; win.Close(); }));
        sp.Children.Add(row);
        win.Content = sp;
        win.Owner = OwnerOf(owner);
        win.ShowDialog();
        return result == true;
    }

    private static Brush Res(string key) => (Brush)App.Current.FindResource(key);

    private static Window Base(double w, string title) => new()
    {
        Title = title,
        Width = w,
        SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        ResizeMode = ResizeMode.NoResize,
        ShowInTaskbar = false,
        Background = Res("BrushPanel"),
        FlowDirection = FlowDirection.RightToLeft,
        FontFamily = new FontFamily("Segoe UI"),
    };

    private static Button MkBtn(string text, Action onClick, bool primary = false, double w = 84)
        => MkBtn(text, w, 34, onClick, primary);

    private static Button MkBtn(string text, double w, double h, Action onClick, bool primary = false)
    {
        var btn = new Button
        {
            Content = text, Width = w, Height = h, Margin = new Thickness(5, 0, 5, 0),
            Cursor = Cursors.Hand, Focusable = false,
            Background = primary ? Res("BrushGold") : Brushes.Transparent,
            Foreground = primary ? new SolidColorBrush(Color.FromRgb(0x0A, 0x0D, 0x14)) : Res("BrushMuted"),
            FontWeight = primary ? FontWeights.Bold : FontWeights.Normal,
            BorderThickness = new Thickness(0),
            Template = MkBtnTemplate(),
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static ControlTemplate MkBtnTemplate()
    {
        const string xaml = @"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                             xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>
            <Border x:Name='bd' Background='{TemplateBinding Background}' CornerRadius='7'
                    BorderBrush='#242D42' BorderThickness='1'>
                <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' Margin='10,0'/>
            </Border>
            <ControlTemplate.Triggers>
                <Trigger Property='IsMouseOver' Value='True'>
                    <Setter TargetName='bd' Property='Opacity' Value='0.85'/>
                </Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>";
        return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
    }
}

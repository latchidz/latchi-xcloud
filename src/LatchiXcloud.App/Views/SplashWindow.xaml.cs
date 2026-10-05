using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using LatchiXcloud.Core.Services;

namespace LatchiXcloud.App.Views;

/// <summary>
/// The v1.0 startup splash: animated, with the synthesized startup chime and the user's
/// profile picture (when set). Shows instantly while the heavy WebView2 initializes —
/// a real gain on low-end machines — then fades out as soon as the main window is ready
/// (but never faster than the chime, so it never feels cut short).
/// </summary>
public partial class SplashWindow : Window
{
    private static readonly TimeSpan MinShow = TimeSpan.FromSeconds(2.4);
    private readonly DateTime _shownAt = DateTime.UtcNow;
    private SoundPlayer? _chime;
    private bool _closing;

    public SplashWindow()
    {
        InitializeComponent();
        VersionLine.Text = "LATCHI xCLOUD  ·  v" + (App.AppVersion);
        ShowProfilePicture();
    }

    private void ShowProfilePicture()
    {
        try
        {
            var pic = ProfileImage.FindExisting(AppPaths.DataDir);
            if (pic is null) return;
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(pic);
            img.EndInit();
            img.Freeze();
            Pic.Source = img;
            Pic.Visibility = Visibility.Visible;
            Emblem.Visibility = Visibility.Collapsed;
        }
        catch
        {
            // a broken picture must never break the splash — fall back to the emblem
        }
    }

    /// <summary>Plays the synthesized startup chime (original composition, Xbox-inspired).</summary>
    public void PlayChime()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/assets/startup-chime.wav");
            var sri = System.Windows.Application.GetResourceStream(uri);
            if (sri is null) return;
            _chime = new SoundPlayer(sri.Stream);
            _chime.Play(); // async — never blocks the UI thread
        }
        catch
        {
            // no audio device / missing resource: stay silent, never crash on startup
        }
    }

    public void SetStatus(string status) => Dispatcher.Invoke(() => SplashStatus.Text = status);

    /// <summary>Main window is ready: fade out (respecting the minimum show time).</summary>
    public void NotifyReady()
    {
        if (_closing) return;
        _closing = true;

        var elapsed = DateTime.UtcNow - _shownAt;
        var remaining = MinShow - elapsed;
        var delay = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;

        var closeTimer = new System.Windows.Threading.DispatcherTimer { Interval = delay };
        closeTimer.Tick += (_, _) =>
        {
            closeTimer.Stop();
            FadeOutAndClose();
        };
        closeTimer.Start();
    }

    /// <summary>Immediate close, no animation (app is shutting down anyway).</summary>
    public void ForceClose()
    {
        _closing = true;
        try { Close(); } catch { }
    }

    private void FadeOutAndClose()
    {
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.32))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        fade.Completed += (_, _) =>
        {
            _chime?.Dispose();
            try { Close(); } catch { }
        };
        Root.BeginAnimation(OpacityProperty, fade);
    }
}

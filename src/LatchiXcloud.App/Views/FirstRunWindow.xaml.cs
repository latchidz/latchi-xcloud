using System.Windows;
using System.Windows.Controls;
using LatchiXcloud.App.Services;
using LatchiXcloud.App.Theme;
using LatchiXcloud.Core.Services;

namespace LatchiXcloud.App.Views;

/// <summary>
/// v1.2 first-run wizard — six steps (§9):
///   (1) LATCHI UI language (Arabic/English),
///   (2) stream quality  — the REAL Better xCloud target resolution,
///   (3) server/region   — the REAL Better xCloud server table (Auto + 19 datacenters),
///   (4) game language   — stream.locale, INDEPENDENT from the app UI language,
///   (5) optional connection helper — external Planet VPN (detect / open / status),
///   (6) Microsoft sign-in — the real Microsoft page opens AFTER configuration.
/// All values are applied to the real Better xCloud configuration before the very
/// first navigation; the Xbox site is never opened before this wizard completes.
/// </summary>
public partial class FirstRunWindow : Window
{
    public string SelectedLanguage { get; private set; } = "ar";
    /// <summary>Chosen Better xCloud target resolution (stream.video.resolution).</summary>
    public string SelectedStreamQuality { get; private set; } = "auto";
    /// <summary>Chosen Better xCloud server/region (server.region; "default" = Auto).</summary>
    public string SelectedServerRegion { get; private set; } = "default";
    /// <summary>Chosen preferred game language (stream.locale).</summary>
    public string SelectedGameLanguage { get; private set; } = "default";
    /// <summary>Result of the wizard: "signin" = user opened the sign-in page, "skip" = postponed.</summary>
    public string Result { get; private set; } = "skip";

    public FirstRunWindow()
    {
        InitializeComponent();

        // REAL Better xCloud options only — no invented values (BxcSettings ↔ bundled script)
        CmbQuality.ItemsSource = BxcSettings.StreamQualities.Select(q => q.Label).ToList();
        CmbQuality.SelectedIndex = 0;
        CmbServer.ItemsSource = BxcSettings.ServerRegions.Select(r => r.Label).ToList();
        CmbServer.SelectedIndex = 0;
        CmbGameLang.ItemsSource = BxcSettings.GameLocales.Select(l => l.Label).ToList();
        CmbGameLang.SelectedIndex = 0;

        ApplyLanguage(SelectedLanguage);
        RefreshVpnStatus();
    }

    private void ApplyLanguage(string lang)
    {
        FlowDirection = lang == "en" ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

        QualityTitle.Text = Loc.S(lang, "wizardQualityTitle");
        QualityDetail.Text = Loc.S(lang, "wizardQualityDetail");
        ServerTitle.Text = Loc.S(lang, "wizardServerTitle");
        ServerDetail.Text = Loc.S(lang, "wizardServerDetail");
        GameLangTitle.Text = Loc.S(lang, "wizardGameLangTitle");
        GameLangDetail.Text = Loc.S(lang, "wizardGameLangDetail");
        VpnTitle.Text = Loc.S(lang, "wizardVpnTitle");
        VpnDetail.Text = Loc.S(lang, "wizardVpnDetail");
        SignInTitle.Text = Loc.S(lang, "wizardSignInTitle");
        SignInDetail.Text = Loc.S(lang, "wizardSignInDetail");

        BtnQualityNext.Content = BtnServerNext.Content = BtnGameLangNext.Content = BtnVpnNext.Content = Loc.S(lang, "wizardNext");
        BtnQualityBack.Content = BtnServerBack.Content = BtnGameLangBack.Content = BtnVpnBack.Content = Loc.S(lang, "wizardBack");
        BtnGoSignIn.Content = Loc.S(lang, "wizardSignInOpen");
        BtnSkip.Content = Loc.S(lang, "wizardSkip");
        BtnVpnOpen.Content = Loc.S(lang, "wizardVpnOpen");
        BtnVpnCheck.Content = Loc.S(lang, "wizardVpnCheck");
    }

    private void ShowStep(StackPanel step, int index, int total)
    {
        StepLanguage.Visibility = step == StepLanguage ? Visibility.Visible : Visibility.Collapsed;
        StepQuality.Visibility = step == StepQuality ? Visibility.Visible : Visibility.Collapsed;
        StepServer.Visibility = step == StepServer ? Visibility.Visible : Visibility.Collapsed;
        StepGameLang.Visibility = step == StepGameLang ? Visibility.Visible : Visibility.Collapsed;
        StepVpn.Visibility = step == StepVpn ? Visibility.Visible : Visibility.Collapsed;
        StepSignIn.Visibility = step == StepSignIn ? Visibility.Visible : Visibility.Collapsed;
        StepIndicator.Text = _lang == "en" ? $"{index} / {total}" : $"{ToArabicDigits(index)} / {ToArabicDigits(total)}";
    }

    private string _lang = "ar";
    private static string ToArabicDigits(int n) =>
        new(n.ToString().Select(c => (char)('٠' + (c - '0'))).ToArray());

    /* ── step 1: language ─────────────────────────────────────────── */

    private void Choose(string lang)
    {
        SelectedLanguage = lang;
        _lang = lang;
        ApplyLanguage(lang);
        ShowStep(StepQuality, 2, 6); // config BEFORE the site opens
    }

    private void BtnArabic_Click(object sender, RoutedEventArgs e) => Choose("ar");
    private void BtnEnglish_Click(object sender, RoutedEventArgs e) => Choose("en");

    /* ── step 2: stream quality ───────────────────────────────────── */

    private void BtnQualityNext_Click(object sender, RoutedEventArgs e) => ShowStep(StepServer, 3, 6);
    private void BtnQualityBack_Click(object sender, RoutedEventArgs e) => ShowStep(StepLanguage, 1, 6);

    /* ── step 3: server / region ──────────────────────────────────── */

    private void BtnServerNext_Click(object sender, RoutedEventArgs e) => ShowStep(StepGameLang, 4, 6);
    private void BtnServerBack_Click(object sender, RoutedEventArgs e) => ShowStep(StepQuality, 2, 6);

    /* ── step 4: game language ────────────────────────────────────── */

    private void BtnGameLangNext_Click(object sender, RoutedEventArgs e) => ShowStep(StepVpn, 5, 6);
    private void BtnGameLangBack_Click(object sender, RoutedEventArgs e) => ShowStep(StepServer, 3, 6);

    /* ── step 5: connection helper (external VPN) ─────────────────── */

    private void BtnVpnNext_Click(object sender, RoutedEventArgs e) => ShowStep(StepSignIn, 6, 6);
    private void BtnVpnBack_Click(object sender, RoutedEventArgs e) => ShowStep(StepGameLang, 4, 6);

    private void BtnVpnOpen_Click(object sender, RoutedEventArgs e)
    {
        if (!VpnHelper.TryOpenClient())
            Dialogs.Alert(this, Loc.S(_lang, "vpnNotInstalled"), Loc.S(_lang, "wizardVpnTitle"));
        RefreshVpnStatus();
    }

    private void BtnVpnCheck_Click(object sender, RoutedEventArgs e) => RefreshVpnStatus();

    private void RefreshVpnStatus()
    {
        var installed = VpnHelper.IsInstalled;
        var (connected, ifName, detail) = VpnHelper.GetConnectionStatus();
        TxtVpnStatus.Text =
            (connected, installed) switch
            {
                (true, _) => Loc.S(_lang, "vpnStatusConnected") + " — " + detail,
                (false, true) => Loc.S(_lang, "vpnStatusInstalledNotConnected"),
                (false, false) => Loc.S(_lang, "vpnStatusNotInstalled"),
            };
        BtnVpnOpen.Visibility = installed ? Visibility.Visible : Visibility.Collapsed;
    }

    /* ── step 6: sign-in ──────────────────────────────────────────── */

    private void BtnGoSignIn_Click(object sender, RoutedEventArgs e)
    {
        // final read of every selection (SelectionChanged handlers keep them current,
        // but re-read defensively in case a ComboBox never fired)
        SelectedStreamQuality = BxcSettings.StreamQualities[Math.Max(0, CmbQuality.SelectedIndex)].Value;
        SelectedServerRegion = BxcSettings.ServerRegions[Math.Max(0, CmbServer.SelectedIndex)].Value;
        SelectedGameLanguage = BxcSettings.GameLocales[Math.Max(0, CmbGameLang.SelectedIndex)].Value;
        Result = "signin";
        Close();
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        SelectedStreamQuality = BxcSettings.StreamQualities[Math.Max(0, CmbQuality.SelectedIndex)].Value;
        SelectedServerRegion = BxcSettings.ServerRegions[Math.Max(0, CmbServer.SelectedIndex)].Value;
        SelectedGameLanguage = BxcSettings.GameLocales[Math.Max(0, CmbGameLang.SelectedIndex)].Value;
        Result = "skip";
        Close();
    }
}

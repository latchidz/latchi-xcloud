using System.Windows;
using System.Windows.Controls;
using LatchiXcloud.App.Theme;
using LatchiXcloud.Core.Services;

namespace LatchiXcloud.App.Views;

/// <summary>
/// v1.1 first-run wizard:
///   (1) pick Arabic/English (the LATCHI UI language),
///   (2) stream setup — the REAL Better xCloud values (target resolution + preferred
///       game language, straight from the bundled script's own options) applied BEFORE
///       the Xbox site is ever opened,
///   (3) sign in to Xbox Cloud Gaming with the Microsoft account through the legitimate
///       web flow.
/// Runs as a dialog over the main window (own HWND → always visible above the WebView2).
/// </summary>
public partial class FirstRunWindow : Window
{
    public string SelectedLanguage { get; private set; } = "ar";
    /// <summary>Chosen Better xCloud target resolution (stream.video.resolution).</summary>
    public string SelectedStreamQuality { get; private set; } = "auto";
    /// <summary>Chosen preferred game language (stream.locale).</summary>
    public string SelectedGameLanguage { get; private set; } = "default";
    /// <summary>Result of the wizard: "signin" = user opened the sign-in page, "skip" = postponed.</summary>
    public string Result { get; private set; } = "skip";

    public FirstRunWindow()
    {
        InitializeComponent();

        // real Better xCloud options only — no invented values (BxcSettings ↔ bundled script)
        CmbQuality.ItemsSource = BxcSettings.StreamQualities.Select(q => q.Label).ToList();
        CmbQuality.SelectedIndex = 0;
        CmbGameLang.ItemsSource = BxcSettings.GameLocales.Select(l => l.Label).ToList();
        CmbGameLang.SelectedIndex = 0;

        ApplyLanguage(SelectedLanguage);
    }

    private void ApplyLanguage(string lang)
    {
        FlowDirection = lang == "en" ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        StreamTitle.Text = Loc.S(lang, "wizardStreamTitle");
        StreamDetail.Text = Loc.S(lang, "wizardStreamDetail");
        LblQuality.Text = Loc.S(lang, "wizardQuality");
        LblGameLang.Text = Loc.S(lang, "wizardGameLang");
        LblGameLangNote.Text = Loc.S(lang, "wizardGameLangNote");
        LblRegionNote.Text = Loc.S(lang, "wizardRegionNote");
        BtnStreamNext.Content = Loc.S(lang, "wizardNext");
        BtnStreamBack.Content = Loc.S(lang, "wizardBack");
        SignInTitle.Text = Loc.S(lang, "wizardSignInTitle");
        SignInDetail.Text = Loc.S(lang, "wizardSignInDetail");
        BtnGoSignIn.Content = Loc.S(lang, "wizardSignInOpen");
        BtnSkip.Content = Loc.S(lang, "wizardSkip");
    }

    /* ── step 1: language ─────────────────────────────────────────── */

    private void Choose(string lang)
    {
        SelectedLanguage = lang;
        ApplyLanguage(lang);
        StepLanguage.Visibility = Visibility.Collapsed;
        StepStream.Visibility = Visibility.Visible; // config BEFORE the site opens
    }

    private void BtnArabic_Click(object sender, RoutedEventArgs e) => Choose("ar");
    private void BtnEnglish_Click(object sender, RoutedEventArgs e) => Choose("en");

    /* ── step 2: stream setup (real BxC values) ───────────────────── */

    private void CmbQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbQuality.SelectedIndex >= 0 && CmbQuality.SelectedIndex < BxcSettings.StreamQualities.Count)
            SelectedStreamQuality = BxcSettings.StreamQualities[CmbQuality.SelectedIndex].Value;
    }

    private void CmbGameLang_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (CmbGameLang.SelectedIndex >= 0 && CmbGameLang.SelectedIndex < BxcSettings.GameLocales.Count)
            SelectedGameLanguage = BxcSettings.GameLocales[CmbGameLang.SelectedIndex].Value;
    }

    private void BtnStreamNext_Click(object sender, RoutedEventArgs e)
    {
        CmbGameLang_SelectionChanged(null, null); // final read of the current selection
        StepStream.Visibility = Visibility.Collapsed;
        StepSignIn.Visibility = Visibility.Visible;
    }

    private void BtnStreamBack_Click(object sender, RoutedEventArgs e)
    {
        StepStream.Visibility = Visibility.Collapsed;
        StepLanguage.Visibility = Visibility.Visible;
    }

    /* ── step 3: sign-in ──────────────────────────────────────────── */

    private void BtnGoSignIn_Click(object sender, RoutedEventArgs e)
    {
        Result = "signin";
        Close();
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        Result = "skip";
        Close();
    }
}

using System.Windows;
using LatchiXcloud.App.Theme;
using LatchiXcloud.Core.Services;

namespace LatchiXcloud.App.Views;

/// <summary>
/// v1.0 first-run wizard: (1) pick Arabic/English, (2) sign in to Xbox Cloud Gaming with the
/// Microsoft account through the legitimate web flow — then the app goes fullscreen into xCloud.
/// Runs as a dialog over the main window (own HWND → always visible above the WebView2).
/// </summary>
public partial class FirstRunWindow : Window
{
    public string SelectedLanguage { get; private set; } = "ar";
    /// <summary>Result of the wizard: "signin" = user opened the sign-in page, "skip" = postponed.</summary>
    public string Result { get; private set; } = "skip";

    public FirstRunWindow()
    {
        InitializeComponent();
        ApplyLanguage(SelectedLanguage);
    }

    private void ApplyLanguage(string lang)
    {
        FlowDirection = lang == "en" ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        SignInTitle.Text = Loc.S(lang, "wizardSignInTitle");
        SignInDetail.Text = Loc.S(lang, "wizardSignInDetail");
        BtnGoSignIn.Content = Loc.S(lang, "wizardSignInOpen");
        BtnSkip.Content = Loc.S(lang, "wizardSkip");
    }

    private void Choose(string lang)
    {
        SelectedLanguage = lang;
        ApplyLanguage(lang);
        StepLanguage.Visibility = Visibility.Collapsed;
        StepSignIn.Visibility = Visibility.Visible;
    }

    private void BtnArabic_Click(object sender, RoutedEventArgs e) => Choose("ar");
    private void BtnEnglish_Click(object sender, RoutedEventArgs e) => Choose("en");

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

namespace LatchiXcloud.Core.Services;

/// <summary>
/// v1.0 first-run experience: choose language → sign in to Xbox Cloud Gaming with the
/// Microsoft account (the legit web flow inside the WebView) → straight into the cloud.
/// Pure logic, unit-tested; the UI lives in MainWindow.
/// </summary>
public static class FirstRunFlow
{
    public const string StepLanguage = "language";
    public const string StepSignIn = "signin";
    public const string StepDone = "done";

    public static string InitialStep(bool firstRunComplete)
        => firstRunComplete ? StepDone : StepLanguage;

    public static string Advance(string step) => step switch
    {
        StepLanguage => StepSignIn,
        StepSignIn => StepDone,
        _ => StepDone
    };

    /// <summary>
    /// Does this URL mean the Microsoft sign-in just succeeded? Better xCloud itself
    /// watches the exact same redirect: https://www.xbox.com/*/auth/msa?*loggedIn*
    /// (strictly www.xbox.com — not store.xbox.com or any other subdomain).
    /// </summary>
    public static bool IsSignInSuccessUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host.ToLowerInvariant();
        if (host != "xbox.com" && host != "www.xbox.com") return false;
        return uri.LocalPath.Contains("/auth/msa", StringComparison.OrdinalIgnoreCase)
               && uri.Query.Contains("loggedIn", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Normalizes a keep-session choice stored in settings.</summary>
    public static string NormalizeKeepChoice(string? choice)
        => choice is "keep" or "signout" or "ask" ? choice : "ask";
}

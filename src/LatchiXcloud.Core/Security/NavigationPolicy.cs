namespace LatchiXcloud.Core.Security;

/// <summary>
/// Top-level navigation allowlist: only Microsoft/Xbox authentication and Xbox Cloud Gaming
/// domains may render inside the app. Anything else is blocked (optionally opened in the
/// system browser by explicit user action). Suffix matching is dot-anchored so
/// "evil-notxbox.com" can never pass as xbox.com.
/// </summary>
public static class NavigationPolicy
{
    /// <summary>Microsoft / Xbox authentication + Xbox Cloud Gaming host suffixes.</summary>
    public static readonly string[] AllowedHostSuffixes =
    {
        "xbox.com",            // Xbox Cloud Gaming (www.xbox.com/*/play) + auth/msa callback
        "xboxlive.com",        // Xbox Live services that occasionally navigate top-level
        "live.com",            // login.live.com, account.live.com, signup.live.com
        "microsoft.com",       // login.microsoftonline.com redirects / account.microsoft.com
        "microsoftonline.com", // AAD login (work/school accounts on xCloud)
        "msauth.net",          // MS auth statics/flow
        "msftauth.net",        // MS auth CDN
        "msftauthimages.net",  // MS auth images
        "passport.net",        // legacy passport flows
        "azureedge.net",       // MS CDN redirects during login
    };

    public static bool IsAllowed(Uri uri)
    {
        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme is "about" or "data")
            return true; // internal/blank documents only — never remote content
        if (scheme is not ("https" or "http"))
            return false; // file://, chrome-extension://, custom schemes → always blocked

        var host = uri.Host.ToLowerInvariant();
        return AllowedHostSuffixes.Any(suffix =>
            host == suffix || host.EndsWith("." + suffix, StringComparison.Ordinal));
    }

    public static bool IsAllowed(string? url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsAllowed(uri);
    }
}

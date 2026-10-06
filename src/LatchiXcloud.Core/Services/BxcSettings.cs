namespace LatchiXcloud.Core.Services;

/// <summary>
/// The REAL Better xCloud settings surface, extracted from the actual bundled
/// userscript (v6.7.12 — resources/better-xcloud.user.js), not from memory:
///
///  • Global prefs live in  localStorage["BetterXcloud"]      (JSON object)
///  • Stream prefs live in  localStorage["BetterXcloud.Stream"]
///  • Key examples (verbatim from the script's settings definitions):
///      "stream.video.resolution"  {default:"auto", options: auto|720p|1080p|1080p-hq}
///      "stream.locale"            {default:"default", options: default|<29 game locales>}
///      "server.region"            {default:"default"} — the region LIST is built
///                                  dynamically from the live service inside the page
///                                  (STATES.serverRegions), so no host-side region picker
///                                  can be honest; Auto + the in-page BxC settings are.
///
/// The bridge writes the SAME values Better xCloud itself reads (localStorage of the
/// shared profile) — one effective configuration, no shadow copy:
///   LATCHI UI → this bridge → localStorage["BetterXcloud"] → Better xCloud → session.
///
/// getGlobalPref/setGlobalPref are INSIDE the userscript's closure (not on window),
/// so the only reliable host→BxC channel is localStorage itself: seeded once before
/// the very first navigation, then read/written live via ExecuteScriptAsync.
/// </summary>
public static class BxcSettings
{
    /// <summary>localStorage key of Better xCloud's GLOBAL settings object (verbatim from the script).</summary>
    public const string GlobalStorageKey = "BetterXcloud";

    /// <summary>One-shot flag: the onboarding seed ran once and must never run again
    /// (it would otherwise overwrite the user's later choices on every launch).</summary>
    public const string SeedFlagKey = "BetterXcloud.Latchi.Seeded";

    public const string StreamQualityKey = "stream.video.resolution";
    public const string GameLocaleKey = "stream.locale";
    public const string ServerRegionKey = "server.region";

    public sealed record QualityOption(string Value, string Label);
    public sealed record LocaleOption(string Value, string Label);

    /// <summary>Target resolution options exactly as defined in Better xCloud v6.7.12
    /// (options of "stream.video.resolution": auto / 720p / 1080p / 1080p-hq).</summary>
    public static readonly IReadOnlyList<QualityOption> StreamQualities = new QualityOption[]
    {
        new("auto", "تلقائي (الموصى به) · Auto"),
        new("720p", "720p — أخف على النت والجهاز"),
        new("1080p", "1080p"),
        new("1080p-hq", "1080p عالي الجودة (HQ)"),
    };

    /// <summary>Preferred GAME language options exactly as defined in Better xCloud v6.7.12
    /// (options of "stream.locale"). The app UI language is a separate LATCHI setting.</summary>
    public static readonly IReadOnlyList<LocaleOption> GameLocales = new LocaleOption[]
    {
        new("default", "افتراضي (لغة حسابك) · Default"),
        new("ar-SA", "العربية"),
        new("en-US", "English (US)"),
        new("en-GB", "English (UK)"),
        new("fr-FR", "Français"),
        new("es-ES", "Español (España)"),
        new("es-MX", "Español (Latinoamérica)"),
        new("de-DE", "Deutsch"),
        new("it-IT", "Italiano"),
        new("pt-BR", "Português (Brasil)"),
        new("pt-PT", "Português (Portugal)"),
        new("nl-NL", "Nederlands"),
        new("pl-PL", "Polski"),
        new("ru-RU", "Русский"),
        new("tr-TR", "Türkçe"),
        new("zh-CN", "中文 (简体)"),
        new("zh-TW", "中文 (繁體)"),
        new("ja-JP", "日本語"),
        new("ko-KR", "한국어"),
        new("th-TH", "ไทย"),
        new("vi-VN", "Tiếng Việt"),
        new("he-IL", "עברית"),
        new("el-GR", "Ελληνικά"),
        new("hu-HU", "Magyar"),
        new("cs-CZ", "Čeština"),
        new("sk-SK", "Slovenčina"),
        new("da-DK", "Dansk"),
        new("fi-FI", "Suomi"),
        new("nb-NO", "Norsk bokmål"),
        new("sv-SE", "Svenska"),
        new("ro-RO", "Română"),
        new("bg-BG", "Български"),
    };

    public static string NormalizeQuality(string? value) =>
        StreamQualities.Any(q => q.Value == value) ? value! : "auto";

    public static string NormalizeGameLocale(string? value) =>
        GameLocales.Any(l => l.Value == value) ? value! : "default";

    /// <summary>Builds the ONE-SHOT onboarding seed: a document-created script that
    /// merges the chosen values into localStorage["BetterXcloud"] before the very first
    /// xbox.com navigation, and only ever runs once per profile (flag-guarded) so it can
    /// never overwrite choices the user makes later inside Better xCloud's own UI.</summary>
    public static string BuildSeedScript(string quality, string gameLocale)
    {
        quality = NormalizeQuality(quality);
        gameLocale = NormalizeGameLocale(gameLocale);
        return
$@"(function () {{
  try {{
    if (!/(^|\.)xbox\.com$/.test(location.hostname)) return;
    var FLAG = {Js(SeedFlagKey)};
    if (localStorage.getItem(FLAG)) return;
    var KEY = {Js(GlobalStorageKey)};
    var cur = {{}};
    try {{ cur = JSON.parse(localStorage.getItem(KEY) || '{{}}') || {{}}; }} catch (e) {{ cur = {{}}; }}
    cur[{Js(StreamQualityKey)}] = {Js(quality)};
    cur[{Js(GameLocaleKey)}] = {Js(gameLocale)};
    localStorage.setItem(KEY, JSON.stringify(cur));
    localStorage.setItem(FLAG, '1');
  }} catch (e) {{}}
}})();";
    }

    /// <summary>Builds the LIVE apply script (Settings page): writes the given values into
    /// localStorage["BetterXcloud"] immediately and returns 'applied'. The page is then
    /// reloaded by the host so Better xCloud re-reads them at document-start.</summary>
    public static string BuildApplyScript(string? quality, string? gameLocale)
    {
        var parts = new List<string>();
        if (quality is not null)
            parts.Add($"cur[{Js(StreamQualityKey)}] = {Js(NormalizeQuality(quality))};");
        if (gameLocale is not null)
            parts.Add($"cur[{Js(GameLocaleKey)}] = {Js(NormalizeGameLocale(gameLocale))};");
        var writes = string.Join("\n    ", parts);
        return
$@"(function () {{
  try {{
    var KEY = {Js(GlobalStorageKey)};
    var cur = {{}};
    try {{ cur = JSON.parse(localStorage.getItem(KEY) || '{{}}') || {{}}; }} catch (e) {{ cur = {{}}; }}
    {writes}
    localStorage.setItem(KEY, JSON.stringify(cur));
    return 'applied';
  }} catch (e) {{ return 'error:' + (e && e.message || e); }}
}})()";
    }

    /// <summary>JS that reads the CURRENT effective values straight from
    /// localStorage["BetterXcloud"] (the same source Better xCloud reads).</summary>
    public static string BuildReadScript() =>
$@"(function () {{
  try {{
    var cur = JSON.parse(localStorage.getItem({Js(GlobalStorageKey)}) || '{{}}') || {{}};
    return JSON.stringify({{
      q: cur[{Js(StreamQualityKey)}] || null,
      l: cur[{Js(GameLocaleKey)}] || null
    }});
  }} catch (e) {{ return '{{}}'; }}
}})()";

    private static string Js(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

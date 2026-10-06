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
    public sealed record RegionOption(string Value, string Label, string Continent);

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

    /// <summary>Server/region options — VERBATIM from the bundled script's own
    /// XcloudInterceptor.SERVER_EXTRA_INFO table (v6.7.12). The stored value is the
    /// live region name exactly as the xCloud offering API returns it (PascalCase,
    /// e.g. "WestEurope" — proven by the script: region.name.toUpperCase() is the
    /// SERVER_EXTRA_INFO key). An unknown value is silently IGNORED by BxC (stays
    /// Auto), so a stale entry can never break streaming — worst case it is a no-op.
    /// The live list (including any brand-new datacenter) is always visible inside
    /// Better xCloud's own settings page in the xCloud UI.</summary>
    public static readonly IReadOnlyList<RegionOption> ServerRegions = new RegionOption[]
    {
        new("default", "تلقائي — أقرب خادم (الموصى به) · Auto", ""),
        new("WestEurope", "غرب أوروبا (هولندا)", "europe"),
        new("SwedenCentral", "وسط السويد", "europe"),
        new("UKSouth", "جنوب المملكة المتحدة", "europe"),
        new("EastUS", "شرق أمريكا", "america-north"),
        new("EastUS2", "شرق أمريكا 2", "america-north"),
        new("NorthCentralUS", "شمال وسط أمريكا", "america-north"),
        new("SouthCentralUS", "جنوب وسط أمريكا", "america-north"),
        new("WestUS", "غرب أمريكا", "america-north"),
        new("WestUS2", "غرب أمريكا 2", "america-north"),
        new("WestUS3", "غرب أمريكا 3", "america-north"),
        new("MexicoCentral", "وسط المكسيك", "america-north"),
        new("BrazilSouth", "جنوب البرازيل", "america-south"),
        new("ChileCentral", "وسط تشيلي", "america-south"),
        new("JapanEast", "شرق اليابان", "asia"),
        new("KoreaCentral", "وسط كوريا", "asia"),
        new("CentralIndia", "وسط الهند", "asia"),
        new("SouthIndia", "جنوب الهند", "asia"),
        new("AustraliaEast", "شرق أستراليا", "australia"),
        new("AustraliaSoutheast", "جنوب شرق أستراليا", "australia"),
    };

    public static string NormalizeRegion(string? value) =>
        ServerRegions.Any(r => r.Value == value) ? value! : "default";

    /// <summary>Builds the ONE-SHOT onboarding seed: a document-created script that
    /// merges the chosen values into localStorage["BetterXcloud"] before the very first
    /// xbox.com navigation, and only ever runs once per profile (flag-guarded) so it can
    /// never overwrite choices the user makes later inside Better xCloud's own UI.</summary>
    public static string BuildSeedScript(string quality, string gameLocale, string? region = null)
    {
        quality = NormalizeQuality(quality);
        gameLocale = NormalizeGameLocale(gameLocale);
        var regionLine = region is not null && NormalizeRegion(region) != "default"
            ? $"\n    cur[{Js(ServerRegionKey)}] = {Js(NormalizeRegion(region))};"
            : "";
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
        cur[{Js(GameLocaleKey)}] = {Js(gameLocale)};{regionLine}
        localStorage.setItem(KEY, JSON.stringify(cur));
    localStorage.setItem(FLAG, '1');
  }} catch (e) {{}}
}})();";
    }

    /// <summary>Builds the LIVE apply script (Settings page): writes the given values into
    /// localStorage["BetterXcloud"] immediately and returns 'applied'. The page is then
    /// reloaded by the host so Better xCloud re-reads them at document-start.</summary>
    public static string BuildApplyScript(string? quality, string? gameLocale, string? region = null)
    {
        var parts = new List<string>();
        if (quality is not null)
            parts.Add($"cur[{Js(StreamQualityKey)}] = {Js(NormalizeQuality(quality))};");
        if (gameLocale is not null)
            parts.Add($"cur[{Js(GameLocaleKey)}] = {Js(NormalizeGameLocale(gameLocale))};");
        if (region is not null)
            parts.Add($"cur[{Js(ServerRegionKey)}] = {Js(NormalizeRegion(region))};");
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
      l: cur[{Js(GameLocaleKey)}] || null,
      r: cur[{Js(ServerRegionKey)}] || null
    }});
  }} catch (e) {{ return '{{}}'; }}
}})()";

    private static string Js(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

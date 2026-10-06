using LatchiXcloud.Core.Services;

namespace LatchiXcloud.Tests;

/// <summary>
/// The BxC bridge must match the ACTUAL bundled userscript — these tests read the real
/// resources/better-xcloud.user.js from the repo and verify every key/value we expose
/// exists there verbatim. If upstream changes, these tests fail and force a bridge audit.
/// </summary>
public class BxcSettingsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("*.sln").Any()
               && !dir.SubdirectoriesContains("resources"))
            dir = dir.Parent;
        if (dir is null) throw new InvalidOperationException("repo root not found");
        return dir.FullName;
    }

    private static string BxcSource() =>
        File.ReadAllText(Path.Combine(RepoRoot(), "resources", "better-xcloud.user.js"));

    [Fact]
    public void GlobalStorageKey_MatchesBundledScript()
    {
        // the script's settings storages are built with super("<key>") on BaseSettingsStorage
        Assert.Contains("super(\"BetterXcloud\"", BxcSource());
    }

    [Fact]
    public void SettingKeys_ExistVerbatimInBundledScript()
    {
        var src = BxcSource();
        Assert.Contains("\"stream.video.resolution\"", src);
        Assert.Contains("\"stream.locale\"", src);
        Assert.Contains("\"server.region\"", src);
    }

    [Fact]
    public void StreamQualityValues_AreTheRealOptions()
    {
        var src = BxcSource();
        // the options object of stream.video.resolution in v6.7.12:
        //   options: {auto: t("default"), "720p": "720p", "1080p": "1080p", "1080p-hq": "1080p (HQ)"}
        foreach (var v in new[] { "\"720p\":", "\"1080p\":", "\"1080p-hq\":" })
            Assert.Contains(v, src);
        foreach (var q in BxcSettings.StreamQualities.Where(q => q.Value != "auto"))
            Assert.Contains("\"" + q.Value + "\":", src);
    }

    [Fact]
    public void GameLocales_AreTheRealOptions()
    {
        var src = BxcSource();
        foreach (var l in BxcSettings.GameLocales)
            Assert.Contains("\"" + l.Value + "\":", src);
    }

    [Fact]
    public void Normalize_RejectsUnknownValues()
    {
        Assert.Equal("auto", BxcSettings.NormalizeQuality("4k"));
        Assert.Equal("auto", BxcSettings.NormalizeQuality(null));
        Assert.Equal("1080p-hq", BxcSettings.NormalizeQuality("1080p-hq"));
        Assert.Equal("default", BxcSettings.NormalizeGameLocale("xx-XX"));
        Assert.Equal("ar-SA", BxcSettings.NormalizeGameLocale("ar-SA"));
    }

    [Fact]
    public void SeedScript_IsOneShot_AndWritesChosenValues()
    {
        var js = BxcSettings.BuildSeedScript("1080p", "en-US");
        Assert.Contains("BetterXcloud.Latchi.Seeded", js);
        Assert.Contains("\"1080p\"", js);
        Assert.Contains("\"en-US\"", js);
        // merge, never replace: it parses the existing JSON object first
        Assert.Contains("JSON.parse(localStorage.getItem", js);
        // one-shot: returns immediately if the flag is already present
        Assert.Contains("if (localStorage.getItem(FLAG)) return;", js);
        // only on xbox.com (document-created scripts run on every navigation)
        Assert.Contains("xbox\\.com", js);
    }

    [Fact]
    public void ApplyScript_WritesImmediatelyAndReports()
    {
        var js = BxcSettings.BuildApplyScript("720p", null);
        Assert.Contains("\"720p\"", js);
        Assert.DoesNotContain("stream.locale", js); // untouched value not rewritten
        Assert.Contains("return 'applied';", js);
    }
}

file static class DirExt
{
    public static bool SubdirectoriesContains(this DirectoryInfo dir, string name) =>
        dir.GetDirectories(name).Length > 0;
}

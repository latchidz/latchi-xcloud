using System.Text.RegularExpressions;

namespace LatchiXcloud.Tests;

/// <summary>
/// v1.2 regression guard: every Loc.S(lang, "key") call used in App code must exist in
/// the Loc table (both AR and EN), and the table must carry the new v1.2 keys
/// (loading overlay, blank-page detector, VPN helper, wizard server step).
/// Static text analysis — runs on any OS, mirrors XamlBrushTests' approach.
/// </summary>
public class LocTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("*.sln").Any()
               && !dir.SubdirectoriesContains("src"))
            dir = dir.Parent;
        if (dir is null) throw new InvalidOperationException("repo root not found");
        return dir.FullName;
    }

    private static HashSet<string> LocKeys()
    {
        var text = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "LatchiXcloud.App", "Theme", "Loc.cs"));
        var keys = new HashSet<string>();
        foreach (Match m in Regex.Matches(text, @"\[""(?<k>[a-zA-Z0-9_]+)""\]"))
            keys.Add(m.Groups["k"].Value);
        return keys;
    }

    [Fact]
    public void UsedKeys_AllExistInTable()
    {
        var table = LocKeys();
        Assert.NotEmpty(table);

        var appDir = Path.Combine(RepoRoot(), "src", "LatchiXcloud.App");
        var used = new HashSet<string>();
        foreach (var cs in Directory.GetFiles(appDir, "*.cs", SearchOption.AllDirectories)
                     .Concat(Directory.GetFiles(Path.Combine(appDir, "Views"), "*.cs", SearchOption.AllDirectories))
                     .Concat(Directory.GetFiles(Path.Combine(appDir, "Smoke"), "*.cs", SearchOption.AllDirectories)))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(cs),
                         @"Loc\.S\(\s*[^,]+,\s*""(?<k>[a-zA-Z0-9_]+)"""))
                used.Add(m.Groups["k"].Value);
        }

        Assert.NotEmpty(used);
        var missing = used.Where(k => !table.Contains(k)).OrderBy(k => k).ToList();
        Assert.True(missing.Count == 0,
            "Loc keys used in code but missing from Loc.cs: " + string.Join(", ", missing));
    }

    [Fact]
    public void V12_KeysArePresent()
    {
        var table = LocKeys();
        // §5/§18 loading overlay + blank-page detector + §9 wizard steps + §12-15 VPN helper
        foreach (var key in new[]
                 {
                     "loginLoading", "emptyPageTitle", "emptyPageDetail",
                     "wizardQualityTitle", "wizardQualityDetail",
                     "wizardServerTitle", "wizardServerDetail",
                     "wizardGameLangTitle", "wizardGameLangDetail",
                     "wizardVpnTitle", "wizardVpnDetail",
                     "wizardVpnOpen", "wizardVpnCheck",
                     "vpnNotInstalled", "vpnStatusConnected",
                     "vpnStatusInstalledNotConnected", "vpnStatusNotInstalled",
                 })
            Assert.Contains(key, table);
    }
}

file static class DirExtensions
{
    public static bool SubdirectoriesContains(this DirectoryInfo dir, string name)
        => dir.GetDirectories().Any(d => d.Name == name);
}

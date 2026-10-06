using System.Xml.Linq;

namespace LatchiXcloud.Tests;

/// <summary>
/// Regression guard for the v1.0.0 startup crash:
/// «Set property 'System.Windows.Controls.Border.BorderBrush' threw an exception.»
/// Root cause: a WPF <b>Color</b> resource (ColBorder) was assigned to a Brush property —
/// XAML compiles fine (StaticResource is resolved at RUNTIME), the app crashed on launch,
/// and CI never caught it because smoke never parsed the production windows.
///
/// This static analysis parses every .xaml in src/ and fails if any Brush-requiring
/// property (attribute or Style Setter) references a Color resource. It runs on any OS.
/// </summary>
public class XamlBrushTests
{
    private static readonly string[] BrushProperties =
        { "BorderBrush", "Background", "Foreground", "Fill", "Stroke", "OpacityMask" };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("*.sln").Any()
               && !dir.SubdirectoriesContains("src"))
            dir = dir.Parent;
        if (dir is null) throw new InvalidOperationException("repo root not found");
        return dir.FullName;
    }

    private static (HashSet<string> Colors, HashSet<string> Brushes, HashSet<string> All) ThemeResources()
    {
        var colors = new HashSet<string>();
        var brushes = new HashSet<string>();
        var all = new HashSet<string>();
        foreach (var themeFile in Directory.GetFiles(
                     Path.Combine(RepoRoot(), "src"), "*.xaml", SearchOption.AllDirectories))
        {
            var doc = XDocument.Load(themeFile);
            foreach (var el in doc.Descendants())
            {
                var key = (string?)el.Attribute("Key") ?? (string?)el.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"));
                if (key is null) continue;
                all.Add(key);
                if (el.Name.LocalName == "Color") colors.Add(key);
                else if (el.Name.LocalName.Contains("Brush", StringComparison.Ordinal)) brushes.Add(key);
            }
        }
        return (colors, brushes, all);
    }

    public static IEnumerable<object[]> XamlFiles() =>
        Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.xaml", SearchOption.AllDirectories)
            .Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(XamlFiles))]
    public void NoColorResourceAssignedToBrushProperty(string file)
    {
        var (colors, brushes, _) = ThemeResources();
        var doc = XDocument.Load(file);

        var offenders = new List<string>();

        // 1) attributes: BorderBrush="{StaticResource X}"
        foreach (var el in doc.Descendants())
        {
            foreach (var attr in el.Attributes())
            {
                if (!BrushProperties.Contains(attr.Name.LocalName)) continue;
                var m = System.Text.RegularExpressions.Regex.Match(attr.Value, @"\{StaticResource (\w+)\}");
                if (!m.Success) continue;
                var res = m.Groups[1].Value;
                if (colors.Contains(res) && !brushes.Contains(res))
                    offenders.Add($"<{el.Name.LocalName} {attr.Name.LocalName}=\"{{StaticResource {res}}}\" (Color used as Brush)");
            }
        }

        // 2) style setters: <Setter Property="BorderBrush" Value="{StaticResource X}"/>
        foreach (var setter in doc.Descendants("Setter"))
        {
            var prop = (string?)setter.Attribute("Property");
            if (prop is null || !BrushProperties.Contains(prop)) continue;
            var value = (string?)setter.Attribute("Value");
            if (value is null) continue;
            var m = System.Text.RegularExpressions.Regex.Match(value, @"\{StaticResource (\w+)\}");
            if (!m.Success) continue;
            var res = m.Groups[1].Value;
            if (colors.Contains(res) && !brushes.Contains(res))
                offenders.Add($"<Setter Property=\"{prop}\" {{StaticResource {res}}} (Color used as Brush)");
        }

        Assert.True(offenders.Count == 0,
            $"{Path.GetFileName(file)} — Color resources assigned to Brush properties (runtime XamlParseException!):\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void EveryStaticResourceInViews_ExistsInTheme()
    {
        var (_, _, all) = ThemeResources();
        var missing = new List<string>();
        foreach (var file in Directory.GetFiles(
                     Path.Combine(RepoRoot(), "src", "LatchiXcloud.App", "Views"), "*.xaml"))
        {
            var text = File.ReadAllText(file);
            foreach (var m in System.Text.RegularExpressions.Regex.Matches(text, @"\{StaticResource (\w+)\}"))
            {
                var res = ((System.Text.RegularExpressions.Match)m).Groups[1].Value;
                if (!all.Contains(res))
                    missing.Add($"{Path.GetFileName(file)}: {res}");
            }
        }
        Assert.True(missing.Count == 0, "Missing StaticResource targets (runtime crash on load):\n" + string.Join("\n", missing));
    }
}

file static class DirExt2
{
    public static bool SubdirectoriesContains(this DirectoryInfo dir, string name) =>
        dir.GetDirectories(name).Length > 0;
}

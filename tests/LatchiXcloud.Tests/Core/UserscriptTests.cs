using LatchiXcloud.Core.Userscript;
using Xunit;

public class UserscriptMetadataTests
{
    private const string Header =
@"// ==UserScript==
// @name         Better xCloud
// @namespace    https://github.com/redphx
// @version      6.7.12
// @description  Improve Xbox Cloud Gaming (xCloud) experience
// @author       redphx
// @license      MIT
// @match        https://www.xbox.com/*/play*
// @match        https://www.xbox.com/*/auth/msa?*loggedIn*
// @exclude      https://www.xbox.com/*/xbox-game-pass/play-day-one
// @run-at       document-start
// @grant        none
// @updateURL    https://example.com/meta.js
// @downloadURL  https://example.com/code.js
// ==/UserScript==
console.log('body');";

    [Fact]
    public void ParsesFullHeader()
    {
        var m = UserscriptMetadata.Parse(Header);
        Assert.Equal("Better xCloud", m.Name);
        Assert.Equal("6.7.12", m.Version);
        Assert.Equal("MIT", m.License);
        Assert.Equal("document-start", m.RunAt);
        Assert.Equal(2, m.Matches.Count);
        Assert.Single(m.Excludes);
        Assert.Contains("none", m.Grants);
        Assert.True(m.IsGrantless);
        Assert.Equal("https://example.com/meta.js", m.UpdateUrl);
    }

    [Fact]
    public void ThrowsWithoutHeader()
    {
        Assert.Throws<FormatException>(() => UserscriptMetadata.Parse("no header here"));
    }

    [Fact]
    public void GrantsOtherThanNone_AreNotGrantless()
    {
        var src = Header.Replace("// @grant        none", "// @grant        GM_getValue");
        Assert.False(UserscriptMetadata.Parse(src).IsGrantless);
    }

    [Fact]
    public void RealShippedScript_Parses()
    {
        var path = Path.Combine(FindRepoRoot(), "resources", "better-xcloud.user.js");
        if (!File.Exists(path)) return; // not in test context
        var m = UserscriptMetadata.Parse(File.ReadAllText(path));
        Assert.Equal("Better xCloud", m.Name);
        Assert.Equal("6.7.12", m.Version);
        Assert.True(m.IsGrantless);
        Assert.Equal("document-start", m.RunAt);
    }

    internal static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LatchiXcloud.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? ".";
    }
}

public class PatternConverterTests
{
    private static readonly UserscriptMetadata Meta = UserscriptMetadata.Parse(
@"// ==UserScript==
// @match        https://www.xbox.com/*/play*
// @match        https://www.xbox.com/*/auth/msa?*loggedIn*
// @exclude      https://www.xbox.com/*/xbox-game-pass/play-day-one
// ==/UserScript==");

    [Theory]
    [InlineData("https://www.xbox.com/en-US/play", true)]
    [InlineData("https://www.xbox.com/play", false)] // no locale segment — MS always redirects to /*/play
    [InlineData("https://www.xbox.com/ar-DZ/play/games/FIFA", true)]
    [InlineData("https://www.xbox.com/en-US/play?title=abc", true)]
    [InlineData("https://www.xbox.com/en-US/auth/msa?loggedIn=true&ru=x", true)]
    [InlineData("https://www.xbox.com/en-US/xbox-game-pass/play-day-one", false)] // excluded
    [InlineData("https://www.xbox.com/en-US/xbox-game-pass/play-day-one?x=1", true)] // T2 glob: query breaks the exact exclude suffix
    [InlineData("https://www.xbox.com/en-US/auth/msa", false)]                    // no loggedIn
    [InlineData("https://login.live.com/", false)]
    [InlineData("https://store.xbox.com/en-US/play", false)]                       // extra subdomain
    [InlineData("https://www.xbox.com/en-US", false)]                              // no /play
    [InlineData("https://evil.com/play", false)]
    public void MatchMatrix(string url, bool expected)
        => Assert.Equal(expected, PatternConverter.TestManaged(url, Meta));

    [Fact]
    public void ToJsRegexSource_EscapesDots()
    {
        var rx = PatternConverter.ToJsRegexSource("https://xbox.com/*");
        Assert.StartsWith("^https://xbox\\.com/", rx);
        Assert.EndsWith("$", rx);
        Assert.DoesNotContain(".*.com", rx); // dot must be escaped
    }
}

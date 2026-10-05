using LatchiXcloud.Core.Security;
using LatchiXcloud.Core.Versioning;
using Xunit;

public class NavigationPolicyTests
{
    [Theory]
    [InlineData("https://www.xbox.com/play", true)]
    [InlineData("https://xbox.com/ar-DZ/play", true)]
    [InlineData("https://login.live.com/", true)]
    [InlineData("https://login.microsoftonline.com/oauth", true)]
    [InlineData("https://account.live.com/summary", true)]
    [InlineData("https://aadcdn.msauth.net/thing.js", true)]
    [InlineData("https://account.xboxlive.com/x", true)]
    [InlineData("about:blank", true)]
    [InlineData("data:text/html,hi", true)]
    [InlineData("https://example.com/", false)]
    [InlineData("https://evil-notxbox.com/play", false)]
    [InlineData("https://xbox.com.evil.io/play", false)]
    [InlineData("https://xboxcom.evil.io/", false)]
    [InlineData("https://store.steampowered.com/", false)]
    [InlineData("file:///C:/Windows/win.ini", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("", false)]
    [InlineData("not a url", false)]
    public void AllowMatrix(string url, bool expected)
        => Assert.Equal(expected, NavigationPolicy.IsAllowed(url));

    [Fact]
    public void HttpNotHttps_StillAllowed_OnlyForKnownHosts()
    {
        // policy is host-based; MS hosts are https in practice but http stays allowed
        // for the same hosts to avoid breaking odd redirects
        Assert.True(NavigationPolicy.IsAllowed("http://www.xbox.com/play"));
        Assert.False(NavigationPolicy.IsAllowed("http://anything-else.com/"));
    }
}

public class SemanticVersionTests
{
    [Theory]
    [InlineData("6.7.12", "6.7.12", 0)]
    [InlineData("v6.7.13", "6.7.12", 1)]
    [InlineData("6.8.0", "6.7.99", 1)]
    [InlineData("7.0.0", "6.99.99", 1)]
    [InlineData("6.7.11", "6.7.12", -1)]
    public void Compare(string a, string b, int sign)
    {
        Assert.True(SemanticVersion.TryParse(a, out var va));
        Assert.True(SemanticVersion.TryParse(b, out var vb));
        Assert.Equal(sign, Math.Sign(va.CompareTo(vb)));
    }

    [Fact]
    public void GarbageRejected()
    {
        Assert.False(SemanticVersion.TryParse("", out _));
        Assert.False(SemanticVersion.TryParse("x.y.z", out _));
        Assert.False(SemanticVersion.TryParse(null, out _));
    }
}

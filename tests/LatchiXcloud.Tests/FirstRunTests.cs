using LatchiXcloud.Core.Services;

namespace LatchiXcloud.Tests;

/// <summary>v1.0 first-run wizard state machine + sign-in URL detection + profile picture.</summary>
public class FirstRunTests
{
    [Fact]
    public void InitialStep_IsLanguage_UntilFirstRunCompletes()
    {
        Assert.Equal(FirstRunFlow.StepLanguage, FirstRunFlow.InitialStep(false));
        Assert.Equal(FirstRunFlow.StepDone, FirstRunFlow.InitialStep(true));
    }

    [Fact]
    public void Advance_WalksTheFullV12Order()
    {
        // v1.2 §9 order: language → quality → server/region → game language → VPN (optional) → sign-in → done
        Assert.Equal(FirstRunFlow.StepQuality, FirstRunFlow.Advance(FirstRunFlow.StepLanguage));
        Assert.Equal(FirstRunFlow.StepServer, FirstRunFlow.Advance(FirstRunFlow.StepQuality));
        Assert.Equal(FirstRunFlow.StepGameLang, FirstRunFlow.Advance(FirstRunFlow.StepServer));
        Assert.Equal(FirstRunFlow.StepVpn, FirstRunFlow.Advance(FirstRunFlow.StepGameLang));
        Assert.Equal(FirstRunFlow.StepSignIn, FirstRunFlow.Advance(FirstRunFlow.StepVpn));
        Assert.Equal(FirstRunFlow.StepDone, FirstRunFlow.Advance(FirstRunFlow.StepSignIn));
        Assert.Equal(FirstRunFlow.StepDone, FirstRunFlow.Advance(FirstRunFlow.StepDone));
        Assert.Equal(FirstRunFlow.StepDone, FirstRunFlow.Advance("garbage"));
    }

    [Theory]
    [InlineData("https://www.xbox.com/en-US/auth/msa?loggedIn=true&ru=%2Fplay", true)]
    [InlineData("https://www.xbox.com/ar-DZ/auth/msa?loggedIn", true)]
    [InlineData("https://www.xbox.com/en-US/play", false)]          // home page — not proof of sign-in
    [InlineData("https://login.live.com/", false)]                  // login domain itself — sign-in IN progress
    [InlineData("https://store.xbox.com/en-US/auth/msa?loggedIn", false)] // not xbox.com www host
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSignInSuccessUrl_MatchesOnlyTheMsaLoggedInRedirect(string? url, bool expected)
        => Assert.Equal(expected, FirstRunFlow.IsSignInSuccessUrl(url));

    [Theory]
    [InlineData("keep", "keep")]
    [InlineData("signout", "signout")]
    [InlineData("ask", "ask")]
    [InlineData("garbage", "ask")]
    [InlineData(null, "ask")]
    public void NormalizeKeepChoice_OnlyKnownValues(string? choice, string expected)
        => Assert.Equal(expected, FirstRunFlow.NormalizeKeepChoice(choice));
}

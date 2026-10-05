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
    public void Advance_WalksLanguageToSignInToDone()
    {
        Assert.Equal(FirstRunFlow.StepSignIn, FirstRunFlow.Advance(FirstRunFlow.StepLanguage));
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

    [Fact]
    public void ProfilePicture_InstallFindRemove()
    {
        var dir = Path.Combine(Path.GetTempPath(), "latchi-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Null(ProfileImage.FindExisting(dir));

            var src = Path.Combine(dir, "me.png");
            File.WriteAllBytes(src, new byte[] { 1, 2, 3 });
            var stored = ProfileImage.Install(src, dir);
            Assert.Equal(Path.Combine(dir, "profile-image.png"), stored);
            Assert.Equal(stored, ProfileImage.FindExisting(dir));

            ProfileImage.Remove(dir);
            Assert.Null(ProfileImage.FindExisting(dir));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void ProfilePicture_RejectsUnsupportedType()
    {
        var dir = Path.Combine(Path.GetTempPath(), "latchi-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var src = Path.Combine(dir, "me.gif");
            File.WriteAllBytes(src, new byte[] { 1 });
            Assert.Throws<InvalidOperationException>(() => ProfileImage.Install(src, dir));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}

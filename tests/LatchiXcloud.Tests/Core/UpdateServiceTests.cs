using LatchiXcloud.Core.Services;
using LatchiXcloud.Core.Updates;
using LatchiXcloud.Core.Userscript;
using Xunit;

public class BetterXcloudUpdateServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _updates, _active, _previous;
    private readonly FakeSource _fake = new();
    private readonly BetterXcloudUpdateService _svc;

    public BetterXcloudUpdateServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lx-tests-" + Guid.NewGuid().ToString("N"));
        _updates = Path.Combine(_root, "Updates");
        _active = Path.Combine(_root, "active");
        _previous = Path.Combine(_root, "previous");
        Directory.CreateDirectory(_root);
        _svc = new BetterXcloudUpdateService(_fake, _updates, _active, _previous);
    }

    public void Dispose() { try { Directory.Delete(_root, recursive: true); } catch { } }

    private string ShippedScript =>
        File.ReadAllText(Path.Combine(FindRepo(), "resources", "better-xcloud.user.js"));

    private static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LatchiXcloud.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? ".";
    }

    private void Bootstrap() => _svc.Bootstrap(
        Path.Combine(FindRepo(), "resources", "better-xcloud.user.js"),
        new BetterXcloudManifest { Version = "6.7.12", UpstreamRepo = "redphx/better-xcloud", Sha256 = "pinned" });

    [Fact]
    public async Task FullPipeline_CheckStageActivateRollbackRefuse()
    {
        Bootstrap();
        Assert.True(File.Exists(BetterXcloudUpdateService.ActiveScriptPath(_active)));

        var latest = await _svc.CheckLatestAsync();
        Assert.NotNull(latest);
        Assert.Equal("9.9.9", latest!.Version);

        await _svc.DownloadAndStageAsync(latest);
        var staged = _svc.PeekStaged();
        Assert.NotNull(staged);
        Assert.Equal("9.9.9", staged!.Version);

        var activated = _svc.ActivateStaged();
        Assert.Equal("9.9.9", activated.Version);
        Assert.True(_svc.HasPrevious());
        Assert.Equal("9.9.9", (await LoadActiveVersion())!);

        // bad version marked → refused + rollback restores the previous one
        _svc.MarkBad("9.9.9");
        Assert.True(_svc.IsBadVersion("9.9.9"));
        var rolled = _svc.Rollback();
        Assert.Equal("6.7.12", rolled.Version);

        await _svc.DownloadAndStageAsync(latest);
        Assert.Throws<InvalidOperationException>(() => _svc.ActivateStaged());
    }

    private async Task<string?> LoadActiveVersion()
    {
        var path = BetterXcloudUpdateService.ActiveScriptPath(_active);
        var meta = UserscriptMetadata.Parse(await File.ReadAllTextAsync(path));
        return meta.Version;
    }

    [Fact]
    public async Task TamperedScript_Rejected()
    {
        Bootstrap();
        var latest = await _svc.CheckLatestAsync();
        _fake.Tamper = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.DownloadAndStageAsync(latest!));
    }

    [Fact]
    public async Task VersionMismatch_Rejected()
    {
        Bootstrap();
        var latest = await _svc.CheckLatestAsync();
        _fake.MismatchVersion = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.DownloadAndStageAsync(latest!));
    }

    [Fact]
    public async Task NonOfficialUrl_Rejected()
    {
        Bootstrap();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _svc.DownloadAndStageAsync(new ReleaseInfo("9.9.9", "https://evil.io/bx.user.js", "", "")));
    }

    [Fact]
    public async Task StagedIntegrityRechecked()
    {
        Bootstrap();
        var latest = await _svc.CheckLatestAsync();
        await _svc.DownloadAndStageAsync(latest!);
        // corrupt the staged script → PeekStaged must reject (sha mismatch)
        var p = Path.Combine(_updates, "better-xcloud-9.9.9.user.js");
        await File.WriteAllTextAsync(p, "// tampered after staging");
        Assert.Null(_svc.PeekStaged());
    }

    [Fact]
    public async Task FutureGmGrant_Rejected()
    {
        Bootstrap();
        var latest = await _svc.CheckLatestAsync();
        _fake.AddGrants = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.DownloadAndStageAsync(latest!));
    }

    /// <summary>Serves the shipped official script relabeled as 9.9.9, from official URLs only.</summary>
    private sealed class FakeSource : IFileSource
    {
        public bool Tamper, MismatchVersion, AddGrants;

        public Task<string> GetLatestReleaseJsonAsync() => Task.FromResult(
            @"{""tag_name"":""v9.9.9"",""prerelease"":false,""html_url"":""https://github.com/redphx/better-xcloud/releases/tag/v9.9.9"",""assets"":[{""name"":""better-xcloud.user.js"",""browser_download_url"":""https://github.com/redphx/better-xcloud/releases/download/v9.9.9/better-xcloud.user.js""}]}");

        public async Task<byte[]> DownloadBytesAsync(string url)
        {
            if (!url.StartsWith("https://github.com/redphx/better-xcloud/releases/download/", StringComparison.Ordinal))
                throw new InvalidOperationException("unofficial");
            var dir = FindRepo();
            var text = await File.ReadAllTextAsync(Path.Combine(dir, "resources", "better-xcloud.user.js"));
            if (Tamper) text = text.Replace("Better xCloud", "EvilClone");
            if (AddGrants) text = text.Replace("// @grant        none", "// @grant        GM_getValue");
            if (MismatchVersion) text = text.Replace("// @version      6.7.12", "// @version      8.8.8");
            else text = text.Replace("// @version      6.7.12", "// @version      9.9.9");
            return System.Text.Encoding.UTF8.GetBytes(text);
        }
    }
}

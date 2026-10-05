using System.Text.Json;
using LatchiXcloud.Core.Services;
using LatchiXcloud.Core.Userscript;
using LatchiXcloud.Core.Versioning;

namespace LatchiXcloud.Core.Updates;

public sealed record ReleaseInfo(string Version, string AssetUrl, string ReleaseUrl, string PublishedAt);

/// <summary>
/// Safe Better xCloud update pipeline: Check (official GitHub API only) → Download →
/// Validate (metadata + version + size + hash) → Stage → Activate on next start →
/// Rollback if the new version fails to initialize. Never activates mid-session.
/// </summary>
public sealed class BetterXcloudUpdateService
{
    private const string OfficialAssetName = "better-xcloud.user.js";
    private const long MinBytes = 100_000, MaxBytes = 3_000_000;

    private readonly IFileSource _source;
    private readonly string _updatesDir;
    private readonly string _activeDir;
    private readonly string _previousDir;

    public BetterXcloudUpdateService(IFileSource source, string updatesDir, string activeDir, string previousDir)
    {
        _source = source;
        _updatesDir = updatesDir;
        _activeDir = activeDir;
        _previousDir = previousDir;
    }

    public static string ActiveScriptPath(string activeDir) => Path.Combine(activeDir, OfficialAssetName);
    public static string ActiveManifestPath(string activeDir) => Path.Combine(activeDir, "manifest.json");

    /// <summary>Query the official source for the latest stable release.</summary>
    public async Task<ReleaseInfo?> CheckLatestAsync()
    {
        var json = await _source.GetLatestReleaseJsonAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!SemanticVersion.TryParse(tag, out _)) return null;
        string? assetUrl = null;
        foreach (var a in root.GetProperty("assets").EnumerateArray())
        {
            if (a.GetProperty("name").GetString() == OfficialAssetName)
            {
                assetUrl = a.GetProperty("browser_download_url").GetString();
                break;
            }
        }
        if (assetUrl is null) return null;
        var releaseUrl = root.TryGetProperty("html_url", out var hu) ? hu.GetString() ?? "" : "";
        var published = root.TryGetProperty("published_at", out var pa) ? pa.GetString() ?? "" : "";
        return new ReleaseInfo(tag.TrimStart('v'), assetUrl, releaseUrl, published);
    }

    /// <summary>Download from the OFFICIAL release asset URL, validate, and stage for next start.</summary>
    public async Task<BetterXcloudManifest> DownloadAndStageAsync(ReleaseInfo release)
    {
        if (!Uri.TryCreate(release.AssetUrl, UriKind.Absolute, out var uri)
            || uri.Host != "github.com"
            || !uri.AbsolutePath.StartsWith("/redphx/better-xcloud/releases/download/", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing non-official download URL: " + Logger.SafeUrl(release.AssetUrl));

        var bytes = await _source.DownloadBytesAsync(release.AssetUrl);
        if (bytes.Length < MinBytes || bytes.Length > MaxBytes)
            throw new InvalidOperationException($"Unexpected userscript size: {bytes.Length}");

        var source = System.Text.Encoding.UTF8.GetString(bytes);
        var meta = UserscriptMetadata.Parse(source);
        if (meta.Name != "Better xCloud")
            throw new InvalidOperationException("Downloaded script is not Better xCloud");
        if (meta.Version != release.Version)
            throw new InvalidOperationException($"Version mismatch: release {release.Version} vs script {meta.Version}");
        if (!meta.Matches.Any(m => m.Contains("xbox.com")))
            throw new InvalidOperationException("Script does not target xbox.com");
        if (!meta.IsGrantless)
            throw new InvalidOperationException($"This Better xCloud version requires userscript APIs this host does not implement yet: {string.Join(", ", meta.Grants)}");

        var stagedPath = Path.Combine(_updatesDir, $"better-xcloud-{release.Version}.user.js");
        Directory.CreateDirectory(_updatesDir);
        await File.WriteAllTextAsync(stagedPath, source);

        var stagedManifestPath = Path.Combine(_updatesDir, "staged.json");
        var manifest = new BetterXcloudManifest
        {
            Version = meta.Version,
            UpstreamRepo = "redphx/better-xcloud",
            SourceUrl = release.ReleaseUrl,
            Sha256 = AtomicFile.Sha256Bytes(bytes),
            DownloadedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "Z",
            RunAt = meta.RunAt,
        };
        AtomicFile.WriteAllText(stagedManifestPath, Json.Serialize(manifest));
        return manifest;
    }

    public BetterXcloudManifest? PeekStaged()
    {
        var p = Path.Combine(_updatesDir, "staged.json");
        var staged = Json.Deserialize<BetterXcloudManifest>(AtomicFile.TryReadAllText(p));
        if (staged is null) return null;
        var script = Path.Combine(_updatesDir, $"better-xcloud-{staged.Version}.user.js");
        if (!File.Exists(script)) return null;
        // integrity must still match — staged files are re-verified before activation
        return AtomicFile.Sha256(script) == staged.Sha256 ? staged : null;
    }

    /// <summary>Swap a verified staged script into the active slot (current → previous).
    /// Only call at startup, never mid-session.</summary>
    public BetterXcloudManifest ActivateStaged()
    {
        var staged = PeekStaged() ?? throw new InvalidOperationException("No verified staged update");
        if (IsBadVersion(staged.Version))
            throw new InvalidOperationException($"Version {staged.Version} previously failed — refusing to activate");

        Directory.CreateDirectory(_activeDir);
        Directory.CreateDirectory(_previousDir);

        var activeScript = ActiveScriptPath(_activeDir);
        var activeManifest = ActiveManifestPath(_activeDir);
        if (File.Exists(activeScript))
        {
            // current → previous (rollback slot)
            foreach (var f in Directory.GetFiles(_previousDir)) File.Delete(f);
            File.Copy(activeScript, Path.Combine(_previousDir, OfficialAssetName), overwrite: true);
            if (File.Exists(activeManifest))
                File.Copy(activeManifest, Path.Combine(_previousDir, "manifest.json"), overwrite: true);
        }

        File.Copy(Path.Combine(_updatesDir, $"better-xcloud-{staged.Version}.user.js"), activeScript, overwrite: true);
        AtomicFile.WriteAllText(activeManifest, Json.Serialize(staged));

        // consume staged files
        File.Delete(Path.Combine(_updatesDir, "staged.json"));
        File.Delete(Path.Combine(_updatesDir, $"better-xcloud-{staged.Version}.user.js"));
        return staged;
    }

    public bool HasPrevious() => File.Exists(Path.Combine(_previousDir, OfficialAssetName));

    /// <summary>Restore the previous known-good script (current active is discarded).</summary>
    public BetterXcloudManifest Rollback()
    {
        var prevScript = Path.Combine(_previousDir, OfficialAssetName);
        if (!File.Exists(prevScript)) throw new InvalidOperationException("No previous version to roll back to");
        var prevManifest = Json.Deserialize<BetterXcloudManifest>(
            AtomicFile.TryReadAllText(Path.Combine(_previousDir, "manifest.json")))
            ?? new BetterXcloudManifest { Version = "Unknown", SourceUrl = "restored-rollback" };

        Directory.CreateDirectory(_activeDir);
        File.Copy(prevScript, ActiveScriptPath(_activeDir), overwrite: true);
        AtomicFile.WriteAllText(ActiveManifestPath(_activeDir), Json.Serialize(prevManifest));
        return prevManifest;
    }

    private string BadVersionsPath => Path.Combine(_updatesDir, "bad-versions.json");

    /// <summary>Versions that failed to initialize. Kept OUTSIDE the active manifest on
    /// purpose: a rollback replaces the active manifest and must never forget them.</summary>
    public void MarkBad(string version)
    {
        var list = Json.Deserialize<List<string>>(AtomicFile.TryReadAllText(BadVersionsPath)) ?? new List<string>();
        if (!list.Contains(version)) list.Add(version);
        Directory.CreateDirectory(_updatesDir);
        AtomicFile.WriteAllText(BadVersionsPath, Json.Serialize(list));
    }

    public bool IsBadVersion(string version)
    {
        var list = Json.Deserialize<List<string>>(AtomicFile.TryReadAllText(BadVersionsPath));
        return list?.Contains(version) == true;
    }

    public BetterXcloudManifest? LoadActiveManifest() =>
        Json.Deserialize<BetterXcloudManifest>(AtomicFile.TryReadAllText(ActiveManifestPath(_activeDir)));

    /// <summary>First-run bootstrap: copy the bundled official script into the active slot.</summary>
    public BetterXcloudManifest Bootstrap(string bundledScriptPath, BetterXcloudManifest bundledManifest)
    {
        Directory.CreateDirectory(_activeDir);
        File.Copy(bundledScriptPath, ActiveScriptPath(_activeDir), overwrite: true);
        AtomicFile.WriteAllText(ActiveManifestPath(_activeDir), Json.Serialize(bundledManifest));
        return bundledManifest;
    }
}

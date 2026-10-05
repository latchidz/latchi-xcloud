namespace LatchiXcloud.Core.Updates;

/// <summary>Record of a Better xCloud script on disk: origin, version, integrity, status.</summary>
public sealed class BetterXcloudManifest
{
    public string Version { get; set; } = "";
    public string UpstreamRepo { get; set; } = "redphx/better-xcloud";
    public string UpstreamCommit { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string DownloadedAt { get; set; } = "";
    public string RunAt { get; set; } = "document-start";

    /// <summary>Versions known to fail initialization — never auto-activate these again.</summary>
    public List<string> BadVersions { get; set; } = new();
}

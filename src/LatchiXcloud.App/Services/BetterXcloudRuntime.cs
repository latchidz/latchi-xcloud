using LatchiXcloud.Core.Services;
using LatchiXcloud.Core.Updates;
using LatchiXcloud.Core.Userscript;

namespace LatchiXcloud.App.Services;

/// <summary>
/// Lifecycle of the integrated Better xCloud script:
/// bundled official release → active slot (first run) → staged updates activated at
/// startup → automatic rollback if a version fails to initialize.
/// The host NEVER runs a script whose metadata it cannot parse.
/// </summary>
public sealed class BetterXcloudRuntime
{
    private readonly string _bundledScript;
    private readonly string _bundledManifest;

    public BetterXcloudUpdateService Updates { get; }
    public BetterXcloudManifest ActiveManifest { get; private set; } = new();
    public UserscriptMetadata Metadata { get; private set; } = new();
    public string ActiveSource { get; private set; } = "";
    public string ActiveVersion => ActiveManifest.Version.Length > 0 ? ActiveManifest.Version : "Unknown";

    public BetterXcloudRuntime()
    {
        Updates = new BetterXcloudUpdateService(
            new HttpFileSource(),
            AppPaths.UpdatesDir, AppPaths.ActiveScriptDir, AppPaths.PreviousScriptDir);
        // folder layout, or embedded copies for the single-file portable build
        (_bundledScript, _bundledManifest) = BundledResources.Resolve();
    }

    /// <summary>Prepare the active script: bootstrap, staged activation, validation.</summary>
    public void Initialize()
    {
        AppPaths.EnsureDataDir();

        // consume a pending Better xCloud data reset (from settings)
        var bxcResetFlag = Path.Combine(AppPaths.BetterXcloudDir, "pending-reset.flag");
        if (File.Exists(bxcResetFlag))
        {
            TryDeleteDir(AppPaths.BetterXcloudDir);
            AppPaths.EnsureDataDir();
            Logger.Info("Better xCloud data reset performed");
        }

        // activate a staged update (never mid-session — this runs before any page loads)
        try
        {
            if (Updates.PeekStaged() is { } staged)
            {
                if (!Updates.IsBadVersion(staged.Version))
                {
                    Updates.ActivateStaged();
                    Logger.Info($"Better xCloud updated on startup → v{staged.Version}");
                }
                else
                {
                    Logger.Warn($"Staged version {staged.Version} is marked bad — discarded");
                    File.Delete(Path.Combine(AppPaths.UpdatesDir, "staged.json"));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Staged activation failed (keeping current): " + ex.Message);
        }

        LoadActiveOrBootstrap();
    }

    private void LoadActiveOrBootstrap()
    {
        var activePath = BetterXcloudUpdateService.ActiveScriptPath(AppPaths.ActiveScriptDir);
        if (!File.Exists(activePath))
        {
            Bootstrap();
            return;
        }

        try
        {
            ActiveSource = File.ReadAllText(activePath);
            Metadata = UserscriptMetadata.Parse(ActiveSource);
            ActiveManifest = Updates.LoadActiveManifest() ?? BundledManifest();
            Logger.Info($"Better xCloud active: v{ActiveManifest.Version} " +
                        $"(upstream {ActiveManifest.UpstreamRepo}@{ActiveManifest.UpstreamCommit})");
        }
        catch (Exception ex)
        {
            Logger.Error("Active script unreadable — re-bootstrapping bundled official copy: " + ex.Message);
            Bootstrap();
        }
    }

    private void Bootstrap()
    {
        if (!File.Exists(_bundledScript))
            throw new FileNotFoundException(
                "Bundled Better xCloud script missing from the installation (resources/better-xcloud.user.js)", _bundledScript);
        var manifest = BundledManifest();
        Updates.Bootstrap(_bundledScript, manifest);
        ActiveSource = File.ReadAllText(_bundledScript);
        Metadata = UserscriptMetadata.Parse(ActiveSource);
        ActiveManifest = manifest;
        Logger.Info($"Better xCloud bootstrapped from bundled official copy: v{manifest.Version}");
    }

    private BetterXcloudManifest BundledManifest() =>
        Json.Deserialize<BetterXcloudManifest>(AtomicFile.TryReadAllText(_bundledManifest))
        ?? throw new FileNotFoundException("Bundled manifest missing (resources/bxc-manifest.json)", _bundledManifest);

    /// <summary>Automatic rollback after a failed initialization of a NEW version.</summary>
    public bool TryMarkBadAndRollback(string failedVersion)
    {
        try
        {
            Updates.MarkBad(failedVersion);
            if (Updates.HasPrevious())
            {
                var restored = Updates.Rollback();
                ActiveSource = File.ReadAllText(BetterXcloudUpdateService.ActiveScriptPath(AppPaths.ActiveScriptDir));
                Metadata = UserscriptMetadata.Parse(ActiveSource);
                ActiveManifest = restored;
                Logger.Warn($"Better xCloud v{failedVersion} failed — rolled back to v{restored.Version}");
                return true;
            }
            Logger.Error($"Better xCloud v{failedVersion} failed and no previous version exists to roll back to");
        }
        catch (Exception ex)
        {
            Logger.Error("Rollback failed: " + ex.Message);
        }
        return false;
    }

    public void ScheduleBetterXcloudDataReset()
    {
        Directory.CreateDirectory(AppPaths.BetterXcloudDir);
        File.WriteAllText(Path.Combine(AppPaths.BetterXcloudDir, "pending-reset.flag"), "reset");
    }

    private static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) { Logger.Error("Could not delete " + dir + ": " + ex.Message); }
    }
}

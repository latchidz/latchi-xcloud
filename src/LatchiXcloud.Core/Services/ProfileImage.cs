namespace LatchiXcloud.Core.Services;

/// <summary>
/// The user's profile picture shown on the v1.0 splash screen. Lives in the data dir as
/// profile-image.{png|jpg|jpeg} — chosen from Settings at any time, no rebuild needed.
/// </summary>
public static class ProfileImage
{
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };

    public static string? FindExisting(string dataDir)
    {
        foreach (var ext in Extensions)
        {
            var p = Path.Combine(dataDir, "profile-image" + ext);
            if (File.Exists(p) && new FileInfo(p).Length > 0) return p;
        }
        return null;
    }

    /// <summary>Installs a picture (copied into the data dir). Returns the stored path.</summary>
    public static string Install(string sourceFile, string dataDir)
    {
        var ext = Path.GetExtension(sourceFile).ToLowerInvariant();
        if (Array.IndexOf(Extensions, ext) < 0)
            throw new InvalidOperationException("unsupported image type (png/jpg/jpeg only)");
        Remove(dataDir);
        var target = Path.Combine(dataDir, "profile-image" + ext);
        File.Copy(sourceFile, target, overwrite: true);
        return target;
    }

    public static void Remove(string dataDir)
    {
        foreach (var ext in Extensions)
        {
            var p = Path.Combine(dataDir, "profile-image" + ext);
            if (File.Exists(p)) { try { File.Delete(p); } catch { /* best effort */ } }
        }
    }
}

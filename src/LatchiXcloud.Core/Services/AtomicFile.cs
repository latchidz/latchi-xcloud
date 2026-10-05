using System.Security.Cryptography;

namespace LatchiXcloud.Core.Services;

/// <summary>Crash-safe file writes: write to .tmp then File.Replace — a power cut can never truncate the original.</summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }

    public static string? TryReadAllText(string path) =>
        File.Exists(path) ? File.ReadAllText(path) : null;

    public static bool TryDelete(string path)
    {
        try { if (File.Exists(path)) { File.Delete(path); return true; } return false; }
        catch { return false; }
    }

    public static string Sha256(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    public static string Sha256Bytes(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

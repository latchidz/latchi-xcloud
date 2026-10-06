using System.Reflection;
using LatchiXcloud.Core.Services;

namespace LatchiXcloud.App.Services;

/// <summary>
/// Resolves the bundled Better xCloud userscript + provenance manifest:
///   • folder layout (installer, repo output): resources/ next to the exe;
///   • single-file portable build: nothing sits next to the exe — both files are
///     embedded in the assembly and extracted ONCE into the writable data dir.
/// The bytes are identical in both cases, so the SHA-256 provenance check holds.
/// </summary>
public static class BundledResources
{
    public const string ScriptName = "better-xcloud.user.js";
    public const string ManifestName = "bxc-manifest.json";

    /// <summary>(script path, manifest path) — extracting on first use when embedded.</summary>
    public static (string Script, string Manifest) Resolve()
    {
        // 1) folder layout wins when present (installer / publish folder)
        var dir = Path.Combine(AppContext.BaseDirectory, "resources");
        var script = Path.Combine(dir, ScriptName);
        var manifest = Path.Combine(dir, ManifestName);
        if (File.Exists(script) && File.Exists(manifest))
            return (script, manifest);

        // 2) single-file portable: extract the embedded copies ONCE into the
        //    writable data dir (never next to the exe — that folder may be read-only)
        var target = Path.Combine(AppPaths.DataDir, "bundled");
        Directory.CreateDirectory(target);
        var tScript = Path.Combine(target, ScriptName);
        var tManifest = Path.Combine(target, ManifestName);
        if (!IsFile(tScript)) Extract(ScriptName, tScript);
        if (!IsFile(tManifest)) Extract(ManifestName, tManifest);
        return (tScript, tManifest);

        static bool IsFile(string p) => File.Exists(p) && new FileInfo(p).Length > 0;
    }

    private static void Extract(string fileName, string to)
    {
        var asm = Assembly.GetExecutingAssembly();
        var res = Array.Find(asm.GetManifestResourceNames(),
            n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (res is null)
            throw new FileNotFoundException($"Embedded bundled resource missing: {fileName}", fileName);
        using var src = asm.GetManifestResourceStream(res);
        if (src is null)
            throw new FileNotFoundException($"Embedded bundled resource unreadable: {res}", res);
        using var dst = File.Create(to);
        src.CopyTo(dst);
    }
}

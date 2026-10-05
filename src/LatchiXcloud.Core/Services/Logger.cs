namespace LatchiXcloud.Core.Services;

/// <summary>
/// Minimal local log with rotation (2 MB x 5). NEVER log: passwords, cookies, tokens,
/// auth headers, or full URLs with query strings (login URLs carry tokens) — hosts/paths only.
/// </summary>
public static class Logger
{
    private static readonly object Lock = new();
    private static string _dir = "";
    private const long MaxBytes = 2 * 1024 * 1024;
    private const int KeepFiles = 5;

    public static void Init(string logsDir)
    {
        _dir = logsDir;
        Directory.CreateDirectory(_dir);
    }

    public static void Info(string msg) => Write("INFO ", msg);
    public static void Warn(string msg) => Write("WARN ", msg);
    public static void Error(string msg) => Write("ERROR", msg);

    private static void Write(string level, string msg)
    {
        try
        {
            if (_dir.Length == 0) return;
            lock (Lock)
            {
                var file = Path.Combine(_dir, $"app-{DateTime.Now:yyyyMMdd}.log");
                var info = new FileInfo(file);
                if (info.Exists && info.Length > MaxBytes)
                {
                    var rolled = $"{file}.{DateTime.Now:HHmmss}.old";
                    if (File.Exists(rolled)) File.Delete(rolled);
                    File.Move(file, rolled);
                    Rotate();
                }
                File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {msg}\n");
            }
        }
        catch { /* logging must never crash the app */ }
    }

    private static void Rotate()
    {
        var old = Directory.GetFiles(_dir, "*.old").OrderByDescending(f => f).Skip(KeepFiles).ToArray();
        foreach (var f in old) { try { File.Delete(f); } catch { } }
    }

    /// <summary>Strips the query string (tokens) so URLs can be logged safely.</summary>
    public static string SafeUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        try
        {
            var u = new Uri(url);
            return u.Host + u.AbsolutePath;
        }
        catch { return "(invalid-url)"; }
    }
}

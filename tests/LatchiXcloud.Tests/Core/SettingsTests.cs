using LatchiXcloud.Core.Models;
using LatchiXcloud.Core.Services;
using Xunit;

public class SettingsTests : IDisposable
{
    private readonly string _dir;
    public SettingsTests() { _dir = Path.Combine(Path.GetTempPath(), "lx-set-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
    public void Dispose() { try { Directory.Delete(_dir, recursive: true); } catch { } }

    [Fact]
    public void Defaults()
    {
        var s = new SettingsStore(_dir);
        Assert.True(s.Current.StartMaximized);
        Assert.True(s.Current.AutoUpdateBetterXcloud);
        Assert.False(s.Current.LowEndMode);
        Assert.Equal("ar", s.Current.Language);
    }

    [Fact]
    public void Roundtrip()
    {
        var s = new SettingsStore(_dir);
        s.Current.LowEndMode = true;
        s.Current.Language = "en";
        s.Save();
        var re = new SettingsStore(_dir);
        Assert.True(re.Current.LowEndMode);
        Assert.Equal("en", re.Current.Language);
    }

    [Fact]
    public void CorruptFallsBack()
    {
        new SettingsStore(_dir).Save();
        File.WriteAllText(Path.Combine(_dir, "Settings", "settings.json"), "{{{ nope");
        Assert.True(new SettingsStore(_dir).Current.StartMaximized);
    }
}

public class AtomicFileTests : IDisposable
{
    private readonly string _dir;
    public AtomicFileTests() { _dir = Path.Combine(Path.GetTempPath(), "lx-atom-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
    public void Dispose() { try { Directory.Delete(_dir, recursive: true); } catch { } }

    [Fact]
    public void RoundtripAndReplace()
    {
        var p = Path.Combine(_dir, "a.txt");
        AtomicFile.WriteAllText(p, "one");
        AtomicFile.WriteAllText(p, "two");
        Assert.Equal("two", AtomicFile.TryReadAllText(p));
        Assert.False(File.Exists(p + ".tmp"));
    }

    [Fact]
    public void MissingReadsNull() => Assert.Null(AtomicFile.TryReadAllText(Path.Combine(_dir, "none.txt")));

    [Fact]
    public void Sha256Stable()
    {
        var p = Path.Combine(_dir, "b.bin");
        File.WriteAllBytes(p, new byte[] { 1, 2, 3 });
        Assert.Equal(AtomicFile.Sha256(p), AtomicFile.Sha256(p));
        Assert.Equal(64, AtomicFile.Sha256(p).Length);
    }
}

public class LoggerTests : IDisposable
{
    private readonly string _dir;
    public LoggerTests() { _dir = Path.Combine(Path.GetTempPath(), "lx-log-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); Logger.Init(_dir); }
    public void Dispose() { try { Directory.Delete(_dir, recursive: true); } catch { } }

    [Fact]
    public void WritesAndStripsQuery()
    {
        Logger.Info("hello");
        Logger.Warn(Logger.SafeUrl("https://login.live.com/?token=SECRET&x=1"));
        var content = Directory.GetFiles(_dir, "*.log").SelectMany(File.ReadAllLines).ToList();
        Assert.Contains(content, l => l.Contains("hello"));
        var safe = content.First(l => l.Contains("login.live.com"));
        Assert.DoesNotContain("SECRET", safe);
        Assert.Contains("login.live.com/", safe);
    }
}

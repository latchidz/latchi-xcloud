using LatchiXcloud.Core.Models;

namespace LatchiXcloud.Core.Services;

/// <summary>Loads/saves Settings/settings.json; corrupt or missing file falls back to defaults.</summary>
public sealed class SettingsStore
{
    private readonly string _file;
    public AppSettings Current { get; private set; }

    public SettingsStore(string dataDir)
    {
        _file = Path.Combine(dataDir, "Settings", "settings.json");
        Current = Json.Deserialize<AppSettings>(AtomicFile.TryReadAllText(_file)) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        if (double.IsNaN(settings.WindowLeft)) settings.WindowLeft = 0;
        if (double.IsNaN(settings.WindowTop)) settings.WindowTop = 0;
        Current = settings;
        AtomicFile.WriteAllText(_file, Json.Serialize(settings));
    }

    public void Save() => Save(Current);
}

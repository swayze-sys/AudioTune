using System.IO;
using System.Text.Json;
using AudioTune.Models;

namespace AudioTune.Services;

public sealed class SettingsService
{
    private readonly string _folder;
    private readonly string _file;
    public AppSettings Current { get; private set; } = new();
    public event Action? SettingsChanged;

    public SettingsService()
    {
        _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioTune");
        _file = Path.Combine(_folder, "settings.json");
    }

    public void Load()
    {
        try
        {
            Directory.CreateDirectory(_folder);
            if (File.Exists(_file))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_file)) ?? new AppSettings();
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(_file, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        SettingsChanged?.Invoke();
    }
}

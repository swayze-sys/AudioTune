using System.IO;
using System.Text.Json;
using AudioTune.Models;

namespace AudioTune.Services;

public sealed class CorrectionPresetRepository
{
    private readonly string _folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AudioTune", "CorrectionPresets");

    private readonly Dictionary<Guid, CorrectionPreset> _presets = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public event Action? PresetsChanged;

    public IReadOnlyList<CorrectionPreset> Presets => _presets.Values.OrderByDescending(p => p.UpdatedAt).ToList();

    public CorrectionPreset? Active
    {
        get
        {
            if (Guid.TryParse(AppServices.Settings.Current.ActiveCorrectionPresetId, out var id) && _presets.TryGetValue(id, out var preset))
                return preset;
            return null;
        }
    }

    public void Load()
    {
        Directory.CreateDirectory(_folder);
        _presets.Clear();
        foreach (var file in Directory.GetFiles(_folder, "preset-*.json"))
        {
            try
            {
                var preset = JsonSerializer.Deserialize<CorrectionPreset>(File.ReadAllText(file));
                if (preset is not null)
                {
                    // Schema v2 adds a master Fine Tune enable switch. Existing v1 presets
                    // used Fine Tune unconditionally, so preserve that behavior during migration.
                    if (preset.SchemaVersion < 2)
                    {
                        preset.FineTuneEnabled = true;
                        preset.SchemaVersion = 2;
                    }
                    _presets[preset.Id] = preset;
                }
            }
            catch { }
        }

        var activeProfile = AppServices.Profiles.LastSession;
        if (activeProfile is not null)
            EnsureDefaultForProfile(activeProfile);
    }

    public CorrectionPreset EnsureDefaultForProfile(HearingSession session)
    {
        var existing = Presets.FirstOrDefault(p => p.HearingProfileId == session.Id);
        if (existing is not null)
        {
            if (Active is null || Active.HearingProfileId != session.Id) SetActive(existing.Id);
            return existing;
        }

        var preset = new CorrectionPreset
        {
            HearingProfileId = session.Id,
            Name = "Personal correction",
            StrengthPercent = Math.Clamp(session.CorrectionStrengthPercent, 0.0, 200.0)
        };
        Save(preset);
        SetActive(preset.Id);
        return preset;
    }

    public CorrectionPreset Create(HearingSession session, string name, CorrectionPreset? source = null)
    {
        var preset = new CorrectionPreset
        {
            HearingProfileId = session.Id,
            Name = string.IsNullOrWhiteSpace(name) ? "Personal correction" : name.Trim(),
            StrengthPercent = source?.StrengthPercent ?? 100.0,
            FineTuneEnabled = source?.FineTuneEnabled ?? true,
            StereoCenterBalanceDb = source?.StereoCenterBalanceDb ?? 0.0,
            StereoCenteringUpdatedAt = source?.StereoCenteringUpdatedAt,
            FineTuneAdjustments = source?.FineTuneAdjustments.Select(x => new FineTuneAdjustment
            {
                FrequencyHz = x.FrequencyHz,
                Ear = x.Ear,
                AdjustmentDb = x.AdjustmentDb,
                Timestamp = x.Timestamp
            }).ToList() ?? new()
        };
        Save(preset);
        return preset;
    }

    public CorrectionPreset? Get(Guid id) => _presets.TryGetValue(id, out var preset) ? preset : null;

    public CorrectionPreset GetActiveForProfile(HearingSession session)
    {
        var active = Active;
        if (active is not null && active.HearingProfileId == session.Id) return active;
        return EnsureDefaultForProfile(session);
    }

    public void Save(CorrectionPreset preset)
    {
        preset.UpdatedAt = DateTime.Now;
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, $"preset-{preset.Id:N}.json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(preset, _jsonOptions));
        File.Move(temp, path, true);
        _presets[preset.Id] = preset;
        PresetsChanged?.Invoke();
    }

    public void SetActive(Guid id)
    {
        if (!_presets.ContainsKey(id)) return;
        AppServices.Settings.Current.ActiveCorrectionPresetId = id.ToString("D");
        AppServices.Settings.Save();
        PresetsChanged?.Invoke();
    }
}

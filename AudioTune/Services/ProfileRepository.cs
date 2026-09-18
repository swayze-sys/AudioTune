using System.IO;
using System.Text.Json;
using AudioTune.Models;

namespace AudioTune.Services;

public sealed class ProfileRepository
{
    private readonly string _folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AudioTune", "Profiles");

    private readonly Dictionary<Guid, HearingSession> _sessions = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public HearingSession? LastSession { get; private set; }
    public IReadOnlyList<HearingSession> Sessions => _sessions.Values
        .OrderBy(s => s.IsArchived)
        .ThenByDescending(s => s.UpdatedAt == default ? s.StartedAt : s.UpdatedAt)
        .ToList();

    public event Action? ProfilesChanged;

    public void Autosave(HearingSession session, bool makeActive = true)
    {
        session.UpdatedAt = DateTime.Now;
        Directory.CreateDirectory(_folder);
        WriteAtomic(GetCanonicalPath(session.Id), JsonSerializer.Serialize(session, _jsonOptions));
        _sessions[session.Id] = session;
        if (makeActive)
        {
            LastSession = session;
            SetActiveSetting(session.Id);
        }
        else if (LastSession?.Id == session.Id)
        {
            LastSession = session;
        }
        ProfilesChanged?.Invoke();
    }

    public void Save(HearingSession session, bool makeActive = false)
    {
        Autosave(session, makeActive);
        AppServices.Log.Log($"Profile saved: {session.Name}", LogLevel.Success);
    }

    public void Rename(HearingSession session, string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return;
        session.Name = trimmed;
        Save(session);
    }

    public HearingSession? Get(Guid id) => _sessions.TryGetValue(id, out var session) ? session : null;

    public HearingSession Duplicate(HearingSession source, string? newName = null)
    {
        var now = DateTime.Now;
        var copy = CloneSession(source);
        copy.Id = Guid.NewGuid();
        copy.Name = string.IsNullOrWhiteSpace(newName) ? $"{source.Name} - copy" : newName.Trim();
        copy.UpdatedAt = now;
        copy.IsArchived = false;
        Save(copy);
        return copy;
    }

    public HearingSession Import(string sourcePath)
    {
        var session = JsonSerializer.Deserialize<HearingSession>(File.ReadAllText(sourcePath))
                      ?? throw new InvalidDataException("The selected file does not contain an AudioTune hearing profile.");
        if (_sessions.ContainsKey(session.Id)) session.Id = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(session.Name)) session.Name = $"Imported profile {DateTime.Now:dd.MM.yyyy HH:mm}";
        session.UpdatedAt = DateTime.Now;
        Save(session);
        AppServices.Log.Log($"Profile imported: {session.Name}", LogLevel.Success);
        return session;
    }

    public void SetArchived(HearingSession session, bool archived)
    {
        session.IsArchived = archived;
        Save(session);
    }

    public bool RestoreBackup(HearingSession session)
    {
        var path = GetCanonicalPath(session.Id);
        var backup = path + ".bak";
        if (!File.Exists(backup)) return false;
        try
        {
            var restored = JsonSerializer.Deserialize<HearingSession>(File.ReadAllText(backup));
            if (restored is null) return false;
            restored.Id = session.Id;
            restored.UpdatedAt = DateTime.Now;
            _sessions[session.Id] = restored;
            WriteAtomic(path, JsonSerializer.Serialize(restored, _jsonOptions));
            if (LastSession?.Id == session.Id) LastSession = restored;
            ProfilesChanged?.Invoke();
            AppServices.Log.Log($"Restored previous backup for profile: {restored.Name}", LogLevel.Success);
            return true;
        }
        catch { return false; }
    }

    public string Compare(HearingSession a, HearingSession b)
    {
        var pairs = from ma in a.Measurements
                    where ma.Status == HearingMeasurementStatus.Detected
                    join mb in b.Measurements.Where(x => x.Status == HearingMeasurementStatus.Detected)
                        on new { ma.Ear, ma.FrequencyHz } equals new { mb.Ear, mb.FrequencyHz }
                    select new { ma.Ear, ma.FrequencyHz, Delta = mb.ThresholdDbFs - ma.ThresholdDbFs };
        var list = pairs.ToList();
        if (list.Count == 0) return "The profiles do not contain matching detected measurement points.";
        double mad = list.Average(x => Math.Abs(x.Delta));
        double max = list.Max(x => Math.Abs(x.Delta));
        var largest = list.OrderByDescending(x => Math.Abs(x.Delta)).First();
        return $"{list.Count} matching points\nMean absolute difference: {mad:0.0} dB\nLargest difference: {max:0.0} dB at {largest.FrequencyHz:0.##} Hz ({largest.Ear})";
    }

    public void SetActive(Guid id)
    {
        if (!_sessions.TryGetValue(id, out var session)) return;
        LastSession = session;
        SetActiveSetting(id);
        AppServices.CorrectionPresets.EnsureDefaultForProfile(session);
        ProfilesChanged?.Invoke();
        AppServices.Log.Log($"Active hearing profile: {session.Name}", LogLevel.Info);
    }

    public void LoadLatest()
    {
        Directory.CreateDirectory(_folder);
        _sessions.Clear();
        var files = Directory.GetFiles(_folder, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path =>
                Path.GetFileName(path).StartsWith("profile-", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).StartsWith("hearing-", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).Equals("current-session.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var file in files)
        {
            try
            {
                var session = JsonSerializer.Deserialize<HearingSession>(File.ReadAllText(file));
                if (session is null) continue;
                NormalizeLegacySession(session, file);
                if (!_sessions.TryGetValue(session.Id, out var existing) || session.UpdatedAt >= existing.UpdatedAt)
                    _sessions[session.Id] = session;
            }
            catch { }
        }

        foreach (var session in _sessions.Values)
        {
            var canonical = GetCanonicalPath(session.Id);
            if (!File.Exists(canonical))
            {
                try { WriteAtomic(canonical, JsonSerializer.Serialize(session, _jsonOptions)); } catch { }
            }
        }

        HearingSession? active = null;
        if (Guid.TryParse(AppServices.Settings.Current.ActiveHearingProfileId, out var activeId))
            _sessions.TryGetValue(activeId, out active);
        LastSession = active ?? Sessions.FirstOrDefault(s => !s.IsArchived) ?? Sessions.FirstOrDefault();
        if (LastSession is not null) SetActiveSetting(LastSession.Id, saveSettings: false);
    }

    public void Export(HearingSession session, string destinationPath)
    {
        File.WriteAllText(destinationPath, JsonSerializer.Serialize(session, _jsonOptions));
        AppServices.Log.Log($"Profile exported: {destinationPath}", LogLevel.Success);
    }

    private void NormalizeLegacySession(HearingSession session, string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(session.Name)) session.Name = $"Hearing Profile {session.StartedAt:dd.MM.yyyy HH:mm}";
        if (session.UpdatedAt == default) session.UpdatedAt = File.GetLastWriteTime(sourceFile);
        session.SchemaVersion = Math.Max(session.SchemaVersion, 3);
        foreach (var m in session.Measurements)
        {
            m.InitialThresholdDbFs ??= m.ThresholdDbFs;
            if (m.Confidence == MeasurementConfidence.Unknown && m.Status == HearingMeasurementStatus.Detected)
                m.Confidence = MeasurementConfidence.Medium;
            m.Trials ??= new();
            m.VerificationThresholdsDbFs ??= new();
            m.VerificationStatuses ??= new();
        }
    }

    private static HearingSession CloneSession(HearingSession source) => new()
    {
        SchemaVersion = source.SchemaVersion,
        AppVersion = source.AppVersion,
        TestProtocolVersion = source.TestProtocolVersion,
        CorrectionAlgorithmVersionAtMeasurement = source.CorrectionAlgorithmVersionAtMeasurement,
        Id = source.Id,
        Name = source.Name,
        StartedAt = source.StartedAt,
        UpdatedAt = source.UpdatedAt,
        CompletedAt = source.CompletedAt,
        HeadphoneId = source.HeadphoneId,
        OutputDeviceId = source.OutputDeviceId,
        OutputDeviceName = source.OutputDeviceName,
        ApplicationSessionVolumePercent = source.ApplicationSessionVolumePercent,
        EndpointMasterVolumePercentAtStart = source.EndpointMasterVolumePercentAtStart,
        SampleRateHz = source.SampleRateHz,
        AudioApi = source.AudioApi,
        MeasurementNotes = source.MeasurementNotes,
        CorrectionStrengthPercent = source.CorrectionStrengthPercent,
        Measurements = source.Measurements.Select(m => new HearingMeasurement
        {
            FrequencyHz = m.FrequencyHz,
            Ear = m.Ear,
            ThresholdDbFs = m.ThresholdDbFs,
            InitialThresholdDbFs = m.InitialThresholdDbFs,
            Timestamp = m.Timestamp,
            Presentations = m.Presentations,
            Status = m.Status,
            Trials = m.Trials.Select(t => new HearingTrial { LevelDbFs = t.LevelDbFs, Heard = t.Heard, Timestamp = t.Timestamp }).ToList(),
            VerificationThresholdsDbFs = m.VerificationThresholdsDbFs.ToList(),
            VerificationStatuses = m.VerificationStatuses.ToList(),
            WasAutomaticallyFlagged = m.WasAutomaticallyFlagged,
            VerificationCompleted = m.VerificationCompleted,
            Confidence = m.Confidence
        }).ToList()
    };

    private string GetCanonicalPath(Guid id) => Path.Combine(_folder, $"profile-{id:N}.json");

    private void SetActiveSetting(Guid id, bool saveSettings = true)
    {
        var value = id.ToString("D");
        if (string.Equals(AppServices.Settings.Current.ActiveHearingProfileId, value, StringComparison.OrdinalIgnoreCase)) return;
        AppServices.Settings.Current.ActiveHearingProfileId = value;
        if (saveSettings) AppServices.Settings.Save();
    }

    private static void WriteAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        var backup = path + ".bak";
        File.WriteAllText(temp, content);
        if (File.Exists(path)) { try { File.Copy(path, backup, true); } catch { } }
        File.Move(temp, path, true);
    }
}

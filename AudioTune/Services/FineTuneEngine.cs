using AudioTune.Models;

namespace AudioTune.Services;

public sealed class FineTuneEngine
{
    public static readonly double[] Frequencies = [63, 125, 250, 500, 2000, 4000, 8000, 12500];
    private const double ReferenceFrequencyHz = 1000.0;
    private const double BaseLevelDbFs = -36.0;
    private const double StepDb = 1.0;
    private const double MaxAdjustmentDb = 6.0;

    public HearingSession? Session { get; private set; }
    public CorrectionPreset? Preset { get; private set; }
    public EarChannel Ear { get; private set; } = EarChannel.Left;
    public int FrequencyIndex { get; private set; }
    public double CurrentFrequency => Frequencies[FrequencyIndex];
    public double CurrentAdjustmentDb { get; private set; }
    public bool IsComplete { get; private set; }

    public event Action? StateChanged;

    public void Start(HearingSession session, CorrectionPreset preset)
    {
        Session = session;
        Preset = preset;
        Ear = EarChannel.Left;
        FrequencyIndex = 0;
        IsComplete = false;
        LoadCurrentAdjustment();
        StateChanged?.Invoke();
    }

    public void AdjustTestLouder() { CurrentAdjustmentDb = Math.Clamp(CurrentAdjustmentDb + StepDb, -MaxAdjustmentDb, MaxAdjustmentDb); StateChanged?.Invoke(); }
    public void AdjustTestQuieter() { CurrentAdjustmentDb = Math.Clamp(CurrentAdjustmentDb - StepDb, -MaxAdjustmentDb, MaxAdjustmentDb); StateChanged?.Invoke(); }

    public void AcceptEqual()
    {
        if (Session is null || Preset is null || IsComplete) return;
        Preset.FineTuneAdjustments.RemoveAll(x => x.Ear == Ear && Math.Abs(x.FrequencyHz - CurrentFrequency) < 0.01);
        Preset.FineTuneAdjustments.Add(new FineTuneAdjustment
        {
            Ear = Ear,
            FrequencyHz = CurrentFrequency,
            AdjustmentDb = CurrentAdjustmentDb,
            Timestamp = DateTime.Now
        });
        AppServices.CorrectionPresets.Save(Preset);
        Advance();
    }

    public void Skip() => Advance();

    public (double ReferenceDbFs, double TestDbFs) GetPlaybackLevels()
    {
        if (Session is null || Preset is null) return (BaseLevelDbFs, BaseLevelDbFs);
        var leftOrRight = Ear;
        var baseCurve = CorrectionPreviewService.Create(Session, Preset, leftOrRight, includeFineTune: false);
        double refGain = Interpolate(baseCurve, ReferenceFrequencyHz);
        double testGain = Interpolate(baseCurve, CurrentFrequency);
        double reference = Math.Clamp(BaseLevelDbFs + refGain, -48.0, -18.0);
        double test = Math.Clamp(BaseLevelDbFs + testGain + CurrentAdjustmentDb, -48.0, -18.0);
        return (reference, test);
    }

    public int CompletedCount => Preset?.FineTuneAdjustments.Count(x => Frequencies.Any(f => Math.Abs(f - x.FrequencyHz) < 0.01)) ?? 0;
    public int TotalCount => Frequencies.Length * 2;

    private void Advance()
    {
        if (FrequencyIndex < Frequencies.Length - 1) FrequencyIndex++;
        else if (Ear == EarChannel.Left) { Ear = EarChannel.Right; FrequencyIndex = 0; }
        else { IsComplete = true; StateChanged?.Invoke(); return; }
        LoadCurrentAdjustment();
        StateChanged?.Invoke();
    }

    private void LoadCurrentAdjustment()
    {
        CurrentAdjustmentDb = Preset?.FineTuneAdjustments
            .Where(x => x.Ear == Ear && Math.Abs(x.FrequencyHz - CurrentFrequency) < 0.01)
            .OrderByDescending(x => x.Timestamp)
            .FirstOrDefault()?.AdjustmentDb ?? 0.0;
    }

    private static double Interpolate(IReadOnlyList<(double Frequency, double GainDb)> points, double frequency)
    {
        if (points.Count == 0) return 0.0;
        if (frequency <= points[0].Frequency) return points[0].GainDb;
        if (frequency >= points[^1].Frequency) return points[^1].GainDb;
        for (int i = 0; i < points.Count - 1; i++)
        {
            var a = points[i]; var b = points[i + 1];
            if (frequency < a.Frequency || frequency > b.Frequency) continue;
            double t = (Math.Log(frequency) - Math.Log(a.Frequency)) / (Math.Log(b.Frequency) - Math.Log(a.Frequency));
            return a.GainDb + ((b.GainDb - a.GainDb) * t);
        }
        return 0.0;
    }
}

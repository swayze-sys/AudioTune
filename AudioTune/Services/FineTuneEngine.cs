using AudioTune.Models;

namespace AudioTune.Services;

public sealed class FineTuneEngine
{
    public static readonly double[] Frequencies = HearingTestEngine.Frequencies.ToArray();
    private const double ReferenceFrequencyHz = 1000.0;
    private const double BaseLevelDbFs = -36.0;
    private const double StepDb = 1.0;
    private const double MaxAdjustmentDb = 6.0;
    private bool _singlePointReview;
    private EarChannel _resumeEar;
    private int _resumeFrequencyIndex;
    private bool _resumeWasComplete;

    public HearingSession? Session { get; private set; }
    public CorrectionPreset? Preset { get; private set; }
    public EarChannel Ear { get; private set; } = EarChannel.Left;
    public int FrequencyIndex { get; private set; }
    public double CurrentFrequency => Frequencies[FrequencyIndex];
    public double CurrentAdjustmentDb { get; private set; }
    public bool IsComplete { get; private set; }
    public bool IsSinglePointReview => _singlePointReview;

    public event Action? StateChanged;

    public void Start(HearingSession session, CorrectionPreset preset)
    {
        Session = session;
        Preset = preset;
        Ear = EarChannel.Left;
        FrequencyIndex = 0;
        _singlePointReview = false;
        IsComplete = !MoveToFirstMissingPoint();
        LoadCurrentAdjustment();
        StateChanged?.Invoke();
    }

    public bool BeginSinglePointReview(EarChannel ear, double frequencyHz)
    {
        if (Preset is null || _singlePointReview) return false;
        int index = Array.FindIndex(Frequencies, frequency => Math.Abs(frequency - frequencyHz) < 0.01);
        if (index < 0 || !HasSavedAdjustment(ear, frequencyHz)) return false;

        _resumeEar = Ear;
        _resumeFrequencyIndex = FrequencyIndex;
        _resumeWasComplete = IsComplete;
        _singlePointReview = true;
        Ear = ear;
        FrequencyIndex = index;
        IsComplete = false;
        LoadCurrentAdjustment();
        AppServices.Log.Log($"Fine Tune point review started: {ear} {frequencyHz:0.##} Hz. The saved value remains active until a replacement is accepted.", LogLevel.Info);
        StateChanged?.Invoke();
        return true;
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
        if (_singlePointReview)
        {
            AppServices.Log.Log($"Fine Tune point updated: {Ear} {CurrentFrequency:0.##} Hz = {CurrentAdjustmentDb:+0.0;-0.0;0.0} dB.", LogLevel.Success);
            FinishSinglePointReview();
            return;
        }
        Advance();
    }

    public void Skip()
    {
        if (_singlePointReview)
        {
            FinishSinglePointReview();
            return;
        }
        Advance();
    }

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

    public int CompletedCount => Preset?.FineTuneAdjustments
        .Where(x => Frequencies.Any(f => Math.Abs(f - x.FrequencyHz) < 0.01))
        .Select(x => (x.Ear, Frequency: Frequencies.First(f => Math.Abs(f - x.FrequencyHz) < 0.01)))
        .Distinct()
        .Count() ?? 0;
    public int TotalCount => Frequencies.Length * 2;

    public bool HasSavedAdjustment(EarChannel ear, double frequencyHz) =>
        Preset?.FineTuneAdjustments.Any(x => x.Ear == ear && Math.Abs(x.FrequencyHz - frequencyHz) < 0.01) == true;

    public double? GetSavedAdjustment(EarChannel ear, double frequencyHz) => Preset?.FineTuneAdjustments
        .Where(x => x.Ear == ear && Math.Abs(x.FrequencyHz - frequencyHz) < 0.01)
        .OrderByDescending(x => x.Timestamp)
        .Select(x => (double?)x.AdjustmentDb)
        .FirstOrDefault();

    private void Advance()
    {
        if (FrequencyIndex < Frequencies.Length - 1) FrequencyIndex++;
        else if (Ear == EarChannel.Left) { Ear = EarChannel.Right; FrequencyIndex = 0; }
        else { IsComplete = true; StateChanged?.Invoke(); return; }
        LoadCurrentAdjustment();
        StateChanged?.Invoke();
    }

    private void FinishSinglePointReview()
    {
        _singlePointReview = false;
        Ear = _resumeEar;
        FrequencyIndex = _resumeFrequencyIndex;
        IsComplete = _resumeWasComplete;
        LoadCurrentAdjustment();
        StateChanged?.Invoke();
    }

    private bool MoveToFirstMissingPoint()
    {
        foreach (var ear in new[] { EarChannel.Left, EarChannel.Right })
        {
            for (int i = 0; i < Frequencies.Length; i++)
            {
                if (HasSavedAdjustment(ear, Frequencies[i])) continue;
                Ear = ear;
                FrequencyIndex = i;
                return true;
            }
        }
        return false;
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

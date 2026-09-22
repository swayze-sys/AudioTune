using AudioTune.Models;

namespace AudioTune.Services;

public static class CorrectionPreviewService
{
    public const int AlgorithmVersion = 7;
    public const double MaxGainDb = 6.0;
    public const double MaximumCombinedGainDb = 12.0;

    public static IReadOnlyList<(double Frequency, double GainDb)> Create(HearingSession session, EarChannel ear)
    {
        var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
        return Create(session, preset, ear);
    }

    public static IReadOnlyList<(double Frequency, double GainDb)> Create(
        HearingSession session,
        CorrectionPreset preset,
        EarChannel ear,
        bool includeFineTune = true)
    {
        var pair = CreateStereoPair(session, preset, includeFineTune);
        return ear == EarChannel.Left ? pair.Left : pair.Right;
    }

    public static (IReadOnlyList<(double Frequency, double GainDb)> Left,
                   IReadOnlyList<(double Frequency, double GainDb)> Right)
        CreateStereoPair(HearingSession session, CorrectionPreset preset, bool includeFineTune = true)
    {
        var left = CreateUnprotected(session, preset, EarChannel.Left, includeFineTune);
        var right = CreateUnprotected(session, preset, EarChannel.Right, includeFineTune);

        if (!AppServices.Settings.Current.StereoPreservationEnabled || left.Count == 0 || right.Count == 0)
            return (left, right);

        return ApplyStereoPreservation(session, left, right);
    }

    /// <summary>
    /// Correction before stereo-image preservation. Intended for diagnostics only.
    /// </summary>
    public static IReadOnlyList<(double Frequency, double GainDb)> CreateBeforeStereoPreservation(
        HearingSession session, CorrectionPreset preset, EarChannel ear, bool includeFineTune = true)
        => CreateUnprotected(session, preset, ear, includeFineTune);

    internal sealed record CorrectionPointComponents(
        double Frequency,
        double HearingModelDb,
        double FineTuneDb,
        double StrengthMultiplier,
        double CombinedBeforeStereoDb,
        bool IsCeiling);

    internal static IReadOnlyList<CorrectionPointComponents> CreateComponentsBeforeStereoPreservation(
        HearingSession session, CorrectionPreset preset, EarChannel ear, bool includeFineTune = true)
        => CreateUnprotectedComponents(session, preset, ear, includeFineTune);

    private static IReadOnlyList<(double Frequency, double GainDb)> CreateUnprotected(
        HearingSession session,
        CorrectionPreset preset,
        EarChannel ear,
        bool includeFineTune)
        => CreateUnprotectedComponents(session, preset, ear, includeFineTune)
            .Select(x => (x.Frequency, x.CombinedBeforeStereoDb))
            .ToList();

    private static IReadOnlyList<CorrectionPointComponents> CreateUnprotectedComponents(
        HearingSession session,
        CorrectionPreset preset,
        EarChannel ear,
        bool includeFineTune)
    {
        var detected = session.Measurements
            .Where(m => m.Status == HearingMeasurementStatus.Detected)
            .OrderBy(m => m.FrequencyHz)
            .ToList();
        var usable = session.Measurements
            .Where(m => m.Status != HearingMeasurementStatus.Skipped)
            .OrderBy(m => m.FrequencyHz)
            .ToList();
        if (usable.Count == 0 || detected.Count == 0) return [];

        var allResidualOffsets = detected
            .Where(m => m.FrequencyHz <= 12500)
            .Select(m => m.ThresholdDbFs - HumanSensitivityModel.GetRelativeThresholdDb(m.FrequencyHz))
            .OrderBy(x => x)
            .ToList();
        if (allResidualOffsets.Count == 0) return [];

        double globalOffset = Median(allResidualOffsets);
        double strength = Math.Clamp(preset.StrengthPercent, 0.0, 200.0) / 100.0;

        var own = usable.Where(m => m.Ear == ear).OrderBy(m => m.FrequencyHz).ToList();
        var otherEar = ear == EarChannel.Left ? EarChannel.Right : EarChannel.Left;
        var otherByFrequency = detected.Where(m => m.Ear == otherEar)
            .GroupBy(m => m.FrequencyHz)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Timestamp).First());

        var points = new List<CorrectionPointComponents>();
        foreach (var m in own)
        {
            double hearingModel;
            if (!preset.HearingProfileEnabled)
            {
                // Bypass only the modeled hearing contribution. Raw measurements and the
                // independently switchable Fine Tune stage remain available and unchanged.
                hearingModel = 0.0;
            }
            else if (m.Status == HearingMeasurementStatus.NotDetectedAtCeiling)
            {
                // A ceiling result requests the maximum hearing-model contribution, but it does
                // not bypass Fine Tune. Both contributions intentionally share the same final
                // +/-12 dB window, just like they do for an ordinary detected measurement.
                hearingModel = MaxGainDb;
            }
            else
            {
                double expected = globalOffset + HumanSensitivityModel.GetRelativeThresholdDb(m.FrequencyHz);
                double ownResidual = m.ThresholdDbFs - expected;

                double commonResidual = ownResidual;
                double interauralResidual = 0.0;
                if (otherByFrequency.TryGetValue(m.FrequencyHz, out var other))
                {
                    double otherResidual = other.ThresholdDbFs - expected;
                    commonResidual = (ownResidual + otherResidual) / 2.0;
                    interauralResidual = (ownResidual - otherResidual) / 2.0;
                }

                double commonGain = SaturatingGain(commonResidual, maxGain: 4.5, kneeDb: 10.0);
                double interauralGain = SaturatingGain(interauralResidual, maxGain: 3.5, kneeDb: 7.0);

                double populationTrust = HumanSensitivityModel.GetPopulationTrust(m.FrequencyHz);
                double measurementTrust = GetMeasurementTrust(m);
                double interauralTrust = 0.60 + (0.40 * populationTrust);

                hearingModel = (commonGain * populationTrust) + (interauralGain * interauralTrust);
                hearingModel *= measurementTrust;
            }

            double fineTune = includeFineTune ? InterpolateFineTune(preset, ear, m.FrequencyHz) : 0.0;
            // Hearing-model and Fine Tune contributions share one final safety window.
            // Fine Tune remains individually bounded to ±6 dB, while their combined
            // correction may now use the complete ±12 dB DSP range at any intensity.
            double finalGain = Math.Clamp(
                (hearingModel + fineTune) * strength,
                -MaximumCombinedGainDb,
                MaximumCombinedGainDb);
            points.Add(new CorrectionPointComponents(
                m.FrequencyHz,
                hearingModel,
                fineTune,
                strength,
                finalGain,
                m.Status == HearingMeasurementStatus.NotDetectedAtCeiling));
        }

        return points;
    }

    private static (IReadOnlyList<(double Frequency, double GainDb)> Left,
                    IReadOnlyList<(double Frequency, double GainDb)> Right)
        ApplyStereoPreservation(
            HearingSession session,
            IReadOnlyList<(double Frequency, double GainDb)> left,
            IReadOnlyList<(double Frequency, double GainDb)> right)
    {
        var frequencies = left.Select(x => x.Frequency)
            .Concat(right.Select(x => x.Frequency))
            .Distinct().OrderBy(x => x).ToList();
        if (frequencies.Count == 0) return (left, right);

        var raw = frequencies.Select(f => new StereoPoint(
            f,
            InterpolateLog(left, f),
            InterpolateLog(right, f),
            IsCeiling(session, EarChannel.Left, f),
            IsCeiling(session, EarChannel.Right, f))).ToList();

        // Smooth only the differential component. The common tonal correction is never smoothed
        // here, so stereo protection cannot flatten or otherwise change the average EQ curve.
        var differentials = raw.Select(x => x.Left - x.Right).ToArray();
        var smoothed = new double[differentials.Length];
        for (int i = 0; i < differentials.Length; i++)
        {
            if (differentials.Length == 1) smoothed[i] = differentials[i];
            else if (i == 0) smoothed[i] = (0.75 * differentials[i]) + (0.25 * differentials[i + 1]);
            else if (i == differentials.Length - 1) smoothed[i] = (0.25 * differentials[i - 1]) + (0.75 * differentials[i]);
            else smoothed[i] = (0.25 * differentials[i - 1]) + (0.50 * differentials[i]) + (0.25 * differentials[i + 1]);
        }

        double baseLimit = Math.Clamp(AppServices.Settings.Current.MaxInterauralCorrectionDifferenceDb, 0.0, 6.0);
        var outLeft = new List<(double Frequency, double GainDb)>();
        var outRight = new List<(double Frequency, double GainDb)>();

        for (int i = 0; i < raw.Count; i++)
        {
            var p = raw[i];
            double limit = GetInterauralLimitDb(p.Frequency, baseLimit);
            double limitedDiff = Math.Clamp(smoothed[i], -limit, limit);
            double l;
            double r;

            // A ceiling result is a lower bound and explicitly requests maximum correction.
            // Preserve that high-side boost and bring the opposite channel closer if needed,
            // rather than reducing the ceiling channel and losing the user's requested behavior.
            if (p.LeftCeiling && !p.RightCeiling && p.Left >= p.Right)
            {
                l = p.Left;
                r = Math.Max(p.Right, l - limit);
            }
            else if (p.RightCeiling && !p.LeftCeiling && p.Right >= p.Left)
            {
                r = p.Right;
                l = Math.Max(p.Left, r - limit);
            }
            else
            {
                double common = (p.Left + p.Right) / 2.0;
                l = common + (limitedDiff / 2.0);
                r = common - (limitedDiff / 2.0);
            }

            double stereoMaxGain = MaximumCombinedGainDb;
            outLeft.Add((p.Frequency, Math.Clamp(l, -stereoMaxGain, stereoMaxGain)));
            outRight.Add((p.Frequency, Math.Clamp(r, -stereoMaxGain, stereoMaxGain)));
        }
        return (outLeft, outRight);
    }

    private static double GetInterauralLimitDb(double frequency, double baseLimit)
    {
        // Localization-critical speech/midrange: strictest limit.
        if (frequency >= 150 && frequency <= 5000) return baseLimit;
        // Bass: allow somewhat larger channel differences.
        if (frequency < 150) return baseLimit * 1.5;
        // Upper treble: progressively relax because individual ear/pinna variation grows and
        // phantom-center localization becomes less dependent on small level differences.
        if (frequency < 8000)
        {
            double t = (frequency - 5000.0) / 3000.0;
            return baseLimit * (1.0 + (0.5 * t));
        }
        return baseLimit * 2.0;
    }

    private static bool IsCeiling(HearingSession session, EarChannel ear, double frequency)
        => session.Measurements.Any(m => m.Ear == ear && Math.Abs(m.FrequencyHz - frequency) < 0.01 && m.Status == HearingMeasurementStatus.NotDetectedAtCeiling);

    private sealed record StereoPoint(double Frequency, double Left, double Right, bool LeftCeiling, bool RightCeiling);

    public static string DescribeAlgorithm() =>
        "v7 · ISO-shaped threshold baseline + ceiling-aware correction combined with optional fine tuning + stereo-image preservation";

    private static double GetMeasurementTrust(HearingMeasurement m) => m.Confidence switch
    {
        MeasurementConfidence.High => 1.0,
        MeasurementConfidence.Medium => 0.85,
        MeasurementConfidence.Low => 0.55,
        _ => m.VerificationCompleted ? 0.95 : 0.85
    };

    private static double SaturatingGain(double residualDb, double maxGain, double kneeDb)
        => maxGain * Math.Tanh(residualDb / kneeDb);

    private static double InterpolateFineTune(CorrectionPreset preset, EarChannel ear, double frequency)
    {
        var items = preset.FineTuneAdjustments.Where(x => x.Ear == ear).OrderBy(x => x.FrequencyHz).ToList();
        if (items.Count == 0) return 0.0;
        if (items.Count == 1) return Math.Abs(Math.Log(frequency / items[0].FrequencyHz)) < 0.15 ? items[0].AdjustmentDb : 0.0;
        if (frequency < items[0].FrequencyHz || frequency > items[^1].FrequencyHz) return 0.0;
        for (int i = 0; i < items.Count - 1; i++)
        {
            var a = items[i]; var b = items[i + 1];
            if (frequency < a.FrequencyHz || frequency > b.FrequencyHz) continue;
            double t = (Math.Log(frequency) - Math.Log(a.FrequencyHz)) / (Math.Log(b.FrequencyHz) - Math.Log(a.FrequencyHz));
            return a.AdjustmentDb + ((b.AdjustmentDb - a.AdjustmentDb) * t);
        }
        return 0.0;
    }

    private static double InterpolateLog(IReadOnlyList<(double Frequency, double GainDb)> points, double frequency)
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

    private static double Median(IReadOnlyList<double> sorted)
    {
        if (sorted.Count == 0) return 0.0;
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }
}

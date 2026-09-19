using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AudioTune.Models;

namespace AudioTune.Services;

public sealed record ParametricEqFilter(double FrequencyHz, double GainDb, double Q = 1.0);

public sealed record DspFilterSet(
    IReadOnlyList<ParametricEqFilter> Left,
    IReadOnlyList<ParametricEqFilter> Right,
    double RequiredHeadroomDb,
    double AppliedPreampDb,
    double PositiveGainScale,
    double PotentialClippingDb,
    double LeftTrimDb,
    double RightTrimDb);

public static class DspFilterService
{
    public static readonly double[] Bands = [31.5, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 12500, 14000, 16000, 18000];

    public static IReadOnlyList<ParametricEqFilter> BuildFilters(HearingSession session, EarChannel ear)
    {
        var set = BuildFilterSet(session);
        return ear == EarChannel.Left ? set.Left : set.Right;
    }

    public static DspFilterSet BuildFilterSet(HearingSession session)
    {
        var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
        return BuildFilterSet(session, preset);
    }

    /// <summary>
    /// Builds the DSP from an explicit correction preset. Persistent APO writes use this overload
    /// so the exact preset shown in the confirmation dialog is also the preset written to disk.
    /// </summary>
    public static DspFilterSet BuildFilterSet(HearingSession session, CorrectionPreset preset)
    {
        var rawLeft = BuildRawFilters(session, preset, EarChannel.Left);
        var rawRight = BuildRawFilters(session, preset, EarChannel.Right);

        double rawRequired = CalculateCompositePeakDb(rawLeft, rawRight, 48000);
        var settings = AppServices.Settings.Current;
        double appliedPreampDb = settings.UseAutomaticPreamp
            ? -rawRequired
            : Math.Clamp(settings.ManualPreampDb, -18.0, 0.0);
        double availableHeadroom = Math.Abs(Math.Min(0.0, appliedPreampDb));

        IReadOnlyList<ParametricEqFilter> left = rawLeft;
        IReadOnlyList<ParametricEqFilter> right = rawRight;
        double positiveScale = 1.0;

        if (settings.LimitPositiveBoostsToPreamp && rawRequired > availableHeadroom + 0.001)
        {
            positiveScale = FindPositiveGainScale(rawLeft, rawRight, availableHeadroom, 48000);
            left = ScalePositiveGains(rawLeft, positiveScale);
            right = ScalePositiveGains(rawRight, positiveScale);
        }

        double finalRequired = CalculateCompositePeakDb(left, right, 48000);
        double potentialClipping = Math.Max(0.0, finalRequired - availableHeadroom);
        var (leftTrimDb, rightTrimDb) = GetStereoCenterTrims(preset.StereoCenterBalanceDb);

        return new DspFilterSet(left, right, finalRequired, appliedPreampDb, positiveScale, potentialClipping, leftTrimDb, rightTrimDb);
    }

    /// <summary>Absolute attenuation currently applied by AudioTune's preamp.</summary>
    public static double CalculateHeadroomDb(HearingSession session)
        => Math.Abs(Math.Min(0.0, BuildFilterSet(session).AppliedPreampDb));

    /// <summary>Composite positive peak of the filters before an optional manual headroom limiter is applied.</summary>
    public static double CalculateRequiredHeadroomDb(HearingSession session)
    {
        var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
        var left = BuildRawFilters(session, preset, EarChannel.Left);
        var right = BuildRawFilters(session, preset, EarChannel.Right);
        return CalculateCompositePeakDb(left, right, 48000);
    }

    /// <summary>Largest positive value requested by the correction curve before it is fitted to PK filters.</summary>
    public static double CalculateTargetCurvePeakDb(HearingSession session)
    {
        var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
        return CalculateTargetCurvePeakDb(session, preset);
    }

    public static double CalculateTargetCurvePeakDb(HearingSession session, CorrectionPreset preset)
    {
        var left = CorrectionPreviewService.Create(session, preset, EarChannel.Left, includeFineTune: preset.FineTuneEnabled);
        var right = CorrectionPreviewService.Create(session, preset, EarChannel.Right, includeFineTune: preset.FineTuneEnabled);
        double leftPeak = left.Count == 0 ? 0.0 : Math.Max(0.0, left.Max(x => x.GainDb));
        double rightPeak = right.Count == 0 ? 0.0 : Math.Max(0.0, right.Max(x => x.GainDb));
        return Math.Max(leftPeak, rightPeak);
    }

    public static string CreateSignature(HearingSession session)
    {
        var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
        return CreateSignature(session, preset);
    }

    public static string CreateSignature(HearingSession session, CorrectionPreset preset)
    {
        var set = BuildFilterSet(session, preset);
        var settings = AppServices.Settings.Current;
        var text = $"alg={CorrectionPreviewService.AlgorithmVersion};profile={session.Id:D};preset={preset.Id:D};strength={preset.StrengthPercent:0.###};fineTuneEnabled={preset.FineTuneEnabled};" +
                   $"autoPreamp={settings.UseAutomaticPreamp};manualPreamp={settings.ManualPreampDb:0.###};limitBoosts={settings.LimitPositiveBoostsToPreamp};appliedPreamp={set.AppliedPreampDb:0.###};scale={set.PositiveGainScale:0.######};stereoPreserve={settings.StereoPreservationEnabled};maxLR={settings.MaxInterauralCorrectionDifferenceDb:0.###};center={preset.StereoCenterBalanceDb:0.###};leftTrim={set.LeftTrimDb:0.###};rightTrim={set.RightTrimDb:0.###};" +
                   string.Join(";", set.Left.Select(x => $"L:{x.FrequencyHz:0.##}:{x.GainDb:0.000}:{x.Q:0.###}")) + ";" +
                   string.Join(";", set.Right.Select(x => $"R:{x.FrequencyHz:0.##}:{x.GainDb:0.000}:{x.Q:0.###}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..16];
    }


    public static (double LeftTrimDb, double RightTrimDb) GetStereoCenterTrims(double balanceDb)
    {
        double b = Math.Clamp(balanceDb, -3.0, 3.0);
        // Positive balance moves the image right by attenuating left. Negative does the opposite.
        return b >= 0.0 ? (-b, 0.0) : (0.0, b);
    }

    /// <summary>
    /// Calculates the transfer function AudioTune writes to Equalizer APO for one channel.
    /// This uses the exact fitted PK filters, the selected global preamp and the optional
    /// stereo-centering channel trim. It therefore represents the generated AudioTune APO
    /// configuration rather than the requested/target correction curve.
    /// </summary>
    public static IReadOnlyList<(double Frequency, double GainDb)> CalculateAppliedResponse(
        HearingSession session, EarChannel ear, bool includePreamp = true, int pointCount = 180)
    {
        var set = BuildFilterSet(session);
        var filters = ear == EarChannel.Left ? set.Left : set.Right;
        double channelTrim = ear == EarChannel.Left ? set.LeftTrimDb : set.RightTrimDb;
        double fixedGain = channelTrim + (includePreamp ? set.AppliedPreampDb : 0.0);

        var frequencies = new SortedSet<double>();
        int count = Math.Max(32, pointCount);
        double logMin = Math.Log(30.0);
        double logMax = Math.Log(18000.0);
        for (int i = 0; i < count; i++)
            frequencies.Add(Math.Exp(logMin + ((logMax - logMin) * i / (count - 1.0))));
        foreach (var band in Bands)
            if (band >= 30.0 && band <= 18000.0) frequencies.Add(band);

        var result = new List<(double Frequency, double GainDb)>(frequencies.Count);
        foreach (double frequency in frequencies)
        {
            double totalDb = fixedGain;
            foreach (var filter in filters)
                totalDb += PeakingMagnitudeDb(filter, frequency, 48000);
            result.Add((frequency, totalDb));
        }
        return result;
    }

    public static IEnumerable<string> ToEqualizerApoLines(IReadOnlyList<ParametricEqFilter> filters)
        => filters.Where(f => Math.Abs(f.GainDb) >= 0.01).Select(f =>
            $"Filter: ON PK Fc {f.FrequencyHz.ToString("0.##", CultureInfo.InvariantCulture)} Hz Gain {f.GainDb.ToString("0.00", CultureInfo.InvariantCulture)} dB Q {f.Q.ToString("0.00", CultureInfo.InvariantCulture)}");

    private static IReadOnlyList<ParametricEqFilter> BuildRawFilters(HearingSession session, CorrectionPreset preset, EarChannel ear)
    {
        // DSP uses Fine Tuning only when the active correction preset has FineTuneEnabled=true.
        // The correction curve represents the DESIRED total frequency response.  It must not be
        // copied 1:1 into the gain of overlapping PK filters; doing that makes adjacent filters
        // add together and can create a much larger real boost than the curve shows.  Instead we
        // fit the PK cascade so that its COMPOSITE response matches the requested curve.
        var curve = CorrectionPreviewService.Create(session, preset, ear, includeFineTune: preset.FineTuneEnabled);
        if (curve.Count == 0) return [];

        var targetGains = Bands
            .Select(f => Math.Clamp(InterpolateLog(curve, f), -12.0, 12.0))
            .ToArray();

        return FitParametricFilters(targetGains, 48000);
    }

    internal static IReadOnlyList<ParametricEqFilter> FitParametricFilters(double[] targetGains, int sampleRate)
    {
        var gains = new double[Bands.Length];
        var qValues = Enumerable.Range(0, Bands.Length).Select(GetBandQ).ToArray();

        // Gauss-Seidel style residual fitting at the band centres.  With spacing-aware Q values
        // this converges quickly and, unlike the old direct mapping, explicitly compensates for
        // the overlap between neighbouring filters.  Eight passes are sufficient for sub-0.1 dB
        // error on normal AudioTune curves while keeping the implementation deterministic.
        const double damping = 0.70;
        const int passes = 8;
        for (int pass = 0; pass < passes; pass++)
        {
            for (int i = 0; i < Bands.Length; i++)
            {
                double currentDb = 0.0;
                for (int j = 0; j < Bands.Length; j++)
                {
                    if (Math.Abs(gains[j]) < 0.000001) continue;
                    currentDb += PeakingMagnitudeDb(
                        new ParametricEqFilter(Bands[j], gains[j], qValues[j]),
                        Bands[i],
                        sampleRate);
                }

                double errorDb = targetGains[i] - currentDb;
                gains[i] = Math.Clamp(gains[i] + (errorDb * damping), -12.0, 12.0);
            }
        }

        return Enumerable.Range(0, Bands.Length)
            .Select(i => new ParametricEqFilter(Bands[i], gains[i], qValues[i]))
            .ToList();
    }

    internal static double GetBandQ(int index)
    {
        // The 14/16/18 kHz centres are so tightly packed that spacing-derived Q values around 8
        // create audible/visible peaks and valleys between otherwise similar adjacent targets.
        // Broaden only this final treble cluster. Keep 12.5 kHz spacing-derived so a genuine
        // transition into the top octave is not flattened into the rest of the response.
        if (Bands[index] >= 14000.0)
            return 1.50;

        // Use the logarithmic spacing of neighbouring centres as the effective bandwidth.
        // Wide octave-spaced bands therefore use about Q=1.4. The 12.5 kHz transition remains
        // narrower, while the final treble cluster above is deliberately smoothed.
        double bandwidthOctaves;
        if (index <= 0)
        {
            bandwidthOctaves = Math.Log2(Bands[1] / Bands[0]);
        }
        else if (index >= Bands.Length - 1)
        {
            bandwidthOctaves = Math.Log2(Bands[^1] / Bands[^2]);
        }
        else
        {
            double left = Math.Log2(Bands[index] / Bands[index - 1]);
            double right = Math.Log2(Bands[index + 1] / Bands[index]);
            bandwidthOctaves = (left + right) / 2.0;
        }

        double ratio = Math.Pow(2.0, Math.Max(0.01, bandwidthOctaves));
        double q = Math.Sqrt(ratio) / Math.Max(0.0001, ratio - 1.0);
        return Math.Clamp(q, 0.70, 10.0);
    }

    private static IReadOnlyList<ParametricEqFilter> ScalePositiveGains(IReadOnlyList<ParametricEqFilter> source, double scale)
        => source.Select(f => f.GainDb > 0.0 ? f with { GainDb = f.GainDb * scale } : f).ToList();

    private static double FindPositiveGainScale(
        IReadOnlyList<ParametricEqFilter> left,
        IReadOnlyList<ParametricEqFilter> right,
        double targetPeakDb,
        int sampleRate)
    {
        if (targetPeakDb <= 0.0) return 0.0;

        double low = 0.0;
        double high = 1.0;
        for (int i = 0; i < 30; i++)
        {
            double mid = (low + high) / 2.0;
            var scaledLeft = ScalePositiveGains(left, mid);
            var scaledRight = ScalePositiveGains(right, mid);
            double peak = CalculateCompositePeakDb(scaledLeft, scaledRight, sampleRate);
            if (peak <= targetPeakDb) low = mid;
            else high = mid;
        }
        return low;
    }

    private static double CalculateCompositePeakDb(
        IReadOnlyList<ParametricEqFilter> left,
        IReadOnlyList<ParametricEqFilter> right,
        int sampleRate)
        => Math.Max(CalculateCompositePeakDb(left, sampleRate), CalculateCompositePeakDb(right, sampleRate));

    private static double CalculateCompositePeakDb(IReadOnlyList<ParametricEqFilter> filters, int sampleRate)
    {
        if (filters.Count == 0) return 0.0;
        double peak = 0.0;
        const int steps = 2048;
        double logMin = Math.Log(20.0);
        double logMax = Math.Log(Math.Min(20000.0, sampleRate * 0.47));
        for (int i = 0; i < steps; i++)
        {
            double f = Math.Exp(logMin + ((logMax - logMin) * i / (steps - 1.0)));
            double totalDb = 0.0;
            foreach (var filter in filters)
                totalDb += PeakingMagnitudeDb(filter, f, sampleRate);
            if (totalDb > peak) peak = totalDb;
        }
        return peak;
    }

    internal static double PeakingMagnitudeDb(ParametricEqFilter filter, double frequency, int sampleRate)
    {
        // RBJ Audio EQ Cookbook peaking-EQ response. This is the same filter family used by
        // NAudio BiQuadFilter.PeakingEQ and Equalizer APO's PK filters.
        double a = Math.Pow(10.0, filter.GainDb / 40.0);
        double w0 = 2.0 * Math.PI * filter.FrequencyHz / sampleRate;
        double alpha = Math.Sin(w0) / (2.0 * filter.Q);
        double cos = Math.Cos(w0);

        double b0 = 1.0 + (alpha * a);
        double b1 = -2.0 * cos;
        double b2 = 1.0 - (alpha * a);
        double a0 = 1.0 + (alpha / a);
        double a1 = -2.0 * cos;
        double a2 = 1.0 - (alpha / a);

        double w = 2.0 * Math.PI * frequency / sampleRate;
        var z1r = Math.Cos(-w); var z1i = Math.Sin(-w);
        var z2r = Math.Cos(-2 * w); var z2i = Math.Sin(-2 * w);
        double nr = b0 + (b1 * z1r) + (b2 * z2r);
        double ni = (b1 * z1i) + (b2 * z2i);
        double dr = a0 + (a1 * z1r) + (a2 * z2r);
        double di = (a1 * z1i) + (a2 * z2i);
        double mag2 = (nr * nr + ni * ni) / Math.Max(1e-18, dr * dr + di * di);
        return 10.0 * Math.Log10(Math.Max(1e-18, mag2));
    }

    private static double InterpolateLog(IReadOnlyList<(double Frequency, double GainDb)> points, double frequency)
    {
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

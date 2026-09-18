namespace AudioTune.Services;

/// <summary>
/// Population reference shape for the threshold of hearing. The tabulated values through
/// 12.5 kHz follow the ISO 226:2023 T_f reference-threshold data. AudioTune only uses the
/// relative shape, never the absolute SPL value. Above 12.5 kHz the standard does not define
/// data; AudioTune holds the last population anchor and progressively reduces model trust.
/// </summary>
public static class HumanSensitivityModel
{
    private static readonly double[] Frequencies =
    [20, 25, 31.5, 40, 50, 63, 80, 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150, 4000, 5000, 6300, 8000, 10000, 12500];

    private static readonly double[] ThresholdDb =
    [78.5, 68.7, 59.5, 51.1, 44.0, 37.5, 31.5, 26.5, 22.1, 17.9, 14.4, 11.4, 8.6, 6.2, 4.4, 3.0, 2.2, 2.4, 3.5, 1.7, -1.3, -4.2, -6.0, -5.4, -1.5, 6.0, 12.6, 13.9, 12.3];

    private static readonly double Reference1Khz = ThresholdDb[17];

    public static double GetRelativeThresholdDb(double frequencyHz)
    {
        var value = InterpolateLog(frequencyHz);
        return value - Reference1Khz;
    }

    public static double GetPopulationTrust(double frequencyHz)
    {
        // ISO 226 is a population/free-field reference, while AudioTune measures headphones
        // end-to-end at the ear. Be deliberately conservative at the extremes where coupling
        // and inter-person variation are largest; Fine Tuning can replace that uncertainty
        // with the user's own moderate-level data.
        if (frequencyHz <= 30) return 0.55;
        if (frequencyHz < 80)
        {
            var lowFrequencyBlend = (Math.Log(frequencyHz) - Math.Log(30.0)) / (Math.Log(80.0) - Math.Log(30.0));
            return 0.55 + (0.45 * Math.Clamp(lowFrequencyBlend, 0.0, 1.0));
        }
        if (frequencyHz <= 10000) return 1.0;
        if (frequencyHz <= 12500) return 0.8;
        if (frequencyHz >= 18000) return 0.15;
        var highFrequencyBlend = (frequencyHz - 12500.0) / (18000.0 - 12500.0);
        return 0.8 + ((0.15 - 0.8) * highFrequencyBlend);
    }

    private static double InterpolateLog(double frequencyHz)
    {
        if (frequencyHz <= Frequencies[0]) return ThresholdDb[0];
        if (frequencyHz >= Frequencies[^1]) return ThresholdDb[^1];

        for (var i = 0; i < Frequencies.Length - 1; i++)
        {
            var a = Frequencies[i];
            var b = Frequencies[i + 1];
            if (frequencyHz < a || frequencyHz > b) continue;
            var t = (Math.Log(frequencyHz) - Math.Log(a)) / (Math.Log(b) - Math.Log(a));
            return ThresholdDb[i] + ((ThresholdDb[i + 1] - ThresholdDb[i]) * t);
        }
        return ThresholdDb[^1];
    }
}

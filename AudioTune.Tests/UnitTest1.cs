using AudioTune.Models;
using AudioTune.Services;

namespace AudioTune.Tests;

public sealed class CorrectionPreviewServiceTests
{
    [Theory]
    [InlineData(80.0, 6.0, 12.0)]
    [InlineData(-80.0, -6.0, -12.0)]
    public void HearingModelAndFineTuneShareTwelveDbWindow(
        double residualDb,
        double fineTuneDb,
        double expectedDb)
    {
        var session = CreateSession(residualDb);
        var preset = CreatePreset(session, fineTuneDb, strengthPercent: 100.0);

        var curve = CorrectionPreviewService.CreateBeforeStereoPreservation(
            session,
            preset,
            EarChannel.Left,
            includeFineTune: true);

        var target = Assert.Single(curve, point => Math.Abs(point.Frequency - 2000.0) < 0.01);
        Assert.Equal(expectedDb, target.GainDb, precision: 6);
        Assert.All(curve, point => Assert.InRange(
            point.GainDb,
            -CorrectionPreviewService.MaximumCombinedGainDb,
            CorrectionPreviewService.MaximumCombinedGainDb));
    }

    [Theory]
    [InlineData(100.0)]
    [InlineData(200.0)]
    public void CombinedCorrectionNeverExceedsTwelveDb(double strengthPercent)
    {
        var session = CreateSession(80.0);
        var preset = CreatePreset(session, 6.0, strengthPercent);

        var curve = CorrectionPreviewService.CreateBeforeStereoPreservation(
            session,
            preset,
            EarChannel.Left,
            includeFineTune: true);

        Assert.NotEmpty(curve);
        Assert.All(curve, point => Assert.InRange(
            point.GainDb,
            -CorrectionPreviewService.MaximumCombinedGainDb,
            CorrectionPreviewService.MaximumCombinedGainDb));
    }

    [Fact]
    public void AlgorithmVersionTracksExpandedCombinedRange()
    {
        Assert.Equal(6, CorrectionPreviewService.AlgorithmVersion);
        Assert.Equal(12.0, CorrectionPreviewService.MaximumCombinedGainDb);
    }

    private static HearingSession CreateSession(double leftResidualAtTwoKhz)
    {
        const double globalOffset = -60.0;
        double[] frequencies = [500, 1000, 2000, 4000, 8000];
        var session = new HearingSession
        {
            Name = "Synthetic clamp test",
            CompletedAt = DateTime.Now
        };

        foreach (var frequency in frequencies)
        {
            double expected = globalOffset + HumanSensitivityModel.GetRelativeThresholdDb(frequency);
            session.Measurements.Add(CreateMeasurement(
                frequency,
                EarChannel.Left,
                expected + (Math.Abs(frequency - 2000.0) < 0.01 ? leftResidualAtTwoKhz : 0.0)));
            session.Measurements.Add(CreateMeasurement(frequency, EarChannel.Right, expected));
        }

        return session;
    }

    private static HearingMeasurement CreateMeasurement(
        double frequency,
        EarChannel ear,
        double thresholdDbFs) => new()
        {
            FrequencyHz = frequency,
            Ear = ear,
            ThresholdDbFs = thresholdDbFs,
            InitialThresholdDbFs = thresholdDbFs,
            Status = HearingMeasurementStatus.Detected,
            Confidence = MeasurementConfidence.High
        };

    private static CorrectionPreset CreatePreset(
        HearingSession session,
        double fineTuneDb,
        double strengthPercent) => new()
        {
            HearingProfileId = session.Id,
            StrengthPercent = strengthPercent,
            FineTuneEnabled = true,
            FineTuneAdjustments =
            [
                new FineTuneAdjustment
                {
                    FrequencyHz = 2000.0,
                    Ear = EarChannel.Left,
                    AdjustmentDb = fineTuneDb
                }
            ]
        };
}

public sealed class DspFilterServiceTests
{
    [Fact]
    public void OnlyFinalTrebleClusterUsesBroadQ()
    {
        int twelveFiveIndex = Array.IndexOf(DspFilterService.Bands, 12500.0);
        int fourteenIndex = Array.IndexOf(DspFilterService.Bands, 14000.0);
        int sixteenIndex = Array.IndexOf(DspFilterService.Bands, 16000.0);
        int eighteenIndex = Array.IndexOf(DspFilterService.Bands, 18000.0);

        Assert.InRange(DspFilterService.GetBandQ(twelveFiveIndex), 3.4, 3.7);
        Assert.Equal(1.5, DspFilterService.GetBandQ(fourteenIndex), precision: 6);
        Assert.Equal(1.5, DspFilterService.GetBandQ(sixteenIndex), precision: 6);
        Assert.Equal(1.5, DspFilterService.GetBandQ(eighteenIndex), precision: 6);
    }

    [Theory]
    [MemberData(nameof(RepresentativeHighTrebleTargets))]
    public void FinalTrebleClusterStaysSmoothBetweenBandCentres(double[] targetGains, double maximumRippleDb)
    {
        var filters = DspFilterService.FitParametricFilters(targetGains, 48000);
        double minimum = double.PositiveInfinity;
        double maximum = double.NegativeInfinity;

        const int samples = 240;
        for (int i = 0; i <= samples; i++)
        {
            double frequency = 14000.0 + ((18000.0 - 14000.0) * i / samples);
            double response = filters.Sum(filter =>
                DspFilterService.PeakingMagnitudeDb(filter, frequency, 48000));
            minimum = Math.Min(minimum, response);
            maximum = Math.Max(maximum, response);
        }

        Assert.InRange(maximum - minimum, 0.0, maximumRippleDb);

        foreach (double frequency in new[] { 14000.0, 16000.0, 18000.0 })
        {
            int index = Array.IndexOf(DspFilterService.Bands, frequency);
            double response = filters.Sum(filter =>
                DspFilterService.PeakingMagnitudeDb(filter, frequency, 48000));
            Assert.InRange(Math.Abs(response - targetGains[index]), 0.0, 0.15);
        }
    }

    public static TheoryData<double[], double> RepresentativeHighTrebleTargets => new()
    {
        {
            [0, 0, 0, 0, 0, 0, 0, 0, 4.8, 10.8, 6.7, 6.0, 6.0],
            1.0
        },
        {
            [0, 0, 0, 0, 0, 0, 0, 0, 0.8, 7.8, 5.3, 6.0, 6.0],
            1.25
        }
    };
}

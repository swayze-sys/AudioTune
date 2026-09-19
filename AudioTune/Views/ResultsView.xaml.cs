using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AudioTune.Controls;
using AudioTune.Models;
using AudioTune.Services;

namespace AudioTune.Views;

public partial class ResultsView : UserControl
{
    public ResultsView()
    {
        InitializeComponent();
        ThresholdChart.PointInspected += (_, e) => ThresholdValueText.Text = FormatPoint(e);
        CorrectionChart.PointInspected += (_, e) => CorrectionValueText.Text = FormatPoint(e);
        Loaded += (_, _) =>
        {
            AppServices.Settings.SettingsChanged += SettingsChanged;
            AppServices.Profiles.ProfilesChanged += ProfilesChanged;
            AppServices.CorrectionPresets.PresetsChanged += PresetsChanged;
            Refresh();
            ApplyDebug();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Settings.SettingsChanged -= SettingsChanged;
            AppServices.Profiles.ProfilesChanged -= ProfilesChanged;
            AppServices.CorrectionPresets.PresetsChanged -= PresetsChanged;
        };
    }

    private void SettingsChanged() => Dispatcher.Invoke(() => { ApplyDebug(); Refresh(); });
    private void ProfilesChanged() => Dispatcher.Invoke(Refresh);
    private void PresetsChanged() => Dispatcher.Invoke(Refresh);
    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
    private void ThresholdModeChanged(object sender, RoutedEventArgs e) { if (IsLoaded) Refresh(); }
    private void CorrectionModeChanged(object sender, RoutedEventArgs e) { if (IsLoaded) Refresh(); }
    private void OpenListeningTest_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenListeningTest();
    private void OpenFineTune_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenFineTune();
    private void OpenCentering_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenStereoCentering();

    private void ApplyDebug()
    {
        bool show = AppServices.Settings.Current.DebugEnabled;
        DeveloperPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        DebugColumn.Width = show ? new GridLength(310) : new GridLength(0);
    }

    private void Refresh()
    {
        ThresholdValueText.Text = "Hover or click a point";
        CorrectionValueText.Text = "Hover or click a point";

        var session = AppServices.Profiles.LastSession;
        var preset = session is null ? null : AppServices.CorrectionPresets.GetActiveForProfile(session);
        bool real = session is not null && session.Measurements.Count > 0;
        bool completed = real && session!.CompletedAt is not null;

        IReadOnlyList<(double Frequency, double Value)> leftThreshold;
        IReadOnlyList<(double Frequency, double Value)> rightThreshold;
        IReadOnlyList<(double Frequency, double GainDb)> leftCorrection;
        IReadOnlyList<(double Frequency, double GainDb)> rightCorrection;

        if (real)
        {
            leftThreshold = session!.Measurements.Where(m => m.Ear == EarChannel.Left).OrderBy(m => m.FrequencyHz).Select(m => (m.FrequencyHz, m.ThresholdDbFs)).ToList();
            rightThreshold = session.Measurements.Where(m => m.Ear == EarChannel.Right).OrderBy(m => m.FrequencyHz).Select(m => (m.FrequencyHz, m.ThresholdDbFs)).ToList();
            bool includeFineTune = ShowFineTuneCheck.IsChecked == true;
            leftCorrection = CorrectionPreviewService.Create(session, preset!, EarChannel.Left, includeFineTune);
            rightCorrection = CorrectionPreviewService.Create(session, preset!, EarChannel.Right, includeFineTune);
            PopulateMeasuredSummary(session, preset!, leftCorrection, rightCorrection);
        }
        else
        {
            var f = HearingTestEngine.Frequencies;
            leftThreshold = f.Select((x, i) => (x, -44 - 20 * Math.Exp(-Math.Pow((Math.Log10(x) - 2.3) / 0.85, 2)) + (i > 22 ? (i - 22) * 2.2 : 0))).ToList();
            rightThreshold = f.Select((x, i) => (x, -42 - 19 * Math.Exp(-Math.Pow((Math.Log10(x) - 2.3) / 0.88, 2)) + (i > 21 ? (i - 21) * 2.5 : 0))).ToList();
            leftCorrection = leftThreshold.Select(p => (p.Frequency, Math.Clamp((p.Value + 52) * 0.18, -CorrectionPreviewService.MaximumCombinedGainDb, CorrectionPreviewService.MaximumCombinedGainDb))).ToList();
            rightCorrection = rightThreshold.Select(p => (p.Frequency, Math.Clamp((p.Value + 52) * 0.18, -CorrectionPreviewService.MaximumCombinedGainDb, CorrectionPreviewService.MaximumCombinedGainDb))).ToList();
            PopulateEmptySummary();
        }

        SetThresholdSeries(leftThreshold, rightThreshold);
        SetCorrectionSeries(session, preset, real, leftCorrection, rightCorrection);
        CorrectionBars.SetData(MergeCorrections(leftCorrection, rightCorrection));

        OpenListeningTestButton.IsEnabled = completed;
        OpenListeningTestSecondaryButton.IsEnabled = completed;
        OpenFineTuneButton.IsEnabled = completed;
        OpenCenteringButton.IsEnabled = completed;
    }

    private void SetThresholdSeries(
        IReadOnlyList<(double Frequency, double Value)> left,
        IReadOnlyList<(double Frequency, double Value)> right)
    {
        var series = new List<FrequencyChart.Series>();
        if (ThresholdBothRadio.IsChecked == true || ThresholdLeftRadio.IsChecked == true)
            series.Add(new FrequencyChart.Series("Left", left, (Brush)FindResource("BlueBrush")));
        if (ThresholdBothRadio.IsChecked == true || ThresholdRightRadio.IsChecked == true)
            series.Add(new FrequencyChart.Series("Right", right, (Brush)FindResource("RedBrush")));
        ThresholdChart.SetSeries(series.ToArray());
    }

    private void SetCorrectionSeries(
        HearingSession? session,
        CorrectionPreset? preset,
        bool real,
        IReadOnlyList<(double Frequency, double GainDb)> leftTarget,
        IReadOnlyList<(double Frequency, double GainDb)> rightTarget)
    {
        IReadOnlyList<(double Frequency, double GainDb)> left = leftTarget;
        IReadOnlyList<(double Frequency, double GainDb)> right = rightTarget;
        string leftName = "Left target";
        string rightName = "Right target";

        if (real && session is not null && preset is not null && CorrectionDspRadio.IsChecked == true)
        {
            left = DspFilterService.CalculateAppliedResponse(session, EarChannel.Left, includePreamp: true);
            right = DspFilterService.CalculateAppliedResponse(session, EarChannel.Right, includePreamp: true);
            leftName = "Left applied DSP";
            rightName = "Right applied DSP";
        }
        else if (real && session is not null && preset is not null && CorrectionPreStereoRadio.IsChecked == true)
        {
            bool includeFineTune = ShowFineTuneCheck.IsChecked == true;
            left = CorrectionPreviewService.CreateBeforeStereoPreservation(session, preset, EarChannel.Left, includeFineTune);
            right = CorrectionPreviewService.CreateBeforeStereoPreservation(session, preset, EarChannel.Right, includeFineTune);
            leftName = "Left before stereo protection";
            rightName = "Right before stereo protection";
        }

        CorrectionChart.SetSeries(
            new FrequencyChart.Series(leftName, left.Select(x => (x.Frequency, x.GainDb)).ToList(), (Brush)FindResource("BlueBrush")),
            new FrequencyChart.Series(rightName, right.Select(x => (x.Frequency, x.GainDb)).ToList(), (Brush)FindResource("RedBrush")));
        SetCorrectionChartRange(left.Select(x => x.GainDb).Concat(right.Select(x => x.GainDb)));
    }

    private void PopulateMeasuredSummary(
        HearingSession session,
        CorrectionPreset preset,
        IReadOnlyList<(double Frequency, double GainDb)> leftCorrection,
        IReadOnlyList<(double Frequency, double GainDb)> rightCorrection)
    {
        var green = (Brush)FindResource("GreenBrush");
        var amber = (Brush)FindResource("AmberBrush");
        var muted = (Brush)FindResource("MutedBrush");
        var cyan = (Brush)FindResource("CyanBrush");

        MeasurementsMetric.Text = $"{session.Measurements.Count} / {HearingTestEngine.Frequencies.Length * 2} measured";
        ResultTimestampText.Text = session.CompletedAt is not null
            ? $"Completed {session.CompletedAt:dd.MM.yyyy HH:mm}"
            : $"In progress - started {session.StartedAt:dd.MM.yyyy HH:mm}";
        PortabilityMetric.Text = AppServices.Headphones.Selected.HasVerifiedReferenceCorrection ? "Portable" : "Device-bound";

        int lowConfidence = session.Measurements.Count(m => m.Confidence == MeasurementConfidence.Low);
        int mediumConfidence = session.Measurements.Count(m => m.Confidence == MeasurementConfidence.Medium);
        ConfidenceMetric.Text = session.CompletedAt is null ? "In progress" : lowConfidence == 0 && mediumConfidence <= 6 ? "High confidence" : "Mixed confidence";
        ConfidenceMetric.Foreground = session.CompletedAt is not null && lowConfidence == 0 ? green : amber;
        ConfidenceIcon.Stroke = ConfidenceMetric.Foreground;
        ConfidenceBadge.Background = new SolidColorBrush(session.CompletedAt is not null && lowConfidence == 0 ? Color.FromRgb(14, 67, 58) : Color.FromRgb(76, 58, 25));

        var allCorrection = leftCorrection.Select(x => (x.Frequency, x.GainDb, Ear: "left"))
            .Concat(rightCorrection.Select(x => (x.Frequency, x.GainDb, Ear: "right"))).ToList();
        var strongest = allCorrection.OrderByDescending(x => x.GainDb).FirstOrDefault();
        string strongestFrequency = FormatFrequency(strongest.Frequency);
        ResultSummaryText.Text = session.CompletedAt is not null ? "Your hearing profile is complete" : "Your partial hearing profile is ready to review";
        ResultInsightText.Text = $"Largest requested correction: {strongestFrequency} ({strongest.GainDb:+0.0;-0.0;0.0} dB, {strongest.Ear}). Raw measurements remain unchanged.";
        MeaningPrimaryText.Text = strongest.GainDb >= 4.0 ? $"Strongest support is requested around {strongestFrequency}." : "Most frequencies need only gentle correction.";
        MeaningPrimaryDetail.Text = $"Current correction strength is {preset.StrengthPercent:0}%.";

        var paired = leftCorrection.Join(rightCorrection, l => l.Frequency, r => r.Frequency, (l, r) => new { l.Frequency, Difference = Math.Abs(l.GainDb - r.GainDb) }).ToList();
        double midDifference = paired.Where(x => x.Frequency >= 150 && x.Frequency <= 5000).Select(x => x.Difference).DefaultIfEmpty(0.0).Max();
        MeaningSecondaryText.Text = AppServices.Settings.Current.StereoPreservationEnabled
            ? "Stereo protection keeps the phantom center stable."
            : "Stereo protection is currently disabled.";
        MeaningSecondaryDetail.Text = $"Largest midrange L/R correction difference: {midDifference:0.0} dB.";

        ProfileStatusValue.Text = session.CompletedAt is null ? "In progress" : "Complete";
        ProfileStatusValue.Foreground = session.CompletedAt is null ? amber : green;
        ProfileStatusDetail.Text = $"{session.Measurements.Count} / {HearingTestEngine.Frequencies.Length * 2} measurements";

        int fineTuneCount = preset.FineTuneAdjustments.Count(x => FineTuneEngine.Frequencies.Any(f => Math.Abs(f - x.FrequencyHz) < 0.01));
        FineTuneStatusValue.Text = fineTuneCount == 0 ? "Optional" : preset.FineTuneEnabled ? "On" : "Off";
        FineTuneStatusValue.Foreground = fineTuneCount == 0 ? amber : preset.FineTuneEnabled ? green : muted;
        FineTuneStatusDetail.Text = fineTuneCount == 0 ? "No saved points" : $"{fineTuneCount} / {FineTuneEngine.Frequencies.Length * 2} points saved";

        bool stereoOn = AppServices.Settings.Current.StereoPreservationEnabled;
        StereoStatusValue.Text = stereoOn ? "On" : "Off";
        StereoStatusValue.Foreground = stereoOn ? green : muted;
        DebugStereoState.Text = $"Stereo protection: {(stereoOn ? "ON" : "OFF")}";
        DebugSession.Text = $"Session: {session.Id.ToString("N")[..8]}";
        DebugMeasurements.Text = $"Measurements: {session.Measurements.Count}";
        DebugPreviewStrength.Text = $"Strength: {preset.StrengthPercent:0}%";

        var filterSet = DspFilterService.BuildFilterSet(session, preset);
        TargetPeakMetric.Text = $"+{DspFilterService.CalculateTargetCurvePeakDb(session, preset):0.0} dB";
        RequiredHeadroomMetric.Text = $"{filterSet.RequiredHeadroomDb:0.0} dB";
        AppliedPreampMetric.Text = $"{filterSet.AppliedPreampDb:0.0} dB";
        StereoDifferenceMetric.Text = stereoOn ? $"<= {AppServices.Settings.Current.MaxInterauralCorrectionDifferenceDb:0.0} dB" : "Off";

        var devices = AppServices.AudioDevices.EnumerateOutputs();
        var dspDevice = devices.FirstOrDefault(d => d.Id == AppServices.Settings.Current.SelectedDspDeviceId)
                        ?? devices.FirstOrDefault(d => d.Id == AppServices.Settings.Current.SelectedOutputDeviceId);
        if (dspDevice is null)
        {
            PersistentDspStatusValue.Text = "Off";
            PersistentDspStatusValue.Foreground = muted;
            PersistentDspStatusDetail.Text = "No DSP target selected";
        }
        else
        {
            var status = AppServices.SystemDsp.GetStatus(dspDevice.Id, dspDevice.Name, session.Id, preset.StrengthPercent, DspFilterService.CreateSignature(session, preset));
            PersistentDspStatusValue.Text = status.AudioTuneApplied ? "Active" : status.LevelMatchedBypass ? "Bypass" : "Off";
            PersistentDspStatusValue.Foreground = status.AudioTuneApplied ? green : status.LevelMatchedBypass ? cyan : muted;
            PersistentDspStatusDetail.Text = status.Message;
        }
    }

    private void PopulateEmptySummary()
    {
        var muted = (Brush)FindResource("MutedBrush");
        MeasurementsMetric.Text = $"0 / {HearingTestEngine.Frequencies.Length * 2} measured";
        ConfidenceMetric.Text = "No measurement";
        ConfidenceMetric.Foreground = muted;
        ConfidenceIcon.Stroke = muted;
        ConfidenceBadge.Background = new SolidColorBrush(Color.FromRgb(23, 40, 58));
        PortabilityMetric.Text = "Device-bound";
        ResultTimestampText.Text = "Charts below contain demonstration data only";
        ResultSummaryText.Text = "Complete a hearing profile to see your result";
        ResultInsightText.Text = "AudioTune will explain the measured profile and generated correction here.";
        ProfileStatusValue.Text = "-"; ProfileStatusValue.Foreground = muted; ProfileStatusDetail.Text = "No measurement";
        FineTuneStatusValue.Text = "Optional"; FineTuneStatusDetail.Text = "No saved points";
        StereoStatusValue.Text = AppServices.Settings.Current.StereoPreservationEnabled ? "On" : "Off";
        PersistentDspStatusValue.Text = "Off"; PersistentDspStatusDetail.Text = "No active system correction";
        TargetPeakMetric.Text = "-"; RequiredHeadroomMetric.Text = "-"; AppliedPreampMetric.Text = "-"; StereoDifferenceMetric.Text = "-";
        DebugSession.Text = "Session: none"; DebugMeasurements.Text = "Measurements: 0"; DebugPreviewStrength.Text = "Strength: -";
        DebugStereoState.Text = $"Stereo protection: {(AppServices.Settings.Current.StereoPreservationEnabled ? "ON" : "OFF")}";
    }

    private void SetCorrectionChartRange(IEnumerable<double> values)
    {
        var list = values.Where(double.IsFinite).ToList();
        if (list.Count == 0) { CorrectionChart.MinimumY = -12; CorrectionChart.MaximumY = 12; return; }
        double minimum = Math.Floor(list.Min() - 1.0);
        double maximum = Math.Ceiling(list.Max() + 1.0);
        CorrectionChart.MinimumY = Math.Max(-24.0, Math.Min(-1.0, minimum));
        CorrectionChart.MaximumY = Math.Min(12.0, Math.Max(1.0, maximum));
    }

    private static IReadOnlyList<(double Frequency, double Value)> MergeCorrections(
        IReadOnlyList<(double Frequency, double GainDb)> left,
        IReadOnlyList<(double Frequency, double GainDb)> right)
        => left.Concat(right).GroupBy(x => x.Frequency).OrderBy(g => g.Key).Select(g => (g.Key, g.Average(x => x.GainDb))).ToList();

    private static string FormatFrequency(double frequency)
        => frequency >= 1000 ? $"{frequency / 1000:0.##} kHz" : $"{frequency:0} Hz";

    private static string FormatPoint(FrequencyChart.PointEventArgs e)
        => $"{e.SeriesName} - {FrequencyChart.FormatFrequencyPrecise(e.Frequency)} - {FrequencyChart.FormatValue(e.Value)} {e.Unit}";
}

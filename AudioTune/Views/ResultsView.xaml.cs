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
        LeftThresholdChart.PointInspected += (_, e) => LeftThresholdValueText.Text = FormatPoint(e);
        RightThresholdChart.PointInspected += (_, e) => RightThresholdValueText.Text = FormatPoint(e);
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

    private void SettingsChanged() => Dispatcher.Invoke(ApplyDebug);
    private void ProfilesChanged() => Dispatcher.Invoke(Refresh);
    private void PresetsChanged() => Dispatcher.Invoke(Refresh);
    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
    private void ShowFineTuneChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) Refresh();
    }
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
        LeftThresholdValueText.Text = "Hover or click a point";
        RightThresholdValueText.Text = "Hover or click a point";
        CorrectionValueText.Text = "Hover or click a point";
        var session = AppServices.Profiles.LastSession;
        var preset = session is null ? null : AppServices.CorrectionPresets.GetActiveForProfile(session);
        CorrectionStrengthMetric.Text = preset is null ? "—" : $"{preset.StrengthPercent:0}%";
        DebugPreviewStrength.Text = preset is null ? "—" : $"{preset.StrengthPercent:0}%";
        IReadOnlyList<(double Frequency, double Value)> left;
        IReadOnlyList<(double Frequency, double Value)> right;
        IReadOnlyList<(double Frequency, double GainDb)> leftCorrection;
        IReadOnlyList<(double Frequency, double GainDb)> rightCorrection;
        bool real = session is not null && session.Measurements.Count > 0;

        if (real)
        {
            left = session!.Measurements.Where(m => m.Ear == EarChannel.Left).OrderBy(m => m.FrequencyHz).Select(m => (m.FrequencyHz, m.ThresholdDbFs)).ToList();
            right = session.Measurements.Where(m => m.Ear == EarChannel.Right).OrderBy(m => m.FrequencyHz).Select(m => (m.FrequencyHz, m.ThresholdDbFs)).ToList();
            bool includeFineTune = ShowFineTuneCheck.IsChecked == true;
            leftCorrection = CorrectionPreviewService.Create(session, preset!, EarChannel.Left, includeFineTune);
            rightCorrection = CorrectionPreviewService.Create(session, preset!, EarChannel.Right, includeFineTune);
            MeasurementsMetric.Text = $"{session.Measurements.Count} / 60";
            ResultTimestampText.Text = session.CompletedAt is not null ? $"Completed {session.CompletedAt:dd.MM.yyyy HH:mm}" : $"In progress · started {session.StartedAt:dd.MM.yyyy HH:mm}";
            DataModeText.Text = session.CompletedAt is null ? "Partial measurement" : "Measured data";
            DataModeBadge.Background = new SolidColorBrush(session.CompletedAt is null ? Color.FromRgb(91, 70, 29) : Color.FromRgb(16, 79, 67));
            DebugSession.Text = session.Id.ToString("N")[..8];
            DebugMeasurements.Text = session.Measurements.Count.ToString();
        }
        else
        {
            // UI demonstration data only. This never gets persisted as a hearing result.
            var f = HearingTestEngine.Frequencies;
            left = f.Select((x, i) => (x, -44 - 20 * Math.Exp(-Math.Pow((Math.Log10(x) - 2.3) / 0.85, 2)) + (i > 22 ? (i - 22) * 2.2 : 0))).ToList();
            right = f.Select((x, i) => (x, -42 - 19 * Math.Exp(-Math.Pow((Math.Log10(x) - 2.3) / 0.88, 2)) + (i > 21 ? (i - 21) * 2.5 : 0))).ToList();
            leftCorrection = left.Select(p => (p.Frequency, Math.Clamp((p.Value + 52) * 0.18, -6, 6))).ToList();
            rightCorrection = right.Select(p => (p.Frequency, Math.Clamp((p.Value + 52) * 0.18, -6, 6))).ToList();
            MeasurementsMetric.Text = "0 / 60";
            ResultTimestampText.Text = "No measurement yet · charts below are UI demonstration data";
            DataModeText.Text = "Demo visualization";
            DebugSession.Text = "none";
            DebugMeasurements.Text = "0";
        }


        if (real && session is not null)
        {
            var devices = AppServices.AudioDevices.EnumerateOutputs();
            var dspDevice = devices.FirstOrDefault(d => d.Id == AppServices.Settings.Current.SelectedDspDeviceId)
                            ?? devices.FirstOrDefault(d => d.Id == AppServices.Settings.Current.SelectedOutputDeviceId);
            if (dspDevice is not null && preset is not null)
            {
                var status = AppServices.SystemDsp.GetStatus(dspDevice.Id, dspDevice.Name, session.Id, preset.StrengthPercent, DspFilterService.CreateSignature(session));
                CorrectionApplyStateText.Text = status.AudioTuneApplied
                    ? $"Applied to {dspDevice.Name} ✓"
                    : status.LevelMatchedBypass ? $"{dspDevice.Name} · level-matched bypass" : "Not currently applied to system audio";
                CorrectionApplyStateText.Foreground = (Brush)FindResource(status.AudioTuneApplied ? "GreenBrush" : status.LevelMatchedBypass ? "CyanBrush" : "MutedBrush");
            }
            else CorrectionApplyStateText.Text = "Not currently applied to system audio";
        }
        else CorrectionApplyStateText.Text = "No active system correction";

        LeftThresholdChart.SetSeries(new FrequencyChart.Series("Left", left, (Brush)FindResource("BlueBrush")));
        RightThresholdChart.SetSeries(new FrequencyChart.Series("Right", right, (Brush)FindResource("RedBrush")));
        var correctionSeries = new List<FrequencyChart.Series>();
        bool showActualDsp = real && session is not null && preset is not null && ShowActualDspCheck.IsChecked == true;

        if (showActualDsp)
        {
            var actualLeft = DspFilterService.CalculateAppliedResponse(session!, EarChannel.Left, includePreamp: true);
            var actualRight = DspFilterService.CalculateAppliedResponse(session!, EarChannel.Right, includePreamp: true);
            correctionSeries.Add(new FrequencyChart.Series("Left · actual APO", actualLeft.Select(x => (x.Frequency, x.GainDb)).ToList(), (Brush)FindResource("BlueBrush")));
            correctionSeries.Add(new FrequencyChart.Series("Right · actual APO", actualRight.Select(x => (x.Frequency, x.GainDb)).ToList(), (Brush)FindResource("RedBrush")));
            SetCorrectionChartRange(actualLeft.Select(x => x.GainDb).Concat(actualRight.Select(x => x.GainDb)));

            var filterSet = DspFilterService.BuildFilterSet(session!);
            CorrectionApplyStateText.Text = $"Generated AudioTune → APO transfer · preamp {filterSet.AppliedPreampDb:0.0} dB";
            CorrectionApplyStateText.Foreground = (Brush)FindResource("CyanBrush");
        }
        else
        {
            correctionSeries.Add(new FrequencyChart.Series(ShowFineTuneCheck.IsChecked == true ? "Left · Fine Tune included" : "Left", leftCorrection.Select(x => (x.Frequency, x.GainDb)).ToList(), (Brush)FindResource("BlueBrush")));
            correctionSeries.Add(new FrequencyChart.Series(ShowFineTuneCheck.IsChecked == true ? "Right · Fine Tune included" : "Right", rightCorrection.Select(x => (x.Frequency, x.GainDb)).ToList(), (Brush)FindResource("RedBrush")));
            SetCorrectionChartRange(leftCorrection.Select(x => x.GainDb).Concat(rightCorrection.Select(x => x.GainDb)));

            if (real && session is not null && preset is not null && ShowRawStereoCheck.IsChecked == true)
            {
                bool includeFineTune = ShowFineTuneCheck.IsChecked == true;
                var rawLeft = CorrectionPreviewService.CreateBeforeStereoPreservation(session, preset, EarChannel.Left, includeFineTune);
                var rawRight = CorrectionPreviewService.CreateBeforeStereoPreservation(session, preset, EarChannel.Right, includeFineTune);
                correctionSeries.Add(new FrequencyChart.Series("Pre-stereo L", rawLeft.Select(x => (x.Frequency, x.GainDb)).ToList(), (Brush)FindResource("BlueBrush"), Dashed: true));
                correctionSeries.Add(new FrequencyChart.Series("Pre-stereo R", rawRight.Select(x => (x.Frequency, x.GainDb)).ToList(), (Brush)FindResource("RedBrush"), Dashed: true));
            }
        }

        CorrectionChart.SetSeries(correctionSeries.ToArray());

        var avg = MergeCorrections(leftCorrection, rightCorrection);
        CorrectionBars.SetData(avg);
        PortabilityMetric.Text = AppServices.Headphones.Selected.HasVerifiedReferenceCorrection ? "Portable" : "Device-bound";
        bool completed = real && session?.CompletedAt is not null;
        OpenListeningTestButton.IsEnabled = completed;
        OpenFineTuneButton.IsEnabled = completed;
        OpenCenteringButton.IsEnabled = completed;
    }

    private void SetCorrectionChartRange(IEnumerable<double> values)
    {
        var list = values.Where(double.IsFinite).ToList();
        if (list.Count == 0)
        {
            CorrectionChart.MinimumY = -12;
            CorrectionChart.MaximumY = 12;
            return;
        }

        double minimum = Math.Floor(list.Min() - 1.0);
        double maximum = Math.Ceiling(list.Max() + 1.0);
        CorrectionChart.MinimumY = Math.Max(-24.0, Math.Min(-1.0, minimum));
        CorrectionChart.MaximumY = Math.Min(12.0, Math.Max(1.0, maximum));
    }

    private static IReadOnlyList<(double Frequency, double Value)> MergeCorrections(
        IReadOnlyList<(double Frequency, double GainDb)> left, IReadOnlyList<(double Frequency, double GainDb)> right)
    {
        var all = left.Concat(right).GroupBy(x => x.Frequency).OrderBy(g => g.Key)
            .Select(g => (g.Key, g.Average(x => x.GainDb))).ToList();
        return all;
    }
    private static string FormatPoint(FrequencyChart.PointEventArgs e)
        => $"{e.SeriesName} · {FrequencyChart.FormatFrequencyPrecise(e.Frequency)} · {FrequencyChart.FormatValue(e.Value)} {e.Unit}";

}

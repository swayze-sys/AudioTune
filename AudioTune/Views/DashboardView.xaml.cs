using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AudioTune.Controls;
using AudioTune.Models;
using AudioTune.Services;

namespace AudioTune.Views;

public partial class DashboardView : UserControl
{
    private bool _refreshingControls;

    public DashboardView()
    {
        InitializeComponent();
        PreviewChart.PointInspected += (_, e) =>
            PreviewValueText.Text = $"{e.SeriesName} · {FrequencyChart.FormatFrequencyPrecise(e.Frequency)} · {FrequencyChart.FormatValue(e.Value)} {e.Unit}";
        Loaded += DashboardView_Loaded;
        Unloaded += DashboardView_Unloaded;
    }

    private void DashboardView_Loaded(object sender, RoutedEventArgs e)
    {
        AppServices.Log.EntryAdded += Log_EntryAdded;
        AppServices.Settings.SettingsChanged += Settings_SettingsChanged;
        AppServices.Profiles.ProfilesChanged += Profiles_ProfilesChanged;
        AppServices.CorrectionPresets.PresetsChanged += Presets_PresetsChanged;
        AppServices.SystemDsp.DspStateChanged += SystemDsp_DspStateChanged;
        ApplyDebugSettings();
        RefreshProfilePreview();
        RefreshActivity();
        RefreshDebugText();
    }

    private void DashboardView_Unloaded(object sender, RoutedEventArgs e)
    {
        AppServices.Log.EntryAdded -= Log_EntryAdded;
        AppServices.Settings.SettingsChanged -= Settings_SettingsChanged;
        AppServices.Profiles.ProfilesChanged -= Profiles_ProfilesChanged;
        AppServices.CorrectionPresets.PresetsChanged -= Presets_PresetsChanged;
        AppServices.SystemDsp.DspStateChanged -= SystemDsp_DspStateChanged;
    }

    private void Profiles_ProfilesChanged() => Dispatcher.Invoke(RefreshProfilePreview);
    private void Presets_PresetsChanged() => Dispatcher.Invoke(RefreshProfilePreview);
    private void SystemDsp_DspStateChanged() => Dispatcher.Invoke(RefreshProfilePreview);

    private void RefreshProfilePreview()
    {
        PreviewValueText.Text = "Hover or click a point";
        var session = AppServices.Profiles.LastSession;
        var devices = AppServices.AudioDevices.EnumerateOutputs();
        var dspDevice = devices.FirstOrDefault(d =>
            string.Equals(d.Id, AppServices.Settings.Current.SelectedDspDeviceId, StringComparison.OrdinalIgnoreCase));

        _refreshingControls = true;
        try
        {
            if (session is null)
            {
                AudioProcessingToggle.IsChecked = false;
                AudioProcessingToggle.IsEnabled = false;
                AudioProcessingStatusText.Text = "No active hearing profile";
                AudioProcessingStatusText.Foreground = (Brush)FindResource("MutedBrush");
                AudioProcessingDescriptionText.Text = "Select or complete a hearing profile before enabling processing.";

                FineTuneToggle.IsChecked = false;
                FineTuneToggle.IsEnabled = false;
                FineTuneStatusText.Text = "No active preset";
                FineTuneStatusText.Foreground = (Brush)FindResource("MutedBrush");

                CalibrationReviewStatusText.Text = "No measurements";
                CalibrationReviewStatusText.Foreground = (Brush)FindResource("MutedBrush");
                CalibrationReviewDescriptionText.Text = "Complete a hearing profile to review confidence.";
                ReviewResultsButton.IsEnabled = false;

                ListeningStateText.Text = "Profile required";
                ListeningStateText.Foreground = (Brush)FindResource("AmberBrush");
                ListeningStateDot.Fill = (Brush)FindResource("AmberBrush");
                OpenAbButton.IsEnabled = false;
                StereoCenteringButton.IsEnabled = false;

                CorrectionMetaText.Text = "No active correction";
                FineTuneLegendText.Text = "Fine Tune (off)";
                PreviewChart.SetSeries();
                PreviewEmptyState.Visibility = Visibility.Visible;
                return;
            }

            var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
            bool profileComplete = session.CompletedAt is not null;
            int lowConfidence = session.Measurements.Count(m => m.Confidence == MeasurementConfidence.Low);
            int fineTuneCount = preset.FineTuneAdjustments
                .Where(x => FineTuneEngine.Frequencies.Any(f => Math.Abs(f - x.FrequencyHz) < 0.01))
                .Select(x => (x.Ear, x.FrequencyHz))
                .Distinct()
                .Count();

            SystemDspService.Status? dspStatus = null;
            if (dspDevice is not null)
            {
                dspStatus = AppServices.SystemDsp.GetStatus(
                    dspDevice.Id,
                    dspDevice.Name,
                    session.Id,
                    preset.StrengthPercent,
                    DspFilterService.CreateSignature(session, preset));
            }

            bool processingActive = dspStatus?.AudioTuneApplied == true;
            bool processingEnabled = dspStatus?.PersistentConfigured == true &&
                                     dspStatus.LevelMatchedBypass == false &&
                                     dspStatus.AudioTuneTargetsSelectedDevice;
            AudioProcessingToggle.IsChecked = processingEnabled;
            AudioProcessingToggle.IsEnabled =
                profileComplete &&
                dspDevice is not null &&
                dspStatus?.EqualizerApoDetected == true &&
                dspStatus.ApoInstalledOnSelectedDevice == true &&
                dspStatus.EnhancementsEnabled != false;

            if (processingActive)
            {
                AudioProcessingStatusText.Text = "Active on selected output";
                AudioProcessingStatusText.Foreground = (Brush)FindResource("GreenBrush");
                AudioProcessingDescriptionText.Text = "Personal correction is being applied through Equalizer APO.";
            }
            else if (processingEnabled)
            {
                AudioProcessingStatusText.Text = "On · update required";
                AudioProcessingStatusText.Foreground = (Brush)FindResource("AmberBrush");
                AudioProcessingDescriptionText.Text = "Processing is active with older settings; use the switch or Devices to refresh it.";
            }
            else if (dspStatus?.LevelMatchedBypass == true)
            {
                AudioProcessingStatusText.Text = "Off · level-matched bypass";
                AudioProcessingStatusText.Foreground = (Brush)FindResource("CyanBrush");
                AudioProcessingDescriptionText.Text = "Processing is bypassed; your profile and matched preamp remain saved.";
            }
            else if (dspDevice is null)
            {
                AudioProcessingStatusText.Text = "No DSP target selected";
                AudioProcessingStatusText.Foreground = (Brush)FindResource("AmberBrush");
                AudioProcessingDescriptionText.Text = "Choose a persistent output target on Devices before enabling processing.";
            }
            else
            {
                AudioProcessingStatusText.Text = dspStatus?.PersistentConfigured == true ? "Update required" : "Processing off";
                AudioProcessingStatusText.Foreground = (Brush)FindResource(
                    dspStatus?.PersistentConfigured == true ? "AmberBrush" : "MutedBrush");
                AudioProcessingDescriptionText.Text = dspStatus?.Message ?? "Persistent processing is not configured.";
            }

            FineTuneToggle.IsChecked = preset.FineTuneEnabled;
            FineTuneToggle.IsEnabled = profileComplete;
            FineTuneStatusText.Text = $"{(preset.FineTuneEnabled ? "Enabled" : "Disabled")} · {fineTuneCount}/{FineTuneEngine.Frequencies.Length * 2} points";
            FineTuneStatusText.Foreground = (Brush)FindResource(preset.FineTuneEnabled ? "GreenBrush" : "MutedBrush");

            CalibrationReviewStatusText.Text = lowConfidence == 0
                ? "No low-confidence points"
                : $"{lowConfidence} {(lowConfidence == 1 ? "point needs" : "points need")} review";
            CalibrationReviewStatusText.Foreground = (Brush)FindResource(lowConfidence == 0 ? "GreenBrush" : "AmberBrush");
            CalibrationReviewDescriptionText.Text = lowConfidence == 0
                ? "All completed measurements currently have usable confidence."
                : "Re-evaluate low-confidence measurements individually.";
            ReviewResultsButton.IsEnabled = session.Measurements.Count > 0;

            ListeningStateText.Text = profileComplete ? "Ready" : "Profile incomplete";
            ListeningStateText.Foreground = (Brush)FindResource(profileComplete ? "GreenBrush" : "AmberBrush");
            ListeningStateDot.Fill = (Brush)FindResource(profileComplete ? "GreenBrush" : "AmberBrush");
            OpenAbButton.IsEnabled = profileComplete;
            StereoCenteringButton.IsEnabled = profileComplete;

            FineTuneLegendText.Text = $"Fine Tune ({(preset.FineTuneEnabled ? "on" : "off")})";
            var filterSet = DspFilterService.BuildFilterSet(session, preset);
            CorrectionMetaText.Text = $"{preset.StrengthPercent:0}% · preamp {filterSet.AppliedPreampDb:+0.0;-0.0;0.0} dB";

            if (session.Measurements.Count == 0)
            {
                PreviewChart.SetSeries();
                PreviewEmptyState.Visibility = Visibility.Visible;
                return;
            }

            bool includeFineTune = preset.FineTuneEnabled;
            var left = CorrectionPreviewService.Create(session, preset, EarChannel.Left, includeFineTune);
            var right = CorrectionPreviewService.Create(session, preset, EarChannel.Right, includeFineTune);
            if (left.Count == 0 && right.Count == 0)
            {
                PreviewChart.SetSeries();
                PreviewEmptyState.Visibility = Visibility.Visible;
                return;
            }

            var previewValues = left.Select(x => x.GainDb).Concat(right.Select(x => x.GainDb)).ToList();
            double previewPeak = previewValues.Count == 0
                ? 6.0
                : Math.Max(6.0, Math.Ceiling(previewValues.Max(v => Math.Abs(v)) + 1.0));
            PreviewChart.MinimumY = -Math.Min(12.0, previewPeak);
            PreviewChart.MaximumY = Math.Min(12.0, previewPeak);
            PreviewChart.SetSeries(
                new FrequencyChart.Series(
                    includeFineTune ? "Left · Fine Tune included" : "Left",
                    left.Select(x => (x.Frequency, x.GainDb)).ToList(),
                    (Brush)FindResource("BlueBrush")),
                new FrequencyChart.Series(
                    includeFineTune ? "Right · Fine Tune included" : "Right",
                    right.Select(x => (x.Frequency, x.GainDb)).ToList(),
                    (Brush)FindResource("RedBrush")));
            PreviewEmptyState.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _refreshingControls = false;
        }
    }

    private void AudioProcessingToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_refreshingControls || !IsLoaded) return;

        var session = AppServices.Profiles.LastSession;
        var device = AppServices.AudioDevices.EnumerateOutputs().FirstOrDefault(d =>
            string.Equals(d.Id, AppServices.Settings.Current.SelectedDspDeviceId, StringComparison.OrdinalIgnoreCase));

        if (session?.CompletedAt is null || device is null)
        {
            RefreshProfilePreview();
            return;
        }

        try
        {
            var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
            if (AudioProcessingToggle.IsChecked == true)
                AppServices.SystemDsp.Apply(session, preset, device.Id, device.Name);
            else
                AppServices.SystemDsp.Disable(session, device.Id, device.Name);
        }
        catch (Exception ex)
        {
            AppServices.Log.Log($"Dashboard DSP toggle failed: {ex.Message}", LogLevel.Error);
            MessageBox.Show(
                $"Audio processing could not be changed.\n\n{ex.Message}",
                "AudioTune - Audio Processing",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            RefreshProfilePreview();
            RefreshActivity();
        }
    }

    private void FineTuneToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_refreshingControls || !IsLoaded) return;

        var session = AppServices.Profiles.LastSession;
        if (session is null)
        {
            RefreshProfilePreview();
            return;
        }

        var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
        preset.FineTuneEnabled = FineTuneToggle.IsChecked == true;
        AppServices.CorrectionPresets.Save(preset);
        AppServices.Log.Log(
            $"Fine Tune {(preset.FineTuneEnabled ? "enabled" : "disabled")} from Dashboard for preset '{preset.Name}'. Stored measurements were preserved.",
            LogLevel.Info);

        var autoApply = AppServices.SystemDsp.TryAutoApplyPreset(session, preset);
        if (autoApply.Attempted && !autoApply.Applied)
            AppServices.Log.Log($"Dashboard Fine Tune DSP update: {autoApply.Message}", LogLevel.Warning);

        RefreshProfilePreview();
        RefreshActivity();
    }

    private void ChangeHeadphone_Click(object sender, RoutedEventArgs e) => OpenDevices();
    private void ManageDsp_Click(object sender, RoutedEventArgs e) => OpenDevices();
    private void EditFineTune_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenFineTune();
    private void ReviewResults_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenResults();
    private void OpenAbTest_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenListeningTest();
    private void StereoCentering_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenStereoCentering();
    private void OpenDevices() => (Window.GetWindow(this) as MainWindow)?.OpenDevicesPage();

    private void Log_EntryAdded(LogEntry entry) => Dispatcher.Invoke(() =>
    {
        RefreshActivity();
        RefreshDebugText();
    });

    private void RefreshActivity() =>
        RecentActivity.ItemsSource = AppServices.Log.Entries.Reverse().Take(5).ToList();

    private void Settings_SettingsChanged() => Dispatcher.Invoke(() =>
    {
        ApplyDebugSettings();
        RefreshProfilePreview();
    });

    private void ApplyDebugSettings()
    {
        DebugExpander.Visibility = AppServices.Settings.Current.DebugEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;
        DebugExpander.IsExpanded = AppServices.Settings.Current.DebugExpanded;
    }

    private void RefreshDebugText()
    {
        DebugTextBox.Text = string.Join(
            Environment.NewLine,
            AppServices.Log.Entries.TakeLast(18).Select(e => $"[{e.Time:HH:mm:ss}] {e.Message}"));
        DebugTextBox.ScrollToEnd();
    }
}

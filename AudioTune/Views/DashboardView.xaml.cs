using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AudioTune.Controls;
using AudioTune.Models;
using AudioTune.Services;

namespace AudioTune.Views;

public partial class DashboardView : UserControl
{
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
        RefreshProfilePreview();
        RecentActivity.ItemsSource = AppServices.Log.Entries.Reverse().Take(7).ToList();
        AppServices.Log.EntryAdded += Log_EntryAdded;
        AppServices.Settings.SettingsChanged += Settings_SettingsChanged;
        AppServices.Profiles.ProfilesChanged += Profiles_ProfilesChanged;
        AppServices.CorrectionPresets.PresetsChanged += Presets_PresetsChanged;
        ApplyDebugSettings();
        RefreshDebugText();
    }

    private void DashboardView_Unloaded(object sender, RoutedEventArgs e)
    {
        AppServices.Log.EntryAdded -= Log_EntryAdded;
        AppServices.Settings.SettingsChanged -= Settings_SettingsChanged;
        AppServices.Profiles.ProfilesChanged -= Profiles_ProfilesChanged;
        AppServices.CorrectionPresets.PresetsChanged -= Presets_PresetsChanged;
    }

    private void Profiles_ProfilesChanged() => Dispatcher.Invoke(RefreshProfilePreview);
    private void Presets_PresetsChanged() => Dispatcher.Invoke(RefreshProfilePreview);
    private void ShowFineTuneChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) RefreshProfilePreview();
    }

    private void RefreshProfilePreview()
    {
        PreviewValueText.Text = "Hover or click a point";
        var session = AppServices.Profiles.LastSession;
        var devices = AppServices.AudioDevices.EnumerateOutputs();
        var output = devices.FirstOrDefault(d => d.Id == AppServices.Settings.Current.SelectedOutputDeviceId);
        SetupDeviceText.Text = output?.Name ?? "Windows default";
        DashboardProfileText.Text = session?.Name ?? "None";
        DashboardProfileStateText.Text = session is null ? "No profile" : $"{session.Measurements.Count}/60 · {(session.CompletedAt is null ? "incomplete" : "complete")}";

        if (session is not null)
        {
            int high = session.Measurements.Count(m => m.Confidence == MeasurementConfidence.High);
            int medium = session.Measurements.Count(m => m.Confidence == MeasurementConfidence.Medium);
            int low = session.Measurements.Count(m => m.Confidence == MeasurementConfidence.Low);
            QualityText.Text = low == 0 ? "Good" : "Review";
            QualityText.Foreground = (Brush)FindResource(low == 0 ? "GreenBrush" : "AmberBrush");
            QualityDetailText.Text = $"{high} high · {medium} medium · {low} low confidence";

            var dspDevice = devices.FirstOrDefault(d => d.Id == AppServices.Settings.Current.SelectedDspDeviceId) ?? output;
            var statusPreset = AppServices.CorrectionPresets.GetActiveForProfile(session);
            if (dspDevice is not null)
            {
                var status = AppServices.SystemDsp.GetStatus(dspDevice.Id, dspDevice.Name, session.Id, statusPreset.StrengthPercent, DspFilterService.CreateSignature(session));
                DashboardDspText.Text = status.AudioTuneApplied ? "ACTIVE" : status.LevelMatchedBypass ? "BYPASS" : status.PersistentConfigured && !status.AudioTuneProfileMatchesActiveProfile ? "UPDATE" : "OFF";
                DashboardDspText.Foreground = (Brush)FindResource(status.AudioTuneApplied ? "GreenBrush" : status.LevelMatchedBypass ? "CyanBrush" : status.PersistentConfigured ? "AmberBrush" : "MutedBrush");
                DashboardDspDetailText.Text = status.AudioTuneApplied ? $"{statusPreset.StrengthPercent:0}% · -{AppServices.SystemDsp.CalculateHeadroomDb(session):0.0} dB" : status.Message;
            }
        }
        else
        {
            QualityText.Text = "—"; QualityDetailText.Text = "No measurements";
            DashboardDspText.Text = "OFF"; DashboardDspDetailText.Text = "No active hearing profile";
        }
        if (session is null || session.Measurements.Count == 0)
        {
            PreviewChart.SetSeries();
            PreviewEmptyState.Visibility = Visibility.Visible;
            return;
        }

        var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
        bool includeFineTune = ShowFineTuneCheck.IsChecked == true;
        var left = CorrectionPreviewService.Create(session, preset, EarChannel.Left, includeFineTune);
        var right = CorrectionPreviewService.Create(session, preset, EarChannel.Right, includeFineTune);
        if (left.Count == 0 && right.Count == 0)
        {
            PreviewChart.SetSeries();
            PreviewEmptyState.Visibility = Visibility.Visible;
            return;
        }

        var previewValues = left.Select(x => x.GainDb).Concat(right.Select(x => x.GainDb)).ToList();
        double previewPeak = previewValues.Count == 0 ? 6.0 : Math.Max(6.0, Math.Ceiling(previewValues.Max(v => Math.Abs(v)) + 1.0));
        PreviewChart.MinimumY = -Math.Min(12.0, previewPeak);
        PreviewChart.MaximumY = Math.Min(12.0, previewPeak);
        PreviewChart.SetSeries(
            new FrequencyChart.Series(ShowFineTuneCheck.IsChecked == true ? "Left · Fine Tune included" : "Left", left.Select(x => (x.Frequency, x.GainDb)).ToList(), (Brush)FindResource("BlueBrush")),
            new FrequencyChart.Series(ShowFineTuneCheck.IsChecked == true ? "Right · Fine Tune included" : "Right", right.Select(x => (x.Frequency, x.GainDb)).ToList(), (Brush)FindResource("RedBrush")));
        PreviewEmptyState.Visibility = Visibility.Collapsed;
    }

    private void Log_EntryAdded(Models.LogEntry obj) => Dispatcher.Invoke(() =>
    {
        RecentActivity.ItemsSource = AppServices.Log.Entries.Reverse().Take(7).ToList();
        RefreshDebugText();
    });

    private void Settings_SettingsChanged() => Dispatcher.Invoke(ApplyDebugSettings);
    private void ApplyDebugSettings()
    {
        DebugExpander.Visibility = AppServices.Settings.Current.DebugEnabled ? Visibility.Visible : Visibility.Collapsed;
        DebugExpander.IsExpanded = AppServices.Settings.Current.DebugExpanded;
    }

    private void RefreshDebugText()
    {
        DebugTextBox.Text = string.Join(Environment.NewLine, AppServices.Log.Entries.TakeLast(18).Select(e => $"[{e.Time:HH:mm:ss}] {e.Message}"));
        DebugTextBox.ScrollToEnd();
    }
}

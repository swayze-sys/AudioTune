using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AudioTune.Controls;
using AudioTune.Services;
using Microsoft.Win32;

namespace AudioTune.Views;

public partial class ListeningTestView : UserControl
{
    private readonly DispatcherTimer _timer;

    public ListeningTestView()
    {
        InitializeComponent();
        LeftCorrectionChart.PointInspected += (_, e) =>
            LeftCorrectionValueText.Text = $"{FrequencyChart.FormatFrequencyPrecise(e.Frequency)} · {FrequencyChart.FormatValue(e.Value)} {e.Unit}";
        RightCorrectionChart.PointInspected += (_, e) =>
            RightCorrectionValueText.Text = $"{FrequencyChart.FormatFrequencyPrecise(e.Frequency)} · {FrequencyChart.FormatValue(e.Value)} {e.Unit}";
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _timer.Tick += Timer_Tick;
        Loaded += ListeningTestView_Loaded;
        Unloaded += ListeningTestView_Unloaded;
    }

    private void ListeningTestView_Loaded(object sender, RoutedEventArgs e)
    {
        AppServices.MusicPreview.StateChanged += MusicPreview_StateChanged;
        AppServices.Settings.SettingsChanged += Settings_SettingsChanged;
        AppServices.Profiles.ProfilesChanged += Profiles_ProfilesChanged;
        _timer.Start();
        RefreshCalibrationState();
        RefreshPlaybackUi();
        ApplyDebugSetting();
        Focus();
    }

    private void ListeningTestView_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        AppServices.MusicPreview.StateChanged -= MusicPreview_StateChanged;
        AppServices.Settings.SettingsChanged -= Settings_SettingsChanged;
        AppServices.Profiles.ProfilesChanged -= Profiles_ProfilesChanged;
        if (AppServices.MusicPreview.IsPlaying) AppServices.MusicPreview.Pause();
    }

    private async void Settings_SettingsChanged()
    {
        await Dispatcher.InvokeAsync(() =>
        {
            ApplyDebugSetting();
            RefreshCalibrationState();
        });
        if (AppServices.MusicPreview.IsLoaded)
        {
            try { await AppServices.MusicPreview.ReloadCalibrationAsync(); }
            catch (Exception ex) { AppServices.Log.Log($"A/B DSP settings refresh failed: {ex.Message}", Models.LogLevel.Error); }
        }
    }
    private async void Profiles_ProfilesChanged()
    {
        await Dispatcher.InvokeAsync(RefreshCalibrationState);
        if (AppServices.MusicPreview.IsLoaded)
        {
            try { await AppServices.MusicPreview.ReloadCalibrationAsync(); }
            catch (Exception ex) { AppServices.Log.Log($"A/B calibration refresh failed: {ex.Message}", Models.LogLevel.Error); }
        }
    }
    private void ApplyDebugSetting() => DebugPanel.Visibility = AppServices.Settings.Current.DebugEnabled ? Visibility.Visible : Visibility.Collapsed;
    private void ShowFineTuneChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) RefreshCalibrationState();
    }

    private void MusicPreview_StateChanged() => Dispatcher.Invoke(RefreshPlaybackUi);
    private void Timer_Tick(object? sender, EventArgs e) => RefreshPlaybackUi();

    private void RefreshCalibrationState()
    {
        var session = AppServices.Profiles.LastSession;
        var ready = session is not null && session.CompletedAt is not null &&
                    session.Measurements.Any(m => m.Ear == Models.EarChannel.Left) &&
                    session.Measurements.Any(m => m.Ear == Models.EarChannel.Right);

        CalibrationReadyText.Text = ready ? "Calibration ready" : "Complete calibration first";
        CalibrationReadyText.Foreground = ready ? new SolidColorBrush(Color.FromRgb(113, 241, 195)) : (Brush)FindResource("AmberBrush");
        CalibrationReadyBadge.Background = ready ? new SolidColorBrush(Color.FromRgb(16, 79, 67)) : new SolidColorBrush(Color.FromRgb(63, 47, 20));
        CalibratedButton.IsEnabled = ready && AppServices.MusicPreview.IsLoaded;

        if (ready && session is not null)
        {
            var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
            bool includeFineTune = ShowFineTuneCheck.IsChecked == true;
            var left = CorrectionPreviewService.Create(session, preset, Models.EarChannel.Left, includeFineTune);
            var right = CorrectionPreviewService.Create(session, preset, Models.EarChannel.Right, includeFineTune);
            var values = left.Select(x => x.GainDb).Concat(right.Select(x => x.GainDb)).ToList();
            double peak = values.Count == 0 ? 6.0 : Math.Max(6.0, Math.Ceiling(values.Max(v => Math.Abs(v)) + 1.0));
            peak = Math.Min(12.0, peak);
            LeftCorrectionChart.MinimumY = -peak;
            LeftCorrectionChart.MaximumY = peak;
            RightCorrectionChart.MinimumY = -peak;
            RightCorrectionChart.MaximumY = peak;
            LeftCorrectionChart.SetSeries(new FrequencyChart.Series(
                includeFineTune ? "Left · Fine Tune included" : "Left",
                left.Select(x => (x.Frequency, x.GainDb)).ToList(),
                (Brush)FindResource("BlueBrush")));
            RightCorrectionChart.SetSeries(new FrequencyChart.Series(
                includeFineTune ? "Right · Fine Tune included" : "Right",
                right.Select(x => (x.Frequency, x.GainDb)).ToList(),
                (Brush)FindResource("RedBrush")));
        }
        else
        {
            LeftCorrectionChart.SetSeries();
            RightCorrectionChart.SetSeries();
        }
    }

    private async void ChooseMusic_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a dynamic music track for A/B listening",
            Filter = "Audio files|*.wav;*.mp3;*.flac;*.m4a;*.aac;*.wma;*.aiff;*.aif|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            PlaybackStatusText.Text = "Loading music...";
            await AppServices.MusicPreview.LoadAsync(dialog.FileName);
            RefreshCalibrationState();
            RefreshPlaybackUi();
            AppServices.MusicPreview.Play();
        }
        catch (Exception ex)
        {
            AppServices.Log.Log($"A/B load failed: {ex.Message}", Models.LogLevel.Error);
            MessageBox.Show(ex.Message, "AudioTune - A/B Listening Test", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshPlaybackUi();
        }
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (!AppServices.MusicPreview.IsLoaded) return;
        if (AppServices.MusicPreview.IsPlaying) AppServices.MusicPreview.Pause();
        else
        {
            if (AppServices.MusicPreview.Duration > TimeSpan.Zero &&
                AppServices.MusicPreview.Position >= AppServices.MusicPreview.Duration - TimeSpan.FromMilliseconds(200))
                AppServices.MusicPreview.Restart();
            AppServices.MusicPreview.Play();
        }
        RefreshPlaybackUi();
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        AppServices.MusicPreview.Restart();
        if (!AppServices.MusicPreview.IsPlaying) AppServices.MusicPreview.Play();
        RefreshPlaybackUi();
    }

    private void Original_Click(object sender, RoutedEventArgs e) => SetMode(false);
    private void Calibrated_Click(object sender, RoutedEventArgs e) => SetMode(true);

    private void SetMode(bool calibrated)
    {
        if (calibrated && !CalibratedButton.IsEnabled) return;
        AppServices.MusicPreview.SetCalibrated(calibrated);
        RefreshPlaybackUi();
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O) { SetMode(false); e.Handled = true; }
        else if (e.Key == Key.C) { SetMode(true); e.Handled = true; }
        else if (e.Key == Key.Space)
        {
            SetMode(!AppServices.MusicPreview.IsCalibrated);
            e.Handled = true;
        }
    }

    private void RefreshPlaybackUi()
    {
        var service = AppServices.MusicPreview;
        var loaded = service.IsLoaded;

        FileNameText.Text = loaded ? service.FileName ?? "Loaded music" : "No music loaded";
        PlayPauseButton.IsEnabled = loaded;
        RestartButton.IsEnabled = loaded;
        CalibratedButton.IsEnabled = loaded && AppServices.Profiles.LastSession?.CompletedAt is not null;
        PlayPauseButton.Content = service.IsPlaying ? "Ⅱ  Pause" : "▶  Play";
        PlaybackStatusText.Text = !loaded ? "Select a music file to begin" : service.IsPlaying ? "Playing · switch A/B at any time" : "Paused · A/B state is preserved";

        var durationSeconds = Math.Max(0.001, service.Duration.TotalSeconds);
        PlaybackProgress.Value = loaded ? Math.Clamp(service.Position.TotalSeconds / durationSeconds, 0, 1) : 0;
        PositionText.Text = FormatTime(service.Position);
        DurationText.Text = FormatTime(service.Duration);
        FormatText.Text = loaded
            ? $"{service.SampleRate:N0} Hz · {service.Channels} channels · {Path.GetExtension(service.FilePath ?? string.Empty).TrimStart('.').ToUpperInvariant()}"
            : "MP3, WAV, FLAC, AAC/M4A and other Media Foundation formats";

        var calibrated = service.IsCalibrated;
        OriginalButton.Tag = calibrated ? null : "Selected";
        CalibratedButton.Tag = calibrated ? "Selected" : null;
        CurrentModeText.Text = calibrated ? "Calibrated" : "Original";
        CurrentModeText.Foreground = calibrated ? (Brush)FindResource("CyanBrush") : (Brush)FindResource("TextBrush");
        HeadroomText.Text = loaded ? $"{service.HeadroomDb:0.0} dB both paths" : "—";

        DebugFormatText.Text = loaded ? $"{service.SampleRate:N0} Hz / stereo float" : "—";
        var device = AppServices.AudioDevices.EnumerateOutputs().FirstOrDefault(d => d.Id == AppServices.Settings.Current.SelectedOutputDeviceId);
        DebugOutputText.Text = device?.Name ?? "Windows default";
        DebugDspText.Text = calibrated ? "Calibration active" : "Bypass / original";
        DebugDspText.Foreground = calibrated ? (Brush)FindResource("GreenBrush") : (Brush)FindResource("MutedBrush");
    }

    private static string FormatTime(TimeSpan value)
    {
        if (value.TotalHours >= 1) return value.ToString(@"h\:mm\:ss");
        return value.ToString(@"m\:ss");
    }
}

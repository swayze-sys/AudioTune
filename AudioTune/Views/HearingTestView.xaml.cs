using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AudioTune.Models;
using AudioTune.Services;

namespace AudioTune.Views;

public partial class HearingTestView : UserControl
{
    private bool _awaitingResponse;
    private bool _transitioning;
    private bool _paused;
    private bool _isLoaded;

    public HearingTestView()
    {
        InitializeComponent();
        Loaded += HearingTestView_Loaded;
        Unloaded += HearingTestView_Unloaded;
    }

    private async void HearingTestView_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        AppServices.HearingTest.StateChanged += HearingTest_StateChanged;
        AppServices.Log.EntryAdded += Log_EntryAdded;
        AppServices.Settings.SettingsChanged += Settings_SettingsChanged;

        if (!AppServices.HearingTest.HasStarted)
            AppServices.HearingTest.Start();

        RefreshAll();
        RefreshLog();
        ApplyDebugSettings();
        Focus();

        if (!AppServices.HearingTest.IsComplete)
            await StartCurrentToneAsync();
        else
            ShowCompletedState();
    }

    private async void HearingTestView_Unloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        AppServices.HearingTest.StateChanged -= HearingTest_StateChanged;
        AppServices.Log.EntryAdded -= Log_EntryAdded;
        AppServices.Settings.SettingsChanged -= Settings_SettingsChanged;
        _awaitingResponse = false;
        try { await AppServices.TonePlayer.StopAsync(); } catch { }
    }

    private void HearingTest_StateChanged() => Dispatcher.Invoke(RefreshAll);
    private void Log_EntryAdded(LogEntry obj) => Dispatcher.Invoke(RefreshLog);
    private void Settings_SettingsChanged() => Dispatcher.Invoke(ApplyDebugSettings);

    private async Task StartCurrentToneAsync()
    {
        if (!_isLoaded || AppServices.HearingTest.IsComplete || _paused)
            return;

        _awaitingResponse = false;
        RefreshAll();
        ToneStateText.Text = "Starting repeating pulsed tone…";
        ToneStateText.Foreground = (Brush)FindResource("MutedBrush");

        try
        {
            await AppServices.TonePlayer.StartRepeatingAsync(
                AppServices.HearingTest.CurrentFrequency,
                AppServices.HearingTest.CurrentLevelDbFs,
                AppServices.HearingTest.Ear);

            if (!_isLoaded) return;
            _awaitingResponse = true;
            ToneStateText.Text = "Tone repeats until you answer";
            ToneStateText.Foreground = (Brush)FindResource("CyanBrush");
            RefreshAll();
        }
        catch (Exception ex)
        {
            _awaitingResponse = false;
            ToneStateText.Text = "Audio output failed";
            ToneStateText.Foreground = (Brush)FindResource("RedBrush");
            AppServices.Log.Log($"Audio error: {ex.Message}", LogLevel.Error);
            MessageBox.Show($"The test tone could not be played.\n\n{ex.Message}", "AudioTune", MessageBoxButton.OK, MessageBoxImage.Error);
            RefreshAll();
        }
    }

    private async Task SubmitResponseAsync(bool heard)
    {
        if (!_awaitingResponse || _transitioning || _paused)
            return;

        _transitioning = true;
        _awaitingResponse = false;
        ToneStateText.Text = heard ? "Heard — advancing…" : "Not heard — advancing…";
        ToneStateText.Foreground = (Brush)FindResource("MutedBrush");
        RefreshAll();

        try
        {
            await AppServices.TonePlayer.StopAsync();
            AppServices.HearingTest.RegisterResponse(heard);

            if (AppServices.HearingTest.IsComplete)
            {
                ShowCompletedState();
                return;
            }

            if (_isLoaded)
                await StartCurrentToneAsync();
        }
        finally
        {
            _transitioning = false;
            RefreshAll();
        }
    }

    private async void Heard_Click(object sender, RoutedEventArgs e) => await SubmitResponseAsync(true);
    private async void NotHeard_Click(object sender, RoutedEventArgs e) => await SubmitResponseAsync(false);

    private async void PauseResume_Click(object sender, RoutedEventArgs e)
    {
        if (_transitioning) return;

        if (_paused)
        {
            _paused = false;
            PauseResumeButton.Content = "Pause tone";
            await StartCurrentToneAsync();
        }
        else
        {
            _paused = true;
            _awaitingResponse = false;
            await AppServices.TonePlayer.StopAsync();
            PauseResumeButton.Content = "Resume tone";
            ToneStateText.Text = "Test paused";
            ToneStateText.Foreground = (Brush)FindResource("MutedBrush");
            RefreshAll();
        }
    }

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Start a new hearing profile? The current profile is already saved and will remain available under Profiles.", "AudioTune",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        _transitioning = true;
        _awaitingResponse = false;
        _paused = false;
        PauseResumeButton.Content = "Pause tone";
        try
        {
            await AppServices.TonePlayer.StopAsync();
            AppServices.HearingTest.Start();
            RefreshAll();
            await StartCurrentToneAsync();
        }
        finally
        {
            _transitioning = false;
            RefreshAll();
        }
    }

    private async void Skip_Click(object sender, RoutedEventArgs e)
    {
        if (_transitioning || AppServices.HearingTest.IsComplete) return;

        _transitioning = true;
        _awaitingResponse = false;
        try
        {
            await AppServices.TonePlayer.StopAsync();
            AppServices.Log.Log($"Skipped {AppServices.HearingTest.Ear} {AppServices.HearingTest.CurrentFrequency:0.##} Hz", LogLevel.Warning);
            AppServices.HearingTest.Skip();
            if (_isLoaded && !AppServices.HearingTest.IsComplete && !_paused)
                await StartCurrentToneAsync();
        }
        finally
        {
            _transitioning = false;
            RefreshAll();
        }
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => AppServices.Log.Clear();
    private void FineTune_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenFineTune();

    private void ShowCompletedState()
    {
        _awaitingResponse = false;
        ToneStateText.Text = "Calibration saved — retest points below or use optional Fine Tuning";
        ToneStateText.Foreground = (Brush)FindResource("GreenBrush");
        PauseResumeButton.Content = "Pause tone";
        FineTuneButton.Visibility = Visibility.Visible;
        RefreshAll();
    }

    private void RefreshAll()
    {
        var engine = AppServices.HearingTest;
        CurrentFrequencyText.Text = engine.CurrentFrequency >= 1000
            ? $"{engine.CurrentFrequency / 1000:0.##} kHz"
            : $"{engine.CurrentFrequency:0} Hz";
        CurrentLevelText.Text = $"{engine.CurrentLevelDbFs:0.0} dBFS";
        ParamLevel.Text = CurrentLevelText.Text;
        ParamAppVolume.Text = $"{TonePlaybackService.ReferenceSessionVolumePercent}% session";
        ParamMasterVolume.Text = engine.Session.EndpointMasterVolumePercentAtStart.HasValue
            ? $"{engine.Session.EndpointMasterVolumePercentAtStart.Value:0}% (unchanged)"
            : "unchanged";
        ParamChannel.Text = engine.Ear == EarChannel.Left ? "Left Ear (L)" : "Right Ear (R)";
        ParamMethod.Text = engine.CurrentSearchPhase;

        LeftEarPill.Background = (Brush)FindResource(engine.Ear == EarChannel.Left ? "BlueBrush" : "PanelBrush");
        RightEarPill.Background = (Brush)FindResource(engine.Ear == EarChannel.Right ? "BlueBrush" : "PanelBrush");

        var left = engine.Session.Measurements.Count(m => m.Ear == EarChannel.Left);
        var right = engine.Session.Measurements.Count(m => m.Ear == EarChannel.Right);
        LeftProgress.Value = left;
        RightProgress.Value = right;
        LeftProgressText.Text = $"Left Ear     {left} / {HearingTestEngine.Frequencies.Length}";
        RightProgressText.Text = $"Right Ear    {right} / {HearingTestEngine.Frequencies.Length}";
        TotalProgressText.Text = $"Total        {left + right} / {HearingTestEngine.Frequencies.Length * 2}";
        ProgressSummary.Text = engine.IsAutomaticVerification
            ? $"Verifying unusual point · {engine.VerificationRemaining} remaining"
            : $"{left + right} / {HearingTestEngine.Frequencies.Length * 2} complete";
        BuildFrequencyProgress();

        HeardButton.IsEnabled = _awaitingResponse && !_transitioning && !_paused && !engine.IsComplete;
        NotHeardButton.IsEnabled = _awaitingResponse && !_transitioning && !_paused && !engine.IsComplete;
        PauseResumeButton.IsEnabled = !engine.IsComplete && !_transitioning;
        FineTuneButton.Visibility = engine.IsComplete ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BuildFrequencyProgress()
    {
        FrequencyProgressPanel.Children.Clear();
        var engine = AppServices.HearingTest;
        var measurements = engine.Session.Measurements
            .Where(m => m.Ear == engine.Ear)
            .GroupBy(m => m.FrequencyHz)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.Timestamp).First());

        foreach (var f in HearingTestEngine.Frequencies)
        {
            var isCurrent = Math.Abs(f - engine.CurrentFrequency) < 0.01 && !engine.IsComplete;
            measurements.TryGetValue(f, out var measurement);
            bool completed = measurement is not null;
            bool ceiling = measurement?.Status == HearingMeasurementStatus.NotDetectedAtCeiling;
            bool flagged = measurement?.WasAutomaticallyFlagged == true && measurement?.VerificationCompleted != true;
            bool verified = measurement?.VerificationCompleted == true;

            var stack = new StackPanel { Width = 42, Margin = new Thickness(0, 4, 0, 0) };
            var dot = new Ellipse
            {
                Width = isCurrent ? 15 : 9,
                Height = isCurrent ? 15 : 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Fill = ceiling
                    ? (Brush)FindResource("AmberBrush")
                    : flagged
                        ? (Brush)FindResource("AmberBrush")
                        : completed
                            ? (Brush)FindResource("GreenBrush")
                            : isCurrent
                                ? (Brush)FindResource("BlueBrush")
                                : new SolidColorBrush(Color.FromRgb(76, 95, 116)),
                Stroke = isCurrent ? (Brush)FindResource("CyanBrush") : null,
                StrokeThickness = isCurrent ? 2 : 0
            };
            var label = new TextBlock
            {
                Text = f >= 1000 ? $"{f / 1000:0.#}k" : $"{f:0}",
                FontSize = 9,
                Foreground = (Brush)FindResource("MutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            stack.Children.Add(dot);
            stack.Children.Add(label);

            var button = new Button
            {
                Tag = f,
                Content = stack,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = engine.CanEditMeasurements && completed ? Cursors.Hand : Cursors.Arrow,
                IsEnabled = engine.CanEditMeasurements && completed,
                ToolTip = completed
                    ? ceiling
                        ? $"{f:0.##} Hz · not detected at test ceiling · click to retest"
                        : $"{f:0.##} Hz · {measurement!.ThresholdDbFs:0.0} dBFS · confidence {measurement.Confidence}" +
                          (verified ? " · verified" : flagged ? " · verification pending" : "") + " · click to retest"
                    : $"{f:0.##} Hz · not measured"
            };
            button.Click += FrequencyPoint_Click;
            FrequencyProgressPanel.Children.Add(button);
        }
    }

    private async void FrequencyPoint_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: double frequency }) return;
        if (!AppServices.HearingTest.CanEditMeasurements) return;

        _transitioning = true;
        _awaitingResponse = false;
        _paused = false;
        PauseResumeButton.Content = "Pause tone";
        try
        {
            await AppServices.TonePlayer.StopAsync();
            if (AppServices.HearingTest.BeginSingleFrequencyRetest(AppServices.HearingTest.Ear, frequency))
            {
                ToneStateText.Text = "Retesting selected frequency…";
                RefreshAll();
                await StartCurrentToneAsync();
            }
        }
        finally
        {
            _transitioning = false;
            RefreshAll();
        }
    }

    private async void LeftEarPill_Click(object sender, MouseButtonEventArgs e)
    {
        if (!AppServices.HearingTest.IsComplete) return;
        await AppServices.TonePlayer.StopAsync();
        AppServices.HearingTest.SelectReviewEar(EarChannel.Left);
        RefreshAll();
    }

    private async void RightEarPill_Click(object sender, MouseButtonEventArgs e)
    {
        if (!AppServices.HearingTest.IsComplete) return;
        await AppServices.TonePlayer.StopAsync();
        AppServices.HearingTest.SelectReviewEar(EarChannel.Right);
        RefreshAll();
    }

    private void ApplyDebugSettings()
    {
        bool debug = AppServices.Settings.Current.DebugEnabled;
        ParametersCard.Visibility = debug ? Visibility.Visible : Visibility.Collapsed;
        LiveLogCard.Visibility = debug ? Visibility.Visible : Visibility.Collapsed;
        if (debug)
        {
            Grid.SetColumnSpan(ProgressCard, 1);
            Grid.SetColumn(ProgressCard, 0);
        }
        else
        {
            Grid.SetColumn(ProgressCard, 0);
            Grid.SetColumnSpan(ProgressCard, 3);
        }
    }

    private void RefreshLog()
    {
        LiveLogText.Text = string.Join(Environment.NewLine, AppServices.Log.Entries.TakeLast(18).Select(e => $"[{e.Time:HH:mm:ss}] {e.Message}"));
        LiveLogText.ScrollToEnd();
    }

    private async void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && HeardButton.IsEnabled)
        {
            await SubmitResponseAsync(true);
            e.Handled = true;
        }
        else if (e.Key == Key.N && NotHeardButton.IsEnabled)
        {
            await SubmitResponseAsync(false);
            e.Handled = true;
        }
    }
}

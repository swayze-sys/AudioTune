using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AudioTune.Models;
using AudioTune.Services;

namespace AudioTune.Views;

public partial class FineTuneView : UserControl
{
    private bool _paused;
    private bool _loaded;
    private bool _refreshingFineTuneEnabled;
    private EarChannel _progressEar = EarChannel.Left;

    public FineTuneView()
    {
        InitializeComponent();
        Loaded += FineTuneView_Loaded;
        Unloaded += FineTuneView_Unloaded;
    }

    private async void FineTuneView_Loaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        var session = AppServices.Profiles.LastSession;
        if (session?.CompletedAt is null)
        {
            MessageBox.Show("Complete a hearing profile before using Fine Tuning.", "AudioTune", MessageBoxButton.OK, MessageBoxImage.Information);
            IsEnabled = false;
            return;
        }
        var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
        _refreshingFineTuneEnabled = true;
        FineTuneEnabledCheck.IsChecked = preset.FineTuneEnabled;
        FineTuneEnabledCheck.Content = preset.FineTuneEnabled ? "Fine Tune ON" : "Fine Tune OFF";
        UpdateFineTuneEnabledDescription(preset);
        _refreshingFineTuneEnabled = false;
        AppServices.FineTune.Start(session, preset);
        _progressEar = AppServices.FineTune.Ear;
        AppServices.FineTune.StateChanged += FineTune_StateChanged;
        RefreshUi();
        await RestartToneAsync();
    }

    private async void FineTuneView_Unloaded(object sender, RoutedEventArgs e)
    {
        _loaded = false;
        AppServices.FineTune.StateChanged -= FineTune_StateChanged;
        try { await AppServices.TonePlayer.StopAsync(); } catch { }
    }

    private async void FineTune_StateChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(FineTune_StateChanged);
            return;
        }

        RefreshUi();
        if (!_paused && !AppServices.FineTune.IsComplete)
            await RestartToneAsync();
    }

    private async Task RestartToneAsync()
    {
        if (!_loaded || _paused || AppServices.FineTune.IsComplete) return;
        var levels = AppServices.FineTune.GetPlaybackLevels();
        await AppServices.TonePlayer.StartFineTuneAlternatingAsync(1000, levels.ReferenceDbFs, AppServices.FineTune.CurrentFrequency, levels.TestDbFs, AppServices.FineTune.Ear);
        LevelText.Text = $"Reference {levels.ReferenceDbFs:0.0} dBFS · Test {levels.TestDbFs:0.0} dBFS";
    }

    private void FineTuneEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (_refreshingFineTuneEnabled) return;
        var session = AppServices.Profiles.LastSession;
        if (session is null) return;
        var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
        preset.FineTuneEnabled = FineTuneEnabledCheck.IsChecked == true;
        FineTuneEnabledCheck.Content = preset.FineTuneEnabled ? "Fine Tune ON" : "Fine Tune OFF";
        AppServices.CorrectionPresets.Save(preset);
        AppServices.Log.Log($"Fine Tune {(preset.FineTuneEnabled ? "enabled" : "disabled")} for preset '{preset.Name}'.", LogLevel.Info);

        var autoApply = AppServices.SystemDsp.TryAutoApplyPreset(session, preset);
        UpdateFineTuneEnabledDescription(preset, autoApply.Message, autoApply.Applied);
    }

    private void UpdateFineTuneEnabledDescription(CorrectionPreset preset, string? dspMessage = null, bool applied = false)
    {
        string state = preset.FineTuneEnabled
            ? "Fine-tune offsets are included in A/B playback and the correction preset."
            : "Fine-tune offsets are bypassed; all measured Fine Tune values remain saved.";

        if (!string.IsNullOrWhiteSpace(dspMessage))
        {
            FineTuneEnabledDescription.Text = $"{state}  {(applied ? "DSP updated immediately:" : "DSP status:")} {dspMessage}";
            return;
        }

        FineTuneEnabledDescription.Text = AppServices.Settings.Current.AutoApplyDspChanges
            ? $"{state} Changes are auto-applied when this preset already has an active persistent DSP."
            : $"{state} Auto-apply is off; use Devices → Apply / Update DSP to update Windows.";
    }

    private void Quieter_Click(object sender, RoutedEventArgs e) => AppServices.FineTune.AdjustTestQuieter();
    private void Louder_Click(object sender, RoutedEventArgs e) => AppServices.FineTune.AdjustTestLouder();
    private void Equal_Click(object sender, RoutedEventArgs e) => AppServices.FineTune.AcceptEqual();
    private void Skip_Click(object sender, RoutedEventArgs e) => AppServices.FineTune.Skip();

    private void FineTuneLeftEar_Click(object sender, RoutedEventArgs e)
    {
        _progressEar = EarChannel.Left;
        BuildFrequencyProgress();
    }

    private void FineTuneRightEar_Click(object sender, RoutedEventArgs e)
    {
        _progressEar = EarChannel.Right;
        BuildFrequencyProgress();
    }

    private async void FrequencyPoint_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: double frequency } || AppServices.FineTune.IsSinglePointReview)
            return;

        await AppServices.TonePlayer.StopAsync();
        _paused = false;
        PauseButton.Content = "Pause tones";
        if (AppServices.FineTune.BeginSinglePointReview(_progressEar, frequency))
            _progressEar = AppServices.FineTune.Ear;
    }

    private async void Pause_Click(object sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        PauseButton.Content = _paused ? "Resume tones" : "Pause tones";
        if (_paused) await AppServices.TonePlayer.StopAsync(); else await RestartToneAsync();
    }

    private void RefreshUi()
    {
        var engine = AppServices.FineTune;
        ProgressBar.Maximum = engine.TotalCount;
        if (engine.IsSinglePointReview)
            _progressEar = engine.Ear;
        BuildFrequencyProgress();
        if (engine.IsComplete)
        {
            EarText.Text = "FINE TUNING COMPLETE";
            EarText.Foreground = (Brush)FindResource("GreenBrush");
            FrequencyText.Text = "Saved";
            AdjustmentText.Text = "The active correction preset now includes your fine-tune offsets.";
            ProgressBar.Value = engine.TotalCount;
            ProgressText.Text = $"{engine.TotalCount} / {engine.TotalCount}";
            PauseButton.IsEnabled = false;
            _ = AppServices.TonePlayer.StopAsync();
            return;
        }
        EarText.Text = (engine.Ear == EarChannel.Left ? "LEFT EAR" : "RIGHT EAR") +
                       (engine.IsSinglePointReview ? " · POINT REVIEW" : "");
        EarText.Foreground = (Brush)FindResource(engine.Ear == EarChannel.Left ? "BlueBrush" : "RedBrush");
        FrequencyText.Text = engine.CurrentFrequency >= 1000 ? $"{engine.CurrentFrequency / 1000:0.##} kHz" : $"{engine.CurrentFrequency:0} Hz";
        AdjustmentText.Text = $"Fine-tune delta {engine.CurrentAdjustmentDb:+0.0;-0.0;0.0} dB";
        ProgressBar.Value = engine.CompletedCount;
        ProgressText.Text = $"{engine.CompletedCount} / {engine.TotalCount}";
        PauseButton.IsEnabled = true;
        var levels = engine.GetPlaybackLevels();
        LevelText.Text = $"Reference {levels.ReferenceDbFs:0.0} dBFS · Test {levels.TestDbFs:0.0} dBFS";
    }

    private void BuildFrequencyProgress()
    {
        if (!IsInitialized) return;

        var engine = AppServices.FineTune;
        FrequencyProgressPanel.Children.Clear();
        FineTuneLeftEarButton.Style = (Style)FindResource(_progressEar == EarChannel.Left ? "PrimaryButtonStyle" : "FlatButtonStyle");
        FineTuneRightEarButton.Style = (Style)FindResource(_progressEar == EarChannel.Right ? "PrimaryButtonStyle" : "FlatButtonStyle");

        int savedForEar = FineTuneEngine.Frequencies.Count(f => engine.HasSavedAdjustment(_progressEar, f));
        PointProgressSummary.Text = $"{(_progressEar == EarChannel.Left ? "Left" : "Right")} ear · {savedForEar} / {FineTuneEngine.Frequencies.Length} saved";

        foreach (double frequency in FineTuneEngine.Frequencies)
        {
            double? adjustment = engine.GetSavedAdjustment(_progressEar, frequency);
            bool saved = adjustment.HasValue;
            bool active = engine.IsSinglePointReview && engine.Ear == _progressEar && Math.Abs(engine.CurrentFrequency - frequency) < 0.01;
            Brush earBrush = (Brush)FindResource(_progressEar == EarChannel.Left ? "BlueBrush" : "RedBrush");

            var stack = new StackPanel { Width = 42, Margin = new Thickness(0, 4, 0, 0) };
            var dot = new Ellipse
            {
                Width = active ? 15 : 9,
                Height = active ? 15 : 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Fill = saved ? earBrush : new SolidColorBrush(Color.FromRgb(76, 95, 116)),
                Stroke = active ? (Brush)FindResource("CyanBrush") : null,
                StrokeThickness = active ? 2 : 0
            };
            var label = new TextBlock
            {
                Text = frequency >= 1000 ? $"{frequency / 1000:0.#}k" : $"{frequency:0}",
                FontSize = 9,
                Foreground = (Brush)FindResource("MutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            stack.Children.Add(dot);
            stack.Children.Add(label);

            var button = new Button
            {
                Tag = frequency,
                Content = stack,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = saved && !engine.IsSinglePointReview ? Cursors.Hand : Cursors.Arrow,
                IsHitTestVisible = saved && !engine.IsSinglePointReview,
                Focusable = saved && !engine.IsSinglePointReview,
                ToolTip = saved
                    ? $"{frequency:0.##} Hz · {adjustment:+0.0;-0.0;0.0} dB · click to evaluate again"
                    : $"{frequency:0.##} Hz · not fine-tuned yet"
            };
            button.Click += FrequencyPoint_Click;
            FrequencyProgressPanel.Children.Add(button);
        }
    }
}

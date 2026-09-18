using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AudioTune.Models;
using AudioTune.Services;

namespace AudioTune.Views;

public partial class FineTuneView : UserControl
{
    private bool _paused;
    private bool _loaded;
    private bool _refreshingFineTuneEnabled;

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

    private async void Pause_Click(object sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        PauseButton.Content = _paused ? "Resume tones" : "Pause tones";
        if (_paused) await AppServices.TonePlayer.StopAsync(); else await RestartToneAsync();
    }

    private void RefreshUi()
    {
        var engine = AppServices.FineTune;
        if (engine.IsComplete)
        {
            EarText.Text = "FINE TUNING COMPLETE";
            EarText.Foreground = (Brush)FindResource("GreenBrush");
            FrequencyText.Text = "Saved";
            AdjustmentText.Text = "The active correction preset now includes your fine-tune offsets.";
            ProgressBar.Value = engine.TotalCount;
            ProgressText.Text = $"{engine.TotalCount} / {engine.TotalCount}";
            _ = AppServices.TonePlayer.StopAsync();
            return;
        }
        EarText.Text = engine.Ear == EarChannel.Left ? "LEFT EAR" : "RIGHT EAR";
        EarText.Foreground = (Brush)FindResource(engine.Ear == EarChannel.Left ? "BlueBrush" : "RedBrush");
        FrequencyText.Text = engine.CurrentFrequency >= 1000 ? $"{engine.CurrentFrequency / 1000:0.##} kHz" : $"{engine.CurrentFrequency:0} Hz";
        AdjustmentText.Text = $"Fine-tune delta {engine.CurrentAdjustmentDb:+0.0;-0.0;0.0} dB";
        ProgressBar.Value = engine.CompletedCount;
        ProgressText.Text = $"{engine.CompletedCount} / {engine.TotalCount}";
        var levels = engine.GetPlaybackLevels();
        LevelText.Text = $"Reference {levels.ReferenceDbFs:0.0} dBFS · Test {levels.TestDbFs:0.0} dBFS";
    }
}

using System.Windows;
using System.Windows.Controls;
using AudioTune.Services;
using AudioTune.Models;

namespace AudioTune.Views;

public partial class StereoCenteringView : UserControl
{
    private bool _loading;
    private HearingSession? _session;
    private CorrectionPreset? _preset;

    public StereoCenteringView()
    {
        InitializeComponent();
        Loaded += StereoCenteringView_Loaded;
        Unloaded += StereoCenteringView_Unloaded;
    }

    private async void StereoCenteringView_Loaded(object sender, RoutedEventArgs e)
    {
        _session = AppServices.Profiles.LastSession;
        if (_session?.CompletedAt is null || _session.Measurements.Count == 0)
        {
            StatusText.Text = "Complete a hearing calibration before running Stereo Centering.";
            BalanceSlider.IsEnabled = false;
            return;
        }

        _preset = AppServices.CorrectionPresets.GetActiveForProfile(_session);
        _loading = true;
        BalanceSlider.Value = Math.Clamp(_preset.StereoCenterBalanceDb, -3.0, 3.0);
        _loading = false;
        RefreshBalanceText();

        try
        {
            await AppServices.TonePlayer.StartStereoCenteringAsync(_session, BalanceSlider.Value);
            StatusText.Text = "Corrected band-limited noise is playing. Adjust until the image is exactly centered.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not start centering signal: {ex.Message}";
        }
    }

    private async void StereoCenteringView_Unloaded(object sender, RoutedEventArgs e)
    {
        await AppServices.TonePlayer.StopAsync();
    }

    private void BalanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BalanceValueText is null || ChannelTrimText is null) return;
        RefreshBalanceText();
        if (!_loading) AppServices.TonePlayer.SetStereoCenterBalance(e.NewValue);
    }

    private void RefreshBalanceText()
    {
        if (BalanceValueText is null || ChannelTrimText is null || BalanceSlider is null) return;
        double value = BalanceSlider.Value;
        BalanceValueText.Text = Math.Abs(value) < 0.001
            ? "Centered · 0.00 dB"
            : value > 0 ? $"Move right · +{value:0.00} dB" : $"Move left · {value:0.00} dB";
        var (leftTrim, rightTrim) = DspFilterService.GetStereoCenterTrims(value);
        ChannelTrimText.Text = $"Post-EQ trim: Left {leftTrim:0.00} dB · Right {rightTrim:0.00} dB";
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => BalanceSlider.Value = 0.0;

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null || _preset is null) return;
        _preset.StereoCenterBalanceDb = BalanceSlider.Value;
        _preset.StereoCenteringEnabled = true;
        _preset.StereoCenteringUpdatedAt = DateTime.Now;
        AppServices.CorrectionPresets.Save(_preset);
        var autoApply = AppServices.SystemDsp.TryAutoApplyPreset(_session, _preset);
        StatusText.Text = autoApply.Applied
            ? $"Saved to correction preset '{_preset.Name}' and applied immediately to the persistent DSP."
            : $"Saved to correction preset '{_preset.Name}'. {autoApply.Message}";
        try { await AppServices.MusicPreview.ReloadCalibrationAsync(); } catch { }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenResults();
}

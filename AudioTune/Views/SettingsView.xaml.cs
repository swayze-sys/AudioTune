using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AudioTune.Services;

namespace AudioTune.Views;

public partial class SettingsView : UserControl
{
    private bool _loading;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += SettingsView_Loaded;
    }

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        _loading = true;
        DebugEnabledCheck.IsChecked = AppServices.Settings.Current.DebugEnabled;
        DebugExpandedCheck.IsChecked = AppServices.Settings.Current.DebugExpanded;
        RawModeCheck.IsChecked = AppServices.Settings.Current.RawWasapiMode;
        LatencySlider.Value = AppServices.Settings.Current.OutputLatencyMs;
        LatencyText.Text = $"{AppServices.Settings.Current.OutputLatencyMs} ms";
        AutomaticPreampCheck.IsChecked = AppServices.Settings.Current.UseAutomaticPreamp;
        ManualPreampSlider.Value = Math.Clamp(AppServices.Settings.Current.ManualPreampDb, -12.0, 0.0);
        ManualPreampText.Text = $"{ManualPreampSlider.Value:0.0} dB";
        LimitBoostsCheck.IsChecked = AppServices.Settings.Current.LimitPositiveBoostsToPreamp;
        StereoPreservationCheck.IsChecked = AppServices.Settings.Current.StereoPreservationEnabled;
        StereoDifferenceSlider.Value = Math.Clamp(AppServices.Settings.Current.MaxInterauralCorrectionDifferenceDb, 0.5, 4.0);
        StereoDifferenceText.Text = $"{StereoDifferenceSlider.Value:0.00} dB";
        _loading = false;
        StereoDifferenceSlider.IsEnabled = StereoPreservationCheck.IsChecked == true;
        RefreshPreampUi();
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppServices.Settings.Current.DebugEnabled = DebugEnabledCheck.IsChecked == true;
        AppServices.Settings.Current.DebugExpanded = DebugExpandedCheck.IsChecked == true;
        AppServices.Settings.Current.RawWasapiMode = RawModeCheck.IsChecked == true;
        AppServices.Settings.Save();
    }

    private void LatencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LatencyText is null) return;
        var value = (int)Math.Round(e.NewValue);
        LatencyText.Text = $"{value} ms";
        if (_loading) return;
        AppServices.Settings.Current.OutputLatencyMs = value;
        AppServices.Settings.Save();
    }

    private void PreampSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppServices.Settings.Current.UseAutomaticPreamp = AutomaticPreampCheck.IsChecked == true;
        AppServices.Settings.Current.LimitPositiveBoostsToPreamp = LimitBoostsCheck.IsChecked == true;
        AppServices.Settings.Current.ManualPreampDb = ManualPreampSlider.Value;
        AppServices.Settings.Save();
        RefreshPreampUi();
    }

    private void ManualPreampSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ManualPreampText is null) return;
        ManualPreampText.Text = $"{e.NewValue:0.0} dB";
        if (_loading) return;
        AppServices.Settings.Current.ManualPreampDb = e.NewValue;
        AppServices.Settings.Save();
        RefreshPreampUi();
    }

    private void RefreshPreampUi()
    {
        if (ManualPreampSlider is null || PreampWarningText is null || PreampWarningBorder is null) return;

        bool automatic = AutomaticPreampCheck.IsChecked == true;
        ManualPreampSlider.IsEnabled = !automatic;
        ManualPreampText.Opacity = automatic ? 0.45 : 1.0;

        var session = AppServices.Profiles.LastSession;
        if (session?.CompletedAt is null || session.Measurements.Count == 0)
        {
            SetPreampStatus("Complete a hearing profile to calculate the composite DSP headroom requirement.", false);
            return;
        }

        var filterSet = DspFilterService.BuildFilterSet(session);
        double requiredRaw = DspFilterService.CalculateRequiredHeadroomDb(session);

        if (automatic)
        {
            SetPreampStatus($"Automatic safe preamp: {filterSet.AppliedPreampDb:0.0} dB. The current unmodified correction requires about -{requiredRaw:0.0} dB of composite headroom.", false);
            return;
        }

        if (filterSet.PositiveGainScale < 0.999)
        {
            SetPreampStatus($"Boost limiter active: selected preamp {filterSet.AppliedPreampDb:0.0} dB. Positive EQ boosts are scaled to {filterSet.PositiveGainScale * 100.0:0}% so the complete filter cascade fits the available headroom.", false);
            return;
        }

        if (filterSet.PotentialClippingDb > 0.01)
        {
            SetPreampStatus($"WARNING: selected preamp {filterSet.AppliedPreampDb:0.0} dB is smaller than the correction's +{filterSet.RequiredHeadroomDb:0.0} dB composite peak. Up to about +{filterSet.PotentialClippingDb:0.0} dB digital overshoot is possible on full-scale material. Enable the boost limiter or use a more negative preamp if clipping is audible.", true);
            return;
        }

        SetPreampStatus($"Selected preamp {filterSet.AppliedPreampDb:0.0} dB covers the current +{filterSet.RequiredHeadroomDb:0.0} dB composite correction peak.", false);
    }


    private void StereoSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppServices.Settings.Current.StereoPreservationEnabled = StereoPreservationCheck.IsChecked == true;
        AppServices.Settings.Current.MaxInterauralCorrectionDifferenceDb = StereoDifferenceSlider.Value;
        StereoDifferenceSlider.IsEnabled = StereoPreservationCheck.IsChecked == true;
        AppServices.Settings.Save();
    }

    private void StereoDifferenceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (StereoDifferenceText is null) return;
        StereoDifferenceText.Text = $"{e.NewValue:0.00} dB";
        if (_loading) return;
        AppServices.Settings.Current.MaxInterauralCorrectionDifferenceDb = e.NewValue;
        AppServices.Settings.Save();
    }

    private void SetPreampStatus(string text, bool warning)
    {
        PreampWarningText.Text = text;
        if (warning)
        {
            PreampWarningBorder.Background = new SolidColorBrush(Color.FromRgb(51, 25, 20));
            PreampWarningBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(130, 55, 40));
            PreampWarningText.Foreground = new SolidColorBrush(Color.FromRgb(255, 177, 150));
        }
        else
        {
            PreampWarningBorder.Background = new SolidColorBrush(Color.FromRgb(12, 37, 58));
            PreampWarningBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(23, 77, 115));
            PreampWarningText.Foreground = new SolidColorBrush(Color.FromRgb(159, 217, 255));
        }
    }
}

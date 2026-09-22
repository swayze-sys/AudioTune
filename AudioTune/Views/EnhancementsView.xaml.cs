using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using AudioTune.Services;

namespace AudioTune.Views;

public partial class EnhancementsView : UserControl
{
    private readonly DispatcherTimer saveTimer;
    private bool refreshing = true;

    public EnhancementsView()
    {
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        saveTimer.Tick += SaveTimer_Tick;
        InitializeComponent();
        Loaded += EnhancementsView_Loaded;
        Unloaded += EnhancementsView_Unloaded;
    }

    private void EnhancementsView_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = AppServices.FxSoundEnhancements.CurrentEffects;
        EnabledCheck.IsChecked = AppServices.FxSoundEnhancements.Enabled;
        ClaritySlider.Value = settings.Clarity;
        AmbienceSlider.Value = settings.Ambience;
        SurroundSlider.Value = settings.Surround;
        DynamicSlider.Value = settings.DynamicBoost;
        BassSlider.Value = settings.Bass;
        refreshing = false;
        RefreshValues();

        var available = AppServices.FxSoundEnhancements.TryProbe(out var details);
        EngineStatusText.Text = details;
        EngineStatusText.Foreground = (Brush)FindResource(available ? "GreenBrush" : "AmberBrush");
        ListeningStatusText.Text = available ? "A/B Listening Test · Ready" : "A/B Listening Test · Engine unavailable";
        ListeningStatusText.Foreground = (Brush)FindResource(available ? "BlueBrush" : "AmberBrush");
        EnabledCheck.IsEnabled = available;
        SetControlsEnabled(available && EnabledCheck.IsChecked == true);
        RefreshSystemHostStatus();
    }

    private void EnhancementsView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!saveTimer.IsEnabled) return;
        saveTimer.Stop();
        Save();
    }

    private void EnabledCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (refreshing) return;
        SetControlsEnabled(EnabledCheck.IsChecked == true);
        ScheduleSave();
    }

    private void EffectSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ClarityValue is null || AmbienceValue is null || SurroundValue is null || DynamicValue is null || BassValue is null) return;
        RefreshValues();
        if (!refreshing) ScheduleSave();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        ClaritySlider.Value = AmbienceSlider.Value = SurroundSlider.Value = DynamicSlider.Value = BassSlider.Value = 0;
        Save();
    }

    private void OpenListeningTest_Click(object sender, RoutedEventArgs e)
    {
        Save();
        (Window.GetWindow(this) as MainWindow)?.OpenListeningTest();
    }

    private void FxSoundRepository_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/fxsound2/fxsound-app")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppServices.Log.Log($"Could not open FxSound repository: {ex.Message}", Models.LogLevel.Warning);
        }
    }
    private void SaveTimer_Tick(object? sender, EventArgs e)
    {
        saveTimer.Stop();
        Save();
    }

    private void ScheduleSave()
    {
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void Save()
    {
        if (refreshing) return;
        var effects = new FxSoundEffectSettings((float)ClaritySlider.Value, (float)AmbienceSlider.Value, (float)SurroundSlider.Value, (float)DynamicSlider.Value, (float)BassSlider.Value);
        var autoApply = AppServices.FxSoundEnhancements.Update(EnabledCheck.IsChecked == true, effects);
        AppServices.Log.Log($"FxSound enhancements {(EnabledCheck.IsChecked == true ? "enabled" : "disabled")}: clarity {effects.Clarity:0.0}, ambience {effects.Ambience:0.0}, surround {effects.Surround:0.0}, dynamic {effects.DynamicBoost:0.0}, bass {effects.Bass:0.0}", Models.LogLevel.Info);
        if (autoApply.Attempted && !autoApply.Applied)
            AppServices.Log.Log($"FxSound system host update failed: {autoApply.Message}", Models.LogLevel.Warning);
        RefreshSystemHostStatus(autoApply.Message);
    }

    private void RefreshValues()
    {
        ClarityValue.Text = ClaritySlider.Value.ToString("0.0");
        AmbienceValue.Text = AmbienceSlider.Value.ToString("0.0");
        SurroundValue.Text = SurroundSlider.Value.ToString("0.0");
        DynamicValue.Text = DynamicSlider.Value.ToString("0.0");
        BassValue.Text = BassSlider.Value.ToString("0.0");
    }

    private void RefreshSystemHostStatus(string? lastApplyMessage = null)
    {
        bool active = AppServices.SystemDsp.IsFxSoundHostActiveForDevice(AppServices.Settings.Current.SelectedDspDeviceId);
        if (active)
        {
            SystemHostStatusText.Text = "System-wide output · Active";
            SystemHostStatusDot.Fill = (Brush)FindResource("GreenBrush");
            SystemHostStatusText.Foreground = (Brush)FindResource("GreenBrush");
            SystemHostBorder.Background = new SolidColorBrush(Color.FromRgb(12, 43, 36));
            SystemHostBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(23, 96, 76));
            SystemHostDescriptionText.Text = "Equalizer APO loads the native AudioTune host on the selected DSP target. Changes are auto-applied when enabled.";
            SystemHostDescriptionText.Foreground = new SolidColorBrush(Color.FromRgb(169, 200, 189));
        }
        else
        {
            SystemHostStatusText.Text = AppServices.FxSoundEnhancements.Enabled
                ? "System-wide output · Pending"
                : "System-wide output · Off";
            SystemHostStatusText.Foreground = (Brush)FindResource("AmberBrush");
            SystemHostStatusDot.Fill = (Brush)FindResource("AmberBrush");
            SystemHostBorder.Background = new SolidColorBrush(Color.FromRgb(32, 26, 14));
            SystemHostBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(93, 71, 32));
            SystemHostDescriptionText.Text = lastApplyMessage ??
                "Apply / Update DSP once on Devices to load the native host through Equalizer APO.";
            SystemHostDescriptionText.Foreground = new SolidColorBrush(Color.FromRgb(216, 198, 155));
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        ClaritySlider.IsEnabled = AmbienceSlider.IsEnabled = SurroundSlider.IsEnabled = DynamicSlider.IsEnabled = BassSlider.IsEnabled = enabled;
    }
}

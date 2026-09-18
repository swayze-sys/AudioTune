using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AudioTune.Models;
using AudioTune.Services;
using Microsoft.Win32;

namespace AudioTune.Views;

public partial class DevicesView : UserControl
{
    private bool _refreshing;
    private bool _refreshingStrength;
    private bool _refreshingAutoApply;
    private bool _initializing = true;
    private readonly DispatcherTimer _strengthAutoApplyTimer;

    public DevicesView()
    {
        // XAML controls can raise ValueChanged/Checked events while InitializeComponent()
        // is still constructing the visual tree.  The auto-apply timer must therefore
        // exist before XAML is loaded and event handlers must ignore initialization.
        _strengthAutoApplyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _strengthAutoApplyTimer.Tick += StrengthAutoApplyTimer_Tick;

        InitializeComponent();
        _initializing = false;

        Loaded += DevicesView_Loaded;
        Unloaded += DevicesView_Unloaded;
    }

    private void DevicesView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            AppServices.Profiles.ProfilesChanged += Profiles_ProfilesChanged;
            AppServices.CorrectionPresets.PresetsChanged += Presets_PresetsChanged;
            AppServices.SystemDsp.DspStateChanged += SystemDsp_DspStateChanged;
            RefreshDevices();
        }
        catch (Exception ex)
        {
            // Do not let a status/registry/DSP problem tear down the whole application.
            AppServices.Log.Log($"Devices page initialization failed: {ex}", LogLevel.Error);
            MessageBox.Show(
                $"The Devices page could not be fully initialized.\n\n{ex.Message}\n\nThe rest of AudioTune can continue running.",
                "AudioTune - Devices", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DevicesView_Unloaded(object sender, RoutedEventArgs e)
    {
        _strengthAutoApplyTimer.Stop();
        AppServices.Profiles.ProfilesChanged -= Profiles_ProfilesChanged;
        AppServices.CorrectionPresets.PresetsChanged -= Presets_PresetsChanged;
        AppServices.SystemDsp.DspStateChanged -= SystemDsp_DspStateChanged;
    }

    private void Profiles_ProfilesChanged() => Dispatcher.Invoke(() =>
    {
        RefreshStrengthControl();
        RefreshDspStatus();
    });

    private void Presets_PresetsChanged() => Dispatcher.Invoke(() =>
    {
        RefreshStrengthControl();
        RefreshDspStatus();
    });

    private void SystemDsp_DspStateChanged() => Dispatcher.Invoke(RefreshDspStatus);

    private void RefreshDevices()
    {
        _refreshing = true;
        try
        {
            var devices = AppServices.AudioDevices.EnumerateOutputs();
            DeviceList.ItemsSource = devices;
            DetectedDeviceCountText.Text = $"{devices.Count} playback device{(devices.Count == 1 ? string.Empty : "s")} detected";

            var audioTuneOutput = devices.FirstOrDefault(d => d.Id == AppServices.Settings.Current.SelectedOutputDeviceId)
                                  ?? devices.FirstOrDefault(d => d.IsDefault)
                                  ?? devices.FirstOrDefault();

            DspDeviceCombo.ItemsSource = devices;
            var dspTarget = devices.FirstOrDefault(d => d.Id == AppServices.Settings.Current.SelectedDspDeviceId)
                            ?? audioTuneOutput
                            ?? devices.FirstOrDefault();
            DspDeviceCombo.SelectedItem = dspTarget;

            _refreshingAutoApply = true;
            AutoApplyCheck.IsChecked = AppServices.Settings.Current.AutoApplyDspChanges;
            _refreshingAutoApply = false;
        }
        finally
        {
            _refreshing = false;
        }

        RefreshStrengthControl();
        RefreshDspStatus();
    }

    private void RefreshStrengthControl()
    {
        _refreshingStrength = true;
        try
        {
            var active = AppServices.Profiles.LastSession;
            var preset = active is null ? null : AppServices.CorrectionPresets.GetActiveForProfile(active);
            var value = Math.Clamp(preset?.StrengthPercent ?? 100.0, 0.0, 200.0);
            CorrectionStrengthSlider.IsEnabled = active is not null && preset is not null;
            CorrectionStrengthSlider.Value = value;
            CorrectionStrengthValue.Text = active is null ? "—" : $"{value:0}%";
        }
        finally
        {
            _refreshingStrength = false;
        }
    }

    private void CorrectionStrengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing || CorrectionStrengthValue is null) return;
        CorrectionStrengthValue.Text = $"{e.NewValue:0}%";
        if (_refreshingStrength) return;

        var active = AppServices.Profiles.LastSession;
        if (active is null) return;
        var preset = AppServices.CorrectionPresets.GetActiveForProfile(active);
        preset.StrengthPercent = Math.Clamp(e.NewValue, 0.0, 200.0);
        AppServices.CorrectionPresets.Save(preset);

        if (AppServices.Settings.Current.AutoApplyDspChanges)
        {
            _strengthAutoApplyTimer.Stop();
            _strengthAutoApplyTimer.Start();
        }
    }

    private void StrengthAutoApplyTimer_Tick(object? sender, EventArgs e)
    {
        _strengthAutoApplyTimer.Stop();
        var result = AppServices.SystemDsp.TryAutoApplyCurrentPreset();
        if (result.Applied)
            AppServices.Log.Log(result.Message, LogLevel.Success);
        RefreshDspStatus();
    }

    private void AutoApplyCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing || _refreshingAutoApply || AutoApplyCheck is null) return;
        AppServices.Settings.Current.AutoApplyDspChanges = AutoApplyCheck.IsChecked == true;
        AppServices.Settings.Save();
        AppServices.Log.Log($"DSP auto-apply {(AppServices.Settings.Current.AutoApplyDspChanges ? "enabled" : "disabled")}.", LogLevel.Info);
        RefreshDspStatus();
    }

    private void DspDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || DspDeviceCombo.SelectedItem is not AudioDeviceInfo device) return;
        AppServices.Settings.Current.SelectedDspDeviceId = device.Id;
        AppServices.Settings.Save();
        AppServices.Log.Log($"System DSP target selected: {device.Name}", LogLevel.Info);
        RefreshDspStatus();
    }

    private AudioDeviceInfo? GetDspTargetDevice() => DspDeviceCombo.SelectedItem as AudioDeviceInfo;

    private async void InstallApo_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
            "AudioTune will download the official Equalizer APO 1.4.2 x64 installer from SourceForge, verify its published SHA-256 checksum, and start the official installer with administrator rights.\n\nThe Equalizer APO Configurator must still be used once to enable the desired Windows playback device. Continue?",
            "AudioTune - Install Equalizer APO", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;

        InstallApoButton.IsEnabled = false;
        ApoDownloadProgress.Value = 0;
        ApoDownloadProgress.Visibility = Visibility.Visible;
        ApoInstallStatusText.Text = "Downloading official Equalizer APO installer...";

        var progress = new Progress<double>(value =>
        {
            ApoDownloadProgress.Value = Math.Clamp(value * 100.0, 0.0, 100.0);
            ApoInstallStatusText.Text = value < 1.0
                ? $"Downloading Equalizer APO... {value:P0}"
                : "Download complete · verifying SHA-256...";
        });

        try
        {
            var result = await AppServices.EqualizerApoInstaller.InstallAsync(progress);
            ApoInstallStatusText.Text = result.Message;
            RefreshDspStatus();

            if (result.Installed && MessageBox.Show(
                    result.Message + "\n\nOpen the Equalizer APO playback-device Configurator now?",
                    "AudioTune - Equalizer APO", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                if (!AppServices.EqualizerApoInstaller.LaunchConfigurator())
                    MessageBox.Show("The Equalizer APO Configurator could not be started.", "AudioTune", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            ApoDownloadProgress.Visibility = Visibility.Collapsed;
            RefreshDspStatus();
        }
    }

    private void ConfigureApo_Click(object sender, RoutedEventArgs e)
    {
        if (!AppServices.EqualizerApoInstaller.LaunchConfigurator())
            MessageBox.Show("The Equalizer APO Configurator was not found or Windows cancelled the administrator prompt.", "AudioTune", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ApplyDsp_Click(object sender, RoutedEventArgs e)
    {
        var active = AppServices.Profiles.LastSession;
        if (active?.CompletedAt is null)
        {
            MessageBox.Show("Select a completed active hearing profile first.", "AudioTune", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var device = GetDspTargetDevice();
        if (device is null)
        {
            MessageBox.Show("Select a DSP target device first.", "AudioTune - System DSP", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var preset = AppServices.CorrectionPresets.GetActiveForProfile(active);
        bool pathMatches = string.IsNullOrWhiteSpace(active.OutputDeviceId) ||
                           string.Equals(active.OutputDeviceId, device.Id, StringComparison.OrdinalIgnoreCase);
        if (!pathMatches && MessageBox.Show(
                $"This end-to-end profile was measured on a different signal path.\n\nMeasured on: {active.OutputDeviceName ?? active.OutputDeviceId}\nDSP target: {device.Name}\n\nApply anyway?",
                "AudioTune - Different signal path", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        // Apply immediately. Detailed headroom/clipping information is shown in the
        // Devices status/summary UI instead of interrupting every update with a dialog.

        var deviceStatus = AppServices.SystemDsp.GetStatus(device.Id, device.Name, active.Id, preset.StrengthPercent, DspFilterService.CreateSignature(active, preset));
        if (deviceStatus.ApoInstalledOnSelectedDevice != true)
        {
            MessageBox.Show($"Equalizer APO is not configured for:\n\n{device.Name}\n\nOpen the APO Configurator and enable this playback device first.", "AudioTune - Equalizer APO", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (deviceStatus.EnhancementsEnabled == false)
        {
            MessageBox.Show($"Windows audio enhancements are disabled for '{device.Name}'.", "AudioTune - Equalizer APO", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            AppServices.SystemDsp.Apply(active, preset, device.Id, device.Name);
            RefreshDspStatus();
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show("Windows denied write access to the Equalizer APO config folder. Start AudioTune once as administrator to apply the persistent DSP.", "AudioTune", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AudioTune - System DSP", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DisableDsp_Click(object sender, RoutedEventArgs e)
    {
        var active = AppServices.Profiles.LastSession;
        var device = GetDspTargetDevice();
        if (active?.CompletedAt is null || device is null)
        {
            MessageBox.Show("A completed active profile and DSP target device are required for level-matched bypass.", "AudioTune", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            AppServices.SystemDsp.Disable(active, device.Id, device.Name);
            RefreshDspStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AudioTune - System DSP", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DisablePersistentDsp_Click(object sender, RoutedEventArgs e)
    {
        var device = GetDspTargetDevice();
        if (device is null) return;
        if (MessageBox.Show($"Disable AudioTune's persistent DSP for this device?\n\n{device.Name}\n\nEqualizer APO remains installed.", "AudioTune - Disable DSP", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        try
        {
            var active = AppServices.Profiles.LastSession;
            AppServices.SystemDsp.DisablePersistent(device.Id, device.Name, active?.Id, active is null ? null : AppServices.CorrectionPresets.GetActiveForProfile(active).StrengthPercent);
            RefreshDspStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AudioTune - System DSP", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ExportDsp_Click(object sender, RoutedEventArgs e)
    {
        var active = AppServices.Profiles.LastSession;
        var device = GetDspTargetDevice();
        if (active?.CompletedAt is null || device is null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export Equalizer APO configuration",
            FileName = "AudioTune.txt",
            Filter = "Equalizer APO config (*.txt)|*.txt|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var preset = AppServices.CorrectionPresets.GetActiveForProfile(active);
            AppServices.SystemDsp.Export(active, preset, dialog.FileName, device.Id, device.Name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AudioTune", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshDsp_Click(object sender, RoutedEventArgs e) => RefreshDspStatus();

    private void RefreshDspStatus()
    {
        var active = AppServices.Profiles.LastSession;
        var device = GetDspTargetDevice();
        var preset = active is null ? null : AppServices.CorrectionPresets.GetActiveForProfile(active);
        var signature = active is null || preset is null ? null : DspFilterService.CreateSignature(active, preset);
        var status = AppServices.SystemDsp.GetStatus(device?.Id, device?.Name, active?.Id, preset?.StrengthPercent, signature);

        Brush green = (Brush)FindResource("GreenBrush");
        Brush amber = (Brush)FindResource("AmberBrush");
        Brush cyan = (Brush)FindResource("CyanBrush");
        Brush muted = (Brush)FindResource("MutedBrush");
        Brush text = (Brush)FindResource("TextBrush");

        CompactDspStatusText.Text = status.Message;
        CompactDspStatusText.Foreground = status.AudioTuneApplied ? green : status.LevelMatchedBypass ? cyan : muted;

        ApoChipText.Text = status.EqualizerApoDetected ? "Equalizer APO detected" : "Equalizer APO not installed";
        ApoChipDot.Fill = status.EqualizerApoDetected ? green : amber;
        PersistentChipText.Text = status.AudioTuneApplied ? "Persistent DSP active" : status.LevelMatchedBypass ? "Persistent DSP bypass" : "Persistent DSP inactive";
        PersistentChipDot.Fill = status.AudioTuneApplied ? green : status.LevelMatchedBypass ? cyan : muted;
        FineTuneChipText.Text = preset is null ? "Fine Tune —" : $"Fine Tune: {(preset.FineTuneEnabled ? "ON" : "OFF")}";
        FineTuneChipDot.Fill = preset?.FineTuneEnabled == true ? green : muted;

        bool autoApply = AppServices.Settings.Current.AutoApplyDspChanges;
        _refreshingAutoApply = true;
        AutoApplyCheck.IsChecked = autoApply;
        _refreshingAutoApply = false;
        AutoApplyStatusText.Text = autoApply
            ? "Fine Tune and intensity changes update an already-active persistent DSP automatically."
            : "Changes are saved, but the persistent DSP is updated only when you press Apply / Update DSP.";

        ChainProfileValue.Text = active?.Name ?? "No profile";
        ChainPresetValue.Text = preset?.Name ?? "No preset";
        ChainFineTuneValue.Text = preset is null ? "—" : preset.FineTuneEnabled ? "ON" : "OFF";
        ChainFineTuneValue.Foreground = preset?.FineTuneEnabled == true ? green : muted;
        ChainCenterValue.Text = preset is null ? "—" : $"{preset.StereoCenterBalanceDb:+0.00;-0.00;0.00} dB";
        ChainApoValue.Text = status.AudioTuneApplied ? "Processing" : status.LevelMatchedBypass ? "Bypass" : status.ApoInstalledOnSelectedDevice == true ? "Ready" : "Not ready";
        ChainApoValue.Foreground = status.AudioTuneApplied ? green : status.LevelMatchedBypass ? cyan : status.ApoInstalledOnSelectedDevice == true ? text : amber;
        ChainTargetValue.Text = device?.Name ?? "Not selected";

        SummaryPresetValue.Text = preset?.Name ?? "—";
        SummaryIntensityValue.Text = preset is null ? "—" : $"{preset.StrengthPercent:0}%";
        SummaryFineTuneValue.Text = preset is null ? "—" : preset.FineTuneEnabled ? "ON" : "OFF";
        SummaryFineTuneValue.Foreground = preset?.FineTuneEnabled == true ? green : text;
        SummaryCenterValue.Text = preset is null ? "—" : $"{preset.StereoCenterBalanceDb:+0.00;-0.00;0.00} dB";

        if (active?.CompletedAt is not null && preset is not null)
        {
            var filterSet = DspFilterService.BuildFilterSet(active, preset);
            SummaryPreampValue.Text = $"{filterSet.AppliedPreampDb:0.0} dB";
            if (filterSet.PotentialClippingDb > 0.01)
            {
                SummaryRiskValue.Text = $"Potential +{filterSet.PotentialClippingDb:0.0} dB";
                SummaryRiskValue.Foreground = amber;
            }
            else
            {
                SummaryRiskValue.Text = "Low";
                SummaryRiskValue.Foreground = green;
            }
        }
        else
        {
            SummaryPreampValue.Text = "—";
            SummaryRiskValue.Text = "—";
            SummaryRiskValue.Foreground = muted;
        }

        ApoInstallPanel.Visibility = status.EqualizerApoDetected ? Visibility.Collapsed : Visibility.Visible;
        InstallApoButton.IsEnabled = !status.EqualizerApoDetected;
        ConfigureApoButton.IsEnabled = status.EqualizerApoDetected;

        bool profileReady = active?.CompletedAt is not null;
        bool deviceReady = status.EqualizerApoDetected && status.ApoInstalledOnSelectedDevice == true && status.EnhancementsEnabled != false;
        ApplyDspButton.IsEnabled = profileReady && deviceReady;
        DisableDspButton.IsEnabled = profileReady && deviceReady && status.PersistentConfigured;
        DisablePersistentDspButton.IsEnabled = deviceReady && status.PersistentConfigured;
        ExportDspButton.IsEnabled = profileReady && device is not null;
    }

    private void UseAsTestOutput_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AudioDeviceInfo device) return;
        AppServices.Settings.Current.SelectedOutputDeviceId = device.Id;
        AppServices.Settings.Save();
        (Window.GetWindow(this) as MainWindow)?.SelectOutputDevice(device.Id);
        AppServices.Log.Log($"Output device selected: {device.Name}", LogLevel.Info);
    }

    private void UseAsDspTarget_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AudioDeviceInfo device) return;
        DspDeviceCombo.SelectedItem = device;
    }

    private void DspTargetInfo_Click(object sender, RoutedEventArgs e) => ShowInfo("DSP Target Device", "This is the Windows playback endpoint that receives AudioTune's persistent Equalizer APO correction. It can be different from the device used for test tones, although end-to-end profiles are safest on the signal path they were measured with.");
    private void AutoApplyInfo_Click(object sender, RoutedEventArgs e) => ShowInfo("Auto-apply DSP changes", "When enabled, changes such as Fine Tune ON/OFF and correction intensity are written immediately to an already-active persistent AudioTune DSP. AudioTune will not silently create a new routing, override a bypass state, or replace a different profile.");
    private void SignalChainInfo_Click(object sender, RoutedEventArgs e) => ShowInfo("Signal Chain", "Shows the active hearing profile, correction preset, Fine Tune and centering state, Equalizer APO processing state, and the selected Windows target device.");
    private void DspActionsInfo_Click(object sender, RoutedEventArgs e) => ShowInfo("DSP Actions", "Apply writes the current preset persistently. Bypass keeps the same preamp/headroom but removes frequency correction. Disable removes AudioTune's persistent DSP for this device. The APO Configurator controls which Windows endpoints Equalizer APO is attached to.");
    private void DspSummaryInfo_Click(object sender, RoutedEventArgs e) => ShowInfo("Current DSP Summary", "The summary is calculated from the active preset. Estimated headroom risk reports theoretical digital overshoot when the selected manual preamp is less negative than the fitted DSP peak.");
    private void PlaybackDevicesInfo_Click(object sender, RoutedEventArgs e) => ShowInfo("Detected Playback Devices", "This collapsed list is optional. Expand it when you want to quickly assign a detected Windows playback endpoint as the AudioTune test output or DSP target.");
    private static void ShowInfo(string title, string text) => MessageBox.Show(text, $"AudioTune - {title}", MessageBoxButton.OK, MessageBoxImage.Information);
}

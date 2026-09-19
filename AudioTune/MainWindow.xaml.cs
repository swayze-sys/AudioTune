using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AudioTune.Models;
using AudioTune.Services;
using AudioTune.Views;

namespace AudioTune;

public partial class MainWindow : Window
{
    private readonly Button[] _navButtons;
    private bool _refreshingProfileCombo;

    public MainWindow()
    {
        InitializeComponent();
        _navButtons = [DashboardNav, HearingNav, ResultsNav, FineTuneNav, ListeningNav, ProfilesNav, DevicesNav, DebugNav, SettingsNav];
        Loaded += MainWindow_Loaded;
        AppServices.Settings.SettingsChanged += Settings_SettingsChanged;
        AppServices.Profiles.ProfilesChanged += Profiles_ProfilesChanged;
        AppServices.CorrectionPresets.PresetsChanged += CorrectionPresets_PresetsChanged;
        AppServices.SystemDsp.DspStateChanged += SystemDsp_DspStateChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var devices = AppServices.AudioDevices.EnumerateOutputs();
        OutputDeviceCombo.ItemsSource = devices;
        var selected = devices.FirstOrDefault(d => d.Id == AppServices.Settings.Current.SelectedOutputDeviceId)
                       ?? devices.FirstOrDefault(d => d.IsDefault)
                       ?? devices.FirstOrDefault();
        if (selected is not null) OutputDeviceCombo.SelectedItem = selected;
        Navigate(new DashboardView(), DashboardNav);
        UpdateDebugState();
        UpdateActiveProfileState();
        UpdateDspState();
    }

    private void OutputDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutputDeviceCombo.SelectedItem is not AudioDeviceInfo device) return;
        AppServices.Settings.Current.SelectedOutputDeviceId = device.Id;
        AppServices.Settings.Save();
        AppServices.Log.Log($"Output device selected: {device.Name}", LogLevel.Info);
        UpdateDspState();
    }

    private void Navigate(UserControl page, Button selected)
    {
        foreach (var b in _navButtons) b.Tag = null;
        selected.Tag = "Selected";
        PageHost.Content = page;
    }

    private void DashboardNav_Click(object sender, RoutedEventArgs e) => Navigate(new DashboardView(), DashboardNav);
    private void HearingNav_Click(object sender, RoutedEventArgs e)
    {
        var active = AppServices.Profiles.LastSession;
        if (active is not null && (!AppServices.HearingTest.HasStarted || AppServices.HearingTest.Session.Id != active.Id))
            AppServices.HearingTest.LoadSession(active);
        else if (!AppServices.HearingTest.HasStarted)
            AppServices.HearingTest.Start();

        Navigate(new HearingTestView(), HearingNav);
    }
    private void ResultsNav_Click(object sender, RoutedEventArgs e) => Navigate(new ResultsView(), ResultsNav);
    private void FineTuneNav_Click(object sender, RoutedEventArgs e) => Navigate(new FineTuneView(), FineTuneNav);
    private void ListeningNav_Click(object sender, RoutedEventArgs e) => Navigate(new ListeningTestView(), ListeningNav);
    private void ProfilesNav_Click(object sender, RoutedEventArgs e) => Navigate(new ProfilesView(), ProfilesNav);
    private void DevicesNav_Click(object sender, RoutedEventArgs e) => OpenDevicesPage();
    private void DebugNav_Click(object sender, RoutedEventArgs e) => Navigate(new DebugView(), DebugNav);
    private void SettingsNav_Click(object sender, RoutedEventArgs e) => Navigate(new SettingsView(), SettingsNav);

    public void OpenListeningTest() => Navigate(new ListeningTestView(), ListeningNav);
    public void OpenFineTune() => Navigate(new FineTuneView(), FineTuneNav);
    public void OpenStereoCentering() => Navigate(new StereoCenteringView(), ResultsNav);
    public void OpenResults() => Navigate(new ResultsView(), ResultsNav);

    public void OpenHearingTestForProfile(Guid profileId)
    {
        var profile = AppServices.Profiles.Get(profileId);
        if (profile is null) return;
        AppServices.HearingTest.LoadSession(profile);
        Navigate(new HearingTestView(), HearingNav);
    }

    public void StartNewHearingTest()
    {
        AppServices.HearingTest.Start();
        Navigate(new HearingTestView(), HearingNav);
    }

    private void Settings_SettingsChanged() => Dispatcher.Invoke(() => { UpdateDebugState(); UpdateDspState(); });
    private void Profiles_ProfilesChanged() => Dispatcher.Invoke(() => { UpdateActiveProfileState(); UpdateDspState(); });
    private void CorrectionPresets_PresetsChanged() => Dispatcher.Invoke(UpdateDspState);
    private void SystemDsp_DspStateChanged() => Dispatcher.Invoke(UpdateDspState);

    private void UpdateActiveProfileState()
    {
        _refreshingProfileCombo = true;
        try
        {
            var profiles = AppServices.Profiles.Sessions.ToList();
            ActiveProfileCombo.ItemsSource = profiles;
            ActiveProfileCombo.SelectedItem = AppServices.Profiles.LastSession is null
                ? null
                : profiles.FirstOrDefault(p => p.Id == AppServices.Profiles.LastSession.Id);
        }
        finally
        {
            _refreshingProfileCombo = false;
        }
    }

    private void ActiveProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingProfileCombo || ActiveProfileCombo.SelectedItem is not HearingSession session) return;
        if (AppServices.Profiles.LastSession?.Id == session.Id) return;
        AppServices.Profiles.SetActive(session.Id);

        if (PageHost.Content is HearingTestView)
        {
            AppServices.HearingTest.LoadSession(session);
            Navigate(new HearingTestView(), HearingNav);
        }
    }
    private void UpdateDebugState()
    {
        bool enabled = AppServices.Settings.Current.DebugEnabled;
        DebugNav.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        DebugStateText.Text = enabled ? "Debug enabled" : "Debug hidden";
        DebugStateText.Foreground = (System.Windows.Media.Brush)FindResource(enabled ? "GreenBrush" : "MutedBrush");
    }


    private void DspStatusButton_Click(object sender, RoutedEventArgs e) => OpenDevicesPage();

    public void OpenDevicesPage()
    {
        try
        {
            Navigate(new DevicesView(), DevicesNav);
        }
        catch (Exception ex)
        {
            AppServices.Log.Log($"Failed to open Devices page: {ex}", LogLevel.Error);
            MessageBox.Show(
                $"The Devices page could not be opened.\n\n{ex.Message}\n\nAudioTune will remain open.",
                "AudioTune - Devices", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateDspState()
    {
        var active = AppServices.Profiles.LastSession;
        var devices = AppServices.AudioDevices.EnumerateOutputs();
        var device = devices.FirstOrDefault(d => d.Id == AppServices.Settings.Current.SelectedDspDeviceId)
                     ?? devices.FirstOrDefault(d => d.Id == AppServices.Settings.Current.SelectedOutputDeviceId);
        if (device is null || active is null)
        {
            DspStatusTopText.Text = "OFF";
            DspStatusTopText.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
            return;
        }
        var preset = AppServices.CorrectionPresets.GetActiveForProfile(active);
        var signature = DspFilterService.CreateSignature(active, preset);
        var status = AppServices.SystemDsp.GetStatus(device.Id, device.Name, active.Id, preset.StrengthPercent, signature);
        if (status.LevelMatchedBypass)
        {
            DspStatusTopText.Text = "BYPASS";
            DspStatusTopText.Foreground = (System.Windows.Media.Brush)FindResource("AmberBrush");
        }
        else if (status.AudioTuneApplied)
        {
            DspStatusTopText.Text = "ACTIVE";
            DspStatusTopText.Foreground = (System.Windows.Media.Brush)FindResource("GreenBrush");
        }
        else if (status.PersistentConfigured && !status.AudioTuneProfileMatchesActiveProfile)
        {
            DspStatusTopText.Text = "UPDATE";
            DspStatusTopText.Foreground = (System.Windows.Media.Brush)FindResource("AmberBrush");
        }
        else
        {
            DspStatusTopText.Text = "OFF";
            DspStatusTopText.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
        }
    }

    public void SelectOutputDevice(string deviceId)
    {
        var device = (OutputDeviceCombo.ItemsSource as IEnumerable<AudioDeviceInfo>)?.FirstOrDefault(d => d.Id == deviceId)
                     ?? AppServices.AudioDevices.EnumerateOutputs().FirstOrDefault(d => d.Id == deviceId);
        if (device is not null) OutputDeviceCombo.SelectedItem = device;
    }

    private void TitleArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleMaximize();
        else if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}

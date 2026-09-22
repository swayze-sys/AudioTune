using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AudioTune.Models;
using AudioTune.Services;
using Microsoft.Win32;

namespace AudioTune.Views;

public partial class ProfilesView : UserControl
{
    private HearingSession? _selected;
    private bool _refreshing;
    private bool _refreshingPreset;

    private sealed class ProfileItem
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public string Subtitle { get; init; } = "";
        public string Status { get; init; } = "";
        public string ActiveLabel { get; init; } = "";
        public string ArchiveLabel { get; init; } = "";
    }

    public ProfilesView()
    {
        InitializeComponent();
        Loaded += ProfilesView_Loaded;
        Unloaded += ProfilesView_Unloaded;
    }

    private void ProfilesView_Loaded(object sender, RoutedEventArgs e)
    {
        AppServices.Profiles.ProfilesChanged += Profiles_ProfilesChanged;
        AppServices.CorrectionPresets.PresetsChanged += PresetsChanged;
        RefreshProfiles();
    }

    private void ProfilesView_Unloaded(object sender, RoutedEventArgs e)
    {
        AppServices.Profiles.ProfilesChanged -= Profiles_ProfilesChanged;
        AppServices.CorrectionPresets.PresetsChanged -= PresetsChanged;
    }

    private void Profiles_ProfilesChanged() => Dispatcher.Invoke(RefreshProfiles);
    private void PresetsChanged() => Dispatcher.Invoke(RefreshPresetList);

    private void RefreshProfiles()
    {
        _refreshing = true;
        try
        {
            var active = AppServices.Profiles.LastSession;
            var previous = _selected?.Id ?? active?.Id;
            var items = BuildProfileItems();
            ProfileList.ItemsSource = items;
            ProfileCountText.Text = items.Count.ToString();

            if (previous.HasValue)
                ProfileList.SelectedItem = items.FirstOrDefault(x => x.Id == previous.Value);
            if (ProfileList.SelectedItem is null && items.Count > 0)
                ProfileList.SelectedIndex = 0;

            _selected = ProfileList.SelectedItem is ProfileItem item
                ? AppServices.Profiles.Get(item.Id)
                : active;

            RefreshSelectedDetails();
            RefreshActiveDetails();
            RefreshPresetList();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private List<ProfileItem> BuildProfileItems()
    {
        string search = ProfileSearchBox?.Text?.Trim() ?? string.Empty;
        var active = AppServices.Profiles.LastSession;
        return AppServices.Profiles.Sessions
            .Where(s => string.IsNullOrWhiteSpace(search) ||
                        s.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                        (s.OutputDeviceName?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false))
            .Select(s => new ProfileItem
            {
                Id = s.Id,
                Name = s.Name,
                Subtitle = $"{s.Measurements.Count}/60 · {(s.CompletedAt is null ? "incomplete" : "complete")} · {s.UpdatedAt:dd.MM.yyyy HH:mm}",
                Status = s.OutputDeviceName is null ? "Device-bound measurement" : s.OutputDeviceName,
                ActiveLabel = active?.Id == s.Id ? "● ACTIVE" : "",
                ArchiveLabel = s.IsArchived ? "ARCHIVED" : ""
            })
            .ToList();
    }

    private void ProfileSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || _refreshing) return;
        RefreshProfiles();
    }

    private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing) return;
        _selected = ProfileList.SelectedItem is ProfileItem item ? AppServices.Profiles.Get(item.Id) : null;
        RefreshSelectedDetails();
        RefreshPresetList();
    }

    private void RefreshSelectedDetails()
    {
        var s = _selected;
        bool has = s is not null;
        bool isActive = has && AppServices.Profiles.LastSession?.Id == s!.Id;

        ActiveProfileBadge.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        SetActiveButton.Visibility = has && !isActive ? Visibility.Visible : Visibility.Collapsed;
        SetActiveButton.IsEnabled = has && !isActive;
        SetActiveButton.Content = "Set as active profile";
        SetActiveButton.Foreground = (Brush)FindResource("TextBrush");

        RenameButton.IsEnabled = has;
        EditButton.IsEnabled = has && s!.Measurements.Count > 0;
        ExportButton.IsEnabled = has;
        ArchiveButton.IsEnabled = has;
        RestoreButton.IsEnabled = has;
        SaveCopyButton.IsEnabled = has;
        CompareButton.IsEnabled = has && AppServices.Profiles.LastSession is not null && AppServices.Profiles.LastSession.Id != s?.Id;

        bool activeCompleted = isActive && s?.CompletedAt is not null;
        FineTuneButton.IsEnabled = activeCompleted;
        CenteringButton.IsEnabled = activeCompleted;
        ListeningButton.IsEnabled = activeCompleted;
        DuplicatePresetButton.IsEnabled = activeCompleted;

        if (!has)
        {
            SelectedProfileName.Text = "No saved profile";
            SelectedProfileDetails.Text = "Select a hearing profile on the left.";
            SelectedProfileDevice.Text = "—";
            SelectedProfileType.Text = "—";
            SelectedProfileCreated.Text = "—";
            SelectedProfileSaved.Text = "—";
            SummaryMeasurements.Text = "—";
            SummaryCeiling.Text = "—";
            SummarySampleRate.Text = "—";
            SummaryProtocol.Text = "—";
            SummaryAlgorithm.Text = "—";
            SummaryAudioApi.Text = "—";
            ArchiveButtonLabel.Text = "Archive profile";
            return;
        }

        int ceiling = s!.Measurements.Count(m => m.Status == HearingMeasurementStatus.NotDetectedAtCeiling);
        SelectedProfileName.Text = s.Name;
        SelectedProfileDetails.Text = $"{s.Measurements.Count}/60 · {(s.CompletedAt is null ? "incomplete" : "complete")} · measured {s.UpdatedAt:dd.MM.yyyy HH:mm}";
        SelectedProfileDevice.Text = s.OutputDeviceName ?? "Measured output device not recorded";
        SelectedProfileType.Text = "Device-bound";
        SelectedProfileCreated.Text = s.StartedAt.ToString("dd.MM.yyyy HH:mm");
        SelectedProfileSaved.Text = s.UpdatedAt.ToString("dd.MM.yyyy HH:mm");

        SummaryMeasurements.Text = $"{s.Measurements.Count} / 60";
        SummaryCeiling.Text = ceiling == 0 ? "0" : $"{ceiling} (at test ceiling)";
        SummarySampleRate.Text = $"{s.SampleRateHz:N0} Hz";
        SummaryProtocol.Text = $"v{s.TestProtocolVersion} · App {s.AppVersion}";
        SummaryAlgorithm.Text = $"v{CorrectionPreviewService.AlgorithmVersion}";
        SummaryAudioApi.Text = s.AudioApi;
        ArchiveButtonLabel.Text = s.IsArchived ? "Unarchive profile" : "Archive profile";
    }

    private void RefreshActiveDetails()
    {
        var active = AppServices.Profiles.LastSession;
        ActiveProfileName.Text = active?.Name ?? "None";
        ActiveProfileDetails.Text = active is null
            ? "Select or create a profile."
            : $"{active.Measurements.Count}/60 · {(active.CompletedAt is null ? "partial" : "complete")}";
    }

    private void RefreshPresetList()
    {
        _refreshingPreset = true;
        try
        {
            var active = AppServices.Profiles.LastSession;
            bool selectedIsActive = _selected is not null && active?.Id == _selected.Id;

            if (active is null || !selectedIsActive)
            {
                PresetCombo.ItemsSource = null;
                PresetCombo.IsEnabled = false;
                PresetAvailabilityText.Text = active is null
                    ? "No active hearing profile."
                    : "Set this hearing profile active to manage its correction presets.";
                ClearPresetStats();
                return;
            }

            var defaultPreset = AppServices.CorrectionPresets.GetActiveForProfile(active);
            var presets = AppServices.CorrectionPresets.Presets.Where(p => p.HearingProfileId == active.Id).ToList();
            PresetCombo.ItemsSource = presets;
            PresetCombo.IsEnabled = true;
            PresetCombo.SelectedItem = presets.FirstOrDefault(p => p.Id == defaultPreset.Id) ?? presets.FirstOrDefault();
            PresetAvailabilityText.Text = string.Empty;
            UpdatePresetDetails();
        }
        finally
        {
            _refreshingPreset = false;
        }
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingPreset || PresetCombo.SelectedItem is not CorrectionPreset preset) return;
        AppServices.CorrectionPresets.SetActive(preset.Id);
        UpdatePresetDetails();
    }

    private void UpdatePresetDetails()
    {
        if (PresetCombo.SelectedItem is not CorrectionPreset p)
        {
            ClearPresetStats();
            return;
        }

        PresetIntensityValue.Text = p.HearingProfileEnabled
            ? $"{p.StrengthPercent:0}%"
            : $"OFF · {p.StrengthPercent:0}% saved";
        PresetIntensityValue.Foreground = (Brush)FindResource(p.HearingProfileEnabled ? "TextBrush" : "MutedBrush");
        PresetFineTuneValue.Text = p.FineTuneEnabled ? $"ON · {p.FineTuneAdjustments.Count} pts" : $"OFF · {p.FineTuneAdjustments.Count} saved";
        PresetFineTuneValue.Foreground = (Brush)FindResource(p.FineTuneEnabled ? "GreenBrush" : "MutedBrush");
        PresetCenterValue.Text = p.StereoCenteringEnabled
            ? $"{p.StereoCenterBalanceDb:+0.00;-0.00;0.00} dB"
            : $"OFF · {p.StereoCenterBalanceDb:+0.00;-0.00;0.00} dB saved";
        PresetCenterValue.Foreground = (Brush)FindResource(p.StereoCenteringEnabled ? "TextBrush" : "MutedBrush");
        PresetAlgorithmValue.Text = $"v{CorrectionPreviewService.AlgorithmVersion}";
    }

    private void ClearPresetStats()
    {
        PresetIntensityValue.Text = "—";
        PresetIntensityValue.Foreground = (Brush)FindResource("TextBrush");
        PresetFineTuneValue.Text = "—";
        PresetFineTuneValue.Foreground = (Brush)FindResource("TextBrush");
        PresetCenterValue.Text = "—";
        PresetCenterValue.Foreground = (Brush)FindResource("TextBrush");
        PresetAlgorithmValue.Text = "—";
    }

    private void NewTest_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.StartNewHearingTest();

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var newName = PromptForText("Rename hearing profile", "Profile name", _selected.Name);
        if (!string.IsNullOrWhiteSpace(newName))
            AppServices.Profiles.Rename(_selected, newName);
    }

    private void SaveCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        AppServices.Profiles.Duplicate(_selected, $"{_selected.Name} - backup {DateTime.Now:yyyy-MM-dd HH-mm}");
    }

    private void SetActive_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is not null) AppServices.Profiles.SetActive(_selected.Id);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is not null) (Window.GetWindow(this) as MainWindow)?.OpenHearingTestForProfile(_selected.Id);
    }

    private void Archive_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is not null) AppServices.Profiles.SetArchived(_selected, !_selected.IsArchived);
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        if (!AppServices.Profiles.RestoreBackup(_selected))
            MessageBox.Show("No usable previous backup was found for this profile.", "AudioTune", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null || AppServices.Profiles.LastSession is null) return;
        MessageBox.Show(AppServices.Profiles.Compare(_selected, AppServices.Profiles.LastSession), "AudioTune - Profile comparison", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Import AudioTune hearing profile", Filter = "AudioTune profile (*.json)|*.json|All files|*.*" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var imported = AppServices.Profiles.Import(dialog.FileName);
            AppServices.Profiles.SetActive(imported.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AudioTune", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var dialog = new SaveFileDialog { Title = "Export AudioTune hearing profile", FileName = $"AudioTune-{SanitizeFileName(_selected.Name)}.json", Filter = "AudioTune profile (*.json)|*.json|All files|*.*" };
        if (dialog.ShowDialog() == true) AppServices.Profiles.Export(_selected, dialog.FileName);
    }

    private void DuplicatePreset_Click(object sender, RoutedEventArgs e)
    {
        var active = AppServices.Profiles.LastSession;
        if (active is null || _selected?.Id != active.Id) return;
        var source = PresetCombo.SelectedItem as CorrectionPreset ?? AppServices.CorrectionPresets.GetActiveForProfile(active);
        var copy = AppServices.CorrectionPresets.Create(active, $"{source.Name} copy", source);
        AppServices.CorrectionPresets.SetActive(copy.Id);
        RefreshPresetList();
    }

    private void FineTune_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenFineTune();
    private void Centering_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenStereoCentering();
    private void Listening_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.OpenListeningTest();

    private string? PromptForText(string title, string label, string initialValue)
    {
        var owner = Window.GetWindow(this);
        var box = new TextBox
        {
            Text = initialValue,
            Margin = new Thickness(0, 6, 0, 14),
            Padding = new Thickness(8, 6, 8, 6),
            MinWidth = 340,
            Background = new SolidColorBrush(Color.FromRgb(10, 18, 27)),
            Foreground = (Brush)FindResource("TextBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1)
        };

        var ok = new Button { Content = "Save", Width = 90, Margin = new Thickness(6, 0, 0, 0), Style = (Style)FindResource("PrimaryButtonStyle") };
        var cancel = new Button { Content = "Cancel", Width = 90, Style = (Style)FindResource("FlatButtonStyle") };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);

        var content = new StackPanel { Margin = new Thickness(18) };
        content.Children.Add(new TextBlock { Text = label, Foreground = (Brush)FindResource("MutedBrush") });
        content.Children.Add(box);
        content.Children.Add(buttons);

        var dialog = new Window
        {
            Title = $"AudioTune - {title}",
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)FindResource("PanelBrush"),
            Content = content,
            ShowInTaskbar = false
        };

        ok.Click += (_, _) => dialog.DialogResult = true;
        cancel.Click += (_, _) => dialog.DialogResult = false;
        box.SelectAll();
        box.Focus();
        return dialog.ShowDialog() == true ? box.Text.Trim() : null;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
        return name;
    }
}

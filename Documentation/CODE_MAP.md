# Code Map

A quick guide for locating behavior.

## Root

- `AudioTune.sln` – solution.
- `build.ps1` – restore/build Debug; PowerShell 5.1 compatible.
- `run.ps1` – starts compiled EXE independently from PowerShell.
- `publish.ps1` – framework-dependent win-x64 single-file publish plus bundled-runtime validation.
- `build-installer.ps1` – publish + verified .NET Desktop Runtime prerequisite + repository-local Inno Setup build.
- `backup-profiles.ps1` – user profile backup helper.
- `Installer/AudioTune.iss` – Inno Setup definition.

## Application shell

- `AudioTune/App.xaml` – global resources/theme/styles.
- `AudioTune/App.xaml.cs` – application startup/shutdown.
- `AudioTune/MainWindow.xaml` – shell/top bar/sidebar/PageHost.
- `AudioTune/MainWindow.xaml.cs` – navigation, output device, active profile and DSP top status.

## Models

- `Models/AppSettings.cs` – persisted app settings.
- `Models/AudioDeviceInfo.cs` – endpoint display model.
- `Models/HeadphoneProfile.cs` – headphone identity/reference metadata model.
- `Models/HearingMeasurement.cs` – HearingSession, HearingMeasurement, HearingTrial, CorrectionPreset, FineTuneAdjustment and enums.
- `Models/LogEntry.cs` – debug/event log model.

## Hearing measurement

- `Services/HearingTestEngine.cs` – test state machine, threshold search, automatic outlier verification, retests.
- `Services/PulseToneSampleProvider.cs` – repeating pulsed sine generation.
- `Services/TonePlaybackService.cs` – WASAPI tone/fine-tune/centering playback and 50% app-session volume.
- `Views/HearingTestView.*` – UI/workflow.

## Correction / psychoacoustics

- `Services/HumanSensitivityModel.cs` – relative ISO-shaped threshold reference/trust.
- `Services/CorrectionPreviewService.cs` – correction model v5, Fine Tune integration, intensity, stereo preservation.
- `Data/Psychoacoustics/iso226.sources.json` – source metadata.

## Fine Tune / centering

- `Services/FineTuneEngine.cs` – Fine Tune state and values.
- `Services/AlternatingFineTuneSampleProvider.cs` – 1 kHz/test alternating tones.
- `Views/FineTuneView.*` – Fine Tune UI/master switch.
- `Services/StereoCenteringSampleProvider.cs` – corrected band-limited centering stimulus.
- `Views/StereoCenteringView.*` – centering UI/save.

## DSP

- `Services/DspFilterService.cs` – target->PEQ fitting, Q, composite peak, preamp, boost limiter, centering trims, response calculation, APO filter-line generation.
- `Services/CalibrationAbSampleProvider.cs` – internal music A/B DSP.
- `Services/MusicPreviewService.cs` – music loading/playback/A-B state.
- `Views/ListeningTestView.*` – A/B UI.

## Persistent Windows DSP

- `Services/SystemDspService.cs` – Equalizer APO status, config generation, apply/bypass/disable, per-device files, auto-apply.
- `Services/EqualizerApoInstallerService.cs` – optional official installer download/hash/UAC/configurator.
- `Views/DevicesView.*` – DSP control center.

## Persistence

- `Services/ProfileRepository.cs` – Hearing Profile load/save/migration/import/export/archive/backup.
- `Services/CorrectionPresetRepository.cs` – Correction Preset load/save/migration/default/active.
- `Services/SettingsService.cs` – settings JSON.

## Other services

- `Services/AppServices.cs` – central service instances.
- `Services/AudioDeviceService.cs` – enumerate/get WASAPI render endpoints and read master volume.
- `Services/HeadphoneProfileService.cs` – headphone reference data.
- `Services/DebugLogService.cs` – app debug log.

## Custom charts

- `Controls/FrequencyChart.cs`
- `Controls/CorrectionBarChart.cs`

These implement custom WPF drawing/point inspection rather than relying on a generic charting package.

## Views

- `DashboardView.*`
- `HearingTestView.*`
- `ResultsView.*`
- `FineTuneView.*`
- `ListeningTestView.*`
- `ProfilesView.*`
- `DevicesView.*`
- `DebugView.*`
- `SettingsView.*`
- `StereoCenteringView.*`

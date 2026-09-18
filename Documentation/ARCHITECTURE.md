# Architecture

## Technology

- C# / WPF
- `net10.0-windows`
- NAudio 3.1.0
- WASAPI shared mode for playback
- Equalizer APO for persistent system-wide processing
- JSON files for settings, hearing profiles and correction presets

## High-level structure

```text
AudioTune/
├── Models/
├── Services/
├── Controls/
├── Views/
├── Assets/
└── Data/
```

The current application is intentionally service-oriented rather than using a full MVVM framework. `AppServices` owns singleton-like service instances.

## Service hub

`Services/AppServices.cs` exposes:

- `SettingsService`
- `DebugLogService`
- `AudioDeviceService`
- `HeadphoneProfileService`
- `ProfileRepository`
- `CorrectionPresetRepository`
- `TonePlaybackService`
- `MusicPreviewService`
- `SystemDspService`
- `EqualizerApoInstallerService`
- `HearingTestEngine`
- `FineTuneEngine`

`AppServices.Initialize()` loads settings, hearing profiles and correction presets.

## Main data flow

```text
HearingTestEngine
    -> HearingSession / HearingMeasurement
    -> ProfileRepository

CorrectionPresetRepository
    -> CorrectionPreset

HearingSession + CorrectionPreset
    -> CorrectionPreviewService
    -> target L/R correction curves

Target curves
    -> DspFilterService
    -> fitted L/R parametric EQ filters
    -> preamp/headroom
    -> centering trims

DspFilterSet
    -> CalibrationAbSampleProvider (A/B music)
    -> SystemDspService (Equalizer APO text)
```

## Main models

Defined primarily in `Models/HearingMeasurement.cs`.

### HearingSession

Owns raw measurement/session data:

- schema/version metadata;
- ID/name/timestamps;
- headphone ID;
- output device ID/name;
- application session reference volume;
- observed Windows master volume at start;
- sample rate and audio API;
- measurements;
- archived state.

### HearingMeasurement

One frequency/ear measurement:

- frequency;
- ear;
- threshold dBFS;
- initial threshold;
- status;
- trial history;
- verification thresholds/statuses;
- confidence;
- flags/timestamps.

### CorrectionPreset

Tuning layer attached to one Hearing Profile:

- own ID;
- Hearing Profile ID;
- name;
- strength/intensity 0–200%;
- Fine Tune ON/OFF;
- Fine Tune adjustments;
- Stereo Center balance.

## UI architecture

`MainWindow` provides:

- custom top bar;
- output-device selector;
- active-profile selector;
- DSP status (`ACTIVE`, `BYPASS`, `UPDATE`, `OFF`);
- sidebar navigation;
- `PageHost` where view `UserControl`s are instantiated.

Important navigation methods:

- `OpenListeningTest()`
- `OpenFineTune()`
- `OpenStereoCentering()`
- `OpenResults()`
- `OpenHearingTestForProfile(Guid)`
- `StartNewHearingTest()`

## Screen responsibilities

### Dashboard

At-a-glance active setup and correction preview. Keep headphone identity and active correction graph visible.

### Hearing Test

Interactive threshold-test state machine and frequency progress. Completed points can be selected/retested.

### Results

Raw L/R threshold plots, desired correction preview, optional Fine Tune inclusion, optional actual APO/DSP transfer and diagnostics.

### Fine Tuning

Moderate-level loudness matching against 1 kHz. Contains the **master Fine Tune ON/OFF** switch for the active Correction Preset.

### A/B Listening Test

Loads a stereo music file and crossfades between level-matched dry and calibrated paths without restarting playback.

### Profiles

Manages saved Hearing Profiles and Correction Presets. The UI was redesigned in v0.4.15/0.4.16 to use profile cards, compact preset information, Quick Actions and Measurement Summary.

### Devices

DSP control center:

- DSP target selection;
- Equalizer APO/persistent DSP/Fine Tune status chips;
- Auto-apply toggle;
- signal-chain visualization;
- DSP actions;
- current DSP summary;
- correction-intensity slider (0–200%);
- collapsed detected playback-device area.

### Settings

Development/debug, audio latency/raw mode, preamp/headroom policy and stereo-preservation settings.

## Persistence architecture

See `PROFILE_MODEL.md` and `DATA_AND_PRIVACY.md`.

Summary:

- settings: `%LOCALAPPDATA%\AudioTune\settings.json`
- hearing profiles: `%LOCALAPPDATA%\AudioTune\Profiles\...`
- correction presets: `%LOCALAPPDATA%\AudioTune\CorrectionPresets\...`
- Equalizer APO installer cache: `%LOCALAPPDATA%\AudioTune\Installers\...`

Persistent Windows DSP is stored in Equalizer APO's config directory, not under the AudioTune profile folder.

## Layering rule for future work

Avoid directly computing DSP inside views. UI should change Settings/Presets or call services. The durable domain logic should stay in service/model code so it can be unit-tested without WPF.

A desired future cleanup is to move more DSP functions into pure, dependency-light core classes and add an automated test project.

# Equalizer APO Integration

## Why Equalizer APO is used

A normal WPF/NAudio process cannot transparently intercept every Windows application audio stream. Equalizer APO acts as the persistent system-wide DSP host for selected Windows playback endpoints.

AudioTune therefore serves as:

- calibration UI;
- profile/preset manager;
- DSP generator;
- Equalizer APO configuration manager.

AudioTune does **not** need to remain running after persistent DSP has been applied.

## Integrated installer

`EqualizerApoInstallerService` currently targets:

- Equalizer APO version **1.4.2 x64**
- official SourceForge download URL
- hard-coded published SHA-256:
  `7403be7427bbe1936a40dded082829b6e217fc4f5990fee5cba501f0ae055afa`

The installer is downloaded to:

`%LOCALAPPDATA%\AudioTune\Installers\EqualizerAPO-x64-1.4.2.exe`

AudioTune verifies SHA-256 before launching it with UAC.

Do not update the version/download without updating and verifying the hash.

## Device selection

Equalizer APO must be configured for the Windows playback endpoint using its Configurator/Device Selector.

AudioTune can launch:

- `Configurator.exe`, or
- `DeviceSelector.exe`

when present.

The app inspects endpoint registration/Windows audio-enhancement state before applying DSP.

## DSP target vs test output

AudioTune maintains separate settings:

- `SelectedOutputDeviceId` – device used for test/A-B playback.
- `SelectedDspDeviceId` – target for persistent APO processing.

Because current profiles are device-bound, applying to a mismatching output chain should warn the user.

## Persistent config layout

AudioTune uses Equalizer APO's config directory.

Conceptual structure:

```text
EqualizerAPO\config\
├── config.txt
├── AudioTune.txt
└── AudioTune\
    ├── device-<endpointguid>.txt
    └── device-<endpointguid>.txt
```

`config.txt` receives one managed include:

```text
# AudioTune managed persistent DSP include
Include: AudioTune.txt
```

`AudioTune.txt` is rebuilt as the master containing all AudioTune-managed per-device content.

The per-device files are the durable source for each endpoint.

## Per-device generated content

An enabled configuration includes comments/metadata and roughly:

```text
# AudioTune enabled
Device: {ENDPOINT-GUID}
Preamp: <global preamp> dB
Channel: L
[optional centering attenuation]
Filter: ON PK Fc ... Gain ... dB Q ...
...
Channel: R
[optional centering attenuation]
Filter: ON PK Fc ... Gain ... dB Q ...
...
Channel: ALL
```

Metadata comments include:

- profile name/ID;
- preset ID;
- correction intensity;
- Fine Tune state/point count;
- correction signature;
- algorithm version;
- target device/GUID;
- target-curve peak;
- actual fitted DSP peak/headroom;
- applied preamp;
- positive-boost scale;
- clipping warning when applicable.

## Level-matched bypass

Bypass is persistent and deliberately keeps the same global preamp/headroom while removing frequency-dependent correction.

This makes A/B comparison fairer than removing both EQ and preamp at once.

A user-selected persistent bypass must not be silently turned back into processing by Auto-apply.

## Disable persistent DSP

Disabling persistent DSP for a device removes its managed device file. If no managed targets remain, AudioTune removes its include from Equalizer APO `config.txt`.

Equalizer APO itself is not uninstalled.

## DSP status model

Top-bar/UI status is derived from:

- Equalizer APO detection;
- APO registration on selected endpoint;
- Windows audio enhancements state;
- persistent file presence;
- targeted endpoint GUID;
- applied profile/preset strength/signature;
- current correction algorithm version;
- bypass marker.

Main states:

- `ACTIVE`
- `BYPASS`
- `UPDATE`
- `OFF`

`UPDATE` means persistent DSP exists but current active correction settings no longer match the stored signature/profile state.

## Correction signature

`DspFilterService.CreateSignature` hashes a deterministic description of:

- algorithm version;
- profile/preset IDs;
- strength;
- Fine Tune enabled state;
- preamp settings;
- boost limiter;
- stereo preservation settings;
- centering;
- final L/R filter frequency/gain/Q values.

This is used to detect whether the on-disk persistent DSP matches current settings.

## Auto-apply DSP changes

`AppSettings.AutoApplyDspChanges` defaults to true for new settings.

Intended behavior:

- Fine Tune ON/OFF -> save preset -> immediately rewrite existing persistent DSP;
- correction intensity change -> same;
- Stereo Centering save -> same.

Auto-apply currently calls the same `SystemDspService.Apply()` path as manual Apply/Update.

Guardrails:

- no complete active profile -> no apply;
- no selected target -> no apply;
- target unavailable -> no apply;
- if existing per-device file explicitly contains `# AudioTune level-matched bypass`, auto-apply refuses to re-enable correction.

Important regression test: this behavior had several bugs during development. See `TESTING.md`.

## Backup of original Equalizer APO config

When persistent management is first prepared, AudioTune creates:

`config.audiotune-backup.txt`

if it does not already exist.

Do not overwrite that backup on every apply.

## Applications not guaranteed to be processed

Equalizer APO relies on the Windows audio-effect path for the selected endpoint. Applications/output modes that bypass that path (for example some exclusive/ASIO paths) may not be processed.

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
- Hearing Profile stage state;
- Fine Tune state/point count;
- Stereo Centering stage state and saved balance;
- correction signature;
- algorithm version;
- target device/GUID;
- target-curve peak;
- actual fitted DSP peak/headroom;
- applied preamp;
- positive-boost scale;
- clipping warning when applicable.

Enabled configurations also contain a per-channel response-explanation table at every fitted DSP band. It lists:

- hearing-model contribution before correction strength;
- saved/interpolated Fine Tune contribution;
- final target after strength, ±12 dB clamp and stereo preservation;
- real summed response of the complete PK-filter cascade;
- final output response after global preamp and Stereo Centering trim.

This table is diagnostic comments only. Equalizer APO ignores the comment lines. It exists to make clear that the `Gain` on one `Filter:` line is that filter's own center gain, not the complete response at that frequency.

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

## Native FxSound host

Enabled sound enhancements are inserted after the hearing-correction PEQ as an Equalizer APO `VSTPlugin:` stage. AudioTune uses Equalizer APO's existing native host rather than registering a second Windows APO, because the selected endpoint is already owned by Equalizer APO at the Windows audio-effect layer.

Generated content ends with:

```text
Channel: ALL
VSTPlugin: Library "C:\ProgramData\AudioTune\Native\AudioTune.FxSound.Apo.0.4.18.dll" Power 1 Clarity ... Ambience ... Surround ... Dynamic ... Bass ...
```

The five effect parameters are normalized from AudioTune's original 0-10 scale to VST values 0-1. The file also stores `# FxSound native host` and `# FxSound signature` comments, so changing the effect state or values produces `UPDATE` until Auto-apply or Apply/Update DSP rewrites the target.

The native module declares two inputs and two outputs. Equalizer APO supplies non-interleaved float buffers; the host converts them to the interleaved float format required by the original DfxDsp engine and back without changing sample values. No memory is allocated on the real-time processing path after block-size preparation. Invalid/unprepared states pass audio through.

`Power=0`, persistent level-matched bypass, and disabling the Enhancements switch omit/bypass the native effect. If the system host is already active on the A/B playback endpoint, AudioTune does not also run the local FxSound sample provider.

Installed host binaries are versioned under `%ProgramData%\AudioTune\Native` to avoid update-time replacement of a DLL loaded by the Windows audio service. Uninstall removes AudioTune's Equalizer APO include and managed files before the host binary is removed; hearing profiles under `%LOCALAPPDATA%\AudioTune` remain preserved.

Reference implementation/host behavior was checked against the official Equalizer APO 1.4.2 source and documentation:

- https://sourceforge.net/p/equalizerapo/wiki/Configuration%20reference/
- https://sourceforge.net/p/equalizerapo/wiki/Developer%20documentation/

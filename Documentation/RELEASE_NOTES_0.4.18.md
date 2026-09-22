# AudioTune v0.4.18

Regular release for Windows x64 (22 September 2026).

## New in this release

- FxSound enhancements: Clarity, Ambience, Surround, Dynamic Boost and Bass use the original FxSound DfxDsp engine. They can be switched independently of the hearing profile, Fine Tune and Stereo Centering.
- A native Equalizer APO host applies the enhancements to the selected system output when the persistent DSP path is configured. The same engine is used for AudioTune's local listening test without double processing.
- The dashboard shows the full processing chain and its individual switches.
- The Sound Enhancements page has larger PNG effect symbols, a wave graphic and revised lighting. Card-edge lighting was made consistent throughout the app.
- The native APO host filename is versioned for this release. The installer does not replace the same-version host DLL during a repeat installation, avoiding an unnecessary replacement of a DLL that may be loaded by Windows Audio.

## Install and prerequisites

Download `AudioTuneSetup-0.4.18.exe`. The offline setup contains the official Microsoft .NET 10 Desktop Runtime prerequisite and installs it only when needed. The AudioTune application directory contains no private .NET runtime. Equalizer APO is optional and needed only for system-wide processing; connect it to the intended playback endpoint from Devices.

Installer SHA-256: `A9B5F33478EA3087819B3F73C9570328FB8BAA81E944A3F701EEE0C8CD7AC7FC`.

Existing hearing profiles and correction presets under `%LOCALAPPDATA%\AudioTune` are preserved. A restart may still be necessary if Windows or Equalizer APO has pending audio-component changes.

## Scope and attribution

AudioTune is experimental software, not a medical audiometer. Measurements are relative dBFS values, not clinical dB HL. The FxSound engine is imported from the pinned upstream commit `d8e7a23d37ed5939c2a3090a1c1756c7f2500b17`; AudioTune is not affiliated with FxSound. The combined distribution is provided under AGPL-3.0-compatible terms. See [license](../LICENSE), [third-party notices](../THIRD_PARTY_NOTICES.md), and [FxSound module details](FXSOUND_MODULE.md).

The adapter and native host pass deterministic sample-exact tests against the imported engine. These tests do not prove that AudioTune's complete output is identical to the standalone FxSound application's virtual-device path. Interactive checks on the installed playback endpoint remain necessary.

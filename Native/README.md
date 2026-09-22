# Isolated FxSound module

`AudioTune.FxSound.Native` is a small C ABI around the pinned original FxSound
`DfxDsp` engine. It accepts interleaved IEEE float audio and exposes the five
FxSound controls as the original 0–10 values:

| AudioTune name | Original `DfxDsp::Effect` |
| --- | --- |
| Clarity | `Fidelity` |
| Ambience | `Ambience` |
| Surround | `Surround` |
| Dynamic Boost | `DynamicBoost` |
| Bass | `Bass` |

`AudioTune.FxSound.Compare` creates deterministic 48 kHz stereo input and
processes it through two independent engines: one calls `DfxDsp` directly and
one calls the AudioTune adapter. The test fails on any non-zero sample
difference. Run it with:

```powershell
.\build-native-fxsound.ps1 -Configuration Release
```

This test proves adapter transparency. It does not by itself prove parity with
the complete installed FxSound virtual-device path, which also includes device
routing and possible format conversion outside `DfxDsp`.

The module is built and packaged with AudioTune. The **Enhancements** page configures it.

`AudioTune.FxSound.Apo` is the native x64 system host. It implements the small VST2 binary surface consumed by Equalizer APO 1.4.2 and exposes six named normalized parameters: `Power`, `Clarity`, `Ambience`, `Surround`, `Dynamic`, and `Bass`. Equalizer APO loads it after AudioTune's L/R PEQ chain. The five effect values map from VST 0-1 to the original DfxDsp 0-10 scale.

`AudioTune.FxSound.Apo.Test` loads the DLL exactly like a host, verifies the ABI and parameter contract, proves sample-transparent `Power=0`, and compares processed planar output against direct interleaved `DfxDsp` output. The combined 6/4/5/7/6 scenario must have zero sample difference.

The installer stores a versioned host DLL under `%ProgramData%\AudioTune\Native` so a future update does not need to overwrite a DLL currently loaded by the Windows audio service. Development builds fall back to the DLL beside `AudioTune.exe`.

When the system host is active on the Listening Test output, `MusicPreviewService` skips its local `FxSoundSampleProvider`. This prevents applying the same effects twice.

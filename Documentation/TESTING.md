# Build and Regression Testing

## Required environment

- Windows x64
- .NET 10 SDK for source builds
- Windows PowerShell 5.1-compatible scripts
- Equalizer APO only required for persistent system-DSP tests

## Baseline build

```powershell
.\build.ps1
```

Expected output directory:

`AudioTune\bin\Debug\net10.0-windows\`

Run:

```powershell
.\run.ps1
```

`run.ps1` must return/close independently; AudioTune must remain open because it starts the WinExe with `Start-Process`.

## Release/publish

Framework-dependent Windows x64 single-file publish:

```powershell
.\publish.ps1
```

Offline installer build:

```powershell
.\build-installer.ps1
```

Expected installer:

`dist\AudioTuneSetup-0.4.18.exe`

`build-installer.ps1` downloads the official .NET 10 Desktop Runtime prerequisite and verifies its published SHA-512 hash. If Inno Setup 6 is not available, the script downloads the official signed compiler installer, validates the Pyrsys B.V. Authenticode signature, and installs it only in the repository-local `.tools` cache.

Packaging regression checks:

- `publish\win-x64` must contain `AudioTune.exe` and both reference JSON files;
- `publish\win-x64` must not contain private runtime files such as `coreclr.dll`, `hostfxr.dll`, or `System.Private.CoreLib.dll`;
- the setup must install the .NET 10 Windows Desktop Runtime only when a compatible major version is missing;
- an upgrade from an older self-contained alpha must remove stale runtime files from `{app}`;
- `%LOCALAPPDATA%\AudioTune` must remain untouched;
- perform a silent test installation into an isolated directory and start `AudioTune.exe` briefly.

## Minimum manual smoke test after every significant change

1. Launch application.
2. Open every navigation page once.
3. Verify Devices does not crash on construction.
4. Verify Profiles renders preset names, Quick Action icons and active badge.
5. Change top-bar active profile and confirm dependent status updates.
6. Open Settings and change a harmless setting; restart app and verify persistence.

## Profile safety regression

Use test data or backup real data first.

Verify:

- starting a new test creates a new profile;
- existing profiles remain listed;
- autosave works during partial test;
- rename does not change profile ID;
- protected copy gets a new ID;
- archive/unarchive does not delete JSON;
- export/import roundtrip works;
- restore backup restores the previous canonical profile version.

## Hearing-test regression

For a short developer run, do not necessarily complete all 60 points manually; use a test harness when available. Until then verify at least:

- repeating pulsed tone starts automatically;
- only selected ear receives signal;
- Heard moves toward lower level when unbracketed;
- Not Heard moves louder;
- high-level steps are 4 dB above -18 dBFS;
- two Not Heard responses at -3 dBFS create `NotDetectedAtCeiling`;
- after an answer, next stimulus starts without an extra Play click;
- pause/resume works;
- completed frequency can be individually retested;
- old point remains saved until retest completes.

## Automatic-outlier verification regression

Use synthetic/session test data once tests exist.

Verify:

- >=8 dB local outlier is flagged;
- maximum 3 per ear automatically queued;
- large disagreement triggers one extra verification only;
- no catch trials are added;
- ceiling-only verification does not get averaged into detected threshold;
- trial history and verification values are preserved.

## Correction-model deterministic tests to add

Create a unit-test project and cover at minimum:

1. ISO-shape interpolation anchors.
2. Global-offset invariance: adding a constant dB offset to all detected thresholds should not materially change residual correction shape.
3. 100% vs 200% intensity scaling and the fixed ±12 dB combined hearing-model + Fine Tune bound.
4. Fine Tune ON/OFF preserves stored points but changes target curve.
5. `NotDetectedAtCeiling` produces max positive target for requested intensity.
6. `NotDetectedAtCeiling` plus +6 dB Fine Tune produces +12 dB at 100% rather than skipping Fine Tune.
7. confidence weighting reduces low-confidence modeled gain.
8. stereo preservation limits midrange L/R difference while preserving common mean.
9. ceiling-side stereo-preservation exception.
10. centering trim never boosts a channel.

## PEQ fitting tests to add

For representative target curves:

- fitted composite response at band centers should match target within a defined tolerance;
- a +12 dB ceiling + Fine Tune target at 18 kHz must fit within 0.6 dB for both channels, including an asymmetric/sparse Fine Tune transition;
- 12.5/14/16/18 kHz boosts must not create the old ~18 dB stacking artifact;
- representative 14–18 kHz plateaus must remain within 1.25 dB inter-band ripple while retaining the 12.5 kHz transition;
- negative-only curve must produce required positive headroom = 0 dB;
- composite peak calculation must reflect signed sum, not sum of absolute gains;
- positive-boost limiter should reduce positive gains only;
- auto preamp should equal `-RequiredHeadroomDb` (no extra 0.5 dB).

## A/B vs APO equivalence – critical future automated test

Generate one `DspFilterSet` and compare:

- mathematical `DspFilterService.PeakingMagnitudeDb` cascade;
- NAudio `BiQuadFilter` cascade used by `CalibrationAbSampleProvider`.

Test at multiple frequencies and with filter gains >6 dB. Guard the shared ±12 dB per-filter range and detect any future A/B/APO response divergence.

## Fine Tune regression

Verify:

- test uses 1 kHz reference;
- frequencies: the same complete 30-point 30 Hz to 18 kHz set as the main hearing test, for 60 total L/R points;
- saved left/right points can be selected individually for review;
- starting a point review retains the previous value until Equal is accepted, and skipping the review leaves it unchanged;
- +/-1 dB controls;
- range ±6 dB;
- Accept stores point;
- master OFF retains data;
- target correction changes ON vs OFF;
- with Auto-apply ON and persistent active DSP, APO file changes immediately.

## Persistent DSP / auto-apply regression

This is high priority because it regressed repeatedly.

Setup:

1. Select target device configured in Equalizer APO.
2. Apply persistent DSP manually once.
3. Confirm top bar `ACTIVE`.

Then test Fine Tune:

1. Toggle OFF.
2. Do **not** open Devices or press Apply.
3. Inspect Equalizer APO managed device file / `AudioTune.txt`.
4. Confirm `# Fine Tune state: OFF`.
5. Confirm filter lines change when Fine Tune materially changes correction.
6. Confirm top bar returns to `ACTIVE`, not `UPDATE`.
7. Toggle ON and repeat.

Test intentional bypass:

1. Set level-matched bypass.
2. Change Fine Tune/intensity.
3. Auto-apply must **not** re-enable correction.

Test intensity:

- change 100 -> 150 -> 200%;
- persistent filters/signature should update;
- Profiles should only display intensity; slider remains Devices-only.

## Preamp/headroom regression

Test both automatic and manual modes.

Automatic:

- preamp = negative composite positive peak;
- no fixed extra 0.5 dB.

Manual:

- 0 dB allowed;
- warning shown when theoretical overshoot >0;
- with limiter OFF filters remain unscaled;
- with limiter ON positive filters scale to fit available headroom;
- negative filter gains remain unchanged.

## Stereo regression

Use centered mono vocal/pink-noise material.

- preservation ON should reduce frequency-dependent image wandering;
- preservation OFF should restore raw L/R target differences;
- Centering positive value attenuates left only;
- negative attenuates right only;
- changing/saving Centering auto-applies when configured.

## UI visual regression

Compare against assets in `AudioTune/Assets/DesignReferences`.

Check:

- Left=blue, Right=red in every relevant graph;
- no white unstyled ComboBoxes;
- no technical CLR type names in preset selectors;
- Active Profile badge is green when selected profile is active;
- Quick Actions have icons;
- Profiles contains no Unicode/font glyphs for profile type, preset statistics or Quick Actions;
- every Devices Signal Chain node and connector uses the shared vector-rendered LineIcon family;
- no cards clip at common desktop resolutions;
- Output Device text is legible;
- long device/profile names truncate gracefully rather than expanding layout uncontrollably;
- sidebar icons render from vector geometry and remain sharp at common DPI scales;
- the Dashboard headphone source has no visible rectangular image edge and its radial glow remains inside the hero composition;
- card, top-bar and sidebar contours remain distinct without clipping content;
- Dashboard Audio Processing OFF creates/retains level-matched bypass rather than deleting the profile;
- Dashboard Audio Processing ON applies the active profile/preset to the selected DSP target;
- Dashboard Fine Tune OFF retains all saved Fine Tune points;
- Dashboard Fine Tune changes respect Auto-apply and never re-enable an explicit persistent bypass.
- Dashboard Hearing Profile OFF removes only the modeled hearing contribution and preserves raw measurements.
- Dashboard Stereo Centering OFF applies zero channel trim while preserving the saved balance.
- Dashboard FxSound OFF preserves all five saved effect values.
- Calibration Review remains in the Processing Chain header and does not create a second card row.
- Listening check contains no duplicate Stereo Centering action.

## Native FxSound / Equalizer APO host regression

The native build must run both executables:

- `AudioTune.FxSound.Compare`: adapter vs direct DfxDsp, zero sample difference for neutral, all five isolated effects, and combined settings.
- `AudioTune.FxSound.Apo.Test`: exported `VSTPluginMain`, 2-in/2-out layout, named parameter contract, sample-transparent Power=0, and sample-exact combined output against direct DfxDsp.

Managed tests verify normalized parameter serialization and that `VSTPlugin:` is emitted after `Channel: ALL`. Also verify manually on a configured endpoint:

1. Enable Enhancements and Apply/Update DSP.
2. Confirm the device file contains `# FxSound native host: ON`, its signature, and the versioned ProgramData DLL path.
3. Play audio from another application and confirm each control changes the system output.
4. Open AudioTune Listening Test and confirm the log reports local processing bypassed because the system host is active.
5. Select level-matched bypass and confirm the VST line is absent.
6. Uninstall and confirm the managed include is removed from Equalizer APO without deleting `%LOCALAPPDATA%\AudioTune` profiles.

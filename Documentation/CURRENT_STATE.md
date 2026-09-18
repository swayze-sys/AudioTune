# Current State – v0.4.16-alpha

## Functional areas present

- Windows WPF shell with dark AudioTune design.
- Output-device selection.
- Active Hearing Profile selection in top bar.
- DSP status in top bar.
- 30 Hz–18 kHz threshold test, left/right.
- Repeating pulsed tones and one-click Heard/Not Heard workflow.
- Adaptive bracket threshold search.
- Autosave/resume.
- Targeted automatic verification of unusual local points.
- Manual individual-frequency retest.
- Saved Hearing Profiles with archive/backup/import/export.
- Separate Correction Presets.
- Correction model v5 using human threshold-shape prior.
- 0–200% correction intensity.
- Optional Fine Tuning with master ON/OFF.
- Stereo Image Preservation.
- Stereo Centering test.
- A/B music playback.
- Adjustable preamp/headroom and optional positive-boost limiting.
- Parametric EQ fitting with frequency-spacing-based Q.
- Actual generated DSP/APO response visualization.
- Equalizer APO detection/installation/configurator launching.
- Per-device persistent APO DSP.
- Level-matched persistent bypass.
- Auto-apply for Fine Tune/intensity/centering changes.
- Debug/log UI.
- Framework-dependent single-file publish with explicit bundled-runtime rejection.
- Offline Inno Setup package containing the verified .NET 10 Desktop Runtime prerequisite.
- App/taskbar icon.

## Current correction mode

**End-to-end / device-bound only.**

Headphone compensation is disabled.

## Current app data model versions

- HearingSession schema: 3
- CorrectionPreset schema: 2
- Test protocol: 3
- Correction model: 5
- App version: 0.4.16-alpha

## Current DSP model summary

```text
Raw thresholds
 -> ISO-shaped relative human prior
 -> common/interaural residual
 -> confidence/trust weighting
 -> saturating gain mapping
 -> Fine Tune (optional)
 -> correction intensity 0-200%
 -> Stereo Image Preservation
 -> Stereo Centering trim
 -> PEQ fitting
 -> preamp/headroom policy
 -> A/B + Equalizer APO
```

## Current UX direction

- Results: keep rich analysis layout.
- Devices: compact DSP control center, explanations behind info buttons.
- Profiles: compact profile manager with profile cards, preset card, Quick Actions and summary.
- Fine Tune: large ON/OFF master switch.
- Debug: useful but optional/collapsible.

## Immediate verification needs

1. Verify Profiles UI fixes from v0.4.16 interactively.
2. Verify Fine Tune auto-apply changes the actual APO file without manual Apply.
3. Fix/test A/B per-filter ±6 dB clamp mismatch for high correction intensity.
4. Add automated tests before further DSP expansion.

## Packaging status

The v0.4.16-alpha source now builds cleanly on Windows with 0 warnings and 0 errors. The setup build bootstraps its signed Inno Setup compiler when absent, embeds the official .NET 10 Desktop Runtime prerequisite with SHA-512 verification, and keeps private .NET runtime files out of the installed AudioTune program directory.

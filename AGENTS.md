# AudioTune – Codex / Developer Instructions

This file is authoritative for automated coding agents working in this repository.

## Read before changing code

Read these files in order before making non-trivial changes:

1. `Documentation/START_HERE_CODEX.md`
2. `Documentation/PROJECT_CONTEXT.md`
3. `Documentation/ARCHITECTURE.md`
4. `Documentation/HEARING_TEST.md`
5. `Documentation/DSP_MODEL.md`
6. `Documentation/PROFILE_MODEL.md`
7. `Documentation/EQUALIZER_APO.md`
8. `Documentation/UI_DESIGN.md`
9. `Documentation/DECISIONS.md`
10. `Documentation/KNOWN_ISSUES.md`
11. `Documentation/TESTING.md`
12. `Documentation/ROADMAP.md`

Do not assume prior chat context exists. The repository documentation is the source of truth for project intent.

## Baseline

- Handoff baseline: **AudioTune v0.4.16-alpha**.
- Date of handoff: **2026-09-18**.
- The exact v0.4.16 source in this repository was statically checked during handoff, but this exact version has **not yet been user-confirmed as a clean Windows build** after the final Profiles UI polish. Build first.
- Do not make feature changes until the baseline builds cleanly.

## Platform and technology constraints

- Windows desktop only for now.
- C# / WPF.
- Target framework: `.NET 10` / `net10.0-windows`.
- Audio: NAudio 3.1.0, WASAPI shared mode.
- Persistent system-wide DSP: Equalizer APO.
- Do **not** migrate to WinUI, Avalonia, MAUI, Electron, web UI, or another framework unless explicitly requested.
- macOS porting was discussed and deliberately postponed.
- All PowerShell scripts must remain **Windows PowerShell 5.1 compatible**.

## Product invariants

These are intentional design decisions, not temporary accidents:

- Raw hearing measurements are valuable source data and must never be silently overwritten by tuning changes.
- A **Hearing Profile** and a **Correction Preset** are separate concepts.
- Starting a new hearing test must not delete or overwrite older profiles.
- Existing `%LOCALAPPDATA%\AudioTune` user data must remain backward-compatible whenever practical.
- The current calibration mode is **device-bound end-to-end**: output chain + headphone + fit + ear + hearing.
- Headphone frequency-response compensation is deliberately **disabled/postponed** for now.
- Do not introduce age-dependent hearing priors. They were researched and deliberately rejected for the current product direction.
- Do not add catch trials to the hearing test.
- `NotDetectedAtCeiling` measurements are **not ignored**. Current policy is to request the maximum positive correction allowed by the current preset strength.
- Left ear UI color = blue. Right ear UI color = red. Preserve this everywhere.
- Fine Tune must be independently switchable ON/OFF without deleting its stored measurements.
- Fine Tune ON/OFF is intended to auto-apply immediately to an already configured persistent DSP when `Auto-apply DSP changes` is enabled.
- A user-selected level-matched bypass must never be silently re-enabled by auto-apply.
- A/B playback and Equalizer APO are intended to use the same DSP filter set.
- Stereo Image Preservation exists specifically to prevent frequency-dependent L/R corrections from destabilizing the phantom center.
- Stereo Centering is a post-EQ trim and attenuates one channel only; it must never add channel gain.
- Manual preamp values up to 0 dB are intentionally allowed, with clipping warnings rather than forced intervention.
- Optional positive-boost limiting may constrain boosts to available headroom; negative cuts must not be treated as clipping risk.
- Correction intensity is user-adjustable from 0–200%, with 100% as the default/reference setting. The control belongs on **Devices**, not Profiles.

## Workflow rules for coding agents

1. Build the current source first:
   ```powershell
   .\build.ps1
   ```
2. If the build fails, fix the build before implementing requested features.
3. After every code change:
   - run `dotnet build` / `build.ps1`;
   - run available tests;
   - inspect warnings introduced by the change;
   - verify XAML event handlers and resource paths.
4. For DSP changes, add or update deterministic automated tests before declaring the change complete.
5. For profile/schema changes, preserve old profile loading and document migration behavior.
6. For persistent DSP changes, verify the generated Equalizer APO text, the status parser, and auto-apply behavior together.
7. For UI changes, compare against the reference images in `AudioTune/Assets/DesignReferences/`.
8. Do not leave a mock/stub implementation in place of functionality that already exists.
9. Do not claim a behavior is verified unless it was actually built/tested on Windows or covered by an automated test.

## Safety / audio-specific rules

- AudioTune is not a medical audiometer and does not produce calibrated dB HL values.
- Hearing thresholds are stored as relative digital levels in dBFS.
- Do not label the output as a diagnosis or clinical hearing-loss measurement.
- Avoid sudden full-scale test tones. Preserve fades and bounded test levels.
- The hearing-test ceiling is currently `-3 dBFS`; application session volume is locked to 50%, while the Windows endpoint/master volume is observed but not changed.
- Do not silently raise Windows master volume.

## Architecture rule of thumb

Keep this separation intact:

`MEASURE -> MODEL -> TUNE -> VALIDATE -> APPLY`

- **MEASURE**: raw thresholds/trials/verification.
- **MODEL**: human-sensitivity prior, confidence, common/interaural residuals.
- **TUNE**: correction preset, intensity, Fine Tune, stereo preservation/centering.
- **VALIDATE**: graphs and A/B listening.
- **APPLY**: fitted PEQ + preamp -> Equalizer APO.

If a proposed change blurs these layers, document why before implementing it.

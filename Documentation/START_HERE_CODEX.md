# Start Here – Codex Handoff

## Purpose of this handoff

AudioTune was developed iteratively through many small implementation/test cycles. The project now contains substantial product decisions that are not obvious from the code alone. This document is designed so a fresh Codex session can continue development without relying on the original chat history.

The repository itself should be treated as the durable project memory.

## Baseline

- Version: **0.4.16-alpha**
- Handoff date: **2026-09-18**
- Platform: Windows x64
- UI: WPF
- Runtime: .NET 10
- Audio: NAudio 3.1.0 / WASAPI shared
- Persistent DSP: Equalizer APO
- Current reference headphone in the UI/data model: Beyerdynamic Amiron Home
- Current calibration mode: **device-bound end-to-end**

This exact v0.4.16 source was packaged after UI changes to Profiles. The Windows build now completes with 0 warnings and 0 errors, and the packaged installer has passed an isolated install/start/uninstall smoke test. The full interactive page and Equalizer APO regression matrix still needs to be completed.

## First Codex session – required sequence

### 1. Read project instructions

Read `AGENTS.md` and every Markdown file under `Documentation/` before editing code.

### 2. Inspect the current tree

Focus first on:

- `AudioTune/Models/HearingMeasurement.cs`
- `AudioTune/Models/AppSettings.cs`
- `AudioTune/Services/HearingTestEngine.cs`
- `AudioTune/Services/HumanSensitivityModel.cs`
- `AudioTune/Services/CorrectionPreviewService.cs`
- `AudioTune/Services/DspFilterService.cs`
- `AudioTune/Services/SystemDspService.cs`
- `AudioTune/Services/FineTuneEngine.cs`
- `AudioTune/Services/ProfileRepository.cs`
- `AudioTune/Services/CorrectionPresetRepository.cs`
- `AudioTune/Services/MusicPreviewService.cs`
- `AudioTune/Services/CalibrationAbSampleProvider.cs`
- `AudioTune/Views/DevicesView.xaml(.cs)`
- `AudioTune/Views/ProfilesView.xaml(.cs)`
- `AudioTune/Views/ResultsView.xaml(.cs)`
- `AudioTune/MainWindow.xaml(.cs)`

### 3. Build before changing anything

From repository root in Windows PowerShell:

```powershell
.\build.ps1
```

If it fails, fix the baseline build first. Do not mix baseline compile fixes with feature work in one conceptual change unless unavoidable.

### 4. Run baseline

```powershell
.\run.ps1
```

The script starts the compiled `AudioTune.exe` as an independent process so closing PowerShell must not close AudioTune.

### 5. Verify user data is preserved

AudioTune user data is outside the repository under `%LOCALAPPDATA%\AudioTune`. Never use repository cleanup commands to delete this directory.

Before schema experiments, use:

```powershell
.\backup-profiles.ps1
```

### 6. Summarize understanding before major refactors

Before architecture work, write a short internal/review summary covering:

- hearing-test state machine;
- Hearing Profile vs Correction Preset;
- correction model v6;
- Fine Tune ON/OFF behavior;
- Stereo Image Preservation and Centering;
- DSP filter fitting/headroom;
- Equalizer APO per-device persistence and auto-apply;
- UI design constraints;
- known issues in `KNOWN_ISSUES.md`.

## Suggested first prompt for a fresh Codex chat

Use this verbatim or nearly verbatim:

> Open the AudioTune repository. Read AGENTS.md and all Markdown files under Documentation before modifying anything. Build the current v0.4.16-alpha baseline first and fix only baseline compile/runtime issues if needed. Then summarize your understanding of the hearing-test protocol, profile/preset separation, correction model v6, Fine Tune, stereo preservation/centering, DSP filter fitting, Equalizer APO persistence/auto-apply, and the UI design rules. Compare the documentation against the implementation and list any inconsistencies. Do not make architectural changes until that review is complete.

## Definition of a safe continuation

A change is not complete merely because the UI looks correct. For AudioTune, a feature can cross several layers:

`UI -> Settings/Preset -> Correction model -> PEQ fitting -> APO config -> Status parser -> A/B playback`

For example, Fine Tune ON/OFF previously changed the UI but did not reliably change the persistent APO output. New work must trace the full path.

## What must not be reconstructed from memory

The following are explicitly documented and should not be guessed:

- hearing test frequency list and level limits;
- profile schema and user-data paths;
- correction algorithm constants;
- Equalizer APO file layout;
- visual reference images;
- decisions not to use catch trials, age weighting, or headphone compensation yet;
- current 0–200% intensity behavior;
- preamp/headroom policy;
- stereo-preservation policy.

Use the code and this documentation as the canonical reference.

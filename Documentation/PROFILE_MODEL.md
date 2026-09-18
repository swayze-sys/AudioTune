# Hearing Profiles and Correction Presets

## Core rule

A **Hearing Profile** is measurement data.

A **Correction Preset** is a tunable interpretation of that measurement.

They must remain separate.

This separation was introduced so a long hearing test does not need to be repeated whenever correction algorithms, intensity or Fine Tune preferences change.

## HearingSession

Defined in `Models/HearingMeasurement.cs`.

Current schema version: 3.

Important fields:

- `Id`
- `Name`
- `StartedAt`, `UpdatedAt`, `CompletedAt`
- `HeadphoneId`
- `OutputDeviceId`, `OutputDeviceName`
- `ApplicationSessionVolumePercent`
- `EndpointMasterVolumePercentAtStart`
- `SampleRateHz`
- `AudioApi`
- `MeasurementNotes`
- `IsArchived`
- `Measurements`

Metadata fields:

- `AppVersion`
- `TestProtocolVersion`
- `CorrectionAlgorithmVersionAtMeasurement`

Legacy field:

- `CorrectionStrengthPercent` remains for loading/migrating old alpha profiles; new intensity belongs in CorrectionPreset.

## HearingMeasurement

One frequency/ear point.

Fields include:

- `FrequencyHz`
- `Ear`
- `ThresholdDbFs`
- `InitialThresholdDbFs`
- `Status`
- `Presentations`
- `Trials`
- verification arrays
- automatic-outlier flag
- verification-completed flag
- confidence

### Status values

- `Detected`
- `NotDetectedAtCeiling`
- `Skipped`

### Trial history

Each `HearingTrial` stores:

- tested `LevelDbFs`
- `Heard` boolean
- timestamp

Do not discard trial history in migrations.

## CorrectionPreset

Current schema version: 2.

Fields:

- `Id`
- `HearingProfileId`
- `Name`
- `StrengthPercent` (0–200)
- `FineTuneEnabled`
- `FineTuneAdjustments`
- `StereoCenterBalanceDb`
- timestamps

A profile may have multiple correction presets.

## FineTuneAdjustment

Stores:

- frequency;
- ear;
- adjustment dB;
- timestamp.

Fine Tune master OFF preserves these values.

## Active profile and active preset

Stored in `AppSettings` as IDs:

- `ActiveHearingProfileId`
- `ActiveCorrectionPresetId`

Changing the active Hearing Profile should ensure a default Correction Preset exists for that profile.

## Persistence locations

### Hearing profiles

`%LOCALAPPDATA%\AudioTune\Profiles`

Canonical naming:

`profile-<GUID-without-dashes>.json`

The repository also recognizes/migrates older names such as `hearing-*.json` and `current-session.json`.

### Correction presets

`%LOCALAPPDATA%\AudioTune\CorrectionPresets`

Naming:

`preset-<GUID-without-dashes>.json`

### Settings

`%LOCALAPPDATA%\AudioTune\settings.json`

## Atomic saves and backups

Profile saves use a temporary file and preserve a `.bak` copy of the previous canonical file where possible.

Important product requirement:

> A new test must never overwrite an old profile.

Profiles can also be saved as protected copies from the Profiles UI.

## Archive behavior

Profiles can be archived rather than immediately deleted. Archive is preferred because hearing measurements are expensive/time-consuming to recreate.

Do not make deletion the primary action.

## Import/export

Hearing profiles can be exported/imported as JSON.

On import, if the ID conflicts with an existing profile, AudioTune generates a new ID.

## Legacy normalization

`ProfileRepository.NormalizeLegacySession` currently:

- gives unnamed sessions a generated name;
- fills `UpdatedAt` from file timestamp when missing;
- advances schema version to at least 3;
- sets missing `InitialThresholdDbFs`;
- assigns medium confidence to old detected points when confidence is unknown;
- initializes missing trial/verification collections.

Keep migration tolerant: alpha versions have changed schemas several times.

## Preset migration

Correction preset schema v2 introduced `FineTuneEnabled`.

Old presets are migrated with Fine Tune enabled to preserve historical behavior.

## Device-bound meaning

Current Hearing Profiles are device-bound because there is no separate verified headphone correction.

A profile's output device metadata should be considered when applying it to DSP. Applying to another endpoint should produce a warning/explicit override rather than silently assuming equivalence.

## Data invariants for future schema changes

1. Never rewrite raw threshold values merely because the correction algorithm changes.
2. Preserve `NotDetectedAtCeiling` as a distinct state.
3. Preserve individual trials and verification history.
4. Preserve device/audio-path metadata.
5. Keep profile and preset IDs stable through normal saves.
6. Migration should be monotonic and non-destructive.
7. Add explicit schema versions when adding durable fields.
8. Update this document and `KNOWN_ISSUES.md` when migration semantics change.

# Data, Privacy and Local Storage

## Local-first design

AudioTune currently stores its working data locally. There is no application account/cloud backend in the current project.

## User data paths

Base directory:

`%LOCALAPPDATA%\AudioTune`

Expected content includes:

```text
AudioTune\
├── settings.json
├── Profiles\
├── CorrectionPresets\
└── Installers\
```

Exact legacy profile file names may vary because the repository supports older alpha formats.

## Profiles

Hearing profiles contain personal hearing-test measurements and should be treated as user data.

They may include:

- frequency/ear thresholds;
- trial history;
- verification data;
- confidence;
- output-device metadata;
- Windows master volume observed at measurement time;
- timestamps/notes.

Do not commit a user's real `%LOCALAPPDATA%\AudioTune` data into a public repository.

For automated tests, create synthetic/anonymized fixtures.

## Correction presets

Correction preset JSON contains tuning preferences tied to a Hearing Profile:

- intensity;
- Fine Tune offsets;
- Fine Tune ON/OFF;
- stereo-centering trim.

## Backup behavior

Profile saves preserve `.bak` files where possible. `backup-profiles.ps1` exists for additional manual backup before risky development/migrations.

## Repository data

The repository contains non-personal reference metadata:

- headphone source metadata;
- psychoacoustic source metadata;
- design screenshots/assets.

## External network use

The current application only needs internet access for the optional integrated Equalizer APO installer download.

Equalizer APO installer behavior:

- downloads official x64 installer from SourceForge;
- verifies hard-coded SHA-256;
- launches installer with UAC.

No remote hearing-profile upload is implemented.

## Equalizer APO files

AudioTune writes persistent DSP files under Equalizer APO's installation/config directory. These are not personal profiles in the same schema, but they contain:

- profile/preset IDs/names in comments;
- filter parameters;
- endpoint GUID/name;
- Fine Tune state;
- intensity/algorithm metadata.

Be mindful of this when collecting debug bundles.

## Uninstall behavior

The Inno Setup script intentionally does not remove `%LOCALAPPDATA%\AudioTune` user data during normal application uninstall.

This is deliberate to avoid destroying difficult-to-recreate hearing measurements.

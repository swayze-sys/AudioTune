# User Workflow

This describes the intended normal workflow from a product perspective.

## Initial setup

1. Launch AudioTune.
2. Select the Windows **Output Device** in the top bar.
3. Current reference headphone UI is Beyerdynamic Amiron Home.
4. If persistent system DSP is desired, install/configure Equalizer APO from Devices.

## Create a Hearing Profile

1. Open Hearing Test.
2. Start a new test rather than editing an old profile if measuring a genuinely new setup/session.
3. AudioTune locks its own audio session to 50%.
4. For each repeated pulsed tone, answer Heard or Not Heard.
5. AudioTune automatically advances level/frequency.
6. Left and right ears are measured separately.
7. At end, suspicious local outliers may be automatically verified.
8. Profile is autosaved continuously and finalized when complete.

## Retest individual points

For an existing completed profile:

1. Open profile / Hearing Test edit mode.
2. Select the ear.
3. Select/click a frequency point.
4. Retest only that point.
5. Existing saved value remains until replacement measurement completes.

Use this when a point looks implausible or a legacy high-frequency result is ambiguous.

## Correction Preset

A completed Hearing Profile has at least one Correction Preset.

The default is `Personal correction`.

Preset stores:

- correction intensity;
- Fine Tune state/data;
- Stereo Centering trim.

## Optional Fine Tuning

1. Open Fine Tuning from Results/Profiles/navigation.
2. Fine Tune can be master ON/OFF without losing data.
3. AudioTune alternates 1 kHz reference and a test frequency in one ear.
4. Make the test signal louder/quieter until it appears approximately equal.
5. Save/continue.
6. Repeat selected frequency set for both ears.

If persistent DSP exists and Auto-apply is ON, Fine Tune master changes should update APO immediately.

## Optional Stereo Centering

Use when centered voices/noise appear biased left/right after correction.

1. Start Stereo Centering.
2. Listen to corrected centered test noise.
3. Adjust until perceived center is centered.
4. Save.

The trim attenuates one side only.

## Correction Intensity

Configured under Devices only.

- 0% = off/no frequency-dependent correction.
- 100% = normal model strength.
- up to 200% = stronger correction.

Changing intensity may automatically update persistent DSP when Auto-apply is enabled.

## A/B Music Validation

1. Open A/B Listening Test.
2. Load a familiar stereo music file.
3. Playback continues at the same position while switching Original/Calibrated.
4. Both paths share the same preamp/headroom to avoid simple loudness bias.
5. Use known vocal/bass/treble material to judge tonal balance and image stability.

## Apply persistent Windows DSP

Under Devices:

1. Select DSP Target Device.
2. Ensure Equalizer APO is installed and registered on that endpoint.
3. Apply / Update DSP.
4. Persistent config remains active after AudioTune closes/reboots.

Available actions:

- Apply / Update DSP
- Bypass DSP (level-matched)
- Disable DSP for target
- Open Equalizer APO Configurator

## Interpret top-bar DSP state

- `ACTIVE` – stored persistent DSP matches active settings.
- `BYPASS` – level-matched bypass active.
- `UPDATE` – persistent DSP exists but settings/signature changed.
- `OFF` – no active matching persistent correction.

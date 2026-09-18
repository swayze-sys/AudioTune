# Project Context

## What AudioTune is

AudioTune is a Windows desktop application for **personal, device-bound headphone calibration**. It measures the user's hearing threshold separately for left and right ears across 30 Hz–18 kHz, stores the raw measurements, derives a personal correction, optionally refines it with moderate-level loudness matching, validates it with A/B music playback, and can apply the same correction persistently to a Windows playback endpoint through Equalizer APO.

AudioTune is not intended to be a clinical audiometer. Its threshold measurements are digital levels in **dBFS**, not calibrated sound-pressure levels or dB HL.

## Core product idea

The practical target is not “measure the headphone in isolation” and not “diagnose hearing loss.” It is:

> Measure and tune the complete chain the user actually hears.

Current chain:

`Windows output path + DAC/sound card + headphone + fit/coupling + ear geometry + hearing`

This is why the current calibration is called **device-bound end-to-end**.

## Why headphone compensation is currently disabled

A separate headphone correction layer was considered early. Traceable measurement sources exist for the Beyerdynamic Amiron Home, but a numeric source curve has not been imported and cross-validated. Rather than encode manually approximated or uncertain response data, headphone compensation is explicitly disabled for now.

This means a hearing profile is tied to the measured output/headphone chain. If the user changes headphones or the relevant playback chain, the safe default is to remeasure.

Do not silently introduce a headphone correction database unless the feature is explicitly resumed and traceable numeric measurement data is available.

## Product workflow

The intended user journey is:

1. Select a Windows output device.
2. Run the threshold hearing test.
3. Save the raw Hearing Profile permanently.
4. AudioTune creates/uses a Correction Preset for that profile.
5. Optionally run Fine Tuning.
6. Optionally run Stereo Centering.
7. Validate using A/B music playback.
8. Apply the correction to a chosen Windows DSP target through Equalizer APO.
9. Keep persistent DSP active across restarts without requiring AudioTune to run.

## Five conceptual layers

### MEASURE

Store what happened during the hearing test:

- threshold result;
- status (`Detected`, `NotDetectedAtCeiling`, `Skipped`);
- every Heard/Not Heard trial;
- automatic verification results;
- confidence;
- output-device and audio-path metadata.

Raw measurements must remain available for future reinterpretation.

### MODEL

Interpret raw threshold data using:

- frequency-dependent human threshold-shape prior;
- common L/R residual;
- interaural residual;
- confidence/trust weighting;
- saturation rather than direct threshold-dB-to-EQ-dB mapping.

### TUNE

A Correction Preset controls interpretation without altering the Hearing Profile:

- correction intensity 0–200%;
- Fine Tune data and master ON/OFF;
- stereo centering trim.

Global DSP settings include preamp/headroom and stereo preservation.

### VALIDATE

The user should be able to understand and compare:

- raw thresholds;
- desired correction curve;
- correction with/without Fine Tune;
- actual generated APO/DSP transfer;
- A/B music playback at matched preamp/headroom.

### APPLY

AudioTune fits the target correction to parametric EQ filters and writes a persistent per-device Equalizer APO configuration.

## Current design principles

- Modern dark/black/navy UI.
- Blue/cyan accents.
- Left ear = blue.
- Right ear = red.
- Prefer compact cards and status chips over large explanatory text blocks.
- Development/debug data should remain available but be optional/collapsible.
- Devices is the DSP control center.
- Profiles manages measurements and presets but should not duplicate the correction-intensity slider.
- Top bar exposes current output device, active profile and DSP state.

## Explicitly postponed / rejected directions

### macOS port

Possible, but postponed. WPF, WASAPI and Equalizer APO are Windows-specific. Do not start a cross-platform migration without an explicit decision.

### Age-dependent correction prior

ISO 7029 age-related threshold statistics were researched. The decision was **not** to incorporate age into the current correction model. Age could be a statistical prior, but the user already performs an individual threshold measurement; adding age risks complexity without enough practical benefit.

### Catch trials

Rejected. They can lengthen the hearing test and may make a user think playback is broken. Quality control is handled instead by targeted verification of suspicious local outliers and manual point retesting.

### Automatic Windows master-volume control

Rejected. AudioTune locks only its own Windows audio session to a reference volume. Windows master/endpoint volume is observed and stored, not changed, to avoid making system notifications or other applications unexpectedly loud.

## Reference hardware used during development

The main real-world test chain during current development has included:

- Beyerdynamic Amiron Home headphone;
- a Windows playback endpoint displayed as `Lautsprecher (2- ASUS Essence STX II Audio Device)`.

Do not hard-code this endpoint. It is a development/reference device only.

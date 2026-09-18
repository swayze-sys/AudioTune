# Source / Reference Metadata

This file records external technical references already represented by repository metadata. It is not a bibliography for medical advice.

## Human sensitivity prior

Repository metadata:

`AudioTune/Data/Psychoacoustics/iso226.sources.json`

Reference:

- ISO 226:2023 – normal equal-loudness-level contours
- official metadata URL: `https://www.iso.org/standard/83117.html`

AudioTune use:

- relative threshold-shape prior only;
- no absolute SPL calibration;
- standardized data used through 12.5 kHz;
- no age-dependent ISO 7029 prior in current model.

Note: the JSON metadata's `correctionAlgorithmVersion` value is stale relative to current model v5 and should be updated.

## Beyerdynamic Amiron Home

Repository metadata:

`AudioTune/Data/Headphones/beyerdynamic-amiron-home.sources.json`

Known sources recorded:

- SoundStage! Network measurement using a G.R.A.S. 43AG fixture;
- oratory1990 measurement/preset collection.

Current policy:

`referenceCorrectionEnabled = false`

No numeric frequency-response curve has been ingested/cross-checked, so these sources are informational only in the present product.

## Equalizer APO

`EqualizerApoInstallerService` currently downloads official Equalizer APO 1.4.2 x64 from SourceForge and verifies SHA-256 before launch.

Current hash:

`7403be7427bbe1936a40dded082829b6e217fc4f5990fee5cba501f0ae055afa`

Do not change installer URL/version/hash independently.

## DSP equations

PEQ response math in `DspFilterService.PeakingMagnitudeDb` follows the RBJ Audio EQ Cookbook peaking-EQ family, matching the filter family used by NAudio's `BiQuadFilter.PeakingEQ` and Equalizer APO PK filters.

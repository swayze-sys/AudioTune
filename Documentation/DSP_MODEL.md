# DSP / Correction Model

## Status

Current correction model: **v5** (`CorrectionPreviewService.AlgorithmVersion = 5`).

This model is experimental. It is designed to produce a useful personal listening correction from relative threshold measurements, not a clinical prescription.

## Why the original median model was replaced

Early AudioTune versions used roughly:

`gain(f) = 0.35 * (threshold(f) - median(thresholds))`

That was intentionally conservative but conceptually weak because it treated the normal frequency dependence of human hearing as if it were an individual deficit. It tended to overinterpret low-frequency threshold differences.

The current model still uses a median, but only to align the unknown dBFS measurement scale to a **frequency-dependent reference shape**.

## Human sensitivity prior

`HumanSensitivityModel` contains ISO 226:2023 threshold-shape anchors through 12.5 kHz.

Important: AudioTune uses only the **relative shape**, normalized to 1 kHz. It does not treat these values as calibrated SPL at the user's ear.

For a measured frequency `f`:

```text
relativeReference(f) = ISO-shaped threshold(f) - ISO-shaped threshold(1 kHz)
```

The global unknown offset is estimated as:

```text
globalOffset = median(measuredThreshold - relativeReference)
```

using detected measurements at <=12.5 kHz.

Then:

```text
expectedThreshold(f) = globalOffset + relativeReference(f)
ownResidual(f)       = measuredThreshold(f) - expectedThreshold(f)
```

This makes the median an alignment parameter rather than a flat-hearing assumption.

## Population trust by frequency

Because ISO 226 is a population/free-field reference while AudioTune measures a headphone/ear chain, the reference is deliberately down-weighted at the extremes.

Current `GetPopulationTrust` behavior:

- <=30 Hz: 0.55
- 30–80 Hz: smoothly rises 0.55 -> 1.0
- 80 Hz–10 kHz: 1.0
- 10–12.5 kHz: 0.8
- 12.5–18 kHz: falls toward 0.15

The 12.5 kHz reference anchor is held above the standardized range; trust is reduced instead of extrapolating a made-up ISO curve.

## No age weighting

Age-dependent ISO 7029 statistics were researched and intentionally not integrated. The individual hearing test is the main evidence source. Do not add age weighting unless the product direction changes explicitly.

## Common vs interaural residual

When both ears have a detected value at the same frequency:

```text
commonResidual     = (leftResidual + rightResidual) / 2
interauralResidual = (ownResidual - otherResidual) / 2
```

The rationale:

- common residual is more entangled with population prior, headphone response and coupling;
- L/R difference is relatively strong individual evidence because both ears share the same nominal playback chain/headphone model.

## Saturating conversion from threshold residual to EQ gain

AudioTune does **not** use 1 dB threshold difference = 1 dB music EQ.

Current mapping:

```text
commonGain     = 4.5 * tanh(commonResidual / 10)
interauralGain = 3.5 * tanh(interauralResidual / 7)
```

This limits the effect of extreme threshold values.

## Measurement-confidence weighting

Current factors:

- High: 1.00
- Medium: 0.85
- Low: 0.55
- Unknown: 0.95 if verified, otherwise 0.85

The modeled gain is multiplied by this trust.

## Population/interaural combination

Current implementation:

```text
interauralTrust = 0.60 + 0.40 * populationTrust
modeledGain = commonGain * populationTrust
            + interauralGain * interauralTrust
modeledGain *= measurementTrust
```

This is empirical/experimental, not a clinical fitting formula.

## NotDetectedAtCeiling policy

This is an explicit product decision.

A `NotDetectedAtCeiling` point is interpreted as “threshold is beyond the measurable test range,” not “no data.” It therefore receives:

```text
MaxGainDb * strength
```

where `MaxGainDb = 6 dB` and strength is 0.0–2.0.

Examples:

- intensity 50% -> +3 dB
- 100% -> +6 dB
- 200% -> +12 dB

Do not filter these points out unless the policy is explicitly revisited.

## Correction intensity

Stored in `CorrectionPreset.StrengthPercent`.

Range: **0–200%**.

- 0% = no frequency-dependent correction from the model/preset
- 100% = reference/current model strength
- 200% = double strength

The user-facing slider belongs on **Devices**. Profiles may display the value but should not duplicate the slider.

For detected points, Fine Tune and modeled correction are combined and then intensity is applied:

```text
finalGain = (modeledGain + fineTune) * strength
```

Clamp behavior:

- at strength <=100%, target curve is bounded to ±6 dB;
- above 100%, bound expands up to ±12 dB at 200%.

## Fine Tuning

Fine Tune is optional moderate-level loudness matching.

Test frequencies per ear:

`63, 125, 250, 500, 2000, 4000, 8000, 12500 Hz`

Reference: 1 kHz.

Current Fine Tune parameters:

- base level: -36 dBFS before baseline correction;
- 1 dB adjustment step;
- adjustment range ±6 dB;
- reference and test alternate automatically;
- test is left/right separately.

Fine Tune offsets are interpolated logarithmically **only within the measured Fine Tune frequency range**. They are not extrapolated below 63 Hz or above 12.5 kHz.

### Fine Tune master switch

`CorrectionPreset.FineTuneEnabled` controls whether stored Fine Tune adjustments participate in correction/DSP.

OFF must not delete `FineTuneAdjustments`.

With Auto-apply enabled and a persistent non-bypassed DSP already configured, Fine Tune ON/OFF is intended to rewrite the Equalizer APO configuration immediately.

## Stereo Image Preservation

This stage runs **after threshold model + Fine Tune + intensity**.

Purpose: prevent independent L/R frequency corrections from moving vocals/phantom center unpredictably.

Only the differential L/R component is smoothed/limited. The common tonal correction is preserved.

Default setting:

`MaxInterauralCorrectionDifferenceDb = 2.0 dB`

Frequency-dependent limit:

- 150 Hz–5 kHz: base limit (default 2 dB total L/R difference)
- <150 Hz: 1.5× base limit
- 5–8 kHz: smoothly relaxes toward 1.5×
- >=8 kHz: 2× base limit

The differential curve uses light neighbor smoothing:

- interior: 0.25 previous + 0.50 current + 0.25 next
- ends: 0.75 current + 0.25 neighbor

### Ceiling exception

If one ear has `NotDetectedAtCeiling` and requests maximum correction, Stereo Image Preservation keeps that side's high requested boost and pulls the opposite channel closer when needed. It does not reduce the ceiling-side boost merely to satisfy the interaural limit.

## Stereo Centering

Fine spectral correction can still leave a small perceived center offset.

The optional Stereo Centering test uses corrected band-limited noise (~300 Hz–4 kHz) and stores `CorrectionPreset.StereoCenterBalanceDb`.

Current trim policy:

- positive balance moves image right by attenuating left;
- negative moves image left by attenuating right;
- range is clamped to ±3 dB;
- no channel is ever boosted by centering.

Centering is applied after EQ.

## Target correction curve vs actual DSP

Do not confuse these:

### Target curve

Produced by `CorrectionPreviewService`.

This is what AudioTune wants the correction to look like.

### Fitted DSP curve

`DspFilterService` approximates the target using parametric EQ filters.

Filter center frequencies:

`31.5, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 12500, 14000, 16000, 18000 Hz`

Target gains at these bands are interpolated from the target curve and clamped to ±12 dB.

## PEQ fitting

Older versions copied each target point directly into an overlapping PEQ filter, causing large composite peaks (e.g. multiple +6 dB treble filters stacking toward ~18 dB).

Current implementation performs iterative residual fitting:

- Q derived from logarithmic spacing between bands;
- Q clamped 0.70–10;
- 8 Gauss-Seidel-like passes;
- damping 0.70;
- each filter gain clamped ±12 dB.

The intent is that the **composite** PEQ response matches the target points rather than each filter gain individually equaling the target value.

## Q explained

Q is the filter's quality factor / effective bandwidth.

- lower Q = wider filter;
- higher Q = narrower filter.

Tightly packed 12.5/14/16/18 kHz centers therefore get higher Q than octave-spaced low/mid bands, reducing unintended stacking.

## Composite peak / headroom

AudioTune estimates the positive peak of the **complete signed filter cascade**, not the largest individual filter.

`CalculateCompositePeakDb` samples 2048 logarithmically spaced frequencies from 20 Hz to roughly 20 kHz.

Negative cuts remain negative. They do **not** count as clipping risk and may reduce a nearby positive composite peak.

### Automatic preamp

When `UseAutomaticPreamp = true`:

```text
AppliedPreampDb = -CompositePositivePeakDb
```

There is currently no extra fixed -0.5 dB safety margin. That margin was deliberately removed.

### Manual preamp

The user can select a less negative preamp, including 0 dB. AudioTune warns about theoretical digital overshoot but does not forcibly stop the user.

Current internal clamp supports down to -18 dB, while the Settings UI currently presents a -12…0 dB slider. Keep this discrepancy visible in `KNOWN_ISSUES.md` unless normalized later.

### Optional positive-boost limiter

If enabled and the selected manual preamp provides less headroom than the composite peak, only positive PEQ filter gains are scaled down until the cascade fits the available headroom. Negative cuts remain unchanged.

## A/B playback

The A/B music path is intended to use the same `DspFilterSet` as Equalizer APO.

Both dry and corrected paths use the same preamp/headroom so a simple level jump does not bias the comparison. Switching uses a short crossfade.

**Known implementation caveat:** `CalibrationAbSampleProvider` currently clamps each NAudio peaking filter gain to ±6 dB when constructing `BiQuadFilter`s, while fitted APO filters may be as large as ±12 dB at >100% intensity. This can make A/B diverge from APO for strong corrections. See `KNOWN_ISSUES.md`.

The provider also clamps final float samples to [-1, +1].

## Actual APO/DSP response visualization

`DspFilterService.CalculateAppliedResponse` computes the generated transfer from:

- fitted PEQ filters;
- global preamp (optionally included);
- L/R Stereo Centering trim.

Results can display this separately from the target correction curve. This distinction is important when debugging PEQ fitting/headroom.

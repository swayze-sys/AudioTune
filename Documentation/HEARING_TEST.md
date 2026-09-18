# Hearing Test Protocol

## Purpose

The hearing test estimates a **relative digital hearing threshold** for each ear and frequency in dBFS. It measures the complete current listening chain and is not a clinical audiogram.

## Frequency set

`HearingTestEngine.Frequencies` currently contains 30 points per ear:

```text
30, 40, 50, 63, 80, 100, 125, 160, 200, 250,
315, 400, 500, 630, 800, 1000, 1250, 1600,
2000, 2500, 3150, 4000, 5000, 6300, 8000,
10000, 12500, 14000, 16000, 18000 Hz
```

Total for a complete standard session: **60 measurements**.

## Audio reference conditions

### Application session volume

`TonePlaybackService.ReferenceSessionVolumePercent = 50`.

AudioTune sets only its own Windows audio session to 50%. It does **not** modify Windows endpoint/master volume.

### Endpoint/master volume

The current Windows master volume is read at test start and stored as metadata (`EndpointMasterVolumePercentAtStart`). This is a reproducibility signal, not a calibrated SPL reference.

### Sample rate / API

- 48 kHz test signal generation.
- WASAPI shared mode.
- Optional raw mode can be enabled in Settings.

## Test stimulus

The threshold test uses `PulseToneSampleProvider`:

- sine wave;
- one ear at a time;
- 3 pulses;
- 500 ms per pulse;
- 150 ms gap between pulses;
- 25 ms fades;
- 450 ms sequence gap;
- sequence repeats continuously until user answers.

This was deliberately changed from “press Play for every trial” to reduce clicking.

The user only needs to answer:

- Heard
- Not Heard

After the answer, AudioTune immediately advances to the next level/frequency and starts the next repeating stimulus.

## Level limits

Current `HearingTestEngine` constants:

- minimum level: `-90 dBFS`
- maximum/test ceiling: `-3 dBFS`
- coarse step: `8 dB`
- high-level upward step: `4 dB` from `-18 dBFS` upward
- final bracket width: `2 dB`
- maximum presentations per frequency: `16`
- ceiling needs 2 consecutive Not Heard confirmations

## Adaptive bracket search

The old 10-down/5-up audiometry-like cycle was replaced because it felt redundant.

The current state machine is:

1. **Coarse** – search until both a Heard boundary and a Not Heard boundary exist.
2. **Refine** – binary-like refinement inside that bracket.
3. **Confirm** – replay the candidate Heard threshold once.
4. Complete the point.

If a response contradicts the active bracket, AudioTune returns to a short local coarse search rather than restarting the whole frequency.

## Not detected at ceiling

If the user does not hear the tone at `-3 dBFS` twice, the measurement is stored as:

`HearingMeasurementStatus.NotDetectedAtCeiling`

This is **not** discarded by the correction model. Current product decision: it requests the maximum positive correction allowed by the active preset strength.

This is an intentional user decision. Do not “fix” it by filtering those measurements out.

## Frequency progression

After completing a point:

- next frequency begins automatically;
- starting level is informed by the previous threshold (`threshold + 8 dB`, bounded);
- left ear is completed first;
- then right ear.

## Automatic verification of suspicious points

No catch trials are used.

After the main two-ear measurement completes, AudioTune identifies strong local outliers among detected points.

Current behavior:

- logarithmic interpolation between neighboring frequencies estimates expected local threshold;
- local deviation >= 8 dB is suspicious;
- at most 3 suspicious points per ear are queued;
- each is retested;
- if verification disagrees strongly (>6 dB) or reaches the ceiling, exactly one additional verification is queued;
- all trials and verification values remain stored.

Final confidence is assigned based on agreement/spread.

A ceiling-only verification is **not averaged** into a valid detected threshold. The original detected value is retained with low confidence.

## Manual point retest

A completed profile can be edited without rerunning all 60 points.

A user can select an individual frequency/ear from Hearing Test progress and retest it. Important invariant:

> The saved old value remains in place until the replacement retest completes.

The completed retest then replaces that point while preserving additional verification/trial history.

## Autosave and profile safety

The session is autosaved:

- at test start;
- after completed measurement points;
- during verification;
- at session completion.

Starting a new hearing test creates a new Hearing Profile. It must never overwrite a completed older profile.

## Deliberately excluded behavior

- No catch/silent trials.
- No clinical dB HL conversion.
- No automatic Windows master-volume manipulation.
- No age-dependent threshold correction.
- No mandatory headphone compensation.

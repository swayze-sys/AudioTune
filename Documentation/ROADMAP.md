# Roadmap

This is a prioritized continuation plan, not a promise that every item must be implemented.

## P0 – Establish a trustworthy baseline

1. Run v0.4.16-alpha interactively on Windows.
2. Verify all pages open, especially Devices and Profiles.
3. Verify user data under `%LOCALAPPDATA%\AudioTune` remains intact.
4. Verify Fine Tune auto-apply end-to-end against actual Equalizer APO files.

## P0 – Add automated tests

Create a test project for non-WPF logic.

Highest-value targets:

- correction model;
- HumanSensitivityModel interpolation/trust;
- stereo-preservation behavior;
- DspFilterService fitting/composite peak;
- APO config generation/signatures;
- profile migration/serialization;
- hearing-test state machine with scripted Heard/Not Heard sequences.

This is the highest leverage engineering improvement for future Codex work.

## P0/P1 – Fix A/B vs APO strong-gain mismatch

Remove or reconcile the ±6 dB per-filter clamp in `CalibrationAbSampleProvider` so >100% intensity / strong ceiling boosts are represented identically in A/B and APO.

Add response-equivalence tests.

## P1 – Validate correction model v5

Use real user listening feedback and synthetic cases to assess:

- common-vs-interaural weighting;
- saturation knees/maxima;
- population-trust taper;
- Fine Tune weighting;
- 0–200% intensity behavior;
- `NotDetectedAtCeiling` max-correction policy;
- stereo-preservation default 2 dB midrange limit.

Do not alter raw profiles during algorithm changes.

## P1 – Improve model explainability

The UI should make it easy to see:

- raw threshold;
- target correction without Fine Tune;
- target correction with Fine Tune;
- final stereo-safe target;
- actual fitted APO response;
- preamp/headroom.

Avoid adding all of these simultaneously to one graph. Use explicit toggles/modes.

## P1 – Profile/preset robustness

- Add dedicated preset management (rename/archive/delete with safeguards) if needed.
- Add side-by-side profile comparison graph rather than text-only comparison.
- Add profile validation/repair diagnostics.
- Consider anonymized example profile(s) in repository for tests.

## P1 – Persistent DSP reliability

- transactional/temp-file writes for managed APO configs;
- clearer applied/pending/error state diagnostics;
- deterministic tests for generated config and parser;
- optional “open generated config” convenience.

## P1 – Installer/productization

The offline Inno Setup flow now embeds the verified .NET Desktop Runtime prerequisite and keeps runtime files out of the AudioTune program directory. Continue improving:

- versioned upgrade behavior;
- clear separation between AudioTune uninstall and persistent user data;
- optional detection/installation flow for Equalizer APO after first launch;
- code signing when distributing beyond local testing.

## P2 – Hearing-test refinements

Possible future improvements:

- adaptive starting-level estimates from neighboring frequencies/previous sessions;
- explicit session consistency/retest score;
- better visualization of measurement confidence;
- optional “verify all low-confidence points” action;
- smarter legacy-profile identification for ambiguous old high-frequency points.

Do not add catch trials unless the product decision is explicitly reversed.

## P2 – Better Fine Tuning

Potential investigation:

- should Fine Tune be scaled by global correction intensity or remain an independent absolute adjustment?
- use band-limited noise/warble tones instead of pure sine at selected frequencies;
- add repeatability/confidence without making the test long;
- compare Fine Tune effect in a dedicated graph/view.

## P2 – DSP fidelity

- quantify PEQ fitting error across full 30 Hz–18 kHz, not only band centers;
- consider a denser or alternative filter basis if target error remains audible;
- optionally export actual DSP response data for debugging.

## P3 – Headphone compensation (deliberately deferred)

Only resume if:

- reliable numeric measurement curves can be imported traceably;
- target curve is explicitly chosen/documented;
- uncertainty above ~8–10 kHz is handled conservatively;
- device-independent hearing profile transfer is worth the added complexity.

Current product works without it.

## P3 – Cross-platform/macOS (deliberately deferred)

Would require:

- replacing WPF with cross-platform UI (likely Avalonia);
- CoreAudio playback layer;
- replacement for Equalizer APO/system-wide DSP;
- packaging/signing/notarization.

Do not begin as incidental refactoring.

## Not on current roadmap

- age-based hearing correction prior;
- clinical diagnosis/audiogram claims;
- cloud account requirement;
- external AI service dependency;
- automatically changing Windows master volume.

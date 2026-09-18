# Design and Engineering Decisions

This file records decisions that are easy to reverse accidentally if only the current code is read.

## ADR-001 – Windows / WPF remains the platform

**Decision:** C# + WPF + .NET 10 on Windows.

**Reason:** Current product is tightly integrated with WASAPI and Equalizer APO, and the desired custom UI can be controlled well in WPF.

**Not now:** Avalonia/macOS port. It was evaluated conceptually and postponed.

---

## ADR-002 – Device-bound end-to-end calibration is the current mode

**Decision:** A Hearing Profile represents the measured output chain + headphone + fit + ears/hearing.

**Reason:** The actual perceived result is what matters, and verified numeric headphone compensation data is not yet integrated.

**Consequence:** Changing headphone/output path should normally require remeasurement or at least a warning.

---

## ADR-003 – Headphone compensation is postponed

**Decision:** Do not apply an Amiron Home response correction yet.

**Reason:** Traceable sources exist, but no numeric response curve has been ingested/cross-checked. Avoid pretending manually read graph values are verified.

---

## ADR-004 – Hearing threshold test uses repeating pulsed tones

**Decision:** Once a test step starts, the pulse sequence repeats until the user chooses Heard/Not Heard; then the next step starts automatically.

**Reason:** Reduce clicks and cognitive friction.

**Do not regress to:** Play -> answer -> Play -> answer for every level.

---

## ADR-005 – Adaptive bracket search instead of repetitive 10-down/5-up cycles

**Decision:** Find a Heard/Not Heard bracket, refine inside it, then confirm the final threshold.

**Reason:** Earlier audiometry-like cycling felt redundant and slow for this product.

---

## ADR-006 – No catch trials

**Decision:** Do not insert silent trials.

**Reason:** They lengthen the test and can make users think playback failed.

**Alternative QA:** automatically verify strong local outliers and allow manual individual-frequency retests.

---

## ADR-007 – Raw measurements and correction settings are separate

**Decision:** Hearing Profile != Correction Preset.

**Reason:** The measurement should survive future model changes. A user should be able to create different tunings from one long test.

---

## ADR-008 – Use a frequency-dependent human prior, not a flat median

**Decision:** Correction model v5 aligns dBFS thresholds to an ISO-226-shaped relative human threshold prior.

**Reason:** The original flat median model falsely treated normal low-frequency hearing sensitivity as a deficit.

**Median's current role:** align the unknown dBFS offset to the reference shape.

---

## ADR-009 – No age-dependent prior

**Decision:** Do not use age/sex-specific ISO 7029 weighting in the current model.

**Reason:** It would mainly function as another prior while the user supplies individual measurements. Complexity was not justified for current goals.

---

## ADR-010 – Threshold residual is not converted linearly to EQ gain

**Decision:** Use saturating `tanh` mappings and trust weighting.

**Reason:** A threshold loss in dB does not imply the same dB correction at normal music loudness.

---

## ADR-011 – Fine Tune is optional moderate-level evidence

**Decision:** Fine Tune compares selected frequencies against 1 kHz at moderate level, per ear.

**Reason:** Threshold hearing and perceived loudness at normal listening levels are different phenomena.

**Fine Tune data:** retained even when master switch is OFF.

---

## ADR-012 – Not-heard ceiling points request maximum correction

**Decision:** `NotDetectedAtCeiling` is not ignored; it receives maximum positive correction allowed by current intensity.

**Reason:** User explicitly rejected dropping these frequencies. The measurement means hearing threshold lies beyond the current test range.

---

## ADR-013 – Stereo Image Preservation protects the phantom center

**Decision:** Limit/smooth final L/R correction difference, especially 150 Hz–5 kHz.

**Reason:** Independent L/R EQ caused vocals to wander/lean to one side.

**Consequence:** Common tonal correction remains; only differential component is constrained.

---

## ADR-014 – Stereo Centering is a post-EQ attenuation-only trim

**Decision:** Centering never adds gain; it attenuates one side up to 3 dB.

**Reason:** Directly centers the perceived image without adding clipping risk.

---

## ADR-015 – Correction intensity range is 0–200%

**Decision:** 100% is normal/reference; user can reduce or exaggerate correction to 200%.

**UI:** Slider belongs on Devices only. Profiles shows the current value informationally.

---

## ADR-016 – Preamp/headroom is user-controllable

**Decision:** Automatic safe preamp is available, but manual values including 0 dB are allowed.

**Reason:** User reported no audible clipping even at 0 dB with current material and wants control.

**Behavior:** warn about theoretical overshoot; do not forcibly stop playback/apply unless optional boost limiter is enabled.

---

## ADR-017 – No extra fixed 0.5 dB safety margin

**Decision:** Automatic preamp equals the estimated composite positive peak, with no extra -0.5 dB.

**Reason:** User explicitly requested removing the additional safety gain.

---

## ADR-018 – Negative cuts are not clipping risk

**Decision:** Headroom is based on the signed composite filter response. Negative filters are not counted as positive gain.

**Reason:** Cuts cannot independently push a digital signal above full scale.

---

## ADR-019 – Fit a PEQ cascade to the target curve

**Decision:** Do not assign target curve values directly as gains of overlapping PEQ filters.

**Reason:** Earlier direct mapping created extreme composite treble peaks (~18 dB) from multiple overlapping +4…+6 dB bands.

---

## ADR-020 – A/B dry path is level-matched to calibrated preamp

**Decision:** Original/bypass comparison keeps the same global preamp/headroom.

**Reason:** Avoid “corrected sounds worse/better because it is simply louder/quieter.”

---

## ADR-021 – Persistent DSP through Equalizer APO, not a resident AudioTune background process

**Decision:** Write a persistent per-device APO configuration.

**Reason:** Correction survives reboot and AudioTune does not need to remain running.

---

## ADR-022 – Auto-apply should update an existing persistent DSP

**Decision:** Fine Tune ON/OFF, intensity and Stereo Centering should immediately rewrite persistent DSP when Auto-apply is enabled.

**Guardrail:** explicit persistent bypass must not be silently re-enabled.

---

## ADR-023 – App session volume is fixed; Windows master is not changed

**Decision:** AudioTune test/playback session volume is 50%; Windows master volume is only recorded.

**Reason:** Prevent unrelated system sounds/notifications from becoming unexpectedly loud.

---

## ADR-024 – Profile safety over destructive cleanup

**Decision:** Autosave, `.bak`, protected copy, archive and JSON export are preferred. New tests never overwrite old profiles.

**Reason:** A full hearing calibration is time-consuming and should be treated as valuable data.

---

## ADR-025 – UI becomes compact via info-on-demand

**Decision:** Devices and Profiles should avoid walls of text. Use status chips, summaries and info buttons.

**Reason:** Earlier screens became cluttered as functionality grew.

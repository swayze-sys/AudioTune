# Condensed Version History

This history focuses on architecture/product decisions, not every minor UI patch.

## v0.4.17 – DSP high-treble smoothing / vector icon pass

- The 14/16/18 kHz PEQ cluster uses Q = 1.5, retaining the 12.5 kHz transition while preventing narrow high-treble ripple.
- Deterministic DSP tests cover inter-band ripple and target retention across the final treble cluster.
- Profiles now uses the shared WPF vector LineIcon family for profile/preset information and all actions.
- Devices signal-chain stages, connections and primary actions use semantic WPF vector icons.

## v0.1.x – Working WPF prototype

- AudioTune name and dark WPF UI established.
- NAudio/WASAPI output.
- 30 Hz–18 kHz hearing-test concept.
- Amiron Home product identity/image.
- Dashboard/Hearing Test/Results initial screens.
- First adaptive threshold engine.

Early compile issues included tuple naming and missing namespaces; this motivated stronger build discipline.

## v0.2.x – A/B playback and test-flow refinement

- Music A/B listening test introduced.
- Test tones changed to repeat continuously until Heard/Not Heard.
- Adaptive bracket search replaced repetitive threshold cycling.
- AudioTune application session volume standardized at 50% without touching Windows master volume.

## v0.3.x – Persistent profiles and Equalizer APO

- Saved hearing profiles protected from overwrite.
- Manual individual-frequency retesting.
- Profile import/export/archive/backup direction.
- Equalizer APO integration/installer.
- Per-device persistent system DSP.
- Devices became DSP management location.
- Level-matched persistent bypass.
- DSP status (`ACTIVE/BYPASS/UPDATE/OFF`).
- Hover/click graph value inspection.
- Correction intensity and profile/preset concepts evolved.

## v0.4.0–0.4.2 – Model/architecture consolidation

- Hearing Profile separated from Correction Preset.
- Human-sensitivity-shaped correction prior introduced instead of flat median model.
- Raw trial history/confidence/outlier verification added.
- Optional Fine Tuning added.
- No catch trials.
- A/B and APO intended to share same parametric DSP model.
- Windows installer/publish scripts introduced.

## v0.4.3–0.4.4 – Headroom and ceiling policy

- Manual preamp/headroom settings.
- Optional positive-boost limiter.
- `NotDetectedAtCeiling` changed from excluded to maximum positive correction by explicit product decision.
- Fine Tune graph display made optional/clearer.
- Build-scope regressions fixed.

## v0.4.5–0.4.6 – Stereo stability

- Stereo Image Preservation introduced after user noticed vocals/image wandering left/right.
- Optional Stereo Centering test added.
- Results layout clipping fixed.

## v0.4.7–0.4.8 – DSP fitting and truthful graphing

- Direct target->PEQ gain mapping replaced with iterative composite filter fitting.
- Frequency-spacing-based Q values introduced.
- Composite headroom corrected to use signed full response.
- Results can distinguish desired correction vs actual generated APO/DSP transfer.
- Fine Tune preview changed from confusing delta overlay to final curve inclusion.

## v0.4.9–0.4.14 – Fine Tune master / auto-apply reliability

- Large Fine Tune ON/OFF master switch.
- Fine Tune retained while disabled.
- APO config emits Fine Tune state/point count.
- Devices redesigned into compact DSP control center.
- Auto-apply intended to immediately rewrite persistent DSP for Fine Tune/intensity/centering changes.
- Several status/signature/initialization bugs fixed.

This area remains a high-priority regression-test target.

## v0.4.15 – Profiles redesign / 0–200% intensity

- Profiles reorganized around Saved Profiles, Selected Profile, Correction Presets, Quick Actions and Measurement Summary.
- Correction Intensity range expanded to 0–200%, but slider remains Devices-only.

## v0.4.16 – Profiles UI polish / handoff baseline

- preset selector displays preset name instead of CLR type name;
- preset stats receive visual icons;
- Quick Actions receive icon + label layout;
- active selected profile shows dedicated green Active Profile badge.

This is the Codex handoff baseline.

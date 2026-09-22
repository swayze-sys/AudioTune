# Known Issues, Limitations and Regression Risks

## Baseline verification status

### v0.4.18 interactive UI and endpoint listening checks still pending

The current source builds successfully on Windows with 0 warnings and 0 errors. A full interactive pass through every page and the real Equalizer APO auto-apply flow is still pending.

**Priority:** P0. Complete the interactive regression matrix before calling the release fully validated.

---

## DSP / audio issues

### A/B vs APO numerical equivalence still needs automated coverage

The former ±6 dB per-filter clamp in the NAudio A/B and Stereo Centering paths has been raised to the same ±12 dB range used by fitted APO filters.

The two implementations still need a numerical response-equivalence test across representative cascades and sample rates.

**Priority:** P1.
### A/B provider hard-clamps final samples

`CalibrationAbSampleProvider` clamps output samples to [-1, +1]. This prevents float overflow but represents hard clipping if the wet signal exceeds full scale.

Manual preamp intentionally allows theoretical overshoot, so this behavior should be documented/tested.

### Manual preamp range mismatch

`DspFilterService` accepts manual preamp down to -18 dB, while the Settings UI currently initializes/presents a -12…0 dB range.

Normalize or deliberately document the intended range.

### Auto-apply DSP needs regression coverage

Fine Tune/intensity/centering auto-apply had multiple iterations where UI state changed but `AudioTune.txt` did not.

Current service path directly calls `SystemDspService.Apply()` when Auto-apply is enabled.

Still verify:

- `# Fine Tune state` changes ON/OFF immediately;
- generated filter lines actually change when Fine Tune changes;
- generated comments show hearing, Fine Tune, target, real summed DSP and final output values at each fitted band;
- top bar returns to ACTIVE rather than remaining UPDATE;
- explicit bypass remains bypassed;
- wrong/different profile is not silently overwritten.

### Equalizer APO config is written directly

There is no transactional multi-file rollback around all managed APO file writes. The master config is rebuilt after per-device write. Consider safer temp+rename semantics for persistent DSP files.

---

## Correction-model limitations

### Experimental psychoacoustic model

The current v7 constants (`tanh` maxima/knees, trust weights, stereo-difference limits) are empirical product parameters, not clinically validated fitting rules.

### Fine Tune is also scaled by global intensity

Current formula applies `StrengthPercent` to `(modeledGain + fineTune)` together. At 200%, a stored +3 dB Fine Tune contribution effectively becomes +6 dB before final clamp.

This is current behavior, but it should be intentionally validated rather than assumed correct.

### NotDetectedAtCeiling means maximum correction, not a measured threshold

This policy is user-requested. It may produce strong high-frequency boosts at 100–200% intensity. The UI should distinguish a lower-bound/ceiling result from a precise threshold.

### Legacy profiles may contain misleading high-frequency status

Very old AudioTune versions did not consistently distinguish “not heard at ceiling” from a detected threshold. If a legacy profile contains apparently detected 14/16/18 kHz points that the user never heard, those points should be manually retested using the current engine.

---

## Measurement limitations

### Not an SPL-calibrated audiometer

No in-ear microphone/calibrated transducer reference is used. dBFS results cannot be called dB HL or an absolute hearing-loss measurement.

### Master volume is not enforced

Only AudioTune's application session volume is fixed at 50%. Windows master volume is stored but not locked. Repeated measurements at different endpoint volume settings are not directly equivalent.

### External amplifier controls are outside AudioTune

Any physical DAC/headphone-amp gain knob or hardware gain mode must remain consistent manually for comparable end-to-end measurements.

---

## UI / maintainability issues

### Native host needs installed-endpoint listening validation

The native host has deterministic ABI, bypass and sample-equivalence tests, and the generated Equalizer APO line is covered by managed tests. A real installed-endpoint listening pass is still required because it depends on Windows endpoint registration, the audio service and third-party application output.

Stereo endpoints are the validated target. Equalizer APO can instantiate the two-channel effect repeatedly for wider layouts, but channel-pair behavior on 5.1/7.1 devices has not yet been interactively validated.

### Enhancement peak/headroom is dynamic

The static PEQ headroom figure does not attempt to predict every time-varying peak produced by Clarity, Ambience, Surround, Dynamic Boost or Bass. The host deliberately preserves the original DfxDsp output instead of adding a second AudioTune limiter. Use automatic PEQ preamp/headroom and validate demanding material; a future true-peak meter could make this risk more visible.

### WPF code-behind is becoming large

The app intentionally started without a heavy framework, but Profiles/Devices/MainWindow code-behind is growing. Refactor domain logic into testable services before attempting a wholesale MVVM rewrite.

### Unicode glyph icons

Some UI icons are Unicode glyphs rather than a dedicated icon font/vector resource set. Rendering can vary by Windows font environment.

### Fixed-size regression history

Several previous UI regressions came from fixed heights that clipped newly added buttons. Prefer Auto heights/scrolling.

---

## Deferred features, not bugs

- verified headphone compensation/database;
- age-dependent prior;
- macOS build;
- clinical hearing interpretation;
- automatic control of Windows master volume;
- catch trials.

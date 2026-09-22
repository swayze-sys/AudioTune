# Isolated FxSound DSP module

## Scope

AudioTune vendors and builds the original FxSound `DfxDsp` engine from the
pinned upstream commit `d8e7a23d37ed5939c2a3090a1c1756c7f2500b17`.
It is isolated behind `AudioTune.FxSound.Native.dll` and a managed owner class,
`FxSoundNativeEngine`.

The engine is active in AudioTune's own Listening Test playback when enabled. It is applied equally to Original and Calibrated, so the hearing-correction A/B difference remains valid.

For the selected system output, the native AudioTune.FxSound.Apo VST2 host is loaded through Equalizer APO after the hearing-correction filters when enhancements are enabled. This does not install a virtual audio device and does not replace the separate hearing-correction stage. The system-wide route depends on a working Equalizer APO installation and device attachment; installed-endpoint behavior must still be checked on the target machine.

## Signal contract

- Windows x64
- interleaved IEEE 32-bit float
- one or two channels
- 8–384 kHz accepted by the adapter
- one native engine instance per stream
- configuration and processing calls must be serialized per instance

The original public API declares sample buffers as `short int*`, but the
upstream implementation explicitly documents and processes this path as
32-bit floating point. The adapter makes that contract explicit at its boundary.

## Effect mapping

AudioTune passes values in the original FxSound 0–10 control range:

1. Clarity -> `DfxDsp::Fidelity`
2. Ambience -> `DfxDsp::Ambience`
3. Surround -> `DfxDsp::Surround`
4. Dynamic Boost -> `DfxDsp::DynamicBoost`
5. Bass -> `DfxDsp::Bass`

The original engine performs its own internal parameter scaling. AudioTune does
not approximate these effects with unrelated EQ curves.

## Deterministic comparison

`AudioTune.FxSound.Compare` generates a fixed two-second 48 kHz stereo signal
containing impulses, multi-tone content and seeded noise. It compares direct
`DfxDsp` output against output through the AudioTune C ABI for:

- all controls at zero;
- each of the five controls individually at 5;
- a combined 6/4/5/7/6 setting.

Any non-zero sample mismatch fails the test. A passing null test therefore
proves the adapter does not alter the original engine's output.

It does not yet prove end-to-end equivalence with the installed FxSound app:
that path includes its virtual device, stream negotiation, routing and any
format conversion outside `DfxDsp`. Such a claim requires synchronized capture
of identical input through both complete paths.

## Licensing

FxSound is AGPL-3.0. Because AudioTune links and distributes this engine, the
repository includes the upstream license, complete imported source and source
provenance, and the combined distribution is offered under AGPL-3.0-compatible
terms. See `LICENSE`, `THIRD_PARTY_NOTICES.md`, and
`ThirdParty/FxSound/SOURCE_INFO.md`.

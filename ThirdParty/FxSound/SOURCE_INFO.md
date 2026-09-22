# FxSound source provenance

- Upstream repository: https://github.com/fxsound2/fxsound-app
- Pinned commit: `d8e7a23d37ed5939c2a3090a1c1756c7f2500b17`
- License: GNU Affero General Public License v3.0 (`LICENSE` in this directory)
- Imported on: 2026-09-21

Imported source trees:

- `dsp/`: the original DfxDsp engine and its DSP support code
- `audiopassthru/`: the original support library required by DfxDsp

The upstream sources are kept separate from AudioTune's adapter under
`Native/AudioTune.FxSound.Native`. AudioTune does not claim authorship of the
upstream implementation. Generated `.lib`, object and intermediate files are
excluded from version control.

The upstream projects currently emit legacy conversion and 64-bit pointer-cast
warnings. They are recorded as upstream warnings; AudioTune's comparison test
still has to pass before packaging.

# First Prompt for Codex

Copy/paste the following into the first Codex session for this repository:

> Open the AudioTune repository. Before changing any code, read AGENTS.md and every Markdown file under Documentation/. The repository documentation is the durable project context and replaces a long prior development chat that you do not have access to.
>
> The baseline is AudioTune v0.4.16-alpha. First run the project's normal Windows build (`build.ps1`) and establish whether the baseline compiles. If it does not, fix only baseline compile/runtime issues first and keep those changes separate from feature work.
>
> Then inspect the key source files referenced in Documentation/CODE_MAP.md and summarize your understanding of: (1) the adaptive hearing-test protocol and outlier verification, (2) Hearing Profile vs Correction Preset separation, (3) correction model v5 and its human-sensitivity prior, (4) Fine Tune and its master ON/OFF state, (5) Stereo Image Preservation and Stereo Centering, (6) PEQ fitting/composite headroom/manual preamp behavior, (7) A/B playback vs Equalizer APO, (8) persistent per-device DSP and Auto-apply, and (9) the UI design rules/reference images.
>
> Compare the documentation against the current implementation and list any inconsistencies. Pay special attention to Documentation/KNOWN_ISSUES.md, especially the A/B ±6 dB per-filter clamp and Fine Tune Auto-apply regression history. Do not make architectural changes until this review is complete.
>
> Preserve existing user profile compatibility. Never silently delete or rewrite raw hearing measurements. Do not add headphone compensation, age weighting, catch trials, macOS work, or automatic Windows master-volume control unless explicitly requested.

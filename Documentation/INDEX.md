# AudioTune Documentation Index

This documentation set covers the current AudioTune v0.4.18 release and its v0.4.16-alpha handoff baseline.

## Start here

- [`START_HERE_CODEX.md`](START_HERE_CODEX.md) – exact handoff procedure and first-session checklist.
- [`GIT_HANDOFF.md`](GIT_HANDOFF.md) – recommended Git/private-repository transfer procedure.
- [`CURRENT_STATE.md`](CURRENT_STATE.md) – concise snapshot of what exists right now.
- [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) – what AudioTune is, why it exists, scope and product philosophy.
- [`CODE_MAP.md`](CODE_MAP.md) – quick map from feature/domain to source files.
- [`ARCHITECTURE.md`](ARCHITECTURE.md) – codebase structure, services, data flow and ownership boundaries.

## Domain logic

- [`HEARING_TEST.md`](HEARING_TEST.md) – hearing-test protocol, adaptive search, verification and retesting.
- [`DSP_MODEL.md`](DSP_MODEL.md) – current correction model v6, Fine Tune, stereo preservation, PEQ fitting and headroom.
- [`PROFILE_MODEL.md`](PROFILE_MODEL.md) – hearing profiles, correction presets, persistence, migrations and data locations.
- [`EQUALIZER_APO.md`](EQUALIZER_APO.md) – installation, per-device persistent DSP, config files, status and auto-apply.
- [FXSOUND_MODULE.md](FXSOUND_MODULE.md) – isolated original FxSound engine, effect mapping, null comparison and integration boundary.

## Product / development

- [`UI_DESIGN.md`](UI_DESIGN.md) – visual language and screen-by-screen design intent.
- [`DECISIONS.md`](DECISIONS.md) – important accepted/rejected design decisions and their rationale.
- [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) – current limitations, open risks and regression checks.
- [`TESTING.md`](TESTING.md) – required build/test workflow and manual regression matrix.
- [`HANDOFF_AUDIT.md`](HANDOFF_AUDIT.md) – static validation performed before packaging.
- [`ROADMAP.md`](ROADMAP.md) – prioritized next work, separated from deliberately postponed ideas.
- [`VERSION_HISTORY.md`](VERSION_HISTORY.md) – condensed history from the original prototype to v0.4.16.
- [`DATA_AND_PRIVACY.md`](DATA_AND_PRIVACY.md) – local data paths, profile backups and external downloads.
- [`USER_WORKFLOW.md`](USER_WORKFLOW.md) – intended end-user workflow from measurement to persistent DSP.
- [`SOURCES.md`](SOURCES.md) – external source/reference metadata already used by the project.

## Visual references

The authoritative UI mockups live in:

`AudioTune/Assets/DesignReferences/`

- `DashboardReference.png`
- `HearingTestReference.png`
- `ResultsReference.png`
- `DevicesReference.png`
- `Profiles-v0415.png`

These are design targets, not screenshots that should be blindly pixel-copied when real content needs more space. Preserve hierarchy, palette, spacing principles and control intent.

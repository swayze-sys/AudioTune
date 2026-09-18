# Git / Codex Transfer Procedure

The safest way to continue AudioTune in Codex is to make the repository itself carry the project memory.

## Repository initialization

From the handoff folder in Windows PowerShell:

```powershell
git init
git add .
git commit -m "AudioTune v0.4.16-alpha Codex handoff baseline"
git tag v0.4.16-alpha-handoff
```

Then create/connect the intended remote repository and push the baseline. The official project location is the public repository `https://github.com/swayze-sys/AudioTune`.

Do not add the user's `%LOCALAPPDATA%\AudioTune` folder to Git. Real hearing-profile data should stay outside the source repository, especially when publishing publicly.

## First Codex task

Point Codex at the repository and use the prompt in root `CODEX_FIRST_PROMPT.md`.

Do not ask Codex to immediately “improve” or refactor the code before it has:

1. read `AGENTS.md` and Documentation;
2. built the baseline;
3. summarized its understanding;
4. compared docs against implementation;
5. identified the known issues/regression risks.

## Commit strategy

Recommended:

- one conceptual change per commit;
- separate baseline compile fixes from feature work;
- commit tests with the behavior they cover;
- tag known-good builds.

Example:

```text
v0.4.16-alpha-handoff
  |
  +-- fix: baseline compile regression
  +-- test: add correction/DSP unit tests
  +-- fix: make A/B PEQ response match APO above 6 dB
  +-- feat: ...
```

## User-data backup before schema changes

Use:

```powershell
.\backup-profiles.ps1
```

before experimenting with persistent profile/preset schema changes.

## Design files

Keep `AudioTune/Assets/DesignReferences/` versioned. These images are project requirements/context, not generated build output.

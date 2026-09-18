# UI Design System and Screen Intent

## Visual identity

AudioTune should look like a modern premium Windows audio utility, not a classic enterprise WPF form.

Core visual language:

- near-black/deep navy background;
- slightly lighter navy cards/panels;
- thin blue-gray borders;
- bright blue primary actions;
- cyan/blue secondary accents;
- green for healthy/active status;
- amber for warning/update/ceiling states;
- white primary text, muted blue-gray secondary text;
- rounded corners;
- generous but disciplined spacing;
- no large white standard WPF controls inside the dark theme.

## Semantic colors

This is a hard UI rule:

- **Left ear = blue**
- **Right ear = red**

Do not use cyan for Right in one graph and red in another.

## Information hierarchy

The UI evolved from explanation-heavy prototype panels toward compact functional control centers.

Preferred order:

1. What is selected/active?
2. What is the current status?
3. What can the user do?
4. Detailed explanation only on demand.

Large paragraphs should be replaced with:

- concise labels;
- status chips;
- compact summaries;
- small `info` buttons/dialogs/tooltips.

## Authoritative design references

Stored under `AudioTune/Assets/DesignReferences/`.

### DashboardReference.png

Original visual target for overall AudioTune shell/dashboard.

Preserve:

- headphone model identity;
- active correction graph;
- dark card layout;
- top-bar output device/profile controls;
- debug/status concept.

### HearingTestReference.png

Target for guided hearing-test layout.

Actual workflow evolved after the mockup: tones now repeat automatically until Heard/Not Heard. Do not reintroduce a mandatory Play/Replay click for every trial.

### ResultsReference.png

Target for dense but readable analysis screen.

User explicitly likes the current Results concept. Do not radically simplify/reorder it without a new request.

### DevicesReference.png

Approved redesign direction. Devices is a compact DSP control center.

Key sections:

- DSP Target Device
- status chips
- Auto-apply DSP changes
- Signal Chain
- DSP Actions
- Current DSP Summary
- Detected Playback Devices collapsed/minimized

Avoid the old large warning/explanation block.

### Profiles-v0415.png

Approved Profiles redesign target.

Key sections:

- compact top headphone/profile-type/active-profile summary;
- Saved Hearing Profiles list/cards on left;
- Selected Hearing Profile details;
- Correction Presets card;
- Quick Actions grid with icons;
- Measurement Summary.

v0.4.16 additionally fixes:

- correction preset display name instead of CLR type name;
- icons for preset stats and Quick Actions;
- green Active Profile badge instead of a disabled gray button.

## Top bar

Keep these visible globally:

- Output Device selector
- Active Profile selector
- DSP status

DSP status meanings:

- green `ACTIVE`
- amber `BYPASS`
- amber `UPDATE`
- muted `OFF`

Clicking DSP status should lead to Devices.

## Dashboard

Do not remove:

- headphone information;
- active-profile correction graph.

Desired direction is concise setup/status cards rather than tutorial text.

## Hearing Test

Primary task must dominate.

Current UX principles:

- repeating pulsed tone starts automatically;
- one user response per test step: Heard / Not Heard;
- immediate advance after response;
- progress across 30 Hz–18 kHz;
- completed points are selectable for manual retest;
- debug/test parameters can remain visible in development mode.

Do not add catch-trial UI.

## Results

User preference: keep the current rich Results organization rather than replacing it with a dramatically simplified dashboard.

Graphs should support:

- hover or click point -> visible frequency/value readout;
- Left blue / Right red;
- optional Fine Tune inclusion that **replaces** the displayed final L/R target rather than adding confusing delta lines;
- optional `actual DSP/APO response` view distinct from target correction.

The distinction should be clear:

- Personal Correction Preview = target tonal correction;
- Actual DSP/APO response = fitted PEQ + preamp + centering.

## Fine Tuning

Must have a prominent master control:

- `FINE TUNE ON`
- `FINE TUNE OFF`

Turning it OFF preserves all stored Fine Tune measurements.

When Auto-apply is enabled and a persistent non-bypassed DSP exists, the change should be applied immediately without navigating to Devices.

## Profiles

Profiles is intentionally information-rich; do not add the correction-intensity slider here.

Correction intensity is displayed as a preset stat only.

Quick Actions should be an icon + label grid, not a vertical wall of buttons.

Prefer archive/backup actions over destructive deletion.

## Devices

This is the primary DSP control page.

Correction Intensity slider lives here only:

- 0–200%
- 100% normal/default

The page should stay compact. Extra explanation belongs behind info buttons.

Detected Playback Devices may be collapsed by default, but if present it should be functional (e.g. assign as test output/DSP target), not merely decorative.

## Settings

Advanced and technical configuration belongs here:

- Debug enabled/expanded
- raw WASAPI mode
- output latency
- automatic vs manual preamp
- manual preamp slider
- optional positive-boost limiter
- Stereo Image Preservation enable/limit

Warnings should be concise and specific.

## Debug

Debug functionality is important during development but must be optional.

- Debug navigation can be hidden through Settings.
- Debug areas should be collapsible.
- Do not remove useful event logs/system status merely to simplify normal UI.

## Responsive/layout rule

Avoid fixed heights when content can grow. Several earlier regressions came from cards/headers clipping after new buttons were added.

Prefer:

- `Auto` height;
- `MinHeight` for visual baseline;
- ScrollViewer for content pages;
- avoid stacking new actions inside fixed-height containers.

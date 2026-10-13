# AudioTune 0.4.127 — current stable release

Release date: 13 October 2026. Windows x64, WinUI 3, .NET 10 Desktop Runtime. The installer does not bundle .NET runtimes.

[Download setup](https://github.com/swayze-sys/AudioTune/releases/download/v0.4.127/AudioTuneSetup-0.4.127.exe) · [Release notes](https://github.com/swayze-sys/AudioTune/releases/tag/v0.4.127) · [English overview](../README.md) · [Deutsche Übersicht](../README.de.md)

## Installation and migration

- Prerequisites: Windows 10 build 19041+ or Windows 11, x64; Microsoft .NET 10 **Desktop Runtime x64**, installed system-wide. Windows App SDK components are included with AudioTune; a development SDK is not needed for installation.
- Application: `C:\Program Files\AudioTune\AudioTune.exe`.
- Interactive-session background observation: `C:\Program Files\AudioTune\PlayerObserver\AudioTune.PlayerObserver.exe`.
- User data: `%LOCALAPPDATA%\AudioTune`.
- Profile storage: `Profiles\<readable profile name> -- <ID>.json`. Raw hearing measurements, correction presets, the selected preset and unfinished Loudness measurements are separate sections of one document.
- Backups: `Backups\Installation-...`, `Backups\ProfileData-...`, `Backups\CombinedProfiles-...` and the previous-document `.bak` copy. Original source files are retained if their verified backup or conversion fails.

Setup recognizes the previous standard WPF installation and per-user AppData installation. It retires old application launchers, updates the relevant product/startup entries and preserves potentially referenced native audio hosts. It does not recursively erase unrelated files. An interrupted migration can be retried; 0.4.127 recognizes remaining observer files even when the previous UI/uninstall entry is already gone.

If an upgrade fails, retain `installation-migration.log` and the indicated backup folder. Its path is `%LOCALAPPDATA%\AudioTune\installation-migration.log`. Close AudioTune normally and retry the current setup. A regular reboot may be required for prepared native audio-component updates; forcibly restarting Windows Audio is not a prerequisite of ordinary profile changes.

Version 0.4.127 checks an installed native controller before startup auto-apply and before manual DSP activation. Running controllers are left alone; recovering a stopped controller can require administrator approval. Newer durable settings from a previous runtime are retained with a backup, and starting the controller does not itself complete a pending Windows restart for native DLLs.

## Audio setup and calibration

1. Choose the output on **Devices**.
2. Complete the native APO setup or use the Equalizer APO fallback offered for that output.
3. Measure a hearing profile; individual points can be revisited.
4. Refine frequency balance with **Fine Tune**.
5. Calibrate **Loudness Adaptation** with paired tones at several listening levels, then capture the quietest and loudest usual music levels. Confirmed unfinished measurements are saved for continuation; completed setup is saved automatically.
6. Check the graphs and listening comparison, then use the desired system-wide processing stages.

Saved corrections on already configured outputs persist without the main window. `AudioTune.PlayerObserver.exe` handles supported player-volume observation in the logged-in interactive session. Native driver/service components handle the Windows audio processing itself. The companion does not provide UI Automation on the Windows logon screen or in another user's session.

## Limits and known issues

- Stereo and 7.1 native playback layouts are supported; in 7.1, hearing correction is applied to front left/right and the other channels are preserved. Mono/HFP and other layouts require separate compatibility checks; full multichannel hearing calibration is not implemented.
- Manufacturer effects and Windows audio-enhancement settings can change the route. Equalizer APO fallback remains available; installing a binding alone does not prove live signal processing.
- Audio drivers differ. Previously reported hard PC resets during audio-service restarts were not conclusively attributed to one cause; the setup avoids introducing global Windows Audio restarts for routine profile application.
- Browser/Spotify control accessibility varies. Hidden controls can retain a validated level while playback continues; occasional safety scans remain necessary.
- Bitdefender behavioral detections of some unsigned builds/helpers remain an open compatibility issue. No antivirus exemption or universal clean result is promised for this exact release.
- AudioTune is not a medical audiometer. Measurements are relative dBFS values. Stable is the regular release channel, not a medical certification or a promise that every device has been tested.

## What is historical in this repository

The existing WPF project, its build scripts and the older architecture/testing documents describe the 0.4.18 source baseline. Use this page and the root READMEs for current user-facing installation and feature information. Historical build commands should not be presented as commands for compiling WinUI 0.4.127.

The current WinUI development source remains private. GitHub's automatically generated source archives correspond to the public historical source, not the current installer.

The README uses a promotional image supplied by the project author, showing AudioTune 0.4.127 and its overview, spectrum and processing chain.

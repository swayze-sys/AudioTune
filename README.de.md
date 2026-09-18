# AudioTune

AudioTune ist eine lokale Windows-Anwendung zur persönlichen, gerätegebundenen Kopfhörer-Kalibrierung. Sie misst die Hörschwellen für das linke und rechte Ohr, bewahrt die Rohmessungen dauerhaft auf, leitet daraus eine einstellbare Korrektur ab und kann dieselben Filter über Equalizer APO dauerhaft auf ein Windows-Wiedergabegerät anwenden.

> **Alpha-Software:** AudioTune ist kein medizinisches Audiometer. Die Messwerte sind relative digitale Pegel in dBFS und dürfen nicht als dB HL, Diagnose oder klinische Hörverlustmessung verstanden werden.

![AudioTune Dashboard](AudioTune/Assets/DesignReferences/DashboardReference.png)

[English README](README.md) · [Dokumentationsübersicht](Documentation/INDEX.md) · [Bekannte Einschränkungen](Documentation/KNOWN_ISSUES.md) · [Roadmap](Documentation/ROADMAP.md)

## Wichtigste Funktionen

- adaptiver Hörtest von 30 Hz bis 18 kHz für jedes Ohr
- automatische Zwischenspeicherung, Verifikation auffälliger Punkte und Einzelpunkt-Wiederholung
- klare Trennung zwischen unveränderten Hörprofilen und einstellbaren Korrektur-Presets
- Korrekturmodell v5 mit optionalem Fine Tune, Stereo Image Preservation und Stereo Centering
- identisches parametrisches Filtermodell für A/B-Wiedergabe und Equalizer APO
- dauerhafter, gerätebezogener Windows-DSP mit pegelgleichem Bypass
- ausschließlich lokale Profildaten unter `%LOCALAPPDATA%\AudioTune`

## Installation

Lade `AudioTuneSetup-0.4.16-alpha.exe` aus dem GitHub-Release herunter und starte es unter 64-Bit-Windows. Das Offline-Setup enthält die erforderliche Microsoft .NET 10 Desktop Runtime und installiert sie nur, wenn noch keine kompatible Version vorhanden ist.

Equalizer APO ist nur für die optionale systemweite Korrektur erforderlich. AudioTune bietet die geprüfte Installation auf der Seite **Devices** an; das gewünschte Wiedergabegerät muss anschließend bewusst ausgewählt werden.

AudioTune verändert die Windows-Gesamtlautstärke nicht automatisch und löscht beim Update oder bei der Deinstallation keine Hörprofile.

## Aus dem Quellcode bauen

Erforderlich sind Windows x64, Windows PowerShell 5.1 oder neuer und das .NET 10 SDK:

```powershell
.\build.ps1
.\run.ps1
```

Das vollständige Offline-Setup wird mit folgendem Befehl erzeugt:

```powershell
.\build-installer.ps1
```

Das Ergebnis liegt unter `dist\AudioTuneSetup-0.4.16-alpha.exe`. Das Build-Skript lädt die signierte Inno-Setup-Buildkomponente bei Bedarf in einen lokalen, nicht versionierten Werkzeugordner und prüft die eingebettete .NET-Runtime mit dem offiziellen SHA-512-Hash.

## Datenschutz und Messsicherheit

- keine Cloud-Konten und kein Profil-Upload
- Rohmessungen werden durch Tuning-Änderungen nicht überschrieben
- neue Hörtests ersetzen keine älteren Profile
- die Anwendung setzt nur ihre eigene Audio-Session auf den Referenzpegel; die Windows-Gesamtlautstärke wird lediglich protokolliert
- Equalizer APO wird nur nach ausdrücklicher Benutzeraktion eingerichtet

Weitere technische und fachliche Details stehen vollständig unter [`Documentation/`](Documentation/INDEX.md).

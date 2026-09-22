#define MyAppName "AudioTune"
#define MyAppVersion "0.4.18"
#define MyAppPublisher "AudioTune"
#define MyAppExeName "AudioTune.exe"
#define DotNetRuntimeVersion "10.0.12"
#define DotNetRuntimeExe "windowsdesktop-runtime-10.0.12-win-x64.exe"
#ifndef MyAppId
  #define MyAppId "65A9181A-0C39-4D8D-83C0-210682BC7D8B"
#endif
#ifndef MyOutputBaseFilename
  #define MyOutputBaseFilename "AudioTuneSetup-0.4.18"
#endif

[Setup]
AppId={{{#MyAppId}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\AudioTune
DefaultGroupName=AudioTune
DisableProgramGroupPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog commandline
OutputDir=..\dist
OutputBaseFilename={#MyOutputBaseFilename}
SetupIconFile=..\AudioTune\Assets\App\AudioTune.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=no
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\publish\win-x64\AudioTune.FxSound.Apo.dll"; DestDir: "{commonappdata}\AudioTune\Native"; DestName: "AudioTune.FxSound.Apo.0.4.18.dll"; Flags: ignoreversion onlyifdoesntexist uninsrestartdelete
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Prerequisites\{#DotNetRuntimeExe}"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsDotNetDesktopRuntime

; Previous alpha installers used a self-contained publish. AudioTune user data is
; stored under %LOCALAPPDATA% and is not affected by cleaning this program folder.
[InstallDelete]
Type: filesandordirs; Name: "{app}\*"

[Icons]
Name: "{group}\AudioTune"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\AudioTune"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{tmp}\{#DotNetRuntimeExe}"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Microsoft .NET Windows Desktop Runtime {#DotNetRuntimeVersion}..."; Flags: waituntilterminated; Check: NeedsDotNetDesktopRuntime
Filename: "{app}\{#MyAppExeName}"; Description: "Launch AudioTune"; Flags: nowait postinstall skipifsilent

; Hearing profiles and correction presets live under %LOCALAPPDATA%\AudioTune.
; The installer deliberately does not remove those user-data folders.

[Code]
function NeedsDotNetDesktopRuntime: Boolean;
var
  FindRec: TFindRec;
  RuntimeRoot: String;
begin
  Result := True;
  RuntimeRoot := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*');

  if FindFirst(RuntimeRoot, FindRec) then
  begin
    try
      repeat
        if ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and
           (FindRec.Name <> '.') and (FindRec.Name <> '..') then
        begin
          Result := False;
          Break;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;
procedure RemoveAudioTuneEqualizerApoConfig;
var
  ConfigDir: String;
  InstallDir: String;
  MainConfig: String;
  Lines: TArrayOfString;
  KeptLines: TArrayOfString;
  I: Integer;
  Count: Integer;
begin
  ConfigDir := '';
  if not RegQueryStringValue(HKLM64, 'SOFTWARE\EqualizerAPO', 'ConfigPath', ConfigDir) then
  begin
    if RegQueryStringValue(HKLM64, 'SOFTWARE\EqualizerAPO', 'InstallPath', InstallDir) then
      ConfigDir := AddBackslash(InstallDir) + 'config';
  end;

  if ConfigDir = '' then
    Exit;

  MainConfig := AddBackslash(ConfigDir) + 'config.txt';
  if LoadStringsFromFile(MainConfig, Lines) then
  begin
    Count := 0;
    SetArrayLength(KeptLines, GetArrayLength(Lines));
    for I := 0 to GetArrayLength(Lines) - 1 do
    begin
      if (CompareText(Trim(Lines[I]), 'Include: AudioTune.txt') <> 0) and
         (CompareText(Trim(Lines[I]), '# AudioTune managed persistent DSP include') <> 0) and
         (CompareText(Trim(Lines[I]), '# AudioTune managed include') <> 0) then
      begin
        KeptLines[Count] := Lines[I];
        Count := Count + 1;
      end;
    end;
    SetArrayLength(KeptLines, Count);
    SaveStringsToFile(MainConfig, KeptLines, False);
  end;

  DelTree(AddBackslash(ConfigDir) + 'AudioTune', True, True, True);
  DeleteFile(AddBackslash(ConfigDir) + 'AudioTune.txt');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveAudioTuneEqualizerApoConfig;
end;

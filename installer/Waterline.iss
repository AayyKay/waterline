#ifndef MyAppName
  #define MyAppName "Waterline"
#endif
#ifndef MyAppVersion
  #define MyAppVersion "2.1.1"
#endif
#ifndef MyAppId
  #define MyAppId "{{8AFEC410-4D36-45F6-A683-CE3DFE7731B2}"
#endif
#ifndef MyStateRoot
  #define MyStateRoot "{localappdata}\Waterline"
#endif
#ifndef MyOutputDir
  #define MyOutputDir "..\release"
#endif
#define MyAppPublisher "Waterline"
#define MyAppExeName "Waterline.exe"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Waterline
DefaultGroupName=Waterline
DisableProgramGroupPage=yes
OutputDir={#MyOutputDir}
OutputBaseFilename=Waterline-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
SetupLogging=yes
MinVersion=10.0.17763
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
SetupIconFile=..\public\icons\waterline-app.ico
VersionInfoVersion={#MyAppVersion}

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Waterline"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Waterline"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Waterline"; Flags: nowait postinstall skipifsilent

[Code]
procedure PreserveStateBeforeInstall;
var
  SourcePath: String;
  BackupPath: String;
begin
  SourcePath := ExpandConstant('{#MyStateRoot}\state.json');
  BackupPath := ExpandConstant('{#MyStateRoot}\upgrade-backups\state-before-{#MyAppVersion}.json');
  if FileExists(SourcePath) and not FileExists(BackupPath) then
  begin
    if not ForceDirectories(ExtractFileDir(BackupPath)) then
      RaiseException('Waterline could not create the upgrade-backup folder. Installation stopped before changing the app.');
    if not CopyFile(SourcePath, BackupPath, True) then
      RaiseException('Waterline could not preserve the current state file. Installation stopped before changing the app.');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    PreserveStateBeforeInstall;
end;

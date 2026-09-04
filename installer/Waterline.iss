#define MyAppName "Waterline"
#ifndef MyAppVersion
  #define MyAppVersion "2.0.0"
#endif
#define MyAppPublisher "Waterline"
#define MyAppExeName "Waterline.exe"

[Setup]
AppId={{8AFEC410-4D36-45F6-A683-CE3DFE7731B2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Waterline
DefaultGroupName=Waterline
DisableProgramGroupPage=yes
OutputDir=..\release
OutputBaseFilename=Waterline-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Waterline"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Waterline"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Waterline"; Flags: nowait postinstall skipifsilent

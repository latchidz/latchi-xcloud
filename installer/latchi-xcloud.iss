; LATCHI xCLOUD — Inno Setup script
; Per-user install (NO admin rights), x64 only. User data lives under
; %LOCALAPPDATA%\LATCHI\xCLOUD and is NEVER touched on uninstall.
;
; Build:  ISCC /DMyAppVersion=0.1.0 installer\latchi-xcloud.iss
; Expects the self-contained publish output in ..\publish\

#define MyAppName "LATCHI xCLOUD"
#define MyAppExeName "LATCHI-xCLOUD.exe"
#ifndef MyAppVersion
#define MyAppVersion "0.1.0"
#endif

[Setup]
AppId={{5E2B9D74-8C1F-4A63-9B0E-LATCHIXCL1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=LATCHI
AppPublisherURL=https://github.com/latchidz/latchi-xcloud
DefaultDirName={localappdata}\LATCHI xCLOUD
DefaultGroupName=LATCHI xCLOUD
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=LATCHI-xCLOUD-{#MyAppVersion}-x64-Setup
SetupIconFile=..\assets\icon\latchi-xcloud.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

; NOTE (deliberate): no [UninstallDelete] — the browser profile, Better xCloud data
; and settings under %LOCALAPPDATA%\LATCHI\xCLOUD must survive an uninstall.

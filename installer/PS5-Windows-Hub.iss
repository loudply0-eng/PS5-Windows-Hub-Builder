#define AppName "PS5 Windows Hub"
#ifndef AppVersion
  #define AppVersion "0.1.1"
#endif
#define AppExe "PS5-Windows-Hub.exe"

[Setup]
AppId={{D4E9723D-1D6B-4B48-BAC2-5B4F6DB987A1}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=ABRJ
AppPublisherURL=https://github.com/loudply0-eng/PS5-Windows-Hub-Builder
AppSupportURL=https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/issues
DefaultDirName={localappdata}\Programs\PS5 Windows Hub
DefaultGroupName=PS5 Windows Hub
UsePreviousAppDir=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
CloseApplications=yes
RestartApplications=no
UninstallDisplayName=PS5 Windows Hub
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=..\dist
OutputBaseFilename=PS5-Windows-Hub-Setup-{#AppVersion}
SetupLogging=yes
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany=ABRJ
VersionInfoDescription=PS5 Windows Hub Setup
VersionInfoProductName=PS5 Windows Hub
VersionInfoProductVersion={#AppVersion}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\dist\PS5-Windows-Hub\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\dist\LICENSE-AnyPS5.txt"; DestDir: "{app}\Licenses"; Flags: ignoreversion
Source: "..\dist\LICENSE-KytyPS5.txt"; DestDir: "{app}\Licenses"; Flags: ignoreversion
Source: "..\dist\AnyPS5-v0.1.5-source.zip"; DestDir: "{app}\Licenses\Source"; Flags: ignoreversion
Source: "..\dist\KytyPS5-2026-11-26-61db2f8-source.zip"; DestDir: "{app}\Licenses\Source"; Flags: ignoreversion
Source: "..\dist\BUILD-INFO.txt"; DestDir: "{app}\About"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\PS5 Windows Hub"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\PS5 Windows Hub"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch PS5 Windows Hub"; Flags: postinstall nowait skipifsilent

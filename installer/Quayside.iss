; Inno Setup 6 script. Build with: iscc /DAppVersion=0.1.0 /DSourceDir=..\artifacts\publish installer\Quayside.iss
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish"
#endif

[Setup]
AppId={{7B0E4C51-2E7A-4F7B-9C1E-5A3F0D8E6B21}
AppName=Quayside
AppVersion={#AppVersion}
AppPublisher=Quayside contributors
AppPublisherURL=https://github.com/
DefaultDirName={autopf}\Quayside
DefaultGroupName=Quayside
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\artifacts
OutputBaseFilename=Quayside-{#AppVersion}-win-x64-setup
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\Quayside.exe

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Quayside"; Filename: "{app}\Quayside.exe"
Name: "{autodesktop}\Quayside"; Filename: "{app}\Quayside.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked

[Run]
Filename: "{app}\Quayside.exe"; Description: "Launch Quayside"; Flags: nowait postinstall skipifsilent

; User data (%LOCALAPPDATA%\Quayside) is intentionally kept on uninstall.

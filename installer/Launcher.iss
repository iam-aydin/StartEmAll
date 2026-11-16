; Inno Setup script - builds Output\Launcher-Setup.exe from the published app in ..\dist
; Installs per-user (no admin prompt) because the app writes settings.json next to its exe.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{B7C4E0A1-5D2F-4E8B-9A36-7F1C2D8E4B50}
AppName=Launcher
AppVersion={#AppVersion}
AppPublisher=StartEmAll
DefaultDirName={autopf}\Launcher
DefaultGroupName=Launcher
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=Launcher-Setup
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\Launcher.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; the app holds this mutex while running, so setup asks the user to close it first
AppMutex=Launcher.SingleInstance
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start Launcher with &Windows"; Flags: unchecked

[Files]
Source: "..\dist\Launcher.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Launcher"; Filename: "{app}\Launcher.exe"
Name: "{autodesktop}\Launcher"; Filename: "{app}\Launcher.exe"; Tasks: desktopicon
Name: "{userstartup}\Launcher"; Filename: "{app}\Launcher.exe"; Tasks: startup

[Run]
Filename: "{app}\Launcher.exe"; Description: "Start Launcher"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; settings.json and icons\ are the user's data: kept on uninstall

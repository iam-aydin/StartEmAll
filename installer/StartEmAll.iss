; Inno Setup script - builds Output\StartEmAll-Setup.exe from the published app in ..\dist
; Installs per-user (no admin prompt) because the app writes settings.json next to its exe.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{B7C4E0A1-5D2F-4E8B-9A36-7F1C2D8E4B50}
AppName=StartEmAll
AppVersion={#AppVersion}
AppPublisher=iam-aydin
AppPublisherURL=https://github.com/iam-aydin/StartEmAll
AppSupportURL=https://github.com/iam-aydin/StartEmAll/issues
DefaultDirName={autopf}\StartEmAll
DefaultGroupName=StartEmAll
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=StartEmAll-Setup
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\StartEmAll.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardImageFile=wizard-side.bmp
WizardSmallImageFile=wizard-small.bmp
; wizard order: Welcome -> install folder -> shortcuts -> Install -> Finish
; DisableDirPage=no forces the folder page every time (Inno hides it by default when it finds an earlier install)
DisableDirPage=no
DisableProgramGroupPage=yes
DisableReadyPage=yes
ShowLanguageDialog=no
UninstallDisplayName=StartEmAll
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; the app holds this mutex while running, so setup asks the user to close it first
AppMutex=StartEmAll.SingleInstance
CloseApplications=yes

[Messages]
WelcomeLabel1=Welcome to StartEmAll
WelcomeLabel2=A fast launcher for your apps and games, with a side strip you can open from any screen edge.%n%nThis takes a few seconds and needs no administrator rights.
FinishedHeadingLabel=You're all set
FinishedLabel=StartEmAll is installed. Tick the box below to open it now.

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start StartEmAll with &Windows"; Flags: unchecked

[Files]
Source: "..\dist\StartEmAll.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\StartEmAll"; Filename: "{app}\StartEmAll.exe"
Name: "{autodesktop}\StartEmAll"; Filename: "{app}\StartEmAll.exe"; Tasks: desktopicon
Name: "{userstartup}\StartEmAll"; Filename: "{app}\StartEmAll.exe"; Tasks: startup

[Run]
Filename: "{app}\StartEmAll.exe"; Description: "Start StartEmAll"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; settings.json and icons\ are the user's data: kept on uninstall

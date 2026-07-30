; ============================================================
;  Onyx // Ghost Cam  —  Windows installer (Inno Setup 6)
;  Null Studio · KYROS
;  Build with:  .\make-installer.ps1
; ============================================================

#define AppName        "GhostCam"
#define AppShortName   "GhostCam"
#define AppVersion     "1.1.0"
#define AppPublisher   "NullStudio"
#define AppAuthor      "KYROS"
#define AppExe         "GhostCam.exe"
#define AppURL         "https://github.com/RDXFGXY1/OnyxGhostCam"

[Setup]
AppId={{9F2C4E77-3A21-4C58-B0D6-7E1A9C4F5B33}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=© {#AppPublisher} · {#AppAuthor}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} — local privacy camera by {#AppAuthor}
VersionInfoVersion={#AppVersion}

; --- install location (user-selectable) ---
DefaultDirName={autopf}\{#AppPublisher}\{#AppShortName}
DisableDirPage=no
DefaultGroupName={#AppShortName}
DisableProgramGroupPage=no
AllowNoIcons=yes

; --- output ---
OutputDir=..\dist
OutputBaseFilename=GhostCam-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes

; --- appearance / behaviour ---
SetupIconFile=onyx.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
LicenseFile=LICENSE.txt
InfoBeforeFile=ABOUT.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";   Description: "Create a &desktop shortcut";               GroupDescription: "Shortcuts:"; Flags: checkedonce
Name: "startmenuicon"; Description: "Add to the &Start menu";                   GroupDescription: "Shortcuts:"; Flags: checkedonce
Name: "quicklaunch";   Description: "Pin to the &taskbar area (quick launch)";  GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "startup";       Description: "Start GhostCam when &Windows starts (hidden in tray)"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
; Published self-contained build (see make-installer.ps1)
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "onyx.ico";     DestDir: "{app}"; Flags: ignoreversion
Source: "ABOUT.txt";    DestDir: "{app}"; Flags: ignoreversion
Source: "README.txt";   DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "LICENSE.txt";  DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";                  Filename: "{app}\{#AppExe}"; IconFilename: "{app}\onyx.ico"; Tasks: startmenuicon
Name: "{group}\{#AppName} — Read Me";        Filename: "{app}\README.txt";                                Tasks: startmenuicon
Name: "{group}\Uninstall {#AppName}";        Filename: "{uninstallexe}";                                  Tasks: startmenuicon
Name: "{autodesktop}\{#AppName}";            Filename: "{app}\{#AppExe}"; IconFilename: "{app}\onyx.ico"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#AppName}"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\onyx.ico"; Tasks: quicklaunch

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "{#AppShortName}"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\README.txt"; Description: "Read the &guide (what it is and how to use it)"; \
    Flags: shellexec postinstall skipifsilent unchecked
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName} now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Messages]
BeveledLabel=Null Studio · KYROS
WelcomeLabel1=Welcome to [name]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nGhostCam is a local privacy camera: it detects your face and covers it in real time, then feeds the result to any app as a virtual camera. Your video never leaves your computer and no telemetry is collected. GhostCam contacts GitHub only to check for updates, which you can switch off.%n%nCreated by KYROS · Null Studio.%n%nIt is recommended that you close all other applications before continuing.
FinishedHeadingLabel=GhostCam is installed
FinishedLabelNoIcons=[name] has been installed on your computer.%n%nNote: to broadcast into other apps, OBS Studio must be installed (GhostCam uses its virtual-camera driver; OBS does not need to be running).
FinishedLabel=[name] has been installed on your computer.%n%nNote: to broadcast into other apps, OBS Studio must be installed (GhostCam uses its virtual-camera driver; OBS does not need to be running).

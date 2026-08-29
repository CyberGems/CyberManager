; Script generated for CyberManager
; Ultra-Lightweight Virtualized Task Manager
; Built by CyberGems (https://cybergems.org)

#define AppName "CyberManager"
#define AppPublisher "CyberGems"
#define AppURL "https://cybergems.org"
#define AppExeName "CyberManager.exe"

[Setup]
AppId={{D5032E3A-3F5C-4E0E-A0E2-34DD8225ECA3}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=.
OutputBaseFilename=CyberManager-Setup-{#AppVersion}
SetupIconFile=.\src\CyberManager.UI\Assets\CyberManager.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
english.CreateShortcutGroup=Create shortcuts:
english.CyberManagerShortcut=CyberManager (Task Manager)
english.OptionsGroup=Options:
english.RunAtStartup=Run CyberManager when Windows starts

spanish.CreateShortcutGroup=Crear accesos directos:
spanish.CyberManagerShortcut=CyberManager (Gestor de Tareas)
spanish.OptionsGroup=Opciones:
spanish.RunAtStartup=Ejecutar CyberManager al iniciar Windows

[Tasks]
Name: "desktopicon"; Description: "{cm:CyberManagerShortcut}"; GroupDescription: "{cm:CreateShortcutGroup}"
Name: "startup"; Description: "{cm:RunAtStartup}"; GroupDescription: "{cm:OptionsGroup}"

[Files]
Source: ".\publish-win64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\CyberManager.ico"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon; IconFilename: "{app}\Assets\CyberManager.ico"

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; ValueType: string; ValueName: "{app}\{#AppExeName}"; ValueData: "~ RUNASADMIN HIGHDPIAWARE"; Flags: uninsdeletevalue

[Run]
Filename: "schtasks"; Parameters: "create /tn ""CyberManager"" /tr """"{app}\{#AppExeName}"" --minimized"" /sc onlogon /rl highest /f"; Tasks: startup; Flags: runhidden
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall shellexec runascurrentuser

[UninstallRun]
Filename: "schtasks"; Parameters: "delete /tn ""CyberManager"" /f"; RunOnceId: "CyberManagerScheduledTaskRemoval"; Flags: runhidden

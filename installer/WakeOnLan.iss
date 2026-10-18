; Instalador de Wake on LAN (Inno Setup 6).
; Se instala por usuario: no pide permisos de administrador.
; Compilar: ISCC.exe /DAppVersion=1.0.0 installer\WakeOnLan.iss  (o usar tools\Publish.ps1)

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "Wake on LAN"
#define AppExe "WakeOnLan.exe"

; VersionInfoVersion solo admite números: "1.2.3-beta" -> "1.2.3".
#if Pos("-", AppVersion) > 0
  #define NumericVersion Copy(AppVersion, 1, Pos("-", AppVersion) - 1)
#else
  #define NumericVersion AppVersion
#endif

[Setup]
AppId={{3F6B2A0E-8C4D-4E61-9B5A-7D2C1E0F9A41}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
DefaultDirName={autopf}\WakeOnLan
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\artifacts
OutputBaseFilename=WakeOnLan-{#AppVersion}-setup
SetupIconFile=..\src\WakeOnLan\Assets\app.ico
; Metadatos del ejecutable: SignPath exige que el nombre y la versión del producto coincidan con la release.
VersionInfoVersion={#NumericVersion}
VersionInfoProductName={#AppName}
VersionInfoProductTextVersion={#AppVersion}
VersionInfoDescription={#AppName} Setup
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

#define MyAppName "abose7ly"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "abose7ly"
#define MyAppExeName "abose7ly.exe"

[Setup]
AppId={{4BD30EF0-A6F2-4A5E-A6B1-AB05E7A7A5A1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\abose7ly
DefaultGroupName=abose7ly
OutputDir=installer
OutputBaseFilename=abose7ly_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\abose7ly"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\abose7ly"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch abose7ly"; Flags: postinstall nowait skipifsilent

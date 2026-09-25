; Inno Setup script for the Windows desktop build (per-user install, no admin rights required).
; Built by scripts/dev/desktop-package.sh with /DAppVersion, /DSourceDir, /DOutputDir and /DOutputBaseName.
#ifndef AppVersion
  #define AppVersion "0.3.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\artifacts\desktop\win-x64\bin"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts\packages"
#endif
#ifndef OutputBaseName
  #define OutputBaseName "AgenticLogAnalyzer-" + AppVersion + "-win-x64-setup"
#endif

[Setup]
AppId={{6F1E2B7A-9C3D-4E5F-8A1B-2C3D4E5F6A7B}
AppName=Agentic Log Analyzer
AppVersion={#AppVersion}
AppPublisher=hackee-fr
DefaultDirName={autopf}\Agentic Log Analyzer
DefaultGroupName=Agentic Log Analyzer
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\AgenticLogAnalyzer.exe

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Agentic Log Analyzer"; Filename: "{app}\AgenticLogAnalyzer.exe"
Name: "{group}\{cm:UninstallProgram,Agentic Log Analyzer}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Agentic Log Analyzer"; Filename: "{app}\AgenticLogAnalyzer.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AgenticLogAnalyzer.exe"; Description: "{cm:LaunchProgram,Agentic Log Analyzer}"; Flags: nowait postinstall skipifsilent

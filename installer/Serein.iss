; Build with Inno Setup 7 (ISCC.exe). Override /DAppSource=... for another publish folder.
#ifndef AppSource
  #define AppSource SourcePath + "..\..\Serein-Profilliste"
#endif

[Setup]
AppId={{8C64B0B1-52D9-4853-B8E4-580EF4510215}
AppName=Serein
AppVersion=0.2.1
AppPublisher=Serein
DefaultDirName={localappdata}\Programs\Serein
DefaultGroupName=Serein
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupArchitecture=x64
MinVersion=10.0.19045
WizardStyle=modern dynamic
OutputDir=..\..
OutputBaseFilename=Serein-Setup-Windows-x64
SetupIconFile=..\Serein.App\Assets\serein.ico
UninstallDisplayIcon={app}\Serein.exe
LicenseFile=..\LICENSE.txt
InfoBeforeFile=Installation.txt
Compression=lzma2/normal
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
SetupMutex=SereinSetup-8C64B0B1-52D9-4853-B8E4-580EF4510215
UninstallDisplayName=Serein

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknüpfung erstellen"; Flags: unchecked

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "smoke-data\*,Serein-*.png,startup-ms.txt,ui-switch-ms.txt,smoke-error.txt"

[Icons]
Name: "{autoprograms}\Serein"; Filename: "{app}\Serein.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Serein"; Filename: "{app}\Serein.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Serein.exe"; Description: "Serein starten"; Flags: nowait postinstall skipifsilent

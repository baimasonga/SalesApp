; Salone Sales — Windows Installer
; Build:  iscc installer/SalesApp.iss   (Inno Setup 6.x, free from jrsoftware.org)
;
; Produces a single setup.exe in installer/Output/. The installer:
;   1. Copies the self-contained publish to C:\Program Files\SaloneSales\
;   2. Creates the data\ and logs\ subfolders
;   3. Registers and starts the Windows Service
;   4. Adds Start Menu + Desktop shortcuts that open the app in the browser
;   5. Adds a firewall rule for the listening port
;   6. Wires up Add/Remove Programs uninstall

#define MyAppName       "Salone Sales"
#define MyAppVersion    "1.0.0"
#define MyAppPublisher  "Salone Sales"
#define MyAppURL        "https://salonesales.sl"
#define MyAppExeName    "SalesApp.exe"
#define MyServiceName   "SaloneSales"
#define MyPort          "5099"

[Setup]
AppId={{D1F0E2B7-5E0F-4F1C-9E1D-9D89F2C1B0A1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\SaloneSales
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
OutputDir=Output
OutputBaseFilename=SaloneSales-Setup-{#MyAppVersion}
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
LicenseFile=..\LICENSE.txt
SetupIconFile=
WizardImageFile=

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce
Name: "starticon";   Description: "Create a Start Menu shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce

[Files]
; Source = the publish folder produced by scripts/publish-win.ps1
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
Name: "{app}\data"; Permissions: users-modify
Name: "{app}\logs"; Permissions: users-modify

[Icons]
Name: "{group}\Open {#MyAppName}"; Filename: "http://localhost:{#MyPort}"; Tasks: starticon
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "http://localhost:{#MyPort}"; Tasks: desktopicon

[Run]
; Set env var so the app integrates with the Windows Service Control Manager
Filename: "{cmd}"; Parameters: "/c setx /M RUN_AS_SERVICE 1"; Flags: runhidden waituntilterminated

; Register the Windows service
Filename: "{sys}\sc.exe"; Parameters: "create {#MyServiceName} binPath= ""\""{app}\{#MyAppExeName}\"" --urls=http://+:{#MyPort}"" start= auto DisplayName= ""{#MyAppName}"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "description {#MyServiceName} ""Salone Sales — sales, inventory and CRM for Sierra Leone businesses."""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "failure {#MyServiceName} reset= 86400 actions= restart/5000/restart/10000/restart/30000"; Flags: runhidden waituntilterminated

; Firewall rule (LAN only — Private + Domain profiles)
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""Salone Sales (Port {#MyPort})"" dir=in action=allow protocol=TCP localport={#MyPort} profile=private,domain"; Flags: runhidden waituntilterminated

; Start the service
Filename: "{sys}\sc.exe"; Parameters: "start {#MyServiceName}"; Flags: runhidden waituntilterminated

; Open the browser when install completes
Filename: "http://localhost:{#MyPort}"; Description: "Launch {#MyAppName} now"; Flags: shellexec postinstall skipifsilent nowait

[UninstallRun]
; Stop + remove the service before deleting files
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden waituntilterminated
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Salone Sales (Port {#MyPort})"""; Flags: runhidden waituntilterminated

[UninstallDelete]
; Leave data\ and logs\ alone — users may want to keep their database after uninstall.
; Comment these out to wipe everything.
;Type: filesandordirs; Name: "{app}\data"
;Type: filesandordirs; Name: "{app}\logs"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

#define MyAppName "DELIMa Smart Launcher"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif

[Setup]
; AppId is a fixed GUID, generated once and NEVER changed.
; It is how Windows knows version 2.0.1 upgrades 2.0.0 rather than installing beside it.
; DO NOT CHANGE THIS GUID.
AppId={{D37E6F18-49A1-4F23-9B2E-6E84218C1D54}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=tecertools Digital Solutions
DefaultDirName={autopf}\DELIMa Launcher
DefaultGroupName=DELIMa Launcher
OutputDir=..\dist
OutputBaseFilename=DELIMaLauncher-Setup-{#MyAppVersion}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\icon.ico
UninstallIconFile=..\icon.ico
LicenseFile=assets\LESEN.rtf
; Windows 10 1809 (build 17763) or later per PRD §8
MinVersion=10.0.17763

[Languages]
Name: "ms"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "lab";   Description: "PC Makmal"
Name: "admin"; Description: "Alat Pentadbir"
Name: "full";  Description: "Kedua-duanya"

[Components]
Name: "lab";   Description: "Aplikasi murid + Provision"; Types: lab full;   Flags: fixed
Name: "admin"; Description: "Alat Pentadbir (satu PC sahaja)"; Types: admin full

[Files]
Source: "..\publish\Launcher\Delima.Launcher.exe";   DestDir: "{app}"; Components: lab;   Flags: ignoreversion
Source: "..\publish\Provision\Delima.Provision.exe"; DestDir: "{app}"; Components: lab;   Flags: ignoreversion
Source: "..\publish\Admin\Delima.Admin.exe";         DestDir: "{app}"; Components: admin; Flags: ignoreversion
Source: "assets\avatars\*";                          DestDir: "{app}\avatars"; Components: lab; Flags: recursesubdirs
Source: "assets\Panduan_Pemasangan.pdf";             DestDir: "{app}\docs"; Flags: isreadme
Source: "assets\Panduan_Import.pdf";                 DestDir: "{app}\docs"; Components: admin
Source: "assets\contoh_roster.csv";                  DestDir: "{app}\docs"; Components: admin
Source: "assets\contoh_kata_laluan.csv";             DestDir: "{app}\docs"; Components: admin

[Dirs]
; Per-machine store. Interactive users must not be able to read it (arch §3.5).
; Permissions: admins-full system-full sets ACL for Administrators and SYSTEM.
; We also explicitly strip inherited %ProgramData% permissions in [Run] and [Code].
Name: "{commonappdata}\DELIMa Launcher"; Permissions: admins-full system-full

[Icons]
Name: "{group}\DELIMa";             Filename: "{app}\Delima.Launcher.exe"; Components: lab
Name: "{commondesktop}\DELIMa";      Filename: "{app}\Delima.Launcher.exe"; Components: lab
Name: "{group}\Sediakan PC Makmal"; Filename: "{app}\Delima.Provision.exe"; Components: lab
Name: "{group}\Alat Pentadbir";     Filename: "{app}\Delima.Admin.exe";    Components: admin
Name: "{commonstartup}\DELIMa";     Filename: "{app}\Delima.Launcher.exe"; Components: lab; Tasks: startup

[Tasks]
Name: "startup";      Description: "Mulakan semasa log masuk (mod kiosk)"; Components: lab; Flags: unchecked
Name: "edgepolicy";   Description: "Guna dasar Edge sekolah — melumpuhkan pengurus kata laluan, DevTools dan mod InPrivate pada SELURUH PC ini"; Components: lab; Flags: unchecked
Name: "chromepolicy"; Description: "Guna dasar Chrome sekolah — melumpuhkan pengurus kata laluan, DevTools dan mod inkognito pada SELURUH PC ini"; Components: lab; Flags: unchecked

[Registry]
Root: HKLM; Subkey: "SOFTWARE\Policies\Microsoft\Edge"; ValueType: dword; \
  ValueName: "PasswordManagerEnabled"; ValueData: 0; Tasks: edgepolicy; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Policies\Microsoft\Edge"; ValueType: dword; \
  ValueName: "DeveloperToolsAvailability"; ValueData: 2; Tasks: edgepolicy; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Policies\Microsoft\Edge"; ValueType: dword; \
  ValueName: "InPrivateModeAvailability"; ValueData: 1; Tasks: edgepolicy; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Policies\Microsoft\Edge"; ValueType: dword; \
  ValueName: "BrowserSignin"; ValueData: 0; Tasks: edgepolicy; Flags: uninsdeletevalue

Root: HKLM; Subkey: "SOFTWARE\Policies\Google\Chrome"; ValueType: dword; \
  ValueName: "PasswordManagerEnabled"; ValueData: 0; Tasks: chromepolicy; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Policies\Google\Chrome"; ValueType: dword; \
  ValueName: "DeveloperToolsAvailability"; ValueData: 2; Tasks: chromepolicy; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Policies\Google\Chrome"; ValueType: dword; \
  ValueName: "IncognitoModeAvailability"; ValueData: 1; Tasks: chromepolicy; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Policies\Google\Chrome"; ValueType: dword; \
  ValueName: "BrowserSignin"; ValueData: 0; Tasks: chromepolicy; Flags: uninsdeletevalue

[Run]
; Strip inherited permissions from %ProgramData% immediately upon install to close the exposure window before first launch
Filename: "{sys}\icacls.exe"; Parameters: """{commonappdata}\DELIMa Launcher"" /inheritance:r /grant:r ""*S-1-5-18:(OI)(CI)F"" /grant:r ""*S-1-5-32-544:(OI)(CI)F"""; Flags: runhidden

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    Exec(ExpandConstant('{sys}\icacls.exe'),
      Format('"%s" /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" /grant:r "*S-1-5-32-544:(OI)(CI)F"', [ExpandConstant('{commonappdata}\DELIMa Launcher')]),
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

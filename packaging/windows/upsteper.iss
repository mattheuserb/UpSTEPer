; Inno Setup 6 script for the Windows installer. Built by scripts/build-installer.ps1, which passes:
;   /DAppVersion=0.1.0-alpha        version shown to users (SemVer)
;   /DNumericVersion=0.1.0.0        file version of the setup executable (numbers only)
;   /DPublishDir=<folder>           output of scripts/publish.ps1
;   /DOutputDir=<folder>            where the setup executable goes
; Design: docs/packaging.md.

#ifndef AppVersion
  #error AppVersion is not defined; build with scripts/build-installer.ps1
#endif

#define AppName "UpSTEPer"
#define AppExe "UpSTEPer.Desktop.exe"
#define ProgId "UpSTEPer.StepFile"
#define RepoUrl "https://github.com/mattheuserb/UpSTEPer"

[Setup]
; Never change the AppId: it identifies the installation, so new versions install over old ones.
AppId={{7E87DD82-E5B3-4591-9C70-AB8838614F28}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Mattheus Erb
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
AppCopyright=Copyright (c) 2026 Mattheus Erb and contributors
VersionInfoVersion={#NumericVersion}
; Per user without admin rights by default; the dialog offers an installation for all users.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
LicenseFile=..\..\LICENSE
SetupIconFile=..\..\src\UpSTEPer.App\Assets\upsteper.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
OutputDir={#OutputDir}
OutputBaseFilename=UpSTEPer-{#AppVersion}-win-x64-setup
ChangesAssociations=yes
ChangesEnvironment=yes
CloseApplications=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
en.OpenWith=Offer UpSTEPer in "Open with" for STEP files (.stp, .step)
de.OpenWith=UpSTEPer unter „Öffnen mit“ für STEP-Dateien anbieten (.stp, .step)
en.AddToPath=Add the command-line tool "upsteper" to PATH
de.AddToPath=Kommandozeilenwerkzeug „upsteper“ zum PATH hinzufügen
en.Integration=Integration:
de.Integration=Integration:
en.StepFile=STEP model
de.StepFile=STEP-Modell

[Tasks]
Name: "openwith"; Description: "{cm:OpenWith}"; GroupDescription: "{cm:Integration}"
Name: "addtopath"; Description: "{cm:AddToPath}"; GroupDescription: "{cm:Integration}"; Flags: unchecked
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; "Open with" only: UpSTEPer is listed for .stp/.step but never made the default program.
; HKA is HKCU for a per-user installation and HKLM for all users.
Root: HKA; Subkey: "Software\Classes\{#ProgId}"; ValueType: string; ValueName: ""; ValueData: "{cm:StepFile}"; Flags: uninsdeletekey; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\{#ProgId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"",0"; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\{#ProgId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\.stp\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\.step\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#AppName}"; Flags: uninsdeletekey; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".stp"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".step"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: openwith

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; Settings and logs in %LOCALAPPDATA%\UpSTEPer belong to the user and stay after uninstalling.

[Code]
// PATH entry for the CLI: the user's PATH for a per-user installation, the system PATH for all users.
function EnvironmentKey(var Root: Integer): String;
begin
  if IsAdminInstallMode then
  begin
    Root := HKEY_LOCAL_MACHINE;
    Result := 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  end
  else
  begin
    Root := HKEY_CURRENT_USER;
    Result := 'Environment';
  end;
end;

function PathContains(const Paths, Dir: String): Boolean;
begin
  Result := Pos(';' + Uppercase(Dir) + ';', ';' + Uppercase(Paths) + ';') > 0;
end;

procedure AddToPath(const Dir: String);
var
  Root: Integer;
  Key, Paths: String;
begin
  Key := EnvironmentKey(Root);
  if not RegQueryStringValue(Root, Key, 'Path', Paths) then
    Paths := '';
  if PathContains(Paths, Dir) then
    exit;
  if (Paths <> '') and (Copy(Paths, Length(Paths), 1) <> ';') then
    Paths := Paths + ';';
  RegWriteExpandStringValue(Root, Key, 'Path', Paths + Dir);
end;

procedure RemoveFromPath(const Dir: String);
var
  Root: Integer;
  Key, Paths: String;
  P: Integer;
begin
  Key := EnvironmentKey(Root);
  if not RegQueryStringValue(Root, Key, 'Path', Paths) then
    exit;
  Paths := ';' + Paths + ';';
  P := Pos(';' + Uppercase(Dir) + ';', Uppercase(Paths));
  if P = 0 then
    exit;
  Delete(Paths, P, Length(Dir) + 1);
  Paths := Copy(Paths, 2, Length(Paths) - 2);
  RegWriteExpandStringValue(Root, Key, 'Path', Paths);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('addtopath') then
    AddToPath(ExpandConstant('{app}'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Also covers installations where the task was not chosen: removing an absent entry does nothing.
  if CurUninstallStep = usPostUninstall then
    RemoveFromPath(ExpandConstant('{app}'));
end;

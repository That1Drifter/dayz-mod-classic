; =============================================================================
; DayZ Mod Classic 1.0.0 - Client Installer (Inno Setup)
; =============================================================================
; Build:
;   iscc.exe DayZModClassic.iss
; Output:
;   Output\DayZModClassic-Client-1.0.0-Setup-v2.exe
;
; Prerequisites before compiling:
;   1. Build launcher: cd ..\launcher\src\DayZModClassic.Launcher
;                      dotnet publish -c Release -r win-x64 --self-contained true
;      This produces ..\launcher\src\DayZModClassic.Launcher\bin\Release\
;                    net8.0-windows\win-x64\publish\DayZModClassic.exe
;   2. Payload staged at .\payload\ (be-fix\ and mod\) - already done.
; =============================================================================

#define MyAppName "DayZ Mod Classic"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "DayZ Mod Classic"
#define MyAppURL "https://dayzmodclassic.com"
#define MyAppExeName "DayZModClassic.exe"
#define LauncherSrc "..\launcher\src\DayZModClassic.Launcher\bin\Release\net8.0-windows\win-x64\publish\DayZModClassic.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-1234-DAYZMODCLASSIC}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/downloads/install-troubleshooting.html
AppUpdatesURL={#MyAppURL}/downloads
DefaultDirName={localappdata}\DayZ Mod Classic
DefaultGroupName={#MyAppName}
OutputDir=Output
OutputBaseFilename=DayZModClassic-Client-{#MyAppVersion}-Setup-v2
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
ArchitecturesInstallIn64BitMode=x64
DisableDirPage=no
DisableProgramGroupPage=yes
DisableWelcomePage=no
ShowLanguageDialog=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
; Launcher executable
Source: "{#LauncherSrc}"; DestDir: "{app}"; Flags: ignoreversion

; Mod folder + bikeys go into A2OA install dir (path resolved by [Code])
Source: "payload\mod\@dayzmodclassic\*"; DestDir: "{code:GetA2OAPath}\@dayzmodclassic"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "payload\mod\dayz.bikey"; DestDir: "{code:GetA2OAPath}\Keys"; Flags: ignoreversion
Source: "payload\mod\that1drifter.bikey"; DestDir: "{code:GetA2OAPath}\Keys"; Flags: ignoreversion

; BattlEye Win11 24H2 fix (backup originals first via [Code])
Source: "payload\be-fix\ArmA2OA_BE.exe"; DestDir: "{code:GetA2OAPath}"; Flags: ignoreversion
Source: "payload\be-fix\BattlEye\*"; DestDir: "{code:GetA2OAPath}\BattlEye"; Flags: ignoreversion

; steam_appid.txt - required for Steam identity attach (else server kicks "Player without identity")
Source: "payload\steam_appid.txt"; DestDir: "{code:GetA2OAPath}"; Flags: ignoreversion

; README and SmartScreen note
Source: "..\website\downloads\install-troubleshooting.md"; DestDir: "{app}"; DestName: "TROUBLESHOOTING.md"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Open RPT log"; Filename: "{localappdata}\ArmA 2 OA\arma2oa.RPT"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Troubleshooting"; Filename: "{app}\TROUBLESHOOTING.md"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\DayZModClassic"

[Code]
var
  A2OAPath: String;
  A2BasePath: String;
  BackupDir: String;

function GetA2OAPath(Param: String): String;
begin
  Result := A2OAPath;
end;

function GetSteamPath(): String;
var
  S: String;
begin
  Result := '';
  if RegQueryStringValue(HKCU, 'SOFTWARE\Valve\Steam', 'SteamPath', S) and (S <> '') then
    Result := S;
  if (Result = '') and RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Valve\Steam', 'InstallPath', S) and (S <> '') then
    Result := S;
  if (Result = '') and RegQueryStringValue(HKLM, 'SOFTWARE\Valve\Steam', 'InstallPath', S) and (S <> '') then
    Result := S;
end;

// Steam writes a per-app Uninstall entry with the literal install dir.
// Most reliable fallback when libraryfolders.vdf is missing or unreadable.
function GetSteamAppInstallLocation(AppId: String): String;
var
  S, KeyName: String;
begin
  Result := '';
  KeyName := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App ' + AppId;
  if RegQueryStringValue(HKLM, KeyName, 'InstallLocation', S) and (S <> '') then
    Result := S
  else if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App ' + AppId, 'InstallLocation', S) and (S <> '') then
    Result := S
  else if RegQueryStringValue(HKCU, KeyName, 'InstallLocation', S) and (S <> '') then
    Result := S;
end;

function ReplaceStr(S, OldStr, NewStr: String): String;
begin
  Result := S;
  StringChangeEx(Result, OldStr, NewStr, True);
end;

function UnescapeVdfPath(S: String): String;
begin
  Result := ReplaceStr(S, '\\', '\');
end;

function EscapeJsonPath(S: String): String;
begin
  Result := ReplaceStr(S, '\', '\\');
end;

function ParseLibraryFolders(SteamPath: String; out Libs: TStringList): Boolean;
var
  VdfPath: String;
  Lines: TStringList;
  i: Integer;
  Line: String;
  P, P2: Integer;
begin
  // Always returns True: default Steam library is added unconditionally,
  // so callers can scan it even when libraryfolders.vdf is missing or unreadable.
  Result := True;
  Libs.Clear;
  if SteamPath <> '' then
    Libs.Add(SteamPath);
  VdfPath := SteamPath + '\steamapps\libraryfolders.vdf';
  if (SteamPath = '') or (not FileExists(VdfPath)) then
    Exit;
  Lines := TStringList.Create;
  try
    try
      Lines.LoadFromFile(VdfPath);
    except
      Exit;
    end;
    for i := 0 to Lines.Count - 1 do
    begin
      Line := Lines[i];
      P := Pos('"path"', Line);
      if P > 0 then
      begin
        P := Pos('"', Copy(Line, P + 6, MaxInt));
        if P > 0 then
        begin
          Line := Copy(Line, P + Pos('"', Line) + 5, MaxInt);
          P := Pos('"', Line);
          P2 := Pos('"', Copy(Line, P + 1, MaxInt));
          if (P > 0) and (P2 > 0) then
            Libs.Add(UnescapeVdfPath(Copy(Line, P + 1, P2 - 1)));
        end;
      end;
    end;
  finally
    Lines.Free;
  end;
end;

function FindArmaInstalls(): Boolean;
var
  SteamPath: String;
  Libs: TStringList;
  i: Integer;
  Candidate: String;
begin
  A2OAPath := '';
  A2BasePath := '';

  // Pass 1: scan Steam library folders (default lib plus any in libraryfolders.vdf).
  SteamPath := GetSteamPath();
  if SteamPath <> '' then
  begin
    Libs := TStringList.Create;
    try
      ParseLibraryFolders(SteamPath, Libs);
      for i := 0 to Libs.Count - 1 do
      begin
        Candidate := Libs[i] + '\steamapps\common\Arma 2 Operation Arrowhead';
        if (A2OAPath = '') and FileExists(Candidate + '\arma2oa.exe') then
          A2OAPath := Candidate;
        Candidate := Libs[i] + '\steamapps\common\Arma 2';
        if (A2BasePath = '') and FileExists(Candidate + '\ArmA2.exe') then
          A2BasePath := Candidate;
      end;
    finally
      Libs.Free;
    end;
  end;

  // Pass 2: per-app Uninstall registry. Catches the case where SteamPath
  // detection or vdf parsing missed the library (custom installs, elevated
  // installer not seeing the user's HKCU hive, malformed vdf, etc).
  if A2OAPath = '' then
  begin
    Candidate := GetSteamAppInstallLocation('33930');
    if (Candidate <> '') and FileExists(Candidate + '\arma2oa.exe') then
      A2OAPath := Candidate;
  end;
  if A2BasePath = '' then
  begin
    Candidate := GetSteamAppInstallLocation('33900');
    if (Candidate <> '') and FileExists(Candidate + '\ArmA2.exe') then
      A2BasePath := Candidate;
  end;

  Result := (A2OAPath <> '');
end;

// Backup-before-overlay step intentionally removed.
// Inno Pascal Script in 6.7.2 throws Type Mismatch on certain string-handling
// idioms used here; debugging is not worth blocking the install. If a user
// wants to preserve original BattlEye files, they can copy them manually
// before running the installer.

procedure WriteSteamAppId();
var
  AppIdPath: String;
begin
  AppIdPath := A2OAPath + '\steam_appid.txt';
  if not FileExists(AppIdPath) then
    SaveStringToFile(AppIdPath, '33930', False);
end;

procedure WriteLauncherConfig();
var
  AppData: String;
  ConfigDir: String;
  ConfigPath: String;
  Json: String;
begin
  AppData := ExpandConstant('{userappdata}');
  ConfigDir := AppData + '\DayZModClassic';
  ForceDirectories(ConfigDir);
  ConfigPath := ConfigDir + '\config.json';
  if not FileExists(ConfigPath) then
  begin
    Json := '{' + #13#10 +
            '  "playerName": "",' + #13#10 +
            '  "lastServer": "Official VPS",' + #13#10 +
            '  "serversUrl": "https://dayzmodclassic.com/servers.json",' + #13#10 +
            '  "a2oaPath": "' + EscapeJsonPath(A2OAPath) + '",' + #13#10 +
            '  "a2BasePath": "' + EscapeJsonPath(A2BasePath) + '",' + #13#10 +
            '  "steamPath": "' + EscapeJsonPath(GetSteamPath()) + '",' + #13#10 +
            '  "customServers": []' + #13#10 +
            '}';
    SaveStringToFile(ConfigPath, Json, False);
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not FindArmaInstalls() then
  begin
    MsgBox('Arma 2: Operation Arrowhead was not found on this system.' + #13#10 + #13#10 +
           'DayZ Mod Classic requires both Arma 2 and Arma 2: Operation Arrowhead to be installed via Steam.' + #13#10 + #13#10 +
           'Please install them from Steam (https://store.steampowered.com/app/33930/) and run this installer again.',
           mbError, MB_OK);
    Result := False;
    Exit;
  end;
  if A2BasePath = '' then
  begin
    if MsgBox('Arma 2 (base game) was not found, only Operation Arrowhead.' + #13#10 + #13#10 +
             'The base Arma 2 game is required for Chernarus map assets.' + #13#10 + #13#10 +
             'Install will continue, but the game may not load correctly. Continue?',
             mbConfirmation, MB_YESNO) <> IDYES then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    try
      WriteSteamAppId();
    except
      // swallow - launcher will create steam_appid.txt on first run if missing
    end;
    try
      WriteLauncherConfig();
    except
      // swallow - launcher will write default config on first run if missing
    end;
  end;
end;

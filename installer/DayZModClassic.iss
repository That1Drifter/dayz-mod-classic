; =============================================================================
; DayZ Mod Classic 1.0.0 - Client Installer (Inno Setup)
; =============================================================================
; Build:
;   iscc.exe DayZModClassic.iss
; Output:
;   Output\DayZModClassic-Client-1.0.0-Setup.exe
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
OutputBaseFilename=DayZModClassic-Client-{#MyAppVersion}-Setup
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
  if RegQueryStringValue(HKCU, 'SOFTWARE\Valve\Steam', 'SteamPath', S) then
    Result := S;
  if (Result = '') and RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Valve\Steam', 'InstallPath', S) then
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
  Result := False;
  Libs.Clear;
  Libs.Add(SteamPath);
  VdfPath := SteamPath + '\steamapps\libraryfolders.vdf';
  if not FileExists(VdfPath) then
    Exit;
  Lines := TStringList.Create;
  try
    Lines.LoadFromFile(VdfPath);
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
    Result := True;
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
  Result := False;
  A2OAPath := '';
  A2BasePath := '';
  SteamPath := GetSteamPath();
  if SteamPath = '' then
    Exit;
  Libs := TStringList.Create;
  try
    if not ParseLibraryFolders(SteamPath, Libs) then
      Exit;
    for i := 0 to Libs.Count - 1 do
    begin
      Candidate := Libs[i] + '\steamapps\common\Arma 2 Operation Arrowhead';
      if FileExists(Candidate + '\arma2oa.exe') and (A2OAPath = '') then
        A2OAPath := Candidate;
      Candidate := Libs[i] + '\steamapps\common\Arma 2';
      if FileExists(Candidate + '\ArmA2.exe') and (A2BasePath = '') then
        A2BasePath := Candidate;
    end;
    Result := (A2OAPath <> '');
  finally
    Libs.Free;
  end;
end;

procedure BackupOneFile(SrcDir, DstDir, FileName: String);
var
  Src, Dst: String;
begin
  Src := SrcDir + '\' + FileName;
  Dst := DstDir + '\' + FileName;
  if FileExists(Src) then
    FileCopy(Src, Dst, False);
end;

procedure BackupExistingBE();
var
  TimeStr: String;
begin
  if A2OAPath = '' then Exit;
  TimeStr := GetDateTimeString('yyyymmdd-hhnnss', '-', '');
  BackupDir := A2OAPath + '\_be-fix-backup-' + TimeStr;
  if not ForceDirectories(BackupDir + '\BattlEye') then Exit;

  BackupOneFile(A2OAPath,                BackupDir,                'ArmA2OA_BE.exe');
  BackupOneFile(A2OAPath + '\BattlEye',  BackupDir + '\BattlEye',  'BEService.exe');
  BackupOneFile(A2OAPath + '\BattlEye',  BackupDir + '\BattlEye',  'BEService_x64.exe');
  BackupOneFile(A2OAPath + '\BattlEye',  BackupDir + '\BattlEye',  'BEClient.dll');
  BackupOneFile(A2OAPath + '\BattlEye',  BackupDir + '\BattlEye',  'BEServer.dll');
end;

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
  if CurStep = ssInstall then
    BackupExistingBE();
  if CurStep = ssPostInstall then
  begin
    WriteSteamAppId();
    WriteLauncherConfig();
  end;
end;

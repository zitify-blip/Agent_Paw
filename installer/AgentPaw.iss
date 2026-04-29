; AgentPaw Inno Setup Script
; tracker §4.8 자동 업데이트 아키텍처 — 무인 설치 지원 (/SILENT /VERYSILENT)
; SHA256 sidecar 는 GitHub Actions release.yml 에서 별도 생성

[Setup]
AppId={{C8A7F2B0-1E4D-4F5A-9B3C-2D6F8E1A7C9D}
AppName=AgentPaw
AppVersion=0.5.41
AppVerName=AgentPaw 0.5.41
AppPublisher=zitify
AppPublisherURL=https://github.com/zitify-blip/Agent_Paw
DefaultDirName={autopf}\AgentPaw
DefaultGroupName=AgentPaw
OutputDir=installer_output
OutputBaseFilename=AgentPaw_Setup_v0.5.41

Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName=AgentPaw
SetupLogging=yes

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "taskbarpin"; Description: "작업 표시줄에 고정"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Windows 시작 시 자동 실행"; GroupDescription: "추가 옵션:"; Flags: unchecked

[Files]
; dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true 결과물 경로
Source: "..\AgentPaw\bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\AgentPaw"; Filename: "{app}\AgentPaw.exe"
Name: "{group}\Uninstall AgentPaw"; Filename: "{uninstallexe}"
Name: "{autodesktop}\AgentPaw"; Filename: "{app}\AgentPaw.exe"; Tasks: desktopicon
Name: "{userstartup}\AgentPaw"; Filename: "{app}\AgentPaw.exe"; Tasks: startupicon

[Run]
; 모바일 API 포트 방화벽 허용 (관리자 권한 필요 시 runas 없이 silently 시도)
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""AgentPaw MobileAPI"" dir=in action=allow protocol=TCP localport=47893"; Flags: runhidden; StatusMsg: "방화벽 규칙 등록 중..."
Filename: "{app}\AgentPaw.exe"; Description: "AgentPaw 실행"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""AgentPaw MobileAPI"""; Flags: runhidden

[UninstallDelete]
Type: dirifempty; Name: "{userappdata}\AgentPaw"

[Code]
procedure PinToTaskbar;
var
  ResultCode: Integer;
  PsCmd: string;
begin
  PsCmd := 'powershell -ExecutionPolicy Bypass -Command "' +
    '$shell = New-Object -ComObject Shell.Application; ' +
    '$folder = $shell.Namespace(''' + ExpandConstant('{app}') + '''); ' +
    '$item = $folder.ParseName(''AgentPaw.exe''); ' +
    '$verb = $item.Verbs() | Where-Object { $_.Name -match ''작업 표시줄에 고정|Pin to Tas'' }; ' +
    'if ($verb) { $verb.DoIt() }"';
  Exec('cmd.exe', '/c ' + PsCmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('taskbarpin') then
    PinToTaskbar;
end;

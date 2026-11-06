; InstaWords for Windows: per-user installer (Inno Setup 6). No admin rights, nothing else to install:
; the app runs on .NET Framework 4.8, which is part of Windows 10 and 11.
;
; Build (CI passes the version from package.json):
;   iscc /DAppVersion=2.2.0 /DSourceDir=..\InstaWords.App\bin\Release\net48 /DOutputDir=..\..\out windows\installer\InstaWords.iss
;
; Silent update (used by the app's «تحديث الآن»):
;   InstaWords_<ver>_x64-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /UPDATE
; /UPDATE makes setup wait for the running copy to exit, then start InstaWords again when done.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef NumericVersion
  #define NumericVersion AppVersion
#endif
#ifndef SourceDir
  #define SourceDir "..\InstaWords.App\bin\Release\net48"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\out"
#endif

; Inno Setup 6.x only (CI pins the exact release: INNO_SETUP_VERSION in .github/workflows/windows.yml).
#if Ver < EncodeVer(6, 0, 0) || Ver >= EncodeVer(7, 0, 0)
  #error Inno Setup 6.x is required to build this installer
#endif

#define AppName "InstaWords"
#define AppExe "InstaWords.exe"
; Keep in sync with UpdateService.InstallerAppId (the app finds its install through this key).
#define AppGuid "{8F3B6C2A-5D4E-4A7B-9C1F-2E6D8A0B4C71}"

[Setup]
AppId={{#AppGuid}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=FastKiwi
AppPublisherURL=https://fastkiwi.app/instawords/
AppSupportURL=https://github.com/sarmadiq9090-coder/tafket/issues
AppUpdatesURL=https://github.com/sarmadiq9090-coder/tafket/releases
AppCopyright=© FastKiwi · MIT License
VersionInfoVersion={#NumericVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup
; Per-user: installs to %LOCALAPPDATA%\Programs\InstaWords, no UAC prompt.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
UsePreviousAppDir=yes
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=InstaWords_{#AppVersion}_x64-setup
SetupIconFile=..\InstaWords.App\Assets\InstaWords.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName} — تفقيط
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
CloseApplicationsFilter={#AppExe}
RestartApplications=no
ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage
SetupLogging=yes

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
arabic.StartupTask=تشغيل InstaWords مع Windows (في شريط المهام، ليعمل الاختصار Ctrl+Alt+T دائماً)
english.StartupTask=Start InstaWords with Windows (in the notification area, so Ctrl+Alt+T always works)
arabic.StartupGroup=التشغيل:
english.StartupGroup=Startup:
arabic.AppComment=كتابة المبالغ بالحروف للشيكات والفواتير
english.AppComment=Amounts in words for cheques and invoices

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:StartupGroup}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\{#AppExe}.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\InstaWords.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Licenses\*"; DestDir: "{app}\Licenses"; Flags: ignoreversion

[InstallDelete]
; A portable marker copied in by hand would turn off in-app updates.
Type: files; Name: "{app}\portable.txt"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "{cm:AppComment}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "{cm:AppComment}"; Tasks: desktopicon

[Registry]
; Same value the app's «تشغيل مع Windows» switch writes, so the two stay in agreement.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "InstaWords"; \
  ValueData: """{app}\{#AppExe}"" --tray"; Flags: uninsdeletevalue; Tasks: startup

; The wizard's language: on its first run the app's interface starts in it (UiLanguage.InstallerChoice).
Root: HKCU; Subkey: "Software\FastKiwi\InstaWords"; ValueType: string; ValueName: "InstallerLanguage"; ValueData: "{language}"; \
  Flags: uninsdeletevalue uninsdeletekeyifempty

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; After a silent in-app update: start the new version again.
Filename: "{app}\{#AppExe}"; Flags: nowait; Check: IsUpdate

[Code]
const
  AppMutexName = 'Local\FastKiwi.InstaWords.Instance';
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

function IsUpdate: Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/UPDATE') = 0 then
      Result := True;
end;

function InitializeSetup: Boolean;
var
  Waited: Integer;
begin
  Result := True;
  // The app starts the update and then exits: give it a moment instead of asking the user.
  if IsUpdate then
  begin
    Waited := 0;
    while CheckForMutexes(AppMutexName) and (Waited < 100) do
    begin
      Sleep(100);
      Waited := Waited + 1;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
  Value: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Stop a running copy (it may be hidden in the tray) so its files can be removed.
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM InstaWords.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    // The app's own «تشغيل مع Windows» switch may have written the Run value too.
    if RegQueryStringValue(HKCU, RunKey, 'InstaWords', Value) then
      if Pos(Lowercase(ExpandConstant('{app}')), Lowercase(Value)) > 0 then
        RegDeleteValue(HKCU, RunKey, 'InstaWords');
  end;
end;

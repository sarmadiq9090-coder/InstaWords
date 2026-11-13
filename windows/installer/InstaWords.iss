; InstaWords for Windows: per-user installer (Inno Setup 6). No admin rights, nothing else to install:
; the app runs on .NET Framework 4.8, which is part of Windows 10 version 1903 and newer, and Windows 11.
;
; Build (CI passes the version from package.json):
;   iscc /DAppVersion=3.0.0 /DSourceDir=..\InstaWords.App\bin\Release\net48 /DOutputDir=..\..\out windows\installer\InstaWords.iss
;
; Silent update (used by the app's «تحديث الآن»):
;   InstaWords_<ver>_x64-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /UPDATE
; /UPDATE makes setup wait for the running copy to exit, then start InstaWords again when done:
;   - installed: the new version starts with --updated (the app says it was updated);
;   - not installed (an error, a file in use, a full disk): Setup rolls back, the program files kept in
;     {app}\update-backup before the install are put back (PrepareToInstall), and the copy the user
;     had starts again with --update-failed (the app says the update did not go through), so a failed
;     update never leaves InstaWords closed. This runs in the NEW setup, so it also covers an update
;     started by an older app (2.2.0 passes the same arguments). Not covered: Setup itself being killed.

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
AppSupportURL=https://github.com/sarmadiq9090-coder/InstaWords/issues
AppUpdatesURL=https://github.com/sarmadiq9090-coder/InstaWords/releases
AppCopyright=© 2026 FastKiwi (Sarmad) · All rights reserved
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
; .NET Framework 4.8 is built into Windows 10 only from version 1903 (build 18362): refuse older builds.
MinVersion=10.0.18362
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
arabic.BackupFailed=تعذّر حفظ نسخة احتياطية من البرنامج المثبّت قبل تحديثه (هل القرص ممتلئ؟)، فلم يُغيَّر شيء.
english.BackupFailed=Could not keep a backup of the installed program before updating it (is the disk full?), so nothing was changed.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:StartupGroup}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\{#AppExe}.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\InstaWords.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Licenses\*"; DestDir: "{app}\Licenses"; Flags: ignoreversion

[InstallDelete]
; A portable marker copied in by hand would turn off in-app updates.
Type: files; Name: "{app}\portable.txt"

[UninstallDelete]
; Left behind only if Setup itself was killed during an update (see PrepareToInstall). User data is elsewhere
; (%APPDATA%\FastKiwi\InstaWords) and is never deleted.
Type: filesandordirs; Name: "{app}\update-backup"

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
; After a silent in-app update: start the new version again (--updated: it tells the user).
Filename: "{app}\{#AppExe}"; Parameters: "--updated"; Flags: nowait; Check: IsUpdate

[Code]
const
  AppMutexName = 'Local\FastKiwi.InstaWords.Instance';
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppGuid}_is1';
  BackupFolder = 'update-backup';

var
  // Set once the files and registry entries are in place (ssPostInstall); still False if Setup stopped earlier.
  InstallFinished: Boolean;
  // Set once PrepareToInstall copied the installed program files to {app}\update-backup.
  BackupMade: Boolean;

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

function BackupDir: String;
begin
  Result := AddBackslash(ExpandConstant('{app}')) + BackupFolder;
end;

function BackupFile(const Name: String): Boolean;
var
  Source: String;
begin
  Source := AddBackslash(ExpandConstant('{app}')) + Name;
  if FileExists(Source) then
    Result := FileCopy(Source, AddBackslash(BackupDir) + Name, False)
  else
    Result := True;
end;

// Before Setup changes anything: keep a copy of the installed program files. Inno Setup's own rollback
// removes the files it added but cannot put back the ones it replaced, so a failed update could otherwise
// leave a mix of old and new files. If the copy cannot be made, nothing is changed at all.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Ok: Boolean;
begin
  Result := '';
  BackupMade := False;
  DelTree(BackupDir, True, True, True);
  if not FileExists(AddBackslash(ExpandConstant('{app}')) + '{#AppExe}') then
    Exit; // a first install: nothing to keep
  Ok := ForceDirectories(BackupDir);
  if Ok then Ok := BackupFile('{#AppExe}');
  if Ok then Ok := BackupFile('{#AppExe}.config');
  if Ok then Ok := BackupFile('InstaWords.Core.dll');
  if Ok then
  begin
    BackupMade := True;
    Log('Kept a copy of the installed program files in ' + BackupDir);
  end
  else
  begin
    DelTree(BackupDir, True, True, True);
    Result := CustomMessage('BackupFailed');
  end;
end;

function RestoreFile(const Name: String): Boolean;
var
  Backup: String;
begin
  Result := True;
  Backup := AddBackslash(BackupDir) + Name;
  if FileExists(Backup) then
  begin
    Result := FileCopy(Backup, AddBackslash(ExpandConstant('{app}')) + Name, False);
    if not Result then
      Log('Could not put back ' + Name);
  end;
end;

// The install did not finish: put the kept program files back, so the copy the user had works as before.
procedure RestoreBackup;
var
  Ok: Boolean;
begin
  try
    Ok := RestoreFile('{#AppExe}');
    if not RestoreFile('{#AppExe}.config') then Ok := False;
    if not RestoreFile('InstaWords.Core.dll') then Ok := False;
    if Ok then
    begin
      DelTree(BackupDir, True, True, True);
      Log('Put the previous program files back.');
    end;
  except
    Log('Could not put the previous program files back: ' + GetExceptionMessage);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    InstallFinished := True;
    if BackupMade then
      DelTree(BackupDir, True, True, True);
  end;
end;

// The installed InstaWords.exe: from this installation's uninstall entry (written by the version being
// updated), else the folder Setup worked on. Empty when neither is known.
function InstalledExe: String;
var
  Dir: String;
begin
  Result := '';
  Dir := '';
  if not RegQueryStringValue(HKCU, UninstallKey, 'InstallLocation', Dir) then
  begin
    try
      Dir := ExpandConstant('{app}');
    except
      Dir := '';
    end;
  end;
  if Dir <> '' then
    Result := AddBackslash(Dir) + '{#AppExe}';
end;

// A silent update that did not finish: its files are back as they were (Setup's rollback + RestoreBackup), so
// start the copy the user had again (with --update-failed, so it says the update did not go through) instead of leaving
// nothing running. A finished install starts the new version through [Run] instead.
procedure DeinitializeSetup;
var
  Exe: String;
  ResultCode: Integer;
begin
  if BackupMade and not InstallFinished then
    RestoreBackup;
  if IsUpdate and not InstallFinished then
  begin
    Exe := InstalledExe;
    if (Exe <> '') and FileExists(Exe) then
    begin
      Log('Update not finished: starting the previous copy again: ' + Exe);
      if not Exec(Exe, '--update-failed', ExtractFileDir(Exe), SW_SHOWNORMAL, ewNoWait, ResultCode) then
        Log('Could not start it: ' + SysErrorMessage(ResultCode));
    end
    else
      Log('Update not finished, and there is no installed copy to start again.');
  end;
end;

// Ends the copies of InstaWords that run from THIS installation (they may be hidden in the tray), so their
// files can be removed. Copies elsewhere (a portable copy, the Store copy) keep running.
procedure CloseInstalledCopies;
var
  Locator, Service, Found, Item: Variant;
  I: Integer;
  Closed: Boolean;
  Prefix, ExePath: String;
begin
  Closed := False;
  Prefix := Lowercase(AddBackslash(ExpandConstant('{app}')));
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Found := Service.ExecQuery('SELECT ProcessId, ExecutablePath FROM Win32_Process WHERE Name = ''{#AppExe}''');
    for I := 0 to Found.Count - 1 do
    begin
      Item := Found.ItemIndex(I);
      if not VarIsNull(Item.ExecutablePath) then
      begin
        // Pascal Script does not pass a Variant to a String parameter: assign it first.
        ExePath := Item.ExecutablePath;
        ExePath := Lowercase(ExePath);
        if Pos(Prefix, ExePath) = 1 then
        begin
          Log('Closing the running copy: ' + ExePath);
          Item.Terminate();
          Closed := True;
        end
        else
          Log('Leaving a copy outside this installation running: ' + ExePath);
      end;
    end;
    // Termination is asynchronous: let Windows release the exe before its file is deleted.
    if Closed then
      Sleep(1000);
  except
    Log('Could not list the running copies: ' + GetExceptionMessage);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Value: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    CloseInstalledCopies;
    // The app's own «تشغيل مع Windows» switch may have written the Run value too.
    if RegQueryStringValue(HKCU, RunKey, 'InstaWords', Value) then
      if Pos(Lowercase(ExpandConstant('{app}')), Lowercase(Value)) > 0 then
        RegDeleteValue(HKCU, RunKey, 'InstaWords');
  end;
end;

; InstaWords for Windows: per-user installer (Inno Setup 6). No admin rights, nothing else to install:
; the app runs on .NET Framework 4.8, which is part of Windows 10 and 11.
;
; Build (CI passes the version from package.json):
;   iscc /DAppVersion=2.2.0 /DSourceDir=..\InstaWords.App\bin\Release\net48 /DOutputDir=..\..\out windows\installer\InstaWords.iss
; The Excel add-in is built first (npm run build:xlam → out\InstaWords.xlam; /DXlamFile=<path> overrides it).
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
#ifndef XlamFile
  #define XlamFile "..\..\out\InstaWords.xlam"
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
AppCopyright=© 2026 FastKiwi (Sarmad) · MIT License
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
arabic.XlamGroup=إضافة Excel (تجريبية):
english.XlamGroup=Excel add-in (experimental):
arabic.XlamTask=تثبيت إضافة Excel (InstaWords.xlam)
english.XlamTask=Install the Excel add-in (InstaWords.xlam)
arabic.ExcelOpenTitle=Excel مفتوح الآن
english.ExcelOpenTitle=Excel is open
arabic.ExcelOpenText=احفظ عملك وأغلق Excel قبل المتابعة، حتى تُثبَّت الإضافة InstaWords.xlam ويبقى تفعيلها. المثبّت لا يغلق Excel بنفسه.
english.ExcelOpenText=Save your work and close Excel before you continue, so the InstaWords.xlam add-in is installed and stays enabled. Setup does not close Excel for you.
arabic.ExcelCheckAgain=أغلقت Excel، تحقّق مرة أخرى
english.ExcelCheckAgain=I have closed Excel, check again
arabic.ExcelContinue=المتابعة دون إغلاق Excel
english.ExcelContinue=Continue without closing Excel
arabic.ExcelContinueNote=إن لم تعمل الدالة TAFQIT بعد إعادة تشغيل Excel، فحدّد InstaWords مرة واحدة في قائمة الوظائف الإضافية.
english.ExcelContinueNote=If TAFQIT does not work after Excel restarts, tick InstaWords once in the Add-ins list.
arabic.XlamInUse=تعذّر تحديث %1 لأن Excel يستخدمه. احفظ عملك وأغلق Excel ثم أعد المحاولة.
english.XlamInUse=%1 could not be updated because Excel is using it. Save your work, close Excel, then retry.
arabic.XlamRetry=إعادة المحاولة
english.XlamRetry=Retry
arabic.XlamSkip=تخطي الإضافة
english.XlamSkip=Skip the add-in
arabic.XlamSkipNote=تبقى النسخة الحالية من الإضافة كما هي.
english.XlamSkipNote=The add-in stays as it is.
arabic.XlamRemoveInUse=تعذّر حذف %1 لأن Excel يستخدمه. احفظ عملك وأغلق Excel ثم أعد المحاولة.
english.XlamRemoveInUse=%1 could not be removed because Excel is using it. Save your work, close Excel, then retry.
arabic.XlamLeave=اتركها
english.XlamLeave=Leave it
arabic.XlamLeaveNote=احذف الملف بنفسك لاحقاً بعد إغلاق Excel.
english.XlamLeaveNote=Delete the file yourself later, after closing Excel.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:StartupGroup}"; Flags: unchecked
; Unchecked: the add-in is experimental, most users have no Excel, and a silent in-app update keeps each user's
; previous choice (UsePreviousTasks), so nobody gets macros added to Excel without ticking this once.
; CI: windows/installer/test-xlam-task.ps1.
Name: "xlamaddin"; Description: "{cm:XlamTask}"; GroupDescription: "{cm:XlamGroup}"; Flags: unchecked

[Files]
; The Excel add-in: [Code] (InstallExcelAddIn) copies it into the user's AddIns folder when the task is chosen.
; Listed first so extracting it from the solid archive is quick.
Source: "{#XlamFile}"; Flags: dontcopy noencryption
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
  AppKey = 'Software\FastKiwi\InstaWords';
  OfficeKey = 'Software\Microsoft\Office';
  XlamName = 'InstaWords.xlam';
  // Where the installed add-in is: written only when this installer copied it, so uninstall never
  // removes a copy the user put in the AddIns folder by hand.
  XlamMarker = 'ExcelAddIn';
  MaxOpen = 99;

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

// ---- Excel add-in (task xlamaddin) ----
// 1. InstaWords.xlam goes to %APPDATA%\Microsoft\AddIns, Excel's own add-ins folder. Setup writes it from its
//    archive, so it carries no Mark of the Web: no "Unblock" step. Excel lists every file in that folder in
//    File > Options > Add-ins, so even without step 2 the user only has to tick it once.
// 2. It is enabled the way Excel itself does it: a value OPEN, OPEN1, OPEN2 ... under
//    HKCU\Software\Microsoft\Office\<version>\Excel\Options holding the quoted path. One value, the first
//    free number, for every Excel version that has run for this user (16.0 = 2016, 2019, 2021 and 365).
//    Existing values are never changed. If another InstaWords.xlam (copied by hand elsewhere) is already
//    enabled, nothing is added: Excel cannot open two workbooks with the same name.
// 3. Uninstall removes the file and only the OPEN value that points to it, then moves the following
//    values down one number, so the list has no gap (Excel reads them in order from OPEN).
// Excel is never started or needed. It holds the add-in open while it runs, so a locked file is reported
// (or, in a silent update, left for the next update); setup never closes Excel.

function ExcelRunning: Boolean;
begin
  // XLMAIN: the class of Excel's main window.
  Result := FindWindowByClassName('XLMAIN') <> 0;
end;

function AddInTarget: String;
begin
  Result := ExpandConstant('{userappdata}\Microsoft\AddIns\') + XlamName;
end;

function OpenValueName(const I: Integer): String;
begin
  if I = 0 then
    Result := 'OPEN'
  else
    Result := 'OPEN' + IntToStr(I);
end;

// The file in an OPEN value: '/R "C:\x\a.xlam"' -> 'C:\x\a.xlam'; '"a.xlam"' -> 'a.xlam'.
function OpenValuePath(const Value: String): String;
var
  P: Integer;
begin
  Result := Trim(Value);
  P := Pos('"', Result);
  if P > 0 then
  begin
    Result := Copy(Result, P + 1, Length(Result));
    P := Pos('"', Result);
    if P > 0 then
      Result := Copy(Result, 1, P - 1);
  end
  else
    while (Length(Result) > 1) and (Result[1] = '/') and (Pos(' ', Result) > 0) do
      Result := Trim(Copy(Result, Pos(' ', Result) + 1, Length(Result)));
end;

// Our add-in: its full path, or the bare file name (Excel's way of naming a file in the AddIns folder).
function IsOurOpenValue(const Value, Target: String): Boolean;
var
  Path: String;
begin
  Path := OpenValuePath(Value);
  Result := SameText(Path, Target) or SameText(Path, XlamName);
end;

function ExcelOptionsKey(const Version: String): String;
begin
  Result := OfficeKey + '\' + Version + '\Excel\Options';
end;

// Office versions with an Excel key for this user (Excel 2007 = 12.0 and later open .xlam files).
function ExcelVersions: TArrayOfString;
var
  Names, Found: TArrayOfString;
  I, N, Dot: Integer;
begin
  N := 0;
  SetArrayLength(Found, 0);
  if RegGetSubkeyNames(HKEY_CURRENT_USER, OfficeKey, Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      Dot := Pos('.', Names[I]);
      if (Dot > 1) and (StrToIntDef(Copy(Names[I], 1, Dot - 1), 0) >= 12) and
         (StrToIntDef(Copy(Names[I], Dot + 1, Length(Names[I])), -1) >= 0) and
         RegKeyExists(HKEY_CURRENT_USER, OfficeKey + '\' + Names[I] + '\Excel') then
      begin
        SetArrayLength(Found, N + 1);
        Found[N] := Names[I];
        N := N + 1;
      end;
    end;
  Result := Found;
end;

procedure RegisterAddIn(const Version, Target: String);
var
  Key, Value: String;
  I, Free: Integer;
begin
  Key := ExcelOptionsKey(Version);
  Free := -1;
  for I := 0 to MaxOpen do
    if RegValueExists(HKEY_CURRENT_USER, Key, OpenValueName(I)) then
    begin
      if RegQueryStringValue(HKEY_CURRENT_USER, Key, OpenValueName(I), Value) then
      begin
        if IsOurOpenValue(Value, Target) then
        begin
          Log(Format('Excel add-in: already enabled for Excel %s (%s).', [Version, OpenValueName(I)]));
          Exit;
        end;
        if SameText(ExtractFileName(OpenValuePath(Value)), XlamName) then
        begin
          Log(Format('Excel add-in: Excel %s already loads another copy (%s = %s); not adding a second one.', [Version, OpenValueName(I), Value]));
          Exit;
        end;
      end;
    end
    else if Free < 0 then
      Free := I;
  if Free < 0 then
    Log(Format('Excel add-in: no free OPEN value for Excel %s.', [Version]))
  else if RegWriteStringValue(HKEY_CURRENT_USER, Key, OpenValueName(Free), '"' + Target + '"') then
    Log(Format('Excel add-in: enabled for Excel %s (%s).', [Version, OpenValueName(Free)]))
  else
    Log(Format('Excel add-in: could not write %s for Excel %s.', [OpenValueName(Free), Version]));
end;

procedure UnregisterAddIn(const Version, Target: String);
var
  Key, Value: String;
  I, J: Integer;
  Found: Boolean;
begin
  Key := ExcelOptionsKey(Version);
  repeat
    Found := False;
    for I := 0 to MaxOpen do
      if RegQueryStringValue(HKEY_CURRENT_USER, Key, OpenValueName(I), Value) and IsOurOpenValue(Value, Target) then
      begin
        Found := True;
        // Close the gap: OPEN<n+1> becomes OPEN<n> and so on, values unchanged.
        J := I;
        while (J < MaxOpen) and RegValueExists(HKEY_CURRENT_USER, Key, OpenValueName(J + 1)) and
              RegQueryStringValue(HKEY_CURRENT_USER, Key, OpenValueName(J + 1), Value) do
        begin
          RegWriteStringValue(HKEY_CURRENT_USER, Key, OpenValueName(J), Value);
          J := J + 1;
        end;
        RegDeleteValue(HKEY_CURRENT_USER, Key, OpenValueName(J));
        Log(Format('Excel add-in: removed from Excel %s (%s).', [Version, OpenValueName(I)]));
        Break;
      end;
  until not Found;
end;

// Asks whether to try again while Excel holds the add-in. Suppressed message boxes answer "no".
function AskRetryXlam(const MessageName, LeaveName, LeaveNoteName, Target: String): Boolean;
begin
  // (No line of [Code] may start with "[": Inno would read it as a section tag.)
  Result := SuppressibleTaskDialogMsgBox(CustomMessage('ExcelOpenTitle'),
    FmtMessage(CustomMessage(MessageName), [Target]), mbError, MB_YESNO, [CustomMessage('XlamRetry'),
    CustomMessage(LeaveName) + #13#10 + CustomMessage(LeaveNoteName)], 0, IDNO) = IDYES;
end;

procedure InstallExcelAddIn;
var
  Source, Target: String;
  Versions: TArrayOfString;
  I: Integer;
  Copied: Boolean;
begin
  Target := AddInTarget;
  ExtractTemporaryFile(XlamName);
  Source := ExpandConstant('{tmp}\') + XlamName;
  ForceDirectories(ExtractFileDir(Target));
  repeat
    // Delete first, so nothing of an older file (such as a Zone.Identifier stream) is kept.
    if FileExists(Target) then
      DeleteFile(Target);
    Copied := (not FileExists(Target)) and CopyFile(Source, Target, True);
  until Copied or not AskRetryXlam('XlamInUse', 'XlamSkip', 'XlamSkipNote', Target);
  if Copied then
  begin
    Log('Excel add-in: installed ' + Target);
    RegWriteStringValue(HKEY_CURRENT_USER, AppKey, XlamMarker, Target);
  end
  else
    Log('Excel add-in: ' + Target + ' is in use (Excel is running); the existing copy is kept.');
  if not FileExists(Target) then
    Exit;
  Versions := ExcelVersions;
  if GetArrayLength(Versions) = 0 then
    Log('Excel add-in: Excel has not run for this user yet; it will be listed in File > Options > Add-ins.');
  for I := 0 to GetArrayLength(Versions) - 1 do
    RegisterAddIn(Versions[I], Target);
end;

procedure UninstallExcelAddIn;
var
  Target: String;
  Versions: TArrayOfString;
  I: Integer;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, AppKey, XlamMarker, Target) or (Target = '') then
    Exit;
  Versions := ExcelVersions;
  for I := 0 to GetArrayLength(Versions) - 1 do
    UnregisterAddIn(Versions[I], Target);
  while FileExists(Target) and not DeleteFile(Target) do
    if not AskRetryXlam('XlamRemoveInUse', 'XlamLeave', 'XlamLeaveNote', Target) then
    begin
      Log('Excel add-in: ' + Target + ' is in use and was left in place.');
      Break;
    end;
  RegDeleteValue(HKEY_CURRENT_USER, AppKey, XlamMarker);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  // Excel saves its add-in list when it closes, so it is best closed before the add-in is enabled.
  if (CurPageID = wpReady) and not WizardSilent and WizardIsTaskSelected('xlamaddin') then
    while ExcelRunning do
      if TaskDialogMsgBox(CustomMessage('ExcelOpenTitle'), CustomMessage('ExcelOpenText'), mbInformation, MB_YESNO, [CustomMessage('ExcelCheckAgain'),
           CustomMessage('ExcelContinue') + #13#10 + CustomMessage('ExcelContinueNote')], 0) <> IDYES then
        Break;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('xlamaddin') then
    InstallExcelAddIn;
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
    // The Excel add-in, when this installer put it there (task xlamaddin).
    UninstallExcelAddIn;
  end;
end;

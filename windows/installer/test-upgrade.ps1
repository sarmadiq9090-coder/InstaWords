<#
  CI check of the classic installer on a throwaway machine (GitHub's windows runner): upgrade from the last
  published release, the single-instance hand-over, a failed update, and uninstall. Never run this on your
  own PC: it installs, closes and uninstalls InstaWords and writes test data to %APPDATA%\FastKiwi\InstaWords.

    pwsh windows/installer/test-upgrade.ps1 -OldSetup out\old\InstaWords_2.2.0_x64-setup.exe `
      -NewSetup out\InstaWords_2.3.0_x64-setup.exe -Portable out\InstaWords_2.3.0_x64-portable.zip `
      -NewerExe out\newer\InstaWords.exe [-OutDir out\upgrade-test]

  -NewerExe is this commit's app built with a higher version (99.0.0): the hand-over needs a newer copy.
  What it checks (each line of the log is PASS or FAIL):
    A. the released setup installs silently; test settings and history are seeded and the old copy runs in the tray;
    B. hand-over from a copy WITHOUT the hand-over protocol (the release): a newer copy started while it runs
       closes it (Restart Manager) and takes its place; the data is untouched;
    C. upgrade with the exact arguments of the in-app update, while the old copy runs in the tray: Setup
       closes it, replaces the files, starts the new version (--updated); settings and history survive;
    D. the same version started again only shows the running copy (and exits);
    E. hand-over WITH the protocol: a newer copy asks the installed one to exit and takes over; an older
       copy started then never closes the newer one;
    F. a failed silent update (a program file kept locked) restarts the installed copy (--update-failed)
       and leaves its files working;
    G. uninstall closes only the copy inside the install folder (a portable copy elsewhere keeps running),
       removes the program and its registration, and keeps the user data.
  Setup and uninstall logs are left in <OutDir>.
#>
param(
  [Parameter(Mandatory = $true)][string]$OldSetup,
  [Parameter(Mandatory = $true)][string]$NewSetup,
  [Parameter(Mandatory = $true)][string]$Portable,
  [Parameter(Mandatory = $true)][string]$NewerExe,
  [string]$OutDir = "out\upgrade-test"
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$OldSetup = (Resolve-Path $OldSetup).Path
$NewSetup = (Resolve-Path $NewSetup).Path
$Portable = (Resolve-Path $Portable).Path
$NewerExe = (Resolve-Path $NewerExe).Path

# The app's own update arguments (UpdateService.SilentInstallArgs).
$UpdateArgs = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /UPDATE'
$UninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F3B6C2A-5D4E-4A7B-9C1F-2E6D8A0B4C71}_is1'
$RunKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$Data = Join-Path $env:APPDATA "FastKiwi\InstaWords"
$Work = Join-Path $env:RUNNER_TEMP "instawords-upgrade"
if (-not $env:RUNNER_TEMP) { $Work = Join-Path $env:TEMP "instawords-upgrade" }
New-Item -ItemType Directory -Force -Path $Work | Out-Null

$script:failures = 0
function Check([bool]$ok, [string]$what) {
  if ($ok) { Write-Host "PASS  $what" }
  else { Write-Host "FAIL  $what"; Write-Host "::error::$what"; $script:failures++ }
}
function Wait-Until([scriptblock]$condition, [int]$seconds) {
  $deadline = (Get-Date).AddSeconds($seconds)
  do {
    if (& $condition) { return $true }
    Start-Sleep -Milliseconds 500
  } while ((Get-Date) -lt $deadline)
  return [bool](& $condition)
}
# Running InstaWords.exe processes, with their path and command line.
function Copies { @(Get-CimInstance Win32_Process -Filter "Name = 'InstaWords.exe'") }
function Copies-In([string]$dir) {
  $prefix = $dir.TrimEnd('\') + '\'
  @(Copies | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) })
}
function Stop-Copies([string]$dir) {
  foreach ($p in Copies-In $dir) { Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue }
  [void](Wait-Until { (Copies-In $dir).Count -eq 0 } 15)
}
# Runs a setup and waits for that process only (Start-Process -Wait would also wait for the app it starts).
function Invoke-Setup([string]$exe, [string]$arguments, [int]$timeoutSec = 300) {
  Write-Host "> $(Split-Path $exe -Leaf) $arguments"
  $psi = New-Object System.Diagnostics.ProcessStartInfo $exe
  $psi.Arguments = $arguments
  $psi.UseShellExecute = $false
  $p = [System.Diagnostics.Process]::Start($psi)
  if (-not $p.WaitForExit($timeoutSec * 1000)) { $p.Kill(); throw "$exe did not finish within $timeoutSec s" }
  Write-Host "  exit code $($p.ExitCode)"
  return $p.ExitCode
}
function Start-Copy([string]$exe, [string]$arguments = "", [string]$dataDir = "") {
  if ($dataDir) { $env:INSTAWORDS_DATA_DIR = $dataDir }
  try {
    if ($arguments) { Start-Process -FilePath $exe -ArgumentList $arguments | Out-Null }
    else { Start-Process -FilePath $exe | Out-Null }
  }
  finally { Remove-Item Env:INSTAWORDS_DATA_DIR -ErrorAction SilentlyContinue }
}
function Hash([string]$path) { (Get-FileHash -Algorithm SHA256 $path).Hash }
function Install-Dir {
  try { return (Get-ItemProperty -Path $UninstallKey -Name InstallLocation -ErrorAction Stop).InstallLocation.TrimEnd('\') } catch { return $null }
}
function Check-Data([string]$when) {
  $settings = Get-Content (Join-Path $Data "settings.json") -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
  $history = Get-Content (Join-Path $Data "history.json") -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
  Check (($settings -match 'CI-UPGRADE-MARKER') -and ($settings -match '"KWD"')) "${when}: settings kept (unit KWD, suffix marker)"
  Check ($history -match 'CI-HISTORY-MARKER') "${when}: history kept (marker entry)"
}

# Test settings: no update check (no network), no hotkey (several copies run at once), no toasts on copy.
$TestSettings = @'
{
  "schema": 1, "language": "ar", "unit": "KWD", "hundredsForm": "مائة", "decimals": "round",
  "prefix": "", "suffix": "CI-UPGRADE-MARKER", "hotkey": "Ctrl+Alt+T", "hotkeyEnabled": false,
  "closeToTray": true, "minimizeToTray": false, "showToast": false, "trayHintShown": true,
  "autoCheckUpdates": false, "uiLanguage": "en"
}
'@
$TestHistory = @'
{ "schema": 1, "entries": [ { "input": "1250.750", "cleaned": "1250.750", "unit": "KWD", "language": "ar",
  "text": "CI-HISTORY-MARKER", "time": "2026-11-13T10:00:00+03:00" } ] }
'@

# The new build's exe, from the portable zip (the setup is built from the same files).
$portableRoot = Join-Path $Work "portable"
Expand-Archive -Path $Portable -DestinationPath $portableRoot -Force
$portableExe = Get-ChildItem -Path $portableRoot -Recurse -Filter InstaWords.exe | Select-Object -First 1 -ExpandProperty FullName
$portableDir = Split-Path $portableExe
$newerDir = Split-Path $NewerExe
$newHash = Hash $portableExe
Write-Host "new build: $portableExe ($((Get-Item $portableExe).VersionInfo.ProductVersion))"
Write-Host "newer build: $NewerExe ($((Get-Item $NewerExe).VersionInfo.ProductVersion))"

try {
  # ---- A. The released version, installed and running in the tray ----
  Check ((Copies).Count -eq 0) "A: no InstaWords running before the test"
  $code = Invoke-Setup $OldSetup "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$OutDir\setup-old.log`""
  Check ($code -eq 0) "A: the released setup installs silently (exit $code)"
  $app = Install-Dir
  Check ($null -ne $app) "A: installed at $app"
  if (-not $app) { throw "the released setup did not register an installation" }
  $installedExe = Join-Path $app "InstaWords.exe"
  $oldVersion = (Get-Item $installedExe).VersionInfo.ProductVersion
  Write-Host "released copy: $installedExe ($oldVersion)"
  New-Item -ItemType Directory -Force -Path $Data | Out-Null
  [System.IO.File]::WriteAllText((Join-Path $Data "settings.json"), $TestSettings, (New-Object System.Text.UTF8Encoding($false)))
  [System.IO.File]::WriteAllText((Join-Path $Data "history.json"), $TestHistory, (New-Object System.Text.UTF8Encoding($false)))
  Start-Copy $installedExe "--tray"
  Check (Wait-Until { (Copies-In $app).Count -eq 1 } 20) "A: the released copy runs in the tray"
  Start-Sleep -Seconds 4 # let it finish starting (it saves nothing until something changes)

  # ---- B. Hand-over from a copy without the protocol ----
  Start-Copy $NewerExe
  Check (Wait-Until { (Copies-In $app).Count -eq 0 } 45) "B: a newer copy closed the released copy (no hand-over protocol there)"
  Check (Wait-Until { (Copies-In $newerDir).Count -eq 1 } 30) "B: the newer copy runs in its place"
  Start-Sleep -Seconds 2
  Check ((Copies).Count -eq 1) "B: exactly one copy runs"
  Check-Data "B"
  Stop-Copies $newerDir

  # ---- C. Upgrade with the in-app update's arguments, while the released copy runs in the tray ----
  Start-Copy $installedExe "--tray"
  Check (Wait-Until { (Copies-In $app).Count -eq 1 } 20) "C: the released copy runs in the tray again"
  Start-Sleep -Seconds 4
  $code = Invoke-Setup $NewSetup "$UpdateArgs /LOG=`"$OutDir\setup-upgrade.log`""
  Check ($code -eq 0) "C: the new setup upgrades silently (exit $code)"
  Check ((Install-Dir) -eq $app) "C: same install folder (UsePreviousAppDir)"
  Check ((Hash $installedExe) -eq $newHash) "C: the installed exe is the new build"
  Check (-not (Test-Path (Join-Path $app "update-backup"))) "C: no backup folder left after a finished install"
  Check (Wait-Until { @(Copies-In $app | Where-Object { $_.CommandLine -match '--updated' }).Count -eq 1 } 30) "C: Setup started the new version (--updated)"
  Start-Sleep -Seconds 2
  Check ((Copies).Count -eq 1) "C: exactly one copy runs after the update"
  Check-Data "C"

  # ---- D. The same version again: shows the running copy and exits ----
  Start-Copy $portableExe
  Start-Sleep -Seconds 3
  Check (Wait-Until { (Copies-In $portableDir).Count -eq 0 } 20) "D: a second copy of the same version exits"
  Check ((Copies-In $app).Count -eq 1) "D: the installed copy still runs"

  # ---- E. Hand-over with the protocol ----
  Start-Copy $NewerExe
  Check (Wait-Until { (Copies-In $app).Count -eq 0 } 30) "E: a newer copy asked the installed one to exit"
  Check (Wait-Until { (Copies-In $newerDir).Count -eq 1 } 30) "E: the newer copy runs in its place"
  Start-Sleep -Seconds 2
  Start-Copy $installedExe
  Start-Sleep -Seconds 3
  Check (Wait-Until { (Copies-In $app).Count -eq 0 } 20) "E: an older copy started then exits"
  Check ((Copies-In $newerDir).Count -eq 1) "E: the newer copy still runs"
  Check-Data "E"
  Stop-Copies $newerDir

  # ---- F. A failed silent update starts the installed copy again ----
  $dll = Join-Path $app "InstaWords.Core.dll"
  $dllHash = Hash $dll
  # Readable by everyone (so the restarted app can load it), but Setup cannot replace it.
  $lock = [System.IO.File]::Open($dll, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
  try {
    $code = Invoke-Setup $NewSetup "$UpdateArgs /LOG=`"$OutDir\setup-failed.log`""
    if ($code -eq 0) {
      Write-Host "::warning::F: Setup finished despite the locked file (exit 0), so the failed-update path was not exercised."
    }
    else {
      Check $true "F: the update with a locked program file failed (exit $code)"
      Check (Wait-Until { @(Copies-In $app | Where-Object { $_.CommandLine -match '--update-failed' }).Count -eq 1 } 30) "F: Setup started the installed copy again (--update-failed)"
    }
  }
  finally { $lock.Dispose() }
  Check ((Hash $installedExe) -eq $newHash) "F: the installed exe is intact"
  Check ((Hash $dll) -eq $dllHash) "F: the installed library is intact"
  Start-Sleep -Seconds 2
  Check (@(Copies-In $app).Count -le 1) "F: at most one installed copy runs"

  # ---- G. Uninstall: only the installed copy is closed, user data is kept ----
  if ((Copies-In $app).Count -eq 0) {
    Start-Copy $installedExe "--tray"
    [void](Wait-Until { (Copies-In $app).Count -eq 1 } 20)
  }
  Check ((Copies-In $app).Count -eq 1) "G: the installed copy runs before uninstall"
  $isolated = Join-Path $Work "portable-data"
  New-Item -ItemType Directory -Force -Path $isolated | Out-Null
  [System.IO.File]::WriteAllText((Join-Path $isolated "settings.json"), $TestSettings, (New-Object System.Text.UTF8Encoding($false)))
  Start-Copy $portableExe "" $isolated # its own data folder, so it runs beside the installed copy
  Check (Wait-Until { (Copies-In $portableDir).Count -eq 1 } 20) "G: a portable copy runs beside it"
  Start-Sleep -Seconds 3
  $uninstaller = Get-ChildItem -Path $app -Filter "unins*.exe" | Select-Object -First 1 -ExpandProperty FullName
  Check ($null -ne $uninstaller) "G: uninstaller found: $uninstaller"
  [void](Invoke-Setup $uninstaller "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$OutDir\uninstall.log`"")
  # The uninstaller hands over to a copy of itself in %TEMP%: wait for the result, not the process.
  $gone = Wait-Until { -not (Test-Path $UninstallKey) -and -not (Test-Path $installedExe) } 120
  Check $gone "G: the program and its registration are removed"
  Check ((Copies-In $app).Count -eq 0) "G: the installed copy was closed"
  Check ((Copies-In $portableDir).Count -eq 1) "G: the portable copy outside the install folder keeps running"
  $run = (Get-ItemProperty -Path $RunKey -ErrorAction SilentlyContinue).InstaWords
  Check ([string]::IsNullOrEmpty($run)) "G: no 'start with Windows' entry left"
  Check-Data "G (after uninstall)"
}
finally {
  Get-Process InstaWords -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  Get-ChildItem -Path $OutDir -Filter *.log -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "::group::$($_.Name)"
    Get-Content $_.FullName | Select-Object -Last 80
    Write-Host "::endgroup::"
  }
}

if ($script:failures -gt 0) { throw "$($script:failures) check(s) failed" }
Write-Host "All upgrade, hand-over and uninstall checks passed."

<#
  CI check of the installer's Excel add-in task (xlamaddin) on a throwaway machine (GitHub's windows runner).
  No Excel is needed or started: the script fakes the registry state Excel leaves behind.

    pwsh windows/installer/test-xlam-task.ps1 -Setup out\InstaWords_2.2.0_x64-setup.exe -Xlam out\InstaWords.xlam [-OutDir out\installer-test]

  Runs a copy of the setup that carries a Mark of the Web (as a downloaded file does), silently, into a
  folder under -OutDir, and checks:
    1. without /TASKS (the default, and what a silent in-app update of an existing user gets): no add-in,
       no registry value;
    2. with /TASKS="xlamaddin": %APPDATA%\Microsoft\AddIns\InstaWords.xlam is the built file, has no
       Zone.Identifier stream, and is enabled as the first free OPENn value in every Excel version key
       (16.0 with two add-ins already there -> OPEN2; 15.0 with none -> OPEN), other values untouched,
       no Excel key created for an Office version without one;
    3. a silent update (previous tasks kept) while the add-in is held open the way Excel holds it:
       setup succeeds, keeps the file, adds no second value;
    4. a silent update with the file free: the add-in is replaced by the built file again;
    5. silent uninstall: the file and only our OPEN value are gone, and the add-in enabled after ours
       moves down (OPEN3 -> OPEN2), so the list has no gap.
  Never run this on your own PC: it rewrites HKCU\Software\Microsoft\Office\{15.0,16.0}\Excel\Options.
#>
param(
  [Parameter(Mandatory = $true)][string]$Setup,
  [Parameter(Mandatory = $true)][string]$Xlam,
  [string]$OutDir = "out\installer-test"
)
$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot "..\.."))
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$Setup = (Resolve-Path $Setup).Path
$Xlam = (Resolve-Path $Xlam).Path
$script:failures = @()

function Check([bool]$ok, [string]$what) {
  if ($ok) { Write-Host "ok   $what" } else { Write-Host "FAIL $what"; $script:failures += $what }
}

$appDir = Join-Path $OutDir "app"
$target = Join-Path $env:APPDATA "Microsoft\AddIns\InstaWords.xlam"
$office = "HKCU:\Software\Microsoft\Office"
$opt16 = "$office\16.0\Excel\Options"
$excel15 = "$office\15.0\Excel"
$opt15 = "$excel15\Options"
$word14 = "$office\14.0\Word"
$appKey = "HKCU:\Software\FastKiwi\InstaWords"
$ours = '"' + $target + '"'
$other = '"C:\Fake Add-ins\Other.xlam"'
$second = '/R "C:\Fake Add-ins\Second.xlam"'
$third = '"C:\Fake Add-ins\Third.xlam"'

function Get-Value([string]$key, [string]$name) {
  $item = Get-ItemProperty -LiteralPath $key -Name $name -ErrorAction SilentlyContinue
  if ($null -eq $item) { return $null }
  return $item.$name
}
function Get-OpenValues([string]$key) {
  if (-not (Test-Path $key)) { return @() }
  $props = (Get-Item -LiteralPath $key).Property | Where-Object { $_ -match '^OPEN\d*$' } | Sort-Object
  return @($props | ForEach-Object { "$_=$(Get-Value $key $_)" })
}
function Show-Registry([string]$when) {
  Write-Host "  [$when] 16.0: $((Get-OpenValues $opt16) -join ' | ')"
  Write-Host "  [$when] 15.0: $((Get-OpenValues $opt15) -join ' | ')"
}
# New-Item -Force on an existing registry key would empty it: create missing keys only.
function Ensure-Key([string]$key) {
  if (-not (Test-Path $key)) { New-Item -Force -Path $key | Out-Null }
}
function Has-MotW([string]$path) {
  return [bool](Get-Item -LiteralPath $path -Stream * | Where-Object Stream -eq 'Zone.Identifier')
}

# Runs a program directly (CreateProcess, no shell: nothing can prompt) and returns its exit code.
function Invoke-Quiet([string]$exe, [string]$arguments, [int]$timeoutSec = 300) {
  $psi = New-Object System.Diagnostics.ProcessStartInfo $exe, $arguments
  $psi.UseShellExecute = $false
  $p = [System.Diagnostics.Process]::Start($psi)
  if (-not $p.WaitForExit($timeoutSec * 1000)) { $p.Kill($true); throw "$exe $arguments did not finish in $timeoutSec s" }
  return $p.ExitCode
}
function Show-Log([string]$log) {
  if (Test-Path $log) { Get-Content $log | Where-Object { $_ -match 'Excel add-in|Error|error|Exception' } | ForEach-Object { Write-Host "    log: $_" } }
}
function Invoke-Setup([string]$label, [string]$extra) {
  $log = Join-Path $OutDir "setup-$label.log"
  $code = Invoke-Quiet $downloaded "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR=`"$appDir`" /LOG=`"$log`" $extra"
  Show-Log $log
  Check ($code -eq 0) "setup ($label) exit code $code"
  Check (Test-Path (Join-Path $appDir "InstaWords.exe")) "InstaWords.exe installed ($label)"
  return $log
}

# ---- the state Excel leaves: two add-ins already enabled in Excel 2016+, Excel 2013 never set up ----
foreach ($k in @($opt16, $opt15)) {
  if (Test-Path $k) {
    $before = Get-OpenValues $k
    if ($before.Count) { Write-Host "::notice::Removing existing values from $k : $($before -join ' | ')" }
    foreach ($v in (Get-Item -LiteralPath $k).Property | Where-Object { $_ -match '^OPEN\d*$' }) { Remove-ItemProperty -LiteralPath $k -Name $v }
  }
}
Ensure-Key $opt16
Set-ItemProperty -LiteralPath $opt16 -Name OPEN -Value $other
Set-ItemProperty -LiteralPath $opt16 -Name OPEN1 -Value $second
Ensure-Key $excel15
Ensure-Key $word14
if (Test-Path "$office\14.0\Excel") { Remove-Item -Recurse -Force "$office\14.0\Excel" }
if (Test-Path $target) { throw "$target exists before the test" }
if (Test-Path $appDir) { Remove-Item -Recurse -Force $appDir }

# The setup as a browser saves it: with a Mark of the Web.
$downloaded = Join-Path $OutDir "InstaWords-setup-downloaded.exe"
Copy-Item -LiteralPath $Setup -Destination $downloaded -Force
Set-Content -LiteralPath $downloaded -Stream Zone.Identifier -Value "[ZoneTransfer]`r`nZoneId=3`r`nHostUrl=https://github.com/" -Encoding ascii
Check (Has-MotW $downloaded) "the setup copy carries a Mark of the Web (Zone.Identifier)"
$builtHash = (Get-FileHash -LiteralPath $Xlam).Hash

Write-Host "1. Default tasks (the box is unchecked)"
Invoke-Setup "default" "" | Out-Null
Show-Registry "default"
Check (-not (Test-Path $target)) "no add-in in %APPDATA%\Microsoft\AddIns"
Check (((Get-OpenValues $opt16) -join '|') -eq "OPEN=$other|OPEN1=$second") "Excel 16.0 values unchanged"
Check ((Get-OpenValues $opt15).Count -eq 0) "nothing written for Excel 15.0"
Check ($null -eq (Get-Value $appKey "ExcelAddIn")) "no add-in marker"

Write-Host "2. /TASKS=xlamaddin"
Invoke-Setup "task" '/TASKS="xlamaddin"' | Out-Null
Show-Registry "task"
Check (Test-Path $target) "$target installed"
if (Test-Path $target) {
  Check ((Get-FileHash -LiteralPath $target).Hash -eq $builtHash) "it is the built InstaWords.xlam (SHA-256)"
  Check (-not (Has-MotW $target)) "it has no Zone.Identifier stream: no Unblock needed"
}
Check ((Get-Value $opt16 "OPEN") -eq $other) "Excel 16.0 OPEN unchanged"
Check ((Get-Value $opt16 "OPEN1") -eq $second) "Excel 16.0 OPEN1 unchanged"
Check ((Get-Value $opt16 "OPEN2") -eq $ours) "Excel 16.0 OPEN2 = our add-in, quoted"
Check ((Get-OpenValues $opt16).Count -eq 3) "Excel 16.0 has exactly three OPEN values"
Check (((Get-OpenValues $opt15) -join '|') -eq "OPEN=$ours") "Excel 15.0 OPEN = our add-in (first free value)"
Check (-not (Test-Path "$office\14.0\Excel")) "no Excel key created for Office 14.0 (Word only)"
Check ((Get-Value $appKey "ExcelAddIn") -eq $target) "add-in marker written"

Write-Host "3. Silent update while the add-in is open (as Excel holds it)"
$lock = [System.IO.File]::Open($target, 'Open', 'Read', 'Read')
try {
  $log = Invoke-Setup "update-locked" ""
} finally {
  $lock.Dispose()
}
Show-Registry "update-locked"
Check (Test-Path $target) "add-in still there"
Check ((Get-FileHash -LiteralPath $target).Hash -eq $builtHash) "add-in intact"
Check ([bool](Select-String -LiteralPath $log -Pattern 'Excel add-in: .* is held open' -Quiet)) "setup logged the file in use and went on"
Check ((Get-OpenValues $opt16).Count -eq 3) "no second value for Excel 16.0"
Check ((Get-OpenValues $opt15).Count -eq 1) "no second value for Excel 15.0"

Write-Host "4. Silent update with the add-in free (previous tasks kept)"
Set-Content -LiteralPath $target -Value "stale" -Encoding ascii
Set-Content -LiteralPath $target -Stream Zone.Identifier -Value "[ZoneTransfer]`r`nZoneId=3" -Encoding ascii
Invoke-Setup "update" "" | Out-Null
Show-Registry "update"
Check ((Get-FileHash -LiteralPath $target).Hash -eq $builtHash) "add-in replaced by the built file"
Check (-not (Has-MotW $target)) "the replaced add-in has no Zone.Identifier stream"
Check ((Get-OpenValues $opt16).Count -eq 3) "still three values for Excel 16.0"

Write-Host "5. Silent uninstall (another add-in was enabled after ours: OPEN3)"
Set-ItemProperty -LiteralPath $opt16 -Name OPEN3 -Value $third
$uninstaller = Join-Path $appDir "unins000.exe"
Check (Test-Path $uninstaller) "uninstaller present"
$ulog = Join-Path $OutDir "uninstall.log"
$code = Invoke-Quiet $uninstaller "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$ulog`""
# The uninstaller finishes from a copy of itself in %TEMP%: wait for it to remove itself.
$deadline = (Get-Date).AddSeconds(120)
while ((Test-Path $uninstaller) -and ((Get-Date) -lt $deadline)) { Start-Sleep -Milliseconds 500 }
Start-Sleep -Seconds 2
Show-Log $ulog
Show-Registry "uninstall"
Check ($code -eq 0) "uninstall exit code $code"
Check (-not (Test-Path $uninstaller)) "uninstalled"
Check (-not (Test-Path $target)) "add-in file removed"
Check ((Get-Value $opt16 "OPEN") -eq $other) "Excel 16.0 OPEN unchanged"
Check ((Get-Value $opt16 "OPEN1") -eq $second) "Excel 16.0 OPEN1 unchanged"
Check ((Get-Value $opt16 "OPEN2") -eq $third) "Excel 16.0 OPEN3 moved down to OPEN2"
Check ($null -eq (Get-Value $opt16 "OPEN3")) "Excel 16.0 OPEN3 gone (no gap, no duplicate)"
Check ((Get-OpenValues $opt15).Count -eq 0) "Excel 15.0 value removed"
Check ($null -eq (Get-Value $appKey "ExcelAddIn")) "add-in marker removed"

if ($script:failures.Count) {
  Write-Host "::error::Excel add-in installer test: $($script:failures.Count) check(s) failed: $($script:failures -join '; ')"
  exit 1
}
Write-Host "Excel add-in installer test: all checks passed."

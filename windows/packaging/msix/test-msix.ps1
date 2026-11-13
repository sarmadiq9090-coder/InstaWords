<#
  CI check of the Store package on a throwaway machine (GitHub's windows runner; needs admin).

    pwsh windows/packaging/msix/test-msix.ps1 -Msix out\msix\InstaWords_2.1.0.0_x64.msix [-OutDir out\msix-test]

  The package itself stays unsigned (the Store signs it). This script signs a COPY with a throwaway
  self-signed certificate whose subject equals the Publisher in identity.json, trusts it, installs
  the copy, and checks:
    - the package family name is the one Partner Center gave (identity.json PackageFamilyName);
    - with -ExpectVersion x.y.z: the package version is x.y.z.0 and About shows x.y.z;
    - the app starts from the package (path under WindowsApps) and shows its window;
    - Store mode: Settings shows "Updates come from Microsoft Store", a "Check for updates" button (in the
      Store copy it opens the Store page; not clicked here) and no auto-update box; "Start with Windows" reads the startup task and can switch it on and off;
    - a setting changed in the app is written where MSIX redirects %APPDATA% (LocalCache\Roaming).
  Leaves <OutDir>\InstaWords-test-signed.msix and the trusted certificate for the App Certification Kit
  step, plus screenshots and a log in <OutDir>. Never run this on your own PC: it trusts a new certificate.
#>
param(
  [Parameter(Mandatory = $true)][string]$Msix,
  [string]$OutDir = "out\msix-test",
  [string]$Identity = "windows\packaging\msix\identity.json",
  [string]$ExpectVersion = ""
)
$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot "..\..\.."))
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$Msix = (Resolve-Path $Msix).Path
$id = Get-Content $Identity -Raw | ConvertFrom-Json

function Find-SdkTool([string]$name) {
  $kits = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
  $tool = Get-ChildItem $kits -Directory -Filter "10.*" | Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Join-Path $_.FullName "x64\$name" } | Where-Object { Test-Path $_ } | Select-Object -First 1
  if (-not $tool) { throw "$name not found under $kits" }
  return $tool
}
function Check([bool]$ok, [string]$what) {
  if ($ok) { Write-Host "ok   $what" } else { Write-Host "FAIL $what"; $script:failures += $what }
}
$failures = @()

# ---- Sign a copy with a throwaway certificate (subject = Publisher) and trust it ----
$signed = Join-Path $OutDir "InstaWords-test-signed.msix"
Copy-Item $Msix $signed -Force
$cert = New-SelfSignedCertificate -Type Custom -Subject $id.Publisher -KeyUsage DigitalSignature `
  -FriendlyName "InstaWords MSIX CI test (throwaway)" -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") -NotAfter (Get-Date).AddDays(2)
$password = ConvertTo-SecureString -String ([guid]::NewGuid().ToString()) -Force -AsPlainText
$pfx = Join-Path $env:RUNNER_TEMP "instawords-test.pfx"
if (-not $env:RUNNER_TEMP) { $pfx = Join-Path ([IO.Path]::GetTempPath()) "instawords-test.pfx" }
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $password | Out-Null
$cer = Join-Path $OutDir "InstaWords-test.cer"
Export-Certificate -Cert $cert -FilePath $cer | Out-Null
Import-Certificate -FilePath $cer -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
$signtool = Find-SdkTool "signtool.exe"
$plain = [Runtime.InteropServices.Marshal]::PtrToStringUni([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))
& $signtool sign /fd SHA256 /f $pfx /p $plain $signed
if ($LASTEXITCODE -ne 0) { throw "signtool failed" }
Remove-Item $pfx -Force

# ---- Install ----
Get-AppxPackage -Name $id.Name | Remove-AppxPackage -ErrorAction SilentlyContinue
Add-AppxPackage -Path $signed
$pkg = Get-AppxPackage -Name $id.Name
Check ($null -ne $pkg) "installed: $($pkg.PackageFullName)"
if (-not $pkg) { throw "the package did not install" }
if ($id.PackageFamilyName) { Check ($pkg.PackageFamilyName -eq $id.PackageFamilyName) "family name $($pkg.PackageFamilyName) = identity.json $($id.PackageFamilyName)" }
if ($ExpectVersion) { Check ($pkg.Version -eq "$ExpectVersion.0") "package version $($pkg.Version) = $ExpectVersion.0" }

# ---- Launch from the package, like the Start menu does ----
Get-Process InstaWords -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Process "shell:AppsFolder\$($pkg.PackageFamilyName)!App"
$proc = $null
$deadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $deadline) {
  $proc = Get-Process InstaWords -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  if ($proc) { break }
  Start-Sleep -Milliseconds 500
}
Check ($null -ne $proc) "the app started and shows its window"
if (-not $proc) { throw "InstaWords did not start from the package" }
Check ($proc.Path -like "*\WindowsApps\*") "runs from the package: $($proc.Path)"

# ---- Store mode, through UI Automation ----
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
function Find-Id($root, [string]$automationId, [int]$timeoutMs = 10000) {
  $cond = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $automationId)
  $deadline = (Get-Date).AddMilliseconds($timeoutMs)
  do {
    $all = $root.FindAll($TS::Descendants, $cond)
    $el = $all | Where-Object { -not $_.Current.IsOffscreen } | Select-Object -First 1
    if (-not $el -and $all.Count -gt 0) { $el = $all[0] }
    if ($el) { return $el }
    Start-Sleep -Milliseconds 200
  } while ((Get-Date) -lt $deadline)
  return $null
}
# A real click (focus + Space): UIA's TogglePattern flips IsChecked without raising the app's Click handler.
function Click-Box($element) {
  $element.SetFocus()
  Start-Sleep -Milliseconds 300
  [System.Windows.Forms.SendKeys]::SendWait(" ")
}
function Toggle-State($element) { $element.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState }
# What Windows recorded for the startup task (2 = enabled, 0 = disabled), read from outside the package.
function Startup-TaskState {
  $key = "HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\$($pkg.PackageFamilyName)\InstaWordsStartup"
  try { return (Get-ItemProperty -Path $key -Name State -ErrorAction Stop).State } catch { return "n/a" }
}
function Save-Shot($element, [string]$name) {
  Start-Sleep -Milliseconds 400
  $r = $element.Current.BoundingRectangle
  $bmp = New-Object System.Drawing.Bitmap([int]$r.Width, [int]$r.Height)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size)
  $bmp.Save((Join-Path $OutDir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
}

$window = $AE::FromHandle($proc.MainWindowHandle)
try { $window.SetFocus() } catch { }
$nav = Find-Id $window "NavSettings"
Check ($null -ne $nav) "Settings tab found"
$nav.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Start-Sleep -Seconds 1

$status = Find-Id $window "UpdateStatus"
$statusText = if ($status) { $status.Current.Name } else { "" }
Check ($statusText -eq "Updates come from Microsoft Store" -or $statusText -eq "التحديثات تأتي من Microsoft Store") "update status says the Store updates it: «$statusText»"
# In the Store copy the button opens InstaWords' Store page (not clicked: the Store app is not part of this test).
Check ($null -ne (Find-Id $window "CheckUpdate" 3000)) "'Check for updates' button shown (opens the Store page)"
Check ($null -eq (Find-Id $window "AutoUpdate" 1500)) "no automatic update check box"

$startup = Find-Id $window "StartWithWindows"
Check ($null -ne $startup) "Start with Windows box found"
if ($startup) {
  # Scroll it into view (the settings page scrolls).
  try { $startup.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern).ScrollIntoView() } catch { }
  Start-Sleep -Milliseconds 500
  Save-Shot $window "store-settings"
  Check $startup.Current.IsEnabled "the startup task answered (box enabled)"
  $before = Toggle-State $startup
  Check ($before -eq [System.Windows.Automation.ToggleState]::Off) "startup task is off by default ($before)"
  if ($startup.Current.IsEnabled) {
    Click-Box $startup
    Start-Sleep -Seconds 2
    $on = Toggle-State $startup
    Write-Host "startup task state recorded by Windows: $(Startup-TaskState)"
    Check ($on -eq [System.Windows.Automation.ToggleState]::On) "startup task switched on through the StartupTask API ($on)"
    Save-Shot $window "store-settings-startup-on"
    Click-Box $startup
    Start-Sleep -Seconds 2
    $off = Toggle-State $startup
    Write-Host "startup task state recorded by Windows: $(Startup-TaskState)"
    Check ($off -eq [System.Windows.Automation.ToggleState]::Off) "startup task switched off again ($off)"
  }
}

# A changed setting is saved (after 0.5 s) where MSIX redirects %APPDATA%.
$toast = Find-Id $window "ShowToast"
if ($toast) {
  Click-Box $toast
  Start-Sleep -Seconds 2
}
$redirected = Join-Path $env:LOCALAPPDATA "Packages\$($pkg.PackageFamilyName)\LocalCache\Roaming\FastKiwi\InstaWords\settings.json"
$classic = Join-Path $env:APPDATA "FastKiwi\InstaWords\settings.json"
Write-Host "settings.json: LocalCache=$(Test-Path $redirected) classic=$(Test-Path $classic)"
Check (Test-Path $redirected) "settings saved under the package's LocalCache\Roaming (what 'Open data folder' opens)"

# About: the check button (opens the Store page), the Store note instead of the GitHub one. (A dialog: a separate top-level window.)
$about = Find-Id $window "AboutButton"
if ($about) { $about.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
$aboutWindow = $null
$deadline = (Get-Date).AddSeconds(10)
$byProcess = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
while (-not $aboutWindow -and (Get-Date) -lt $deadline) {
  $aboutWindow = $AE::RootElement.FindAll($TS::Children, $byProcess) | Where-Object { $_.Current.AutomationId -eq "AboutWindow" } | Select-Object -First 1
  if (-not $aboutWindow) { $aboutWindow = Find-Id $window "AboutWindow" 500 }
}
Check ($null -ne $aboutWindow) "About window opened"
if ($aboutWindow) {
  $aboutStatus = Find-Id $aboutWindow "AboutUpdateStatus"
  $aboutText = if ($aboutStatus) { $aboutStatus.Current.Name } else { "" }
  Check ($aboutText -eq $statusText) "About says the Store updates it: «$aboutText»"
  Check ($null -ne (Find-Id $aboutWindow "AboutCheckUpdate" 3000)) "About: 'Check for updates' button shown (opens the Store page)"
  if ($ExpectVersion) {
    $aboutVersion = Find-Id $aboutWindow "AboutVersion"
    $versionText = if ($aboutVersion) { $aboutVersion.Current.Name } else { "" }
    Check ($versionText -match "(^|\D)$([regex]::Escape($ExpectVersion))(\D|$)") "About shows version ${ExpectVersion}: «$versionText»"
  }
  Save-Shot $aboutWindow "store-about"
}

# ---- Clean up the app (the certificate and the signed copy stay for the WACK step) ----
Get-Process InstaWords -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
Remove-AppxPackage -Package $pkg.PackageFullName
Check ($null -eq (Get-AppxPackage -Name $id.Name)) "uninstalled"

if ($failures.Count -gt 0) {
  Write-Host "::error::MSIX test: $($failures.Count) check(s) failed: $($failures -join '; ')"
  exit 1
}
Write-Host "All MSIX checks passed."

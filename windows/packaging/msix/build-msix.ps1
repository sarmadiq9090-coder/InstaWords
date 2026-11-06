<#
  Builds the Microsoft Store package: out\msix\InstaWords_<x.y.z.0>_x64.msix (unsigned: the Store signs it).

    dotnet build windows/InstaWords.App -c Release
    pwsh windows/packaging/msix/build-msix.ps1 [-Version 2.1.0] [-AppDir ...] [-OutDir out\msix]

  1. scripts/msix-stage.mjs: the app files, Assets\, Strings\ and AppxManifest.xml (identity.json + version)
  2. MakePri: resources.pri (the ms-resource: strings and the scale-/targetsize- logo variants)
  3. MakeAppx pack: the .msix (MakeAppx checks the manifest against the schema while packing)
  Needs Node 22.18+ and the Windows 10/11 SDK (both are on GitHub's windows-latest runners).
  Writes path=<msix> and file=<name> to $GITHUB_OUTPUT when run in GitHub Actions.
#>
param(
  [string]$AppDir = "windows\InstaWords.App\bin\Release\net48",
  [string]$OutDir = "out\msix",
  [string]$Version = ""
)
$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot "..\..\.."))

function Find-SdkTool([string]$name) {
  $kits = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
  $tool = Get-ChildItem $kits -Directory -Filter "10.*" -ErrorAction SilentlyContinue |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Join-Path $_.FullName "x64\$name" } |
    Where-Object { Test-Path $_ } | Select-Object -First 1
  if (-not $tool) { throw "$name not found under $kits. Install the Windows 10/11 SDK." }
  return $tool
}

function Invoke-Tool([string]$exe, [string[]]$arguments) {
  Write-Host "> $(Split-Path $exe -Leaf) $($arguments -join ' ')"
  & $exe @arguments
  if ($LASTEXITCODE -ne 0) { throw "$(Split-Path $exe -Leaf) failed with exit code $LASTEXITCODE" }
}

$makeappx = Find-SdkTool "makeappx.exe"
$makepri = Find-SdkTool "makepri.exe"
Write-Host "Windows SDK: $(Split-Path (Split-Path $makeappx))"

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$stage = Join-Path $OutDir "stage"

# 1. Layout
$nodeArgs = @("scripts/msix-stage.mjs", "--app", $AppDir, "--out", $stage)
if ($Version) { $nodeArgs += @("--version", $Version) }
$json = & node @nodeArgs
if ($LASTEXITCODE -ne 0) { throw "scripts/msix-stage.mjs failed" }
$info = $json | Select-Object -Last 1 | ConvertFrom-Json
Write-Host "Identity: $($info.name) / $($info.publisher), version $($info.version)"

# 2. resources.pri. The config lives outside the layout so it is not packed. Its <packaging> section
#    (resource packs, used only by bundles) is removed: everything goes into the one resources.pri.
$priConfig = Join-Path $OutDir "priconfig.xml"
Invoke-Tool $makepri @("createconfig", "/cf", $priConfig, "/dq", "en-US", "/pv", "10.0.0", "/o")
[xml]$cfg = Get-Content $priConfig -Raw
foreach ($node in @($cfg.SelectNodes("//packaging"))) { [void]$node.ParentNode.RemoveChild($node) }
$cfg.Save($priConfig)
Invoke-Tool $makepri @("new", "/pr", $stage, "/cf", $priConfig, "/mn", (Join-Path $stage "AppxManifest.xml"), "/of", (Join-Path $stage "resources.pri"), "/o")

# 3. Package
$msix = Join-Path $OutDir $info.file
Invoke-Tool $makeappx @("pack", "/d", $stage, "/p", $msix, "/o")
$size = (Get-Item $msix).Length
Write-Host ("Built {0} ({1:N0} bytes)" -f $msix, $size)

if ($env:GITHUB_OUTPUT) {
  "path=$msix" >> $env:GITHUB_OUTPUT
  "file=$($info.file)" >> $env:GITHUB_OUTPUT
}

<#
.SYNOPSIS
  Builds Custom Soundboard and copies the mod/ folder into your local
  Rain World install so it can be enabled from the in-game mod menu.

.PARAMETER RainWorldPath
  Override if Rain World isn't installed at the default Steam location.
#>
param(
    [string]$RainWorldPath = $(if ($env:RAINWORLD_PATH) { $env:RAINWORLD_PATH } else { "C:\Program Files (x86)\Steam\steamapps\common\Rain World" })
)

$ErrorActionPreference = "Stop"

# Make sure our per-user .NET SDK install wins over any PATH-shadowing
# runtime-only "dotnet" that might be installed system-wide.
$userDotnet = "$env:USERPROFILE\.dotnet"
if (Test-Path "$userDotnet\dotnet.exe") {
    $env:PATH = "$userDotnet;$env:PATH"
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $repoRoot "src\SoundboardMod.csproj"
$modSource = Join-Path $repoRoot "mod"
$modId = "dion_soundboard"
$modDest = Join-Path $RainWorldPath "RainWorld_Data\StreamingAssets\mods\$modId"

Write-Host "Building $csproj ..." -ForegroundColor Cyan
dotnet build $csproj --configuration Debug
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed, aborting deploy."
}

if (-not (Test-Path $RainWorldPath)) {
    Write-Error "Rain World not found at '$RainWorldPath'. Pass -RainWorldPath or set RAINWORLD_PATH."
}

Write-Host "Copying mod/ -> $modDest ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $modDest | Out-Null
Copy-Item -Path (Join-Path $modSource '*') -Destination $modDest -Recurse -Force

Write-Host ""
Write-Host "Done. Now, in-game: Options -> Mods -> enable 'Custom Soundboard' -> restart." -ForegroundColor Green
Write-Host "(A restart is required whenever soundeffects/meta.json or modify/soundeffects/sounds.txt change.)"

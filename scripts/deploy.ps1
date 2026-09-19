<#
.SYNOPSIS
  Builds Custom Soundboard and copies the mod/ folder into your local
  Rain World install so it can be enabled from the in-game mod menu.

.PARAMETER RainWorldPath
  Override if Rain World isn't installed at the default Steam location.

.PARAMETER SkipBuild
  Skip the dotnet build step and just copy mod/ as-is (uses whatever
  SoundboardMod.dll is already sitting in mod/plugins/, e.g. the one
  committed to the repo). Useful on a machine without the .NET SDK or
  a local Rain World install configured for building.
#>
param(
    [string]$RainWorldPath = $(if ($env:RAINWORLD_PATH) { $env:RAINWORLD_PATH } else { "C:\Program Files (x86)\Steam\steamapps\common\Rain World" }),
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $repoRoot "src\SoundboardMod.csproj"
$modSource = Join-Path $repoRoot "mod"
$modId = "dion_soundboard"
$modDest = Join-Path $RainWorldPath "RainWorld_Data\StreamingAssets\mods\$modId"

if ($SkipBuild) {
    $existingDll = Join-Path $modSource "plugins\SoundboardMod.dll"
    if (-not (Test-Path $existingDll)) {
        Write-Error "-SkipBuild was passed but $existingDll doesn't exist. Run without -SkipBuild at least once first."
    }
    Write-Host "Skipping build, using existing $existingDll" -ForegroundColor Yellow
}
else {
    # Make sure our per-user .NET SDK install wins over any PATH-shadowing
    # runtime-only "dotnet" that might be installed system-wide.
    $userDotnet = "$env:USERPROFILE\.dotnet"
    if (Test-Path "$userDotnet\dotnet.exe") {
        $env:PATH = "$userDotnet;$env:PATH"
    }

    Write-Host "Building $csproj ..." -ForegroundColor Cyan
    dotnet build $csproj --configuration Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed, aborting deploy."
    }
}

if (-not (Test-Path $RainWorldPath)) {
    Write-Error "Rain World not found at '$RainWorldPath'. Pass -RainWorldPath or set RAINWORLD_PATH."
}

Write-Host "Copying mod/ -> $modDest ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $modDest | Out-Null

# Versions before 1.0.0 kept sounds in soundeffects/ and registered them in
# modify/soundeffects/sounds.txt. Copy-Item never deletes, so an old install
# would keep both - and the game would go on merging the stale sounds.txt.
$legacy = @(
    (Join-Path $modDest "modify\soundeffects\sounds.txt"),
    (Join-Path $modDest "soundeffects")
)
foreach ($path in $legacy) {
    if (Test-Path $path) {
        Write-Host "Removing leftover from an older version: $path" -ForegroundColor Yellow
        Remove-Item -Path $path -Recurse -Force
    }
}
$legacyModify = Join-Path $modDest "modify\soundeffects"
if ((Test-Path $legacyModify) -and -not (Get-ChildItem $legacyModify -Force)) { Remove-Item $legacyModify -Force }
$legacyModifyRoot = Join-Path $modDest "modify"
if ((Test-Path $legacyModifyRoot) -and -not (Get-ChildItem $legacyModifyRoot -Force)) { Remove-Item $legacyModifyRoot -Force }

Copy-Item -Path (Join-Path $modSource '*') -Destination $modDest -Recurse -Force

Write-Host ""
Write-Host "Done. Now, in-game: Options -> Mods -> enable 'Custom Soundboard' -> apply/restart." -ForegroundColor Green
Write-Host "After that, soundboard.yaml changes need no restart: press RELOAD CONFIG in the mod's options screen."
Write-Host "(Your personal copy of soundboard.yaml lives in the game's data folder - OPEN FOLDER in the options screen finds it.)"
Write-Host "(Deploying never touches it. To try the shipped defaults again, delete that copy and restart.)"

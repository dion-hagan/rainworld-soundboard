<#
.SYNOPSIS
  Refreshes the soundboard.yaml the game actually reads (your personal copy)
  from the one in this repo - with Rain World still running.

.DESCRIPTION
  The game reads the copy in its data folder, never the soundboard.yaml inside
  the mod's own folder (that one is only a template, so mod updates can't wipe a
  player's changes). While developing, that means edits to mod\soundboard.yaml
  in this repo don't reach the game by themselves. This script:

    1. backs up your current personal copy (soundboard.yaml.<time>.bak - it may hold
       checkbox changes made in the options screen) if it differs from the repo file,
    2. copies mod\soundboard.yaml over it,
    3. copies any new or changed sound files from mod\sounds into the installed mod,
       so they can be found without a full deploy (the plugin DLL can't be replaced
       while the game is running - use deploy.ps1 for code changes).

  Then press RELOAD CONFIG in the mod's options screen. No restart needed.

  Use -WhatIf to see what it would do without changing anything.

.PARAMETER RainWorldPath
  Override if Rain World isn't installed at the default Steam location.

.PARAMETER UserFolder
  The game's per-user soundboard folder. Default:
  %USERPROFILE%\AppData\LocalLow\Videocult\Rain World\Soundboard

.PARAMETER KeepBackups
  How many .bak files to keep (oldest are deleted). Default 10.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$RainWorldPath = $(if ($env:RAINWORLD_PATH) { $env:RAINWORLD_PATH } else { "C:\Program Files (x86)\Steam\steamapps\common\Rain World" }),
    [string]$UserFolder = (Join-Path $env:USERPROFILE "AppData\LocalLow\Videocult\Rain World\Soundboard"),
    [int]$KeepBackups = 10
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceYaml = Join-Path $repoRoot "mod\soundboard.yaml"
$sourceSounds = Join-Path $repoRoot "mod\sounds"
$modId = "dion_soundboard"
$installedMod = Join-Path $RainWorldPath "RainWorld_Data\StreamingAssets\mods\$modId"
$targetYaml = Join-Path $UserFolder "soundboard.yaml"

if (-not (Test-Path $sourceYaml)) {
    Write-Error "Couldn't find $sourceYaml - run this from the repo."
}

function Get-NormalizedText([string]$path) {
    return ([System.IO.File]::ReadAllText($path) -replace "`r`n", "`n").TrimEnd()
}

# --- 1 + 2: the config -------------------------------------------------------------
if (-not (Test-Path $UserFolder)) {
    if ($PSCmdlet.ShouldProcess($UserFolder, "Create folder")) {
        New-Item -ItemType Directory -Force -Path $UserFolder | Out-Null
    }
}

$changed = $true
if (Test-Path $targetYaml) {
    if ((Get-NormalizedText $sourceYaml) -eq (Get-NormalizedText $targetYaml)) {
        $changed = $false
        Write-Host "soundboard.yaml: your personal copy already matches the repo." -ForegroundColor DarkGray
    }
    else {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $backup = Join-Path $UserFolder "soundboard.yaml.$stamp.bak"
        if ($PSCmdlet.ShouldProcess($targetYaml, "Back up to $(Split-Path -Leaf $backup)")) {
            Copy-Item -Path $targetYaml -Destination $backup
            Write-Host "Backed up your previous copy -> $backup" -ForegroundColor Yellow
        }
    }
}

if ($changed) {
    if ($PSCmdlet.ShouldProcess($targetYaml, "Replace with $sourceYaml")) {
        Copy-Item -Path $sourceYaml -Destination $targetYaml -Force
        Write-Host "soundboard.yaml: refreshed $targetYaml" -ForegroundColor Green
    }
}

# Keep only the newest few backups.
$backups = @(Get-ChildItem -Path $UserFolder -Filter "soundboard.yaml.*.bak" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)
if ($backups.Count -gt $KeepBackups) {
    foreach ($old in ($backups | Select-Object -Skip $KeepBackups)) {
        if ($PSCmdlet.ShouldProcess($old.FullName, "Delete old backup")) {
            Remove-Item -Path $old.FullName -Force
        }
    }
}

# --- 3: new / changed sound files ---------------------------------------------------------
if ((Test-Path $sourceSounds) -and (Test-Path $installedMod)) {
    $installedSounds = Join-Path $installedMod "sounds"
    $copied = 0
    foreach ($file in Get-ChildItem -Path $sourceSounds -File -Recurse) {
        $relative = $file.FullName.Substring($sourceSounds.Length).TrimStart('\')
        $dest = Join-Path $installedSounds $relative
        $existing = if (Test-Path $dest) { Get-Item $dest } else { $null }
        if ($null -eq $existing -or $existing.Length -ne $file.Length -or $existing.LastWriteTimeUtc -lt $file.LastWriteTimeUtc) {
            if ($PSCmdlet.ShouldProcess($dest, "Copy sound $relative")) {
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null
                Copy-Item -Path $file.FullName -Destination $dest -Force
            }
            $copied++
            Write-Host "  sound: $relative" -ForegroundColor Cyan
        }
    }
    if ($copied -eq 0) { Write-Host "sounds: installed mod is already up to date." -ForegroundColor DarkGray }
}
elseif (-not (Test-Path $installedMod)) {
    Write-Host "sounds: the mod isn't installed at $installedMod - run deploy.ps1 first. (Config was still refreshed.)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Done. In the game: Options -> Mods -> Custom Soundboard -> RELOAD CONFIG." -ForegroundColor Green
Write-Host "(Changed the C# code? That needs deploy.ps1 and a game restart.)"

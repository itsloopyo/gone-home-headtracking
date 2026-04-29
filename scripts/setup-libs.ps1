#!/usr/bin/env pwsh
#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libsDir = Join-Path $projectRoot "src\GoneHomeHeadTracking\libs"

# Import shared game detection module
$modulePath = Join-Path $projectRoot "cameraunlock-core\powershell\GamePathDetection.psm1"
Import-Module $modulePath -Force

$gameId = 'gone-home'
$config = Get-GameConfig -GameId $gameId

# Find game installation
$gamePath = Find-GamePath -GameId $gameId

if (-not $gamePath) {
    Write-GameNotFoundError -GameName 'Gone Home' -EnvVar $config.EnvVar -SteamFolder $config.SteamFolder
    exit 1
}

Write-Host "Found game installation at: $gamePath" -ForegroundColor Green

# Find the Managed folder (contains game DLLs)
$managedPath = Get-ManagedPath -GamePath $gamePath -DataFolder $config.DataFolder

if (-not (Test-Path $managedPath)) {
    Write-Host "ERROR: Managed folder not found at: $managedPath" -ForegroundColor Red
    Write-Host "The game installation may be corrupted. Try verifying game files in Steam."
    exit 1
}

Write-Host "Found Managed folder at: $managedPath" -ForegroundColor Green

# Required DLLs for building the mod (newer modular Unity)
$requiredDlls = @(
    "Assembly-CSharp.dll",
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.PhysicsModule.dll",
    "UnityEngine.TextRenderingModule.dll",
    "UnityEngine.UIModule.dll",
    "UnityEngine.UI.dll"
)

# Check if all libs already exist and are up-to-date
$stale = @($requiredDlls | Where-Object {
    $dest = Join-Path $libsDir $_
    $src = Join-Path $managedPath $_
    -not (Test-Path $dest) -or (Get-Item $src).LastWriteTime -gt (Get-Item $dest).LastWriteTime
})

if ((Test-Path $libsDir) -and $stale.Count -eq 0) {
    Write-Host "All libs are up-to-date, skipping copy." -ForegroundColor Green
    exit 0
}

# Create libs directory if it doesn't exist
if (-not (Test-Path $libsDir)) {
    New-Item -ItemType Directory -Path $libsDir -Force | Out-Null
    Write-Host "Created libs directory: $libsDir" -ForegroundColor Green
}

# Copy each required DLL
$copyCount = 0
foreach ($dll in $requiredDlls) {
    $sourcePath = Join-Path $managedPath $dll
    $destPath = Join-Path $libsDir $dll

    if (-not (Test-Path $sourcePath)) {
        Write-Host "ERROR: Required DLL not found: $sourcePath" -ForegroundColor Red
        exit 1
    }

    Copy-Item -Path $sourcePath -Destination $destPath -Force
    Write-Host "Copied: $dll" -ForegroundColor Cyan
    $copyCount++
}

Write-Host ""
Write-Host "SUCCESS: Copied $copyCount DLLs to libs/" -ForegroundColor Green
Write-Host "You can now build the project with: pixi run build"

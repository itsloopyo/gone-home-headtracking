#Requires -Version 5.1
<#
.SYNOPSIS
    Removes HeadTracking mod from Gone Home.
.DESCRIPTION
    Restores the original Assembly-CSharp.dll and removes all mod files.
    Uses cameraunlock-core shared modules.
.PARAMETER CleanTemp
    Also remove temp directory error logs (used by vanilla.ps1).
#>
param(
    [switch]$CleanTemp
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Import shared modules
$coreModulesPath = Join-Path $PSScriptRoot "..\cameraunlock-core\powershell"
Import-Module (Join-Path $coreModulesPath "GamePathDetection.psm1") -Force

$GameId = 'GoneHome'

Write-Host ""
Write-Host "=== Gone Home - Uninstall HeadTracking ===" -ForegroundColor Magenta
Write-Host ""

# Find game
$gamePath = Find-GamePath -GameId $GameId
if (-not $gamePath) {
    Write-Host "ERROR: Gone Home installation not found" -ForegroundColor Red
    exit 1
}

$config = Get-GameConfig -GameId $GameId
Write-Host "Found Gone Home at: $gamePath" -ForegroundColor Green
Write-Host ""

$managedPath = Get-ManagedPath -GamePath $gamePath -DataFolder $config.DataFolder
$assemblyCSharpPath = Join-Path $managedPath "Assembly-CSharp.dll"
$assemblyCSharpBackup = Join-Path $managedPath "Assembly-CSharp.dll.original"

# Restore original Assembly-CSharp.dll if backup exists
if (Test-Path $assemblyCSharpBackup) {
    Write-Host "Restoring original Assembly-CSharp.dll..." -ForegroundColor Yellow
    Copy-Item $assemblyCSharpBackup $assemblyCSharpPath -Force
    Remove-Item $assemblyCSharpBackup -Force
    Write-Host "  Restored from backup" -ForegroundColor Gray
} else {
    Write-Host "No backup found - Assembly-CSharp.dll not modified" -ForegroundColor Yellow
}

# Remove mod DLLs
$dllsToRemove = @(
    "HeadTracking.dll",
    "CameraUnlock.Core.dll",
    "CameraUnlock.Core.Unity.dll",
    "Mono.Cecil.dll"
)

Write-Host "Removing mod files..." -ForegroundColor Yellow
foreach ($dll in $dllsToRemove) {
    $dllPath = Join-Path $managedPath $dll
    if (Test-Path $dllPath) {
        Remove-Item $dllPath -Force
        Write-Host "  Removed: $dll" -ForegroundColor Gray
    }
}

# Remove log files
$logFiles = @(
    "HeadTracking.log",
    "HeadTracking_BOOT.log"
)

foreach ($log in $logFiles) {
    $logPath = Join-Path $managedPath $log
    if (Test-Path $logPath) {
        Remove-Item $logPath -Force
        Write-Host "  Removed: $log" -ForegroundColor Gray
    }
}

# Optionally clean temp directory error logs
if ($CleanTemp) {
    $tempLog = Join-Path ([System.IO.Path]::GetTempPath()) "HeadTracking_BOOT_ERROR.log"
    if (Test-Path $tempLog) {
        Remove-Item $tempLog -Force
        Write-Host "  Removed: HeadTracking_BOOT_ERROR.log (from temp)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "HeadTracking mod uninstalled." -ForegroundColor Green
Write-Host ""

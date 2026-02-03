#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Packages the mod for release distribution.
.DESCRIPTION
    Creates a release ZIP containing:
    - install.cmd and uninstall.cmd scripts
    - Mod DLLs and patcher (in mod subfolder)
    - Documentation
.NOTES
    Run via: pixi run package
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force
$csprojPath = Join-Path $projectRoot "src\GoneHomeHeadTracking\GoneHomeHeadTracking.csproj"
$buildOutput = Join-Path $projectRoot "src\GoneHomeHeadTracking\bin\Release\net48"
$toolsDir = Join-Path $projectRoot "tools"
$distDir = Join-Path $projectRoot "release"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Gone Home Head Tracking - Packager" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get version
$version = Get-CsprojVersion $csprojPath
Write-Host "Version: $version" -ForegroundColor Yellow
Write-Host ""

# Validate build output exists
$modDll = Join-Path $buildOutput "HeadTracking.dll"
if (-not (Test-Path $modDll)) {
    Write-Host "ERROR: Build output not found. Run 'pixi run build' first." -ForegroundColor Red
    exit 1
}

# Create dist directory
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

# Create staging directory
$stagingDir = Join-Path $distDir "GoneHomeHeadTracking-v$version"
if (Test-Path $stagingDir) {
    Remove-Item -Recurse -Force $stagingDir
}
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

Write-Host "Staging release files..." -ForegroundColor Cyan

# Copy install/uninstall scripts
Copy-Item (Join-Path $scriptDir "install.cmd") -Destination $stagingDir -Force
Write-Host "  install.cmd" -ForegroundColor Green
Copy-Item (Join-Path $scriptDir "uninstall.cmd") -Destination $stagingDir -Force
Write-Host "  uninstall.cmd" -ForegroundColor Green

# Copy mod files to mod subfolder
$modDestDir = Join-Path $stagingDir "mod"
New-Item -ItemType Directory -Path $modDestDir -Force | Out-Null

$modDlls = @("HeadTracking.dll", "CameraUnlock.Core.dll", "CameraUnlock.Core.Unity.dll")
foreach ($dll in $modDlls) {
    $dllPath = Join-Path $buildOutput $dll
    if (-not (Test-Path $dllPath)) {
        Write-Host "ERROR: Required DLL not found: $dll" -ForegroundColor Red
        exit 1
    }
    Copy-Item $dllPath -Destination $modDestDir -Force
    Write-Host "  mod/$dll" -ForegroundColor Green
}

# Ensure Mono.Cecil is in tools folder and copy to mod
$cecilPath = & (Join-Path $scriptDir "ensure-cecil.ps1") -ToolsDir $toolsDir
Copy-Item $cecilPath -Destination $modDestDir -Force
Write-Host "  mod/Mono.Cecil.dll" -ForegroundColor Green

# Copy patcher source
$patcherSource = Join-Path $scriptDir "patcher\BootstrapPatcher.cs"
if (-not (Test-Path $patcherSource)) {
    Write-Host "ERROR: Patcher not found: $patcherSource" -ForegroundColor Red
    exit 1
}
Copy-Item $patcherSource -Destination $modDestDir -Force
Write-Host "  mod/BootstrapPatcher.cs" -ForegroundColor Green

# Copy documentation
$docFiles = @("README.md", "LICENSE", "CHANGELOG.md", "THIRD-PARTY-NOTICES.txt")
foreach ($doc in $docFiles) {
    $docPath = Join-Path $projectRoot $doc
    if (Test-Path $docPath) {
        Copy-Item $docPath -Destination $stagingDir -Force
        Write-Host "  $doc" -ForegroundColor Green
    } elseif ($doc -eq "LICENSE") {
        Write-Host "  WARNING: $doc not found" -ForegroundColor Yellow
    }
}

Write-Host ""

# Create ZIP archive
$zipName = "GoneHomeHeadTracking-v$version.zip"
$zipPath = Join-Path $distDir $zipName

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creating ZIP archive..." -ForegroundColor Yellow

Push-Location $stagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $zipPath -Force
} finally {
    Pop-Location
}

# Cleanup staging
Remove-Item -Recurse -Force $stagingDir

$zipSize = (Get-Item $zipPath).Length / 1KB
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Package Created Successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output: $zipPath" -ForegroundColor Cyan
Write-Host ("Size: {0:N1} KB" -f $zipSize) -ForegroundColor Gray
Write-Host ""
Write-Host "Contents:" -ForegroundColor Yellow
Write-Host "  - install.cmd, uninstall.cmd" -ForegroundColor Gray
Write-Host "  - mod/ (DLLs, Mono.Cecil, patcher)" -ForegroundColor Gray
Write-Host "  - Documentation" -ForegroundColor Gray
Write-Host ""

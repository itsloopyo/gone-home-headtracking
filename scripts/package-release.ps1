#!/usr/bin/env pwsh
#Requires -Version 5.1
# Packages the mod for release distribution.
# Produces two ZIPs:
#   - GoneHomeHeadTracking-v{version}-installer.zip (GitHub Release: install.cmd + mod/ + docs)
#   - GoneHomeHeadTracking-v{version}-nexus.zip     (Nexus Mods: extract-to-game-folder layout)

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

# Mod DLLs that go into the game's Managed folder
$modDlls = @("HeadTracking.dll", "CameraUnlock.Core.dll", "CameraUnlock.Core.Unity.dll")

# Validate build output exists
foreach ($dll in $modDlls) {
    $dllPath = Join-Path $buildOutput $dll
    if (-not (Test-Path $dllPath)) {
        Write-Host "ERROR: Required DLL not found: $dll. Run 'pixi run build' first." -ForegroundColor Red
        exit 1
    }
}

# Validate install/uninstall scripts
foreach ($script in @("install.cmd", "uninstall.cmd")) {
    $scriptPath = Join-Path $scriptDir $script
    if (-not (Test-Path $scriptPath)) {
        Write-Host "ERROR: Required script not found: $scriptPath" -ForegroundColor Red
        exit 1
    }
}

# Validate patcher source
$patcherSource = Join-Path $scriptDir "patcher\BootstrapPatcher.cs"
if (-not (Test-Path $patcherSource)) {
    Write-Host "ERROR: Patcher not found: $patcherSource" -ForegroundColor Red
    exit 1
}

# Create dist directory
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

# --- Installer ZIP (GitHub Release) ---

Write-Host "--- Installer ZIP ---" -ForegroundColor Yellow
Write-Host ""

$ghStagingDir = Join-Path $distDir "staging-installer"
if (Test-Path $ghStagingDir) { Remove-Item -Recurse -Force $ghStagingDir }
New-Item -ItemType Directory -Path $ghStagingDir -Force | Out-Null

# Copy install/uninstall scripts
foreach ($script in @("install.cmd", "uninstall.cmd")) {
    Copy-Item (Join-Path $scriptDir $script) -Destination $ghStagingDir -Force
    Write-Host "  $script" -ForegroundColor Green
}

# Stamp launcher-manifest.json with the real release version and copy it
# into the installer ZIP root. The launcher reads this file to decide how
# to stage the mod (delivery_mode: install_cmd -> shell out to install.cmd).
$manifestSource = Join-Path $projectRoot "launcher-manifest.json"
if (-not (Test-Path $manifestSource)) {
    Write-Host "ERROR: launcher-manifest.json not found at repo root ($manifestSource)" -ForegroundColor Red
    exit 1
}
$manifestJson = Get-Content $manifestSource -Raw | ConvertFrom-Json
$manifestJson.mod_info.version = $version
$manifestDest = Join-Path $ghStagingDir "launcher-manifest.json"
# `Set-Content -Encoding UTF8` on Windows PowerShell 5.1 writes a BOM
# (EF BB BF) which serde_json rejects with "expected value at line 1
# column 1". Write through the .NET API with an explicit no-BOM encoder
# so the file is portable across every strict JSON parser.
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText(
    $manifestDest,
    ($manifestJson | ConvertTo-Json -Depth 10),
    $utf8NoBom
)
Write-Host "  launcher-manifest.json (v$version)" -ForegroundColor Green

# Copy mod files to mod subfolder
$modDestDir = Join-Path $ghStagingDir "mod"
New-Item -ItemType Directory -Path $modDestDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutput $dll) -Destination $modDestDir -Force
    Write-Host "  mod/$dll" -ForegroundColor Green
}

# Ensure Mono.Cecil is in tools folder and copy to mod
$cecilPath = & (Join-Path $scriptDir "ensure-cecil.ps1") -ToolsDir $toolsDir
Copy-Item $cecilPath -Destination $modDestDir -Force
Write-Host "  mod/Mono.Cecil.dll" -ForegroundColor Green

# Copy patcher source
Copy-Item $patcherSource -Destination $modDestDir -Force
Write-Host "  mod/BootstrapPatcher.cs" -ForegroundColor Green

# Copy documentation
$docFiles = @("README.md", "LICENSE", "CHANGELOG.md", "THIRD-PARTY-NOTICES.txt")
foreach ($doc in $docFiles) {
    $docPath = Join-Path $projectRoot $doc
    if (Test-Path $docPath) {
        Copy-Item $docPath -Destination $ghStagingDir -Force
        Write-Host "  $doc" -ForegroundColor Green
    } elseif ($doc -eq "LICENSE") {
        Write-Host "  WARNING: $doc not found" -ForegroundColor Yellow
    }
}

$ghZipName = "GoneHomeHeadTracking-v$version-installer.zip"
$ghZipPath = Join-Path $distDir $ghZipName
if (Test-Path $ghZipPath) { Remove-Item $ghZipPath -Force }

Write-Host ""
Write-Host "Creating installer ZIP..." -ForegroundColor Cyan

Push-Location $ghStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $ghZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $ghStagingDir

$ghZipSize = (Get-Item $ghZipPath).Length / 1KB
Write-Host ("  $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green

# --- Nexus Mods ZIP (extract-to-game-folder) ---

Write-Host ""
Write-Host "--- Nexus Mods ZIP ---" -ForegroundColor Yellow
Write-Host ""

$nexusStagingDir = Join-Path $distDir "staging-nexus"
if (Test-Path $nexusStagingDir) { Remove-Item -Recurse -Force $nexusStagingDir }

# Mirror game directory structure: GoneHome_Data\Managed\
$nexusManagedDir = Join-Path $nexusStagingDir "GoneHome_Data\Managed"
New-Item -ItemType Directory -Path $nexusManagedDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutput $dll) -Destination $nexusManagedDir -Force
    Write-Host "  GoneHome_Data/Managed/$dll" -ForegroundColor Green
}

$nexusZipName = "GoneHomeHeadTracking-v$version-nexus.zip"
$nexusZipPath = Join-Path $distDir $nexusZipName
if (Test-Path $nexusZipPath) { Remove-Item $nexusZipPath -Force }

Write-Host ""
Write-Host "Creating Nexus ZIP..." -ForegroundColor Cyan

Push-Location $nexusStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $nexusZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $nexusStagingDir

$nexusZipSize = (Get-Item $nexusZipPath).Length / 1KB
Write-Host ("  $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green

# --- Summary ---

Write-Host ""
Write-Host "=== Package Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host ("Installer: $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green
Write-Host ("Nexus Mods: $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green

# Output both zip paths for CI capture (one per line)
Write-Output $ghZipPath
Write-Output $nexusZipPath

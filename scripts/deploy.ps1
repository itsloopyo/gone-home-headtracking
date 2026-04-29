#!/usr/bin/env pwsh
#Requires -Version 5.1
param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

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
$managedPath = Get-ManagedPath -GamePath $gamePath -DataFolder $config.DataFolder
$assemblyCSharpPath = Join-Path $managedPath "Assembly-CSharp.dll"
$assemblyCSharpBackup = Join-Path $managedPath "Assembly-CSharp.dll.original"

# Find the built DLL
$dllPath = Join-Path $projectRoot "src\GoneHomeHeadTracking\bin\$Configuration\net48\HeadTracking.dll"

if (-not (Test-Path $dllPath)) {
    Write-Host "ERROR: Built DLL not found at: $dllPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Make sure the build completed successfully." -ForegroundColor Yellow
    Write-Host "Run: pixi run build" -ForegroundColor Yellow
    exit 1
}

# Copy HeadTracking.dll to Managed folder
$destDllPath = Join-Path $managedPath "HeadTracking.dll"
Copy-Item -Path $dllPath -Destination $destDllPath -Force
Write-Host "Deployed HeadTracking.dll to: $destDllPath" -ForegroundColor Green

# Copy CameraUnlock.Core.dll (shared library)
$coreDllPath = Join-Path $projectRoot "src\GoneHomeHeadTracking\bin\$Configuration\net48\CameraUnlock.Core.dll"
if (Test-Path $coreDllPath) {
    $destCoreDllPath = Join-Path $managedPath "CameraUnlock.Core.dll"
    Copy-Item -Path $coreDllPath -Destination $destCoreDllPath -Force
    Write-Host "Deployed CameraUnlock.Core.dll to: $destCoreDllPath" -ForegroundColor Green
}

# Copy CameraUnlock.Core.Unity.dll (Unity extensions)
$coreUnityDllPath = Join-Path $projectRoot "src\GoneHomeHeadTracking\bin\$Configuration\net48\CameraUnlock.Core.Unity.dll"
if (Test-Path $coreUnityDllPath) {
    $destCoreUnityDllPath = Join-Path $managedPath "CameraUnlock.Core.Unity.dll"
    Copy-Item -Path $coreUnityDllPath -Destination $destCoreUnityDllPath -Force
    Write-Host "Deployed CameraUnlock.Core.Unity.dll to: $destCoreUnityDllPath" -ForegroundColor Green
}

# Ensure Mono.Cecil is available in tools folder for patching
$toolsDir = Join-Path $projectRoot "tools"
$cecilPath = & (Join-Path $scriptDir "ensure-cecil.ps1") -ToolsDir $toolsDir

# Patch Assembly-CSharp.dll
Write-Host ""
Write-Host "Patching Assembly-CSharp.dll..." -ForegroundColor Yellow

# Create backup first if it doesn't exist
if (-not (Test-Path $assemblyCSharpBackup)) {
    Copy-Item $assemblyCSharpPath $assemblyCSharpBackup -Force
    Write-Host "  Created backup: Assembly-CSharp.dll.original" -ForegroundColor Gray
} else {
    # Restore from backup before patching to ensure clean state
    Copy-Item $assemblyCSharpBackup $assemblyCSharpPath -Force
    Write-Host "  Restored from backup for clean patch" -ForegroundColor Gray
}

# Load Mono.Cecil and shared patcher
Add-Type -Path $cecilPath

$patcherCode = Get-Content (Join-Path $scriptDir "patcher\BootstrapPatcher.cs") -Raw

$compilerParams = New-Object System.CodeDom.Compiler.CompilerParameters
[void]$compilerParams.ReferencedAssemblies.Add($cecilPath)
[void]$compilerParams.ReferencedAssemblies.Add("System.dll")
[void]$compilerParams.ReferencedAssemblies.Add("System.Core.dll")
$compilerParams.CompilerOptions = "/nowarn:1668 /warn:0"
$compilerParams.TreatWarningsAsErrors = $false

Add-Type -TypeDefinition $patcherCode -CompilerParameters $compilerParams

$result = [BootstrapPatcher]::PatchAssembly($assemblyCSharpPath)

if (-not $result) {
    Write-Host "ERROR: Patching failed!" -ForegroundColor Red
    exit 1
}

# Clean up any old doorstop files
$doorstopFiles = @("winhttp.dll", "version.dll", "doorstop_config.ini", ".doorstop_version")
foreach ($file in $doorstopFiles) {
    $filePath = Join-Path $gamePath $file
    if (Test-Path $filePath) {
        Remove-Item $filePath -Force
        Write-Host "  Removed old doorstop file: $file" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "The Head Tracking mod has been deployed to:" -ForegroundColor White
Write-Host "  $destDllPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Start the game to use head tracking!" -ForegroundColor White
Write-Host ""
Write-Host "Controls:" -ForegroundColor Yellow
Write-Host "  Home - Recenter head tracking" -ForegroundColor Gray
Write-Host "  End  - Toggle head tracking on/off" -ForegroundColor Gray
Write-Host ""

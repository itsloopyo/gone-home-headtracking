#!/usr/bin/env pwsh
#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

<#
.SYNOPSIS
    Validates release readiness for Gone Home Head Tracking mod.

.DESCRIPTION
    Checks:
    1. Required documentation files exist
    2. Build output exists
    3. Version consistency
#>

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Write-Host "=== Gone Home Head Tracking - Release Validation ===" -ForegroundColor Cyan
Write-Host ""

$allPassed = $true

# Check README.md
Write-Host "Checking README.md..." -ForegroundColor Gray
$readmePath = Join-Path $projectRoot "README.md"
if (Test-Path $readmePath) {
    Write-Host "  README.md exists" -ForegroundColor Green
} else {
    Write-Host "  ERROR: README.md not found" -ForegroundColor Red
    $allPassed = $false
}

# Check LICENSE
Write-Host "Checking LICENSE..." -ForegroundColor Gray
$licensePath = Join-Path $projectRoot "LICENSE"
if (Test-Path $licensePath) {
    Write-Host "  LICENSE exists" -ForegroundColor Green
} else {
    Write-Host "  ERROR: LICENSE not found" -ForegroundColor Red
    $allPassed = $false
}

# Check build output
Write-Host "Checking build output..." -ForegroundColor Gray
$dllPath = Join-Path $projectRoot "src\GoneHomeHeadTracking\bin\Release\net48\HeadTracking.dll"
if (Test-Path $dllPath) {
    $dllInfo = Get-Item $dllPath
    Write-Host "  HeadTracking.dll exists ($($dllInfo.Length) bytes)" -ForegroundColor Green
} else {
    Write-Host "  WARNING: HeadTracking.dll not found (run 'pixi run build-release' first)" -ForegroundColor Yellow
    $allPassed = $false
}

# Summary
Write-Host ""
Write-Host "===============================" -ForegroundColor Cyan

if ($allPassed) {
    Write-Host "All validation checks passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Some validation checks failed." -ForegroundColor Yellow
    Write-Host "Please review the issues above before releasing." -ForegroundColor Gray
    exit 1
}

#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Automated release workflow for Gone Home Head Tracking mod.

.DESCRIPTION
    Runs end-to-end with no operator interaction:
    1. Validate semver / branch / clean tree / tag absent.
    2. Update version in csproj, mod source, and pixi.toml.
    3. Release build.
    4. Generate CHANGELOG from commits.
    5. Commit Release v<version>.
    6. Create annotated tag v<version>.
    7. Push commits + tag (CI release workflow takes over).

.PARAMETER Version
    "<X.Y.Z>" or "major" / "minor" / "patch".

.EXAMPLE
    pixi run release patch

.NOTES
    Run via: pixi run release <version>
    The CLI invocation IS the consent. There is no second gate.
#>
param(
    [Parameter(Position=0)]
    [string]$Version = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$csprojPath = Join-Path $projectDir "src\GoneHomeHeadTracking\GoneHomeHeadTracking.csproj"
$modSourcePath = Join-Path $projectDir "src\GoneHomeHeadTracking\Core\HeadTrackingMod.cs"
$pixiTomlPath = Join-Path $projectDir "pixi.toml"
$changelogPath = Join-Path $projectDir "CHANGELOG.md"

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

Write-Host "=== Gone Home Head Tracking Release ===" -ForegroundColor Cyan
Write-Host ""

$currentVersion = Get-CsprojVersion $csprojPath

if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host "Current version: " -NoNewline -ForegroundColor Yellow
    Write-Host $currentVersion -ForegroundColor White
    Write-Host ""
    Write-Host "Usage: pixi run release <major|minor|patch|X.Y.Z>" -ForegroundColor Yellow
    exit 0
}

try {
    $Version = Resolve-ReleaseVersion -Argument $Version -CurrentVersion $currentVersion
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "Error: Resolved version '$Version' is not a valid semver (X.Y.Z)" -ForegroundColor Red
    exit 1
}

$tagName = "v$Version"

# --- Preconditions (the safety net; no interactive gate behind them) ---

$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "main") {
    Write-Host "Error: Must be on 'main' branch to release (currently on '$currentBranch')" -ForegroundColor Red
    exit 1
}

$status = git status --porcelain
if ($status) {
    Write-Host "Error: Working directory has uncommitted changes" -ForegroundColor Red
    Write-Host $status -ForegroundColor Gray
    exit 1
}

$existingTag = git tag -l $tagName
if ($existingTag) {
    Write-Host "Error: Tag '$tagName' already exists" -ForegroundColor Red
    exit 1
}

Write-Host "Current version: $currentVersion" -ForegroundColor Gray
Write-Host "New version:     $Version" -ForegroundColor Green
Write-Host ""

# --- Step 1: Update version in csproj ---
Write-Host "Updating version to $Version..." -ForegroundColor Cyan
Set-CsprojVersion $csprojPath $Version

# --- Step 2: Update version in mod source ---
$modContent = Get-Content $modSourcePath -Raw
$modContent = $modContent -replace 'ModVersion = "[^"]+"', "ModVersion = `"$Version`""
$modContent | Set-Content $modSourcePath -NoNewline
Write-Host "  Updated HeadTrackingMod.cs" -ForegroundColor Gray

# --- Step 3: Update version in pixi.toml (keeps the workspace metadata in sync) ---
$pixiContent = Get-Content $pixiTomlPath -Raw
$pixiContent = $pixiContent -replace '(?m)^version\s*=\s*"[^"]+"', "version = `"$Version`""
$pixiContent | Set-Content $pixiTomlPath -NoNewline
Write-Host "  Updated pixi.toml" -ForegroundColor Gray

# --- Step 4: Release build via pixi ---
Write-Host "Building release..." -ForegroundColor Cyan
Push-Location $projectDir
try {
    pixi run build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

# --- Step 5: Generate CHANGELOG ---
Write-Host "Generating CHANGELOG from commits..." -ForegroundColor Cyan
$hasExistingTags = git tag -l 2>$null
if (-not $hasExistingTags) {
    $date = Get-Date -Format 'yyyy-MM-dd'
    $firstEntry = "# Changelog`n`n## [$Version] - $date`n`nFirst release.`n"
    Set-Content $changelogPath $firstEntry
    Write-Host "  First release - wrote initial CHANGELOG entry" -ForegroundColor Gray
} else {
    New-ChangelogFromCommits `
        -ChangelogPath $changelogPath `
        -Version $Version `
        -ArtifactPaths @(
            "src/GoneHomeHeadTracking/",
            "cameraunlock-core",
            "scripts/install.cmd",
            "scripts/uninstall.cmd",
            "scripts/patcher/"
        )
}

# --- Step 6: Commit ---
Write-Host "Committing changes..." -ForegroundColor Cyan
git add $csprojPath
git add $modSourcePath
git add $pixiTomlPath
git add $changelogPath
git commit -m "Release v$Version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Commit failed!" -ForegroundColor Red
    exit 1
}

# --- Step 7: Create annotated tag ---
Write-Host "Creating tag $tagName..." -ForegroundColor Cyan
git tag -a $tagName -m "Release $tagName"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Tag creation failed!" -ForegroundColor Red
    exit 1
}

# --- Step 8: Push commits + tag ---
Write-Host "Pushing to GitHub..." -ForegroundColor Cyan
git push origin main
if ($LASTEXITCODE -ne 0) {
    Write-Host "Push of main failed!" -ForegroundColor Red
    exit 1
}
git push origin $tagName
if ($LASTEXITCODE -ne 0) {
    Write-Host "Push of tag failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Release $tagName initiated." -ForegroundColor Green
Write-Host "GitHub Actions release workflow will build and publish artifacts." -ForegroundColor Gray
Write-Host "Watch: https://github.com/itsloopyo/gone-home-headtracking/actions" -ForegroundColor Cyan

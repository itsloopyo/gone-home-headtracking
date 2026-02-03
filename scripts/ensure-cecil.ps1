#Requires -Version 5.1
<#
.SYNOPSIS
    Ensures Mono.Cecil is available in the tools directory.
.DESCRIPTION
    Downloads Mono.Cecil from NuGet if not already present.
    Returns the path to Mono.Cecil.dll.
.PARAMETER ToolsDir
    Path to the tools directory where Mono.Cecil.dll should be placed.
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$ToolsDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$cecilPath = Join-Path $ToolsDir "Mono.Cecil.dll"

if (-not (Test-Path $cecilPath)) {
    Write-Host "Downloading Mono.Cecil for patching..." -ForegroundColor Yellow
    if (-not (Test-Path $ToolsDir)) {
        New-Item -ItemType Directory -Path $ToolsDir -Force | Out-Null
    }

    $nugetUrl = "https://www.nuget.org/api/v2/package/Mono.Cecil/0.11.5"
    $zipPath = Join-Path $ToolsDir "mono.cecil.zip"
    $extractPath = Join-Path $ToolsDir "mono.cecil"

    Invoke-WebRequest -Uri $nugetUrl -OutFile $zipPath
    Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force
    Copy-Item (Join-Path $extractPath "lib\net40\Mono.Cecil.dll") $cecilPath -Force

    # Cleanup
    Remove-Item $zipPath -Force
    Remove-Item $extractPath -Recurse -Force

    Write-Host "  Downloaded Mono.Cecil to tools/" -ForegroundColor Green
}

return $cecilPath

#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Creates Unity stub assemblies for CI builds.
.DESCRIPTION
    Builds UnityEngine.dll from the checked-in UnityStubs.cs and creates
    empty stub assemblies for other Unity modules. Used by CI workflows
    when real game DLLs aren't available.
.PARAMETER LibsPath
    Path to the libs directory where stubs will be created.
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$LibsPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "Creating Unity stub assemblies for CI build..." -ForegroundColor Cyan

# Build UnityEngine.dll with all types from the checked-in UnityStubs.cs
$projContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net35</TargetFramework>
    <LangVersion>11</LangVersion>
    <AssemblyName>UnityEngine</AssemblyName>
    <NoWarn>CS0169;CS0649;CS0067;CS0660;CS0661</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="UnityStubs.cs" />
  </ItemGroup>
</Project>
"@
$projPath = Join-Path $LibsPath "Stub_UnityEngine.csproj"
$projContent | Out-File -FilePath $projPath -Encoding utf8

dotnet build $projPath -c Release -o $LibsPath --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "::error::Failed to build UnityEngine stub"
    exit 1
}
Write-Host "  Created UnityEngine.dll (with types)" -ForegroundColor Green
Remove-Item $projPath -ErrorAction SilentlyContinue

# Build empty stubs for other Unity modules
$emptySource = "// Empty stub assembly"
$emptySourcePath = Join-Path $LibsPath "EmptyStub.cs"
$emptySource | Out-File -FilePath $emptySourcePath -Encoding utf8

$emptyModules = @(
    "UnityEngine.CoreModule",
    "UnityEngine.IMGUIModule",
    "UnityEngine.PhysicsModule",
    "UnityEngine.UIModule",
    "UnityEngine.TextRenderingModule",
    "UnityEngine.UI"
)

foreach ($moduleName in $emptyModules) {
    $emptyProjContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net35</TargetFramework>
    <AssemblyName>$moduleName</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="EmptyStub.cs" />
  </ItemGroup>
</Project>
"@
    $emptyProjPath = Join-Path $LibsPath "Stub_$moduleName.csproj"
    $emptyProjContent | Out-File -FilePath $emptyProjPath -Encoding utf8

    dotnet build $emptyProjPath -c Release -o $LibsPath --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Host "::error::Failed to build $moduleName stub"
        exit 1
    }
    Write-Host "  Created $moduleName.dll (empty)" -ForegroundColor Green
    Remove-Item $emptyProjPath -ErrorAction SilentlyContinue
}

# Cleanup temp files
Remove-Item $emptySourcePath -ErrorAction SilentlyContinue
Remove-Item (Join-Path $LibsPath "*.deps.json") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $LibsPath "*.pdb") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $LibsPath "obj") -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Unity stub assemblies created successfully" -ForegroundColor Green

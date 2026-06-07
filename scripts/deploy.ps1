#!/usr/bin/env pwsh
#Requires -Version 5.1
# Thin wrapper - dev-deploy orchestration lives in
# cameraunlock-core/powershell/DevDeploy.psm1. To change deploy behaviour
# edit the orchestrator, not this wrapper. The only mod-specific bit is
# the patcher block below (Gone Home uses a custom BootstrapPatcher.cs;
# mods on the shared screen-center patcher would call
# Invoke-HeadTrackingPatch from AssemblyPatching.psm1 instead).

param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    # Optional explicit game path. The launcher's run_pixi_task always
    # appends the resolved game path here, mirroring the install.cmd
    # contract. When absent we fall back to Find-GamePath.
    [Parameter(Mandatory=$false, Position=1)]
    [string]$GivenPath,
    # Catchall so /y, -y, --yes etc. don't trip PowerShell's "unexpected
    # positional" error. Dev deploy doesn't prompt, so it's a no-op here.
    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$RemainingArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$toolsDir = Join-Path $projectRoot "tools"

Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\DevDeploy.psm1") -Force
Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\ModDeployment.psm1") -Force

# Mono.Cecil is fetched once into tools/ and reused across deploys.
$cecilPath = & (Join-Path $scriptDir "ensure-cecil.ps1") -ToolsDir $toolsDir

# Compile the custom Mono.Cecil bootstrap patcher once. Gone Home uses this
# rather than the shared screen-center patcher in AssemblyPatching.psm1 because
# its Unity Mono build needs a different injection point. Compiling once (not
# inside each scriptblock) lets the patch and unpatch callbacks share the type
# - Add-Type of the same type twice in one session throws.
Add-Type -Path $cecilPath
$patcherCode = Get-Content (Join-Path $scriptDir "patcher\BootstrapPatcher.cs") -Raw
$cp = New-Object System.CodeDom.Compiler.CompilerParameters
[void]$cp.ReferencedAssemblies.Add($cecilPath)
[void]$cp.ReferencedAssemblies.Add("System.dll")
[void]$cp.ReferencedAssemblies.Add("System.Core.dll")
$cp.CompilerOptions = "/nowarn:1668 /warn:0"
$cp.TreatWarningsAsErrors = $false
Add-Type -TypeDefinition $patcherCode -CompilerParameters $cp

$buildOutput = Join-Path $projectRoot "src\GoneHomeHeadTracking\bin\$Configuration\net35"
$result = Invoke-DevDeployCecil `
    -GameId 'gone-home' `
    -GameDisplayName 'Gone Home' `
    -BuildOutputPath $buildOutput `
    -ModDllName 'HeadTracking.dll' `
    -ManagedSubfolder 'GoneHome_Data\Managed' `
    -ExtraDlls @('CameraUnlock.Core.dll', 'CameraUnlock.Core.Unity.dll') `
    -GivenPath $GivenPath `
    -PatchMarker 'HeadTracking_Patched_GoneHome_v4' `
    -Patcher {
        param($assemblyPath)
        if (-not [BootstrapPatcher]::PatchAssembly($assemblyPath)) {
            throw "BootstrapPatcher::PatchAssembly returned false"
        }
    } `
    -Unpatcher {
        param($assemblyPath)
        if (-not [BootstrapPatcher]::UnpatchAssembly($assemblyPath)) {
            throw "BootstrapPatcher::UnpatchAssembly returned false"
        }
    }

Write-DeploymentSuccess `
    -ModName "Head Tracking mod" `
    -DeployPath $result.DeployedDllPath `
    -RecenterKey "Home" `
    -ToggleKey "End"

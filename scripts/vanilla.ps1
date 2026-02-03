#Requires -Version 5.1
<#
.SYNOPSIS
    Removes mod to revert to vanilla Gone Home.
.DESCRIPTION
    Delegates to uninstall.ps1 with -CleanTemp to also remove temp error logs.
#>

& (Join-Path $PSScriptRoot "uninstall.ps1") -CleanTemp

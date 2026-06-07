@echo off
:: ============================================
:: Gone Home Head Tracking - Install
:: ============================================
:: Mono.Cecil patcher for Gone Home (Unity Mono).
:: Thin wrapper - the install body lives in cameraunlock-core and is
:: bundled into the release zip's shared/ folder by Copy-SharedBundle.
:: To change install behaviour edit the body, not this wrapper.
:: ============================================

:: --- CONFIG BLOCK ---
set "GAME_ID=gone-home"
set "MOD_DISPLAY_NAME=Gone Home Head Tracking"
set "MOD_DLLS=HeadTracking.dll CameraUnlock.Core.dll CameraUnlock.Core.Unity.dll Mono.Cecil.dll"
set "MOD_INTERNAL_NAME=GoneHomeHeadTracking"
set "MOD_VERSION=1.1.1"
set "STATE_FILE=.headtracking-state.json"
set "FRAMEWORK_TYPE=MonoCecil"
set "MANAGED_SUBFOLDER=GoneHome_Data\Managed"
set "ASSEMBLY_DLL=Assembly-CSharp.dll"
set "PATCHER_FILE=BootstrapPatcher.cs"
set "PATCH_MARKER=HeadTracking_Patched_GoneHome_v4"
set "MOD_CONTROLS=Controls:&echo   Home    - Recenter head tracking&echo   End     - Toggle head tracking on/off&echo   PgUp    - Cycle tracking mode (full / rotation only / position only)&echo   PgDn    - Toggle yaw mode (horizon-locked / camera-local)"
:: --- END CONFIG BLOCK ---

:: WRAPPER_DIR is what the body uses to resolve sibling files (mod/,
:: shared/find-game.ps1) - its own %~dp0 inside the body file would point
:: at the body's location, which is wrong in both release and dev.
set "WRAPPER_DIR=%~dp0"

set "_BODY=%WRAPPER_DIR%shared\install-body-cecil.cmd"
if not exist "%_BODY%" set "_BODY=%WRAPPER_DIR%..\cameraunlock-core\scripts\install-body-cecil.cmd"
if not exist "%_BODY%" (
    echo ERROR: install-body-cecil.cmd not found in shared\ or ..\cameraunlock-core\scripts\.
    echo If this is a release ZIP, re-download it from GitHub ^(corrupt installer^).
    echo If this is the dev tree, run: git submodule update --init --recursive
    exit /b 1
)
call "%_BODY%" %*
exit /b %errorlevel%

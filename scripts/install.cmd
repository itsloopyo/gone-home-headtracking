@echo off
:: ============================================
:: Gone Home Head Tracking - Install
:: ============================================
:: Thin wrapper - install body lives in cameraunlock-core/scripts/install-body-cecil.cmd,
:: staged into the release ZIP's shared/ by Copy-SharedBundle. To change
:: install behaviour edit the body, not this wrapper. Everything below the
:: CONFIG BLOCK is copied verbatim from
:: cameraunlock-core/scripts/templates/install-wrapper-cecil.cmd.
:: ============================================

:: --- CONFIG BLOCK ---
set "GAME_ID=gone-home"
set "MOD_DISPLAY_NAME=Gone Home Head Tracking"
set "MOD_DLLS=HeadTracking.dll CameraUnlock.Core.dll CameraUnlock.Core.Unity.dll Mono.Cecil.dll"
set "MOD_INTERNAL_NAME=GoneHomeHeadTracking"
set "MOD_VERSION=1.5.0"
set "STATE_FILE=.headtracking-state.json"
set "FRAMEWORK_TYPE=MonoCecil"
set "MANAGED_SUBFOLDER=GoneHome_Data\Managed"
set "ASSEMBLY_DLL=Assembly-CSharp.dll"
set "PATCHER_FILE=BootstrapPatcher.cs"
set "PATCH_MARKER=HeadTracking_Patched_GoneHome_v4"
set "MOD_CONTROLS=Controls:&echo   End     - Toggle head tracking on/off&echo   PgUp    - Cycle tracking mode (full / rotation only / position only)&echo   PgDn    - Toggle yaw mode (horizon-locked / camera-local)"
:: --- END CONFIG BLOCK ---

:: Pin delayed expansion off before `%*` is expanded on the `call` below.
:: Under `cmd /V:ON`, or with DelayedExpansion=1 in
:: HKCU\Software\Microsoft\Command Processor, cmd.exe eats a `!` out of the
:: expanded line, and a real game path like C:\Games\Oh! My Game reaches the
:: body already mangled. The body pins expansion off at its own outer scope
:: too, but that is one `call` too late to save the argument it was handed.
setlocal disabledelayedexpansion

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
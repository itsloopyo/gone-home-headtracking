@echo off
:: ============================================
:: Gone Home Head Tracking - Install
:: ============================================
:: Based on cameraunlock-core/scripts/templates/install.cmd
:: ============================================

:: --- CONFIG BLOCK ---
set "MOD_DISPLAY_NAME=Gone Home Head Tracking"
set "GAME_EXE=GoneHome.exe"
set "GAME_DISPLAY_NAME=Gone Home"
set "STEAM_FOLDER_NAME=Gone Home"
set "ENV_VAR_NAME=GONEHOME_PATH"
set "MOD_DLLS=HeadTracking.dll CameraUnlock.Core.dll CameraUnlock.Core.Unity.dll Mono.Cecil.dll"
set "MOD_INTERNAL_NAME=GoneHomeHeadTracking"
set "MOD_VERSION=1.0.0"
set "MANAGED_SUBFOLDER=GoneHome_Data\Managed"
set "ASSEMBLY_DLL=Assembly-CSharp.dll"
set "MOD_CONTROLS=Controls:&echo   Home   - Recenter head tracking&echo   End    - Toggle head tracking on/off&echo   PgUp   - Toggle position tracking (6DOF/3DOF)"
set "GOG_IDS="
set "SEARCH_DIRS="
:: --- END CONFIG BLOCK ---

call :main %*
set "_EC=%errorlevel%"
echo.
pause
exit /b %_EC%

:main
setlocal enabledelayedexpansion

echo.
echo === %MOD_DISPLAY_NAME% - Install ===
echo.

set "SCRIPT_DIR=%~dp0"
set "GAME_PATH="

:: --- Find game path ---

:: Check command line argument
if not "%~1"=="" (
    if exist "%~1\%GAME_EXE%" (
        set "GAME_PATH=%~1"
        goto :found_game
    )
    echo ERROR: %GAME_EXE% not found at: "%~1"
    echo.
    exit /b 1
)

:: Check environment variable
if defined %ENV_VAR_NAME% (
    call set "_ENV_PATH=%%%ENV_VAR_NAME%%%"
    if exist "!_ENV_PATH!\%GAME_EXE%" (
        set "GAME_PATH=!_ENV_PATH!"
        goto :found_game
    )
)

:: Check Steam
call :find_steam_game
if defined GAME_PATH goto :found_game

:: Check GOG
call :find_gog_game
if defined GAME_PATH goto :found_game

:: Check Epic
call :find_epic_game
if defined GAME_PATH goto :found_game

:: Check common directories
call :find_game_in_dirs
if defined GAME_PATH goto :found_game

echo ERROR: Could not find %GAME_DISPLAY_NAME% installation.
echo.
echo Please either:
echo   1. Set %ENV_VAR_NAME% environment variable to your game folder
echo   2. Run: install.cmd "C:\path\to\game"
echo.
exit /b 1

:found_game
echo Game found: %GAME_PATH%
echo.

:: --- Check if game is running ---
tasklist /fi "imagename eq %GAME_EXE%" 2>nul | findstr /i "%GAME_EXE%" >nul 2>&1
if not errorlevel 1 (
    echo ERROR: %GAME_DISPLAY_NAME% is currently running.
    echo Please close the game before installing.
    echo.
    exit /b 1
)

set "MANAGED_PATH=%GAME_PATH%\%MANAGED_SUBFOLDER%"
set "ASSEMBLY_PATH=%MANAGED_PATH%\%ASSEMBLY_DLL%"
set "BACKUP_PATH=%MANAGED_PATH%\%ASSEMBLY_DLL%.original"
set "MOD_DIR=%SCRIPT_DIR%mod"

:: Verify Managed folder exists
if not exist "%MANAGED_PATH%" (
    echo ERROR: %MANAGED_SUBFOLDER% folder not found.
    echo   Expected at: !MANAGED_PATH!
    echo.
    exit /b 1
)

:: Verify Assembly DLL exists
if not exist "%ASSEMBLY_PATH%" (
    echo ERROR: %ASSEMBLY_DLL% not found.
    echo   Expected at: !ASSEMBLY_PATH!
    echo.
    exit /b 1
)

:: Verify mod files exist
for %%f in (%MOD_DLLS%) do (
    if not exist "%MOD_DIR%\%%f" (
        echo ERROR: %%f not found in mod folder.
        echo   Make sure all files from the release package are intact.
        echo.
        exit /b 1
    )
)

if not exist "%MOD_DIR%\BootstrapPatcher.cs" (
    echo ERROR: BootstrapPatcher.cs not found in mod folder.
    echo   Make sure all files from the release package are intact.
    echo.
    exit /b 1
)

:: --- Back up Assembly DLL ---
echo Backing up %ASSEMBLY_DLL%...
if not exist "%BACKUP_PATH%" (
    copy /y "%ASSEMBLY_PATH%" "%BACKUP_PATH%" >nul
    echo   Created: %ASSEMBLY_DLL%.original
) else (
    echo   Backup already exists, restoring clean state...
    copy /y "%BACKUP_PATH%" "%ASSEMBLY_PATH%" >nul
)
echo.

:: --- Copy mod files ---
echo Deploying mod files...

set "DEPLOY_FAILED=0"
for %%f in (%MOD_DLLS%) do (
    copy /y "%MOD_DIR%\%%f" "%MANAGED_PATH%\" >nul
    if errorlevel 1 (
        echo   ERROR: Failed to copy %%f
        set "DEPLOY_FAILED=1"
    ) else (
        echo   Deployed %%f
    )
)

if "!DEPLOY_FAILED!"=="1" (
    echo.
    echo ERROR: File deployment failed.
    echo.
    exit /b 1
)
echo.

:: --- Unblock DLLs (Windows blocks files downloaded from the internet) ---
powershell -ExecutionPolicy Bypass -Command ^
    "Get-ChildItem '%MANAGED_PATH%\*.dll' | Unblock-File -ErrorAction SilentlyContinue"

:: --- Patch Assembly DLL ---
echo Patching %ASSEMBLY_DLL%...

set "CECIL_PATH=%MANAGED_PATH%\Mono.Cecil.dll"
set "PATCHER_PATH=%MOD_DIR%\BootstrapPatcher.cs"

powershell -ExecutionPolicy Bypass -Command ^
    "Add-Type -Path '%CECIL_PATH%'; " ^
    "$code = Get-Content '%PATCHER_PATH%' -Raw; " ^
    "$cp = New-Object System.CodeDom.Compiler.CompilerParameters; " ^
    "$cp.ReferencedAssemblies.Add('%CECIL_PATH%'); " ^
    "$cp.ReferencedAssemblies.Add('System.dll'); " ^
    "$cp.ReferencedAssemblies.Add('System.Core.dll'); " ^
    "$cp.CompilerOptions = '/nowarn:1668 /warn:0'; " ^
    "$cp.TreatWarningsAsErrors = $false; " ^
    "Add-Type -TypeDefinition $code -CompilerParameters $cp; " ^
    "if (-not [BootstrapPatcher]::PatchAssembly('%ASSEMBLY_PATH%')) { exit 1 }"

if errorlevel 1 (
    echo.
    echo ERROR: Patching failed.
    echo Try verifying game files through Steam and running the installer again.
    echo.
    exit /b 1
)

echo.
echo ========================================
echo   Installation Complete!
echo ========================================
echo.
echo %MOD_DISPLAY_NAME% has been installed to:
echo   %MANAGED_PATH%
echo.
echo Next steps:
echo   1. Configure OpenTrack to output UDP to 127.0.0.1:4242
echo   2. Start OpenTrack and enable tracking
echo   3. Launch %GAME_DISPLAY_NAME%
echo.
if defined MOD_CONTROLS (
    echo !MOD_CONTROLS!
    echo.
)
exit /b 0

:: ============================================
:: Find game in Steam libraries
:: ============================================
:find_steam_game
set "STEAM_PATH="

:: Get Steam install path from registry (64-bit)
for /f "tokens=2*" %%a in ('reg query "HKLM\SOFTWARE\WOW6432Node\Valve\Steam" /v InstallPath 2^>nul') do set "STEAM_PATH=%%b"

:: Try 32-bit registry
if not defined STEAM_PATH (
    for /f "tokens=2*" %%a in ('reg query "HKLM\SOFTWARE\Valve\Steam" /v InstallPath 2^>nul') do set "STEAM_PATH=%%b"
)

:: Check default Steam library
if defined STEAM_PATH (
    if exist "%STEAM_PATH%\steamapps\common\%STEAM_FOLDER_NAME%\%GAME_EXE%" (
        set "GAME_PATH=%STEAM_PATH%\steamapps\common\%STEAM_FOLDER_NAME%"
        exit /b 0
    )
)

:: Parse libraryfolders.vdf for additional Steam library paths
if defined STEAM_PATH (
    set "VDF_FILE=%STEAM_PATH%\steamapps\libraryfolders.vdf"
    if exist "!VDF_FILE!" (
        for /f "tokens=1,2 delims=	 " %%a in ('findstr /c:"\"path\"" "!VDF_FILE!" 2^>nul') do (
            set "_LIB_PATH=%%~b"
            set "_LIB_PATH=!_LIB_PATH:\\=\!"
            if exist "!_LIB_PATH!\steamapps\common\%STEAM_FOLDER_NAME%\%GAME_EXE%" (
                set "GAME_PATH=!_LIB_PATH!\steamapps\common\%STEAM_FOLDER_NAME%"
                exit /b 0
            )
        )
    )
)

exit /b 1

:: ============================================
:: Find game in GOG registry
:: ============================================
:find_gog_game
if not defined GOG_IDS exit /b 1
for %%g in (%GOG_IDS%) do (
    for /f "tokens=2*" %%a in ('reg query "HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\%%g" /v path 2^>nul') do (
        if exist "%%b\%GAME_EXE%" ( set "GAME_PATH=%%b" & exit /b 0 )
    )
    for /f "tokens=2*" %%a in ('reg query "HKLM\SOFTWARE\GOG.com\Games\%%g" /v path 2^>nul') do (
        if exist "%%b\%GAME_EXE%" ( set "GAME_PATH=%%b" & exit /b 0 )
    )
)
exit /b 1

:: ============================================
:: Find game in Epic Games manifests
:: ============================================
:find_epic_game
set "_EPIC_MANIFESTS=%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests"
if not exist "%_EPIC_MANIFESTS%" exit /b 1
for %%m in ("%_EPIC_MANIFESTS%\*.item") do (
    for /f "usebackq delims=" %%l in ("%%m") do (
        set "_EL=%%l"
        if not "!_EL:InstallLocation=!"=="!_EL!" (
            set "_EL=!_EL:*InstallLocation=!"
            set "_EL=!_EL:~4!"
            set "_EL=!_EL:~0,-2!"
            set "_EL=!_EL:\\=\!"
            if exist "!_EL!\%GAME_EXE%" ( set "GAME_PATH=!_EL!" & exit /b 0 )
        )
    )
)
exit /b 1

:: ============================================
:: Find game by scanning common directories
:: ============================================
:find_game_in_dirs
if not defined SEARCH_DIRS exit /b 1
for %%d in (%SEARCH_DIRS%) do (
    if exist "%%~d\%GAME_EXE%" ( set "GAME_PATH=%%~d" & exit /b 0 )
    for /f "delims=" %%p in ('dir /b /ad "%%~d" 2^>nul') do (
        if exist "%%~d\%%p\%GAME_EXE%" ( set "GAME_PATH=%%~d\%%p" & exit /b 0 )
        for /f "delims=" %%s in ('dir /b /ad "%%~d\%%p" 2^>nul') do (
            if exist "%%~d\%%p\%%s\%GAME_EXE%" ( set "GAME_PATH=%%~d\%%p\%%s" & exit /b 0 )
        )
    )
)
exit /b 1

@echo off
setlocal enabledelayedexpansion

rem ---------------------------------------------------------------------------
rem  ONI multiplayer mod - test PC installer
rem
rem  Put this file next to the ONI_Together_dev folder and run it.
rem
rem  ASCII only on purpose. cmd.exe parses a batch file line by line using the
rem  console code page, so non-ASCII text here gets mangled into commands on
rem  machines whose code page differs - and "chcp" partway down the file is
rem  already too late to help.
rem ---------------------------------------------------------------------------

echo.
echo ===============================================================
echo   ONI Together - install mod on this test PC
echo ===============================================================
echo.

set "SRC=%~dp0ONI_Together_dev"

if not exist "%SRC%\ONI_Together.dll" (
    echo [FAIL] ONI_Together_dev was not found next to this file.
    echo.
    echo        Copy the whole folder, not just this .bat
    echo        Looked in: %~dp0
    goto :fail
)

rem --- the game holds the dll open, so a copy would half-succeed ---
tasklist /fi "imagename eq OxygenNotIncluded.exe" 2>nul | find /i "OxygenNotIncluded.exe" >nul
if not errorlevel 1 (
    echo [FAIL] Oxygen Not Included is running.
    echo        Close the game completely, then run this again.
    goto :fail
)

rem --- read the real Documents path; OneDrive redirection makes the
rem     usual %%USERPROFILE%%\Documents guess wrong on many machines ---
set "DOCS="
for /f "tokens=2,*" %%A in ('reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders" /v Personal 2^>nul') do set "DOCS=%%B"
call set "DOCS=%DOCS%"

if not defined DOCS set "DOCS=%USERPROFILE%\Documents"
if not exist "%DOCS%" set "DOCS=%USERPROFILE%\Documents"
if not exist "%DOCS%" set "DOCS=%USERPROFILE%\OneDrive\Documents"

set "KLEI=%DOCS%\Klei\OxygenNotIncluded"

if not exist "%KLEI%" (
    echo [FAIL] ONI user folder not found:
    echo        %KLEI%
    echo.
    echo        Launch Oxygen Not Included once, reach the main menu,
    echo        quit, then run this again.
    goto :fail
)

set "DEST=%KLEI%\mods\dev\ONI_Together_dev"

echo   from : %SRC%
echo   to   : %DEST%
echo.

if not exist "%KLEI%\mods\dev" mkdir "%KLEI%\mods\dev" >nul 2>&1

rem --- /MIR also deletes leftovers from an older build; stale files there
rem     cause failures that look like anything but a stale file ---
robocopy "%SRC%" "%DEST%" /MIR /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
    echo [FAIL] Copy failed. robocopy exit code: %errorlevel%
    goto :fail
)

if not exist "%DEST%\ONI_Together.dll" (
    echo [FAIL] Copy finished but the dll is missing at the destination.
    goto :fail
)

echo   [OK] Mod installed.
echo.

set "HASH="
for /f "skip=1 tokens=*" %%H in ('certutil -hashfile "%DEST%\ONI_Together.dll" SHA256 2^>nul') do (
    if not defined HASH set "HASH=%%H"
)

echo ---------------------------------------------------------------
echo   Build check - SHA256 of ONI_Together.dll
echo   This MUST match the host PC, or the two builds differ.
echo ---------------------------------------------------------------
echo   !HASH!
echo.
echo ---------------------------------------------------------------
echo   Next steps
echo ---------------------------------------------------------------
echo   1. Launch Oxygen Not Included
echo   2. Main menu - MODS - enable "Oxygen Not Included Together"
echo   3. Restart when asked
echo   4. Disable every other mod
echo   5. Compare the SHA256 above with the host PC
echo.
echo ===============================================================
echo   Done
echo ===============================================================
echo.
pause
exit /b 0

:fail
echo.
echo ===============================================================
echo   Install did not complete
echo ===============================================================
echo.
pause
exit /b 1

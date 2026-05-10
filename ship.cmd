@echo off
REM ============================================================
REM ship.cmd
REM "I made changes, ship them" — commits everything currently
REM modified/staged, then tags and pushes a new version.
REM
REM Usage:
REM    ship.cmd 0.2.2 "fix Critical Hit calculation rounding"
REM    ship.cmd 0.3.0 "add Etro/XIVGear URL parsing"
REM
REM This is the one-stop release button. Use it when you've
REM made changes and want to ship in one command.
REM
REM If you've already committed and just want to tag, use
REM release.cmd instead.
REM ============================================================

setlocal enabledelayedexpansion

set "VERSION=%~1"
set "MESSAGE=%~2"

if "%VERSION%"=="" (
    echo Usage: ship.cmd VERSION "commit/release message"
    echo Example: ship.cmd 0.2.2 "fix Crit rounding"
    exit /b 1
)

if "%MESSAGE%"=="" (
    echo ERROR: Commit message required.
    echo Example: ship.cmd 0.2.2 "fix Crit rounding"
    exit /b 1
)

REM ---- validate version format X.Y.Z ----
echo %VERSION% | findstr /r /c:"^[0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*$" >nul
if errorlevel 1 (
    echo ERROR: Version must be in X.Y.Z format ^(e.g. 0.2.2^)
    exit /b 1
)

set "TAG=v%VERSION%"

echo.
echo ===== GearGoblin Ship: %TAG% =====
echo.

REM ---- sanity ----
if not exist "GearGoblin.csproj" (
    echo ERROR: not in project root.
    exit /b 1
)

REM ---- show what's about to be committed ----
echo === Changes to ship ===
git status --short
echo.

REM ---- bail if nothing to commit AND no commits ahead of remote ----
git diff --quiet HEAD
set "HAS_UNCOMMITTED=%errorlevel%"
git diff --quiet --cached HEAD
set "HAS_STAGED=%errorlevel%"

if "%HAS_UNCOMMITTED%"=="0" if "%HAS_STAGED%"=="0" (
    echo Nothing to commit. If you just want to tag existing commits, use release.cmd instead.
    exit /b 1
)

set /p CONFIRM=Commit these changes and tag as %TAG%? (y/n): 
if /i not "!CONFIRM!"=="y" (
    echo Aborted.
    exit /b 0
)

REM ---- check tag doesn't exist ----
git rev-parse --verify "%TAG%" >nul 2>&1
if not errorlevel 1 (
    echo ERROR: Tag %TAG% already exists locally.
    exit /b 1
)

git ls-remote --tags origin "refs/tags/%TAG%" 2>nul | findstr "%TAG%" >nul
if not errorlevel 1 (
    echo ERROR: Tag %TAG% already exists on origin.
    exit /b 1
)

REM ---- stage everything ----
git add -A
if errorlevel 1 ( echo ERROR: git add failed & exit /b 1 )

REM ---- commit ----
git commit -m "%MESSAGE%"
if errorlevel 1 (
    echo ERROR: git commit failed.
    exit /b 1
)

REM ---- push branch ----
git push
if errorlevel 1 ( echo ERROR: git push failed & exit /b 1 )

REM ---- tag ----
git tag -a %TAG% -m "%MESSAGE%"
if errorlevel 1 ( echo ERROR: git tag failed & exit /b 1 )

REM ---- push tag ----
git push origin %TAG%
if errorlevel 1 (
    echo ERROR: tag push failed. Removing local tag for retry:
    git tag --delete %TAG%
    exit /b 1
)

echo.
echo ============================================
echo SHIPPED %TAG%
echo.
echo Watch CI:
echo   https://github.com/LastOnionKnight/GearGoblin/actions
echo.
echo Once green, install/update via:
echo   /xlplugins ^> search GearGoblin ^> Update
echo ============================================
echo.

endlocal

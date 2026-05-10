@echo off
REM ============================================================
REM release.cmd
REM Reusable GearGoblin release script.
REM
REM Usage:
REM    release.cmd 0.2.1
REM    release.cmd 0.2.1 "Materia advisor + icons"
REM
REM What it does:
REM    1. Verifies you're in the GearGoblin project root
REM    2. Verifies your working tree is clean (no uncommitted changes)
REM    3. Verifies the tag doesn't already exist
REM    4. Verifies the version follows X.Y.Z format
REM    5. Asks for confirmation
REM    6. Tags and pushes — that's it. CI does the rest.
REM
REM Prerequisites:
REM    - You've already committed all your code changes for this version.
REM    - This script just *tags* the existing main branch.
REM    - To make code changes, commit them first, then run this.
REM ============================================================

setlocal enabledelayedexpansion

REM ---- arg parsing ----
set "VERSION=%~1"
set "RELEASE_NOTES=%~2"

if "%VERSION%"=="" (
    echo Usage: release.cmd VERSION ["release notes"]
    echo Example: release.cmd 0.2.1
    echo Example: release.cmd 0.3.0 "Etro URL parsing"
    exit /b 1
)

REM ---- validate version format X.Y.Z ----
echo %VERSION% | findstr /r /c:"^[0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*$" >nul
if errorlevel 1 (
    echo ERROR: Version must be in X.Y.Z format (e.g. 0.2.1, not v0.2.1, not 0.2.1.0)
    exit /b 1
)

set "TAG=v%VERSION%"

echo.
echo ===== GearGoblin Release: %TAG% =====
echo.

REM ---- sanity check we're in the right place ----
if not exist "GearGoblin.csproj" (
    echo ERROR: GearGoblin.csproj not found.
    echo Run this from the GearGoblin project root.
    exit /b 1
)

if not exist "repo.json" (
    echo ERROR: repo.json not found. Are you in the right folder?
    exit /b 1
)

REM ---- check working tree is clean ----
for /f %%i in ('git status --porcelain') do (
    echo ERROR: You have uncommitted changes. Commit or stash before tagging:
    echo.
    git status --short
    exit /b 1
)

REM ---- check we're on main ----
for /f %%b in ('git rev-parse --abbrev-ref HEAD') do set BRANCH=%%b
if not "!BRANCH!"=="main" (
    echo WARNING: You are on branch '!BRANCH!', not main.
    set /p CONTINUE_BRANCH=Continue anyway? (y/n): 
    if /i not "!CONTINUE_BRANCH!"=="y" exit /b 1
)

REM ---- check tag doesn't already exist locally ----
git rev-parse --verify "%TAG%" >nul 2>&1
if not errorlevel 1 (
    echo ERROR: Tag %TAG% already exists locally.
    echo To redo it: git tag --delete %TAG%
    exit /b 1
)

REM ---- check tag doesn't already exist on remote ----
git ls-remote --tags origin "refs/tags/%TAG%" 2>nul | findstr "%TAG%" >nul
if not errorlevel 1 (
    echo ERROR: Tag %TAG% already exists on origin (GitHub).
    echo You cannot reuse a published tag.
    exit /b 1
)

REM ---- pull latest main so we don't tag stale code ----
echo Pulling latest from origin/main...
git pull origin main --ff-only
if errorlevel 1 (
    echo ERROR: git pull failed. Resolve before tagging.
    exit /b 1
)

REM ---- show what we're about to tag ----
echo.
echo === Most recent commit (will be tagged as %TAG%) ===
git log -1 --oneline
echo.

set /p CONFIRM=Tag and push %TAG%? (y/n): 
if /i not "!CONFIRM!"=="y" (
    echo Aborted.
    exit /b 0
)

REM ---- tag ----
if "%RELEASE_NOTES%"=="" (
    git tag %TAG%
) else (
    git tag -a %TAG% -m "%RELEASE_NOTES%"
)
if errorlevel 1 ( echo ERROR: git tag failed & exit /b 1 )

REM ---- push tag ----
git push origin %TAG%
if errorlevel 1 (
    echo ERROR: tag push failed.
    echo Removing local tag so you can retry:
    git tag --delete %TAG%
    exit /b 1
)

echo.
echo ============================================
echo SUCCESS. %TAG% pushed to origin.
echo.
echo CI will now:
echo   1. Build with version=%VERSION%
echo   2. Create GitHub Release %TAG% with latest.zip
echo   3. Bump repo.json AssemblyVersion to %VERSION%.0
echo   4. Update DownloadLink* URLs to point at %TAG%
echo   5. Commit repo.json back to main
echo.
echo Watch progress:
echo   https://github.com/LastOnionKnight/GearGoblin/actions
echo.
echo Verify when green:
echo   https://github.com/LastOnionKnight/GearGoblin/releases/tag/%TAG%
echo ============================================
echo.

endlocal

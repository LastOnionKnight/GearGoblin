@echo off
REM ============================================================
REM fix-v0.2.1.cmd
REM One-shot fix for the v0.2.0 build failure.
REM Removes <IconUrl> and <ImageUrls> from csproj (they don't
REM belong there), commits, tags v0.2.1, pushes.
REM
REM Run from the GearGoblin project root:
REM    D:\GearGoblin-v0.1\GearGoblin> fix-v0.2.1.cmd
REM ============================================================

setlocal enabledelayedexpansion

echo.
echo ===== GearGoblin v0.2.1 fix =====
echo.

REM --- sanity check we're in the right place ---
if not exist "GearGoblin.csproj" (
    echo ERROR: GearGoblin.csproj not found in current directory.
    echo Run this from D:\GearGoblin-v0.1\GearGoblin\
    exit /b 1
)

REM --- back up csproj before editing ---
copy /y GearGoblin.csproj GearGoblin.csproj.bak >nul
echo Backed up csproj to GearGoblin.csproj.bak

REM --- strip the bad elements with PowerShell ---
REM This removes any <IconUrl>...</IconUrl> and <ImageUrls>...</ImageUrls>
REM blocks from the csproj. Multi-line tolerant.
echo Stripping invalid IconUrl/ImageUrls from csproj...
powershell -NoProfile -Command ^
    "$c = Get-Content -Raw 'GearGoblin.csproj';" ^
    "$c = [regex]::Replace($c, '\s*<IconUrl>[^<]*</IconUrl>', '');" ^
    "$c = [regex]::Replace($c, '\s*<ImageUrls>[\s\S]*?</ImageUrls>', '');" ^
    "Set-Content -Path 'GearGoblin.csproj' -Value $c -NoNewline"

if errorlevel 1 (
    echo ERROR: PowerShell strip failed. Restoring backup.
    copy /y GearGoblin.csproj.bak GearGoblin.csproj >nul
    exit /b 1
)

REM --- show diff so user sees what changed ---
echo.
echo === Changes to csproj ===
git diff --stat GearGoblin.csproj
echo.

REM --- stage and commit ---
git add GearGoblin.csproj
if errorlevel 1 ( echo ERROR: git add failed & exit /b 1 )

git status --short
echo.

set /p CONFIRM=Commit and tag v0.2.1? (y/n): 
if /i not "!CONFIRM!"=="y" (
    echo Aborted. Backup at GearGoblin.csproj.bak — restore manually if needed.
    exit /b 0
)

git commit -m "Fix CI: remove invalid IconUrl/ImageUrls from csproj (they live in repo.json)"
if errorlevel 1 ( echo ERROR: git commit failed & exit /b 1 )

git push
if errorlevel 1 ( echo ERROR: git push failed & exit /b 1 )

git tag v0.2.1
if errorlevel 1 ( echo ERROR: git tag failed & exit /b 1 )

git push origin v0.2.1
if errorlevel 1 ( echo ERROR: tag push failed & exit /b 1 )

echo.
echo ============================================
echo SUCCESS. v0.2.1 tagged and pushed.
echo Watch CI at:
echo   https://github.com/LastOnionKnight/GearGoblin/actions
echo ============================================
echo.
del GearGoblin.csproj.bak >nul 2>&1

endlocal

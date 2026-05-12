# release.ps1 - Unified release script for GearGoblin and Tonberry Tactics
# ASCII only (PowerShell 5.1 chokes on UTF-8 BOM).
#
# What it does:
#   1. Finds the .csproj file in the current directory.
#   2. Reads <Version>X.Y.Z</Version> from it.
#   3. Stages all currently-modified tracked files.
#   4. Pulls the CHANGELOG entry for this version (if CHANGELOG.md exists).
#   5. Commits with that message.
#   6. Tags vX.Y.Z.
#   7. Pushes commits and tag to origin/main with --follow-tags.
#
# Same script works in:
#   D:\GearGoblin-v0.1\GearGoblin\
#   D:\TonberryTactics-workspace\TonberryTactics\
# Just drop it next to the .csproj and run .\release.ps1
#
# Flags:
#   -DryRun       Show what would happen, do not commit/tag/push.
#   -Message msg  Override the auto-generated commit message.
#   -SkipPush     Commit and tag locally, do not push.

param(
    [switch]$DryRun,
    [string]$Message = "",
    [switch]$SkipPush
)

$ErrorActionPreference = "Stop"

# ---- 1. Find the csproj ------------------------------------------------------

$csprojFiles = Get-ChildItem -Path . -Filter "*.csproj" -File
if ($csprojFiles.Count -eq 0) {
    Write-Host "ERROR: No .csproj file found in $(Get-Location)." -ForegroundColor Red
    Write-Host "Run this script from the project root." -ForegroundColor Yellow
    exit 1
}
if ($csprojFiles.Count -gt 1) {
    Write-Host "ERROR: Multiple .csproj files found. Cannot auto-detect project." -ForegroundColor Red
    $csprojFiles | ForEach-Object { Write-Host "  - $($_.Name)" }
    exit 1
}

$csproj      = $csprojFiles[0]
$projectName = [System.IO.Path]::GetFileNameWithoutExtension($csproj.Name)
Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  release.ps1" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Project:  $projectName" -ForegroundColor White
Write-Host "  csproj:   $($csproj.Name)" -ForegroundColor White
Write-Host "  cwd:      $(Get-Location)" -ForegroundColor White

# ---- 2. Read the version from csproj -----------------------------------------

$csprojContent = Get-Content $csproj.FullName -Raw
if ($csprojContent -match "<Version>([\d\.]+)</Version>") {
    $version = $Matches[1]
} else {
    Write-Host "ERROR: Could not find <Version>X.Y.Z</Version> in $($csproj.Name)." -ForegroundColor Red
    exit 1
}

# Normalize version: tag is vX.Y.Z (no trailing .0).
# Versions like 0.4.2.0 -> 0.4.2 for the tag.
$tagVersion = $version -replace '\.0+$', ''
if ($tagVersion -notmatch '^\d+\.\d+\.\d+') {
    $tagVersion = $version  # fall back to raw if normalization confuses things
}
$tag = "v$tagVersion"
Write-Host "  Version:  $version" -ForegroundColor White
Write-Host "  Tag:      $tag" -ForegroundColor White
Write-Host ""

# ---- 3. Check git state ------------------------------------------------------

# Are we in a git repo?
git rev-parse --is-inside-work-tree 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Not inside a git repository." -ForegroundColor Red
    exit 1
}

# Does the tag already exist?
$existingTag = git tag --list $tag
if ($existingTag) {
    Write-Host "ERROR: Tag $tag already exists. Bump the version in $($csproj.Name) first." -ForegroundColor Red
    exit 1
}

# What's the current branch?
$branch = git rev-parse --abbrev-ref HEAD
Write-Host "  Branch:   $branch" -ForegroundColor White

# Show what's staged + unstaged.
$status = git status --short
if (-not $status) {
    Write-Host ""
    Write-Host "WARNING: No changes detected. Nothing to commit." -ForegroundColor Yellow
    Write-Host "Did you forget to copy the dropin files into place?" -ForegroundColor Yellow
    if (-not $DryRun) { exit 1 }
}

Write-Host ""
Write-Host "Changes to be committed:" -ForegroundColor Yellow
git status --short
Write-Host ""

# ---- 4. Generate commit message from CHANGELOG (if it exists) ----------------

if (-not $Message) {
    $changelogPath = Join-Path (Get-Location) "CHANGELOG.md"
    if (Test-Path $changelogPath) {
        $changelog = Get-Content $changelogPath -Raw
        # Match "## [X.Y.Z] - YYYY-MM-DD" through to the next "## [" or EOF.
        # Use [\s\S] for cross-line matching.
        $pattern = "## \[$([regex]::Escape($tagVersion))\][\s\S]*?(?=## \[|\z)"
        if ($changelog -match $pattern) {
            $entry = $Matches[0].Trim()
            # First line: "## [X.Y.Z] - 2026-05-11"
            # Use that as the subject. Strip the brackets.
            $firstLine = ($entry -split "`n")[0].Trim()
            $subject = $firstLine -replace '^## \[(.+?)\]\s*[-]+\s*(.+)$', "$projectName $1 - $2"
            if ($subject -eq $firstLine) {
                $subject = "$projectName $tagVersion"
            }
            # Body: everything after the subject line, trimmed.
            $body = ($entry -split "`n", 2)[1].Trim()
            $Message = "$subject`n`n$body"
        } else {
            Write-Host "WARNING: No CHANGELOG entry found for version $tagVersion. Using fallback message." -ForegroundColor Yellow
            $Message = "$projectName $tagVersion"
        }
    } else {
        $Message = "$projectName $tagVersion"
    }
}

Write-Host "Commit message preview:" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor DarkGray
Write-Host $Message
Write-Host "----------------------------------------" -ForegroundColor DarkGray
Write-Host ""

# ---- 5. Dry-run early exit ---------------------------------------------------

if ($DryRun) {
    Write-Host "DRY RUN - would now:" -ForegroundColor Cyan
    Write-Host "  git add -A"
    Write-Host "  git commit -m <message above>"
    Write-Host "  git tag $tag"
    if (-not $SkipPush) {
        Write-Host "  git push origin $branch --follow-tags"
    }
    Write-Host ""
    Write-Host "Done (dry run). No changes made." -ForegroundColor Green
    exit 0
}

# ---- 6. Commit + tag + push --------------------------------------------------

Write-Host "Staging all changes..." -ForegroundColor Cyan
git add -A
if ($LASTEXITCODE -ne 0) { Write-Host "git add failed" -ForegroundColor Red; exit 1 }

Write-Host "Committing..." -ForegroundColor Cyan
# Write commit message to a temp file so newlines survive PowerShell's
# argument-parsing layer.
$msgFile = [System.IO.Path]::GetTempFileName()
Set-Content -Path $msgFile -Value $Message -Encoding UTF8
git commit -F $msgFile
$commitExit = $LASTEXITCODE
Remove-Item $msgFile -Force
if ($commitExit -ne 0) {
    Write-Host "git commit failed" -ForegroundColor Red
    exit 1
}

Write-Host "Tagging $tag..." -ForegroundColor Cyan
git tag -a $tag -m "$projectName $tagVersion"
if ($LASTEXITCODE -ne 0) { Write-Host "git tag failed" -ForegroundColor Red; exit 1 }

if ($SkipPush) {
    Write-Host ""
    Write-Host "Local commit + tag complete. Push skipped (-SkipPush)." -ForegroundColor Green
    Write-Host "To push later: git push origin $branch --follow-tags" -ForegroundColor White
    exit 0
}

Write-Host "Pushing to origin/$branch with tags..." -ForegroundColor Cyan
git push origin $branch --follow-tags
if ($LASTEXITCODE -ne 0) {
    Write-Host "git push failed" -ForegroundColor Red
    Write-Host "Local commit + tag are saved. Retry with: git push origin $branch --follow-tags" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "==================================================" -ForegroundColor Green
Write-Host "  Release complete: $projectName $tag" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green

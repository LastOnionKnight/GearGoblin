$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$mainWindowPath = Join-Path $repoRoot "UI\MainWindow.cs"
$chromePath = Join-Path $repoRoot "Theme\TtChrome.cs"

$mainWindow = Get-Content -Raw $mainWindowPath
$chrome = Get-Content -Raw $chromePath

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$transparentContentHostPattern =
    'ImGui\.BeginChild\(\s*"##content"\s*,\s*new Vector2\(0,\s*-36f\)\s*,\s*false\s*,\s*ImGuiWindowFlags\.NoBackground\s*\)'

$paintedContentHostPattern =
    'ImGui\.BeginChild\(\s*"##content"\s*,\s*new Vector2\(0,\s*-36f\)\s*,\s*false\s*,\s*ImGuiWindowFlags\.None\s*\)'

Assert-True `
    (-not [regex]::IsMatch($mainWindow, $transparentContentHostPattern)) `
    "Regression: the main tab content child is transparent, so short tabs can show the game world behind the window."

Assert-True `
    ([regex]::IsMatch($mainWindow, $paintedContentHostPattern)) `
    "Expected the main tab content child to explicitly draw its themed background for the full remaining height."

Assert-True `
    ([regex]::IsMatch($chrome, 'ImGuiCol\.ChildBg,\s+Sink')) `
    "Expected TtChrome to provide a non-transparent child background for the content host."

Assert-True `
    (-not [regex]::IsMatch($chrome, 'AlwaysAutoResize')) `
    "Regression: AlwaysAutoResize must stay out of TtChrome panels so the prior width fix remains intact."

$transparentHeaderHostPattern =
    'ImGui\.PushStyleColor\(ImGuiCol\.ChildBg,\s*Theme\.TtChrome\.Rgba\(\d+,\s*\d+,\s*\d+,\s*0\.5f\)\)'

$paintedHeaderHostPattern =
    'ImGui\.PushStyleColor\(ImGuiCol\.ChildBg,\s*Theme\.TtChrome\.Sink\)'

Assert-True `
    (-not [regex]::IsMatch($mainWindow, $transparentHeaderHostPattern)) `
    "Regression: the header region (##identity) background has transparency, allowing the game world to bleed through."

Assert-True `
    ([regex]::IsMatch($mainWindow, $paintedHeaderHostPattern)) `
    "Expected the header region (##identity) to explicitly draw the opaque Sink background."

Write-Host "UI render regression checks passed."

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $PSScriptRoot "Build-GameHubWidget.ps1"
$assetScript = Join-Path $PSScriptRoot "Export-GameHubWidgetAssets.ps1"
$outputRoot = Join-Path $repoRoot "src\GameHub.Widget\bin\$Configuration\net472"
$distRoot = Join-Path $repoRoot "dist"
$stagingRoot = Join-Path $distRoot "GameHub.Widget-$Version"
$widgetFolderName = "8A9E2A7E-6B91-4D93-8C87-03B93BC0A6B7"
$widgetRoot = Join-Path $stagingRoot $widgetFolderName
$zipPath = Join-Path $distRoot "GameHub.Widget-$Version.zip"

& powershell -ExecutionPolicy Bypass -File $buildScript -Configuration $Configuration
& powershell -ExecutionPolicy Bypass -File $assetScript -OutputRoot $outputRoot

if (Test-Path $stagingRoot) {
    Remove-Item -Path $stagingRoot -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

New-Item -ItemType Directory -Path $widgetRoot -Force | Out-Null

$filesToCopy = @(
    "8A9E2A7E-6B91-4D93-8C87-03B93BC0A6B7.dll",
    "8A9E2A7E-6B91-4D93-8C87-03B93BC0A6B7.pdb",
    "GameHub.Core.dll",
    "GameHub.Core.pdb",
    "preview_5x4.png",
    "thumb.png"
)

foreach ($fileName in $filesToCopy) {
    $sourcePath = Join-Path $outputRoot $fileName
    if (-not (Test-Path $sourcePath)) {
        throw "Expected build artifact '$sourcePath' was not found."
    }

    Copy-Item -Path $sourcePath -Destination (Join-Path $widgetRoot $fileName) -Force
}

$installNotes = @"
Game Hub Widget v$Version

Folder:
- 8A9E2A7E-6B91-4D93-8C87-03B93BC0A6B7

Files inside that folder:
- 8A9E2A7E-6B91-4D93-8C87-03B93BC0A6B7.dll
- 8A9E2A7E-6B91-4D93-8C87-03B93BC0A6B7.pdb
- GameHub.Core.dll
- GameHub.Core.pdb
- preview_5x4.png
- thumb.png

Install:
1. Close WigiDash Manager before replacing widget files.
2. Copy the folder `8A9E2A7E-6B91-4D93-8C87-03B93BC0A6B7` into `%AppData%\G.SKILL\WigiDashManager\Widgets\`.
3. Reopen WigiDash Manager and add the Game Hub widget.

Notes:
- This widget targets WigiDash on Windows and depends on the WigiDash widget framework provided by the installed manager.
- Widget state is stored per widget instance under %%AppData%%\G.SKILL\WigiDashManager\Widgets\GameHub Config\.
- Supported launcher scanning includes Steam, Epic, EA, GOG Galaxy, Battle.net, Xbox / Game Pass, and Ubisoft Connect.
- Battle.net titles may open the Battle.net client first, matching Blizzard's own shortcut behavior for tested games.
"@

Set-Content -Path (Join-Path $stagingRoot "INSTALL.txt") -Value $installNotes -Encoding ASCII

if (-not (Test-Path $distRoot)) {
    New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
}

Compress-Archive -Path (Join-Path $stagingRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Created widget package: $zipPath"

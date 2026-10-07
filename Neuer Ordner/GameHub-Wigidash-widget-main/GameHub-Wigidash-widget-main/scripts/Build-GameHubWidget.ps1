param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"

if (-not (Test-Path $vswherePath)) {
    throw "vswhere.exe was not found. Install Visual Studio Build Tools with MSBuild support."
}

$installPath = & $vswherePath -latest -products * -requires Microsoft.Component.MSBuild -property installationPath

if (-not $installPath) {
    throw "No Visual Studio Build Tools installation with MSBuild was found."
}

$msbuildPath = Join-Path $installPath "MSBuild\Current\Bin\MSBuild.exe"

if (-not (Test-Path $msbuildPath)) {
    throw "MSBuild.exe was not found at '$msbuildPath'."
}

$projectPath = Join-Path $repoRoot "src\GameHub.Widget\GameHub.Widget.csproj"

Write-Host "Using MSBuild: $msbuildPath"
Write-Host "Building widget project: $projectPath"

& $msbuildPath $projectPath /t:Rebuild /p:Configuration=$Configuration /v:m

if ($LASTEXITCODE -ne 0) {
    throw "Widget build failed with exit code $LASTEXITCODE."
}

Write-Host "Widget build completed successfully."

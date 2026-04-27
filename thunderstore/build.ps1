param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $projectRoot "bin\$Configuration"
$manifestPath = Join-Path $projectRoot "manifest.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$packageName = $manifest.name
$version = $manifest.version_number
$dllName = "Thanks.Fog&ColdControl.dll"
$dllPath = Join-Path $outputRoot $dllName
$packageRoot = Join-Path $outputRoot "$packageName-package"
$pluginsRoot = Join-Path $packageRoot "BepInEx\plugins"
$packagePath = Join-Path $outputRoot "$packageName-$version.zip"

if (-not (Test-Path $dllPath)) {
    throw "Missing build output: $dllPath"
}

New-Item -ItemType Directory -Path $pluginsRoot -Force | Out-Null

Copy-Item $dllPath -Destination (Join-Path $pluginsRoot $dllName) -Force
Copy-Item (Join-Path $projectRoot "README.md") -Destination (Join-Path $packageRoot "README.md") -Force
Copy-Item (Join-Path $projectRoot "CHANGELOG.md") -Destination (Join-Path $packageRoot "CHANGELOG.md") -Force
Copy-Item (Join-Path $projectRoot "manifest.json") -Destination (Join-Path $packageRoot "manifest.json") -Force
Copy-Item (Join-Path $projectRoot "icon.png") -Destination (Join-Path $packageRoot "icon.png") -Force

Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $packagePath -Force

Write-Host "Package created:"
Write-Host $packagePath

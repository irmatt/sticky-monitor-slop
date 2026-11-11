$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$distApp = Join-Path $root 'dist\app'
$packagesRoot = Join-Path $root 'packages'
$portableRoot = Join-Path $packagesRoot 'StickyMonitor-Portable'
$installerRoot = Join-Path $packagesRoot 'StickyMonitor-Installer'
$portableZip = Join-Path $packagesRoot 'StickyMonitor-Portable.zip'

if (-not (Test-Path $distApp)) {
    throw "Build output not found at $distApp. Run npm run build first."
}

if (Test-Path $portableRoot) {
    Remove-Item $portableRoot -Recurse -Force
}

if (Test-Path $installerRoot) {
    Remove-Item $installerRoot -Recurse -Force
}

if (Test-Path $portableZip) {
    Remove-Item $portableZip -Force
}

New-Item -ItemType Directory -Path $packagesRoot -Force | Out-Null
New-Item -ItemType Directory -Path $portableRoot -Force | Out-Null
New-Item -ItemType Directory -Path $installerRoot -Force | Out-Null

Copy-Item -Path (Join-Path $distApp '*') -Destination $portableRoot -Recurse -Force
Copy-Item -Path (Join-Path $distApp '*') -Destination $installerRoot -Recurse -Force
Copy-Item -Path (Join-Path $root 'assets\app-icon.ico') -Destination $installerRoot -Force
Copy-Item -Path (Join-Path $root 'README.md') -Destination (Join-Path $portableRoot 'README.txt') -Force
Copy-Item -Path (Join-Path $root 'README.md') -Destination (Join-Path $installerRoot 'README.txt') -Force
Copy-Item -Path (Join-Path $root 'scripts\install-package.ps1') -Destination $installerRoot -Force
Copy-Item -Path (Join-Path $root 'scripts\uninstall-package.ps1') -Destination $installerRoot -Force

Compress-Archive -Path (Join-Path $portableRoot '*') -DestinationPath $portableZip -Force

Write-Host "Created portable folder: $portableRoot"
Write-Host "Created portable zip: $portableZip"
Write-Host "Created installer folder: $installerRoot"

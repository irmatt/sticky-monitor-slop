$ErrorActionPreference = 'Stop'

$shortcutPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Sticky Monitor.lnk'
$startupPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\Sticky Monitor.lnk'

if (Test-Path $shortcutPath) {
    Remove-Item $shortcutPath -Force
    Write-Host "Removed Start Menu shortcut."
}

if (Test-Path $startupPath) {
    Remove-Item $startupPath -Force
    Write-Host "Removed startup shortcut."
}

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $root 'dist\app\StickyMonitor.App.exe'
$iconPath = Join-Path $root 'assets\app-icon.ico'

if (-not (Test-Path $exePath)) {
    throw "Build output not found at $exePath. Run npm run build first."
}

$shell = New-Object -ComObject WScript.Shell
try {
    $startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    $shortcutPath = Join-Path $startMenuDir 'Sticky Monitor.lnk'

    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = Split-Path -Parent $exePath
    $shortcut.IconLocation = $iconPath
    $shortcut.Description = 'Sticky Monitor'
    $shortcut.Save()

    Write-Host "Created Start Menu shortcut: $shortcutPath"
}
finally {
    [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
}

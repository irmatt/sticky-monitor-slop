$ErrorActionPreference = 'Stop'

$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\StickyMonitor'
$startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shortcutPath = Join-Path $startMenuDir 'Sticky Monitor.lnk'
$exePath = Join-Path $installRoot 'StickyMonitor.App.exe'
$iconPath = Join-Path $installRoot 'app-icon.ico'

if (Test-Path $installRoot) {
    Remove-Item $installRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Copy-Item -Path (Join-Path $packageRoot '*') -Destination $installRoot -Recurse -Force

$shell = New-Object -ComObject WScript.Shell
try {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = $installRoot
    $shortcut.IconLocation = $iconPath
    $shortcut.Description = 'Sticky Monitor'
    $shortcut.Save()
}
finally {
    [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
}

Write-Host "Installed Sticky Monitor to: $installRoot"
Write-Host "Created Start Menu shortcut: $shortcutPath"
Start-Process -FilePath $exePath

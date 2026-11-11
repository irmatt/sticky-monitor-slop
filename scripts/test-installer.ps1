$ErrorActionPreference = 'Continue'
$log = 'C:\Users\irmat\Tools\sticky-fresh.log'
Remove-Item $log -Force -ErrorAction SilentlyContinue
function W($s) { $s | Out-File $log -Append -Encoding utf8 }

$appDir = 'C:\Users\irmat\AppData\Local\Programs\StickyMonitor'
$setup = 'C:\Users\irmat\Tools\sb3\packages\StickyMonitor-Setup-1.1.0.exe'
$startupLnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'Sticky Monitor.lnk'

W "=== fresh-downloader simulation $(Get-Date) ==="

# wipe the machine state a brand new user would not have
Get-Process -Name 'StickyMonitor.App' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
if (Test-Path $appDir) { Remove-Item $appDir -Recurse -Force -ErrorAction SilentlyContinue }
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{56029fb9-dd62-49fa-975e-f31a006df3ca}_is1' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $startupLnk -Force -ErrorAction SilentlyContinue
W ("wiped. app dir gone: " + (-not (Test-Path $appDir)))

# 1. install like a downloader would
$i = Start-Process $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru -Wait
W ("install exit code     : " + $i.ExitCode)
W ("installed version     : " + (Get-Item (Join-Path $appDir 'StickyMonitor.App.exe')).VersionInfo.FileVersion)
W ("start menu shortcut   : " + (Test-Path 'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Sticky Monitor.lnk'))
W ("uninstall entry       : " + (Test-Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{56029fb9-dd62-49fa-975e-f31a006df3ca}_is1'))

# 2. run it, then uninstall while it is running
Start-Process (Join-Path $appDir 'StickyMonitor.App.exe') -ArgumentList '--minimized'
Start-Sleep -Seconds 8
W ("running before uninstall: " + @(Get-Process -Name StickyMonitor.App -ErrorAction SilentlyContinue).Count)
W ("startup shortcut       : " + (Test-Path $startupLnk))

$u = Start-Process (Join-Path $appDir 'unins000.exe') -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/LOG=C:\Users\irmat\Tools\sticky-unins2.log' -PassThru -Wait
W ("uninstall exit code   : " + $u.ExitCode)
Start-Sleep -Seconds 4
W ("running after uninstall: " + @(Get-Process -Name StickyMonitor.App -ErrorAction SilentlyContinue).Count)
W ("install dir gone      : " + (-not (Test-Path $appDir)))
W ("startup shortcut gone : " + (-not (Test-Path $startupLnk)))
W ("uninstall entry gone  : " + (-not (Test-Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{56029fb9-dd62-49fa-975e-f31a006df3ca}_is1')))
W ("user settings kept    : " + (Test-Path 'C:\Users\irmat\AppData\Local\StickyMonitor\settings.json'))

# 3. reinstall so the box keeps working
$r = Start-Process $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru -Wait
W ("reinstall exit code   : " + $r.ExitCode)
Start-Process (Join-Path $appDir 'StickyMonitor.App.exe') -ArgumentList '--minimized'
Start-Sleep -Seconds 8
$procs = @(Get-Process -Name 'StickyMonitor.App' -ErrorAction SilentlyContinue)
W ("running now           : " + $procs.Count + " session=" + ($procs | Select-Object -First 1).SessionId)
W "done"

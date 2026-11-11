$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\StickyMonitor.App\StickyMonitor.App.csproj'
$distApp = Join-Path $root 'dist\app'
$iss = Join-Path $root 'installer\StickyMonitor.iss'

Write-Host 'Publishing application...'
dotnet publish $project -c Release -r win-x64 --self-contained true -o $distApp
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish failed.'
}

$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw 'Inno Setup (ISCC.exe) not found. Install it from https://jrsoftware.org/isinfo.php'
}

Write-Host 'Compiling installer...'
& $iscc $iss
if ($LASTEXITCODE -ne 0) {
    throw 'Inno Setup compilation failed.'
}

Write-Host 'Installer created in packages\.'

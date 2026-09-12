param(
    [string]$Source = "$(Join-Path $PSScriptRoot '..\artifacts\publish')"
)

$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\QuickShelf'
$startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$startupDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup'
$startMenuShortcut = Join-Path $startMenuDir 'QuickShelf.lnk'
$startupShortcut = Join-Path $startupDir 'QuickShelf.lnk'

if (-not (Test-Path $Source)) {
    throw "Publish directory not found: $Source"
}

Get-Process QuickShelf -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Get-ChildItem $installDir -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
Copy-Item (Join-Path $Source '*') $installDir -Recurse -Force

$exe = Join-Path $installDir 'QuickShelf.exe'
if (-not (Test-Path $exe)) {
    throw "QuickShelf.exe was not found after installation."
}

$shell = New-Object -ComObject WScript.Shell
foreach ($shortcutPath in @($startMenuShortcut, $startupShortcut)) {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $installDir
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Description = 'QuickShelf'
    $shortcut.Save()
}

Start-Process $exe
Write-Host "QuickShelf installed to $installDir"
Write-Host "Startup shortcut: $startupShortcut"

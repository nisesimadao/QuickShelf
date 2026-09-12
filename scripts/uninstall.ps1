$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\QuickShelf'
$startMenuShortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\QuickShelf.lnk'
$startupShortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\QuickShelf.lnk'

Get-Process QuickShelf -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 250

Remove-Item $startMenuShortcut -Force -ErrorAction SilentlyContinue
Remove-Item $startupShortcut -Force -ErrorAction SilentlyContinue
Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host 'QuickShelf uninstalled.'

$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\QuickShelf'
$startMenuShortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\QuickShelf.lnk'
$legacyStartupShortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\QuickShelf.lnk'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runValueName = 'QuickShelf'
$taskName = 'QuickShelf Autostart'

Get-Process QuickShelf -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 250

Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
Remove-ItemProperty -Path $runKey -Name $runValueName -ErrorAction SilentlyContinue
Remove-Item $startMenuShortcut -Force -ErrorAction SilentlyContinue
Remove-Item $legacyStartupShortcut -Force -ErrorAction SilentlyContinue
Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host 'QuickShelf uninstalled.'

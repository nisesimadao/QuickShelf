param(
    [string]$Source = ''
)

$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\QuickShelf'
$startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$startupDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup'
$startMenuShortcut = Join-Path $startMenuDir 'QuickShelf.lnk'
$legacyStartupShortcut = Join-Path $startupDir 'QuickShelf.lnk'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runValueName = 'QuickShelf'
$taskName = 'QuickShelf Autostart'

if ([string]::IsNullOrWhiteSpace($Source)) {
    if (Test-Path (Join-Path $PSScriptRoot 'QuickShelf.exe')) {
        $Source = $PSScriptRoot
    }
    else {
        $Source = Join-Path $PSScriptRoot '..\artifacts\publish'
    }
}

$Source = [System.IO.Path]::GetFullPath($Source)
if (-not (Test-Path $Source)) {
    throw "Publish directory not found: $Source"
}

Get-Process QuickShelf -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Get-ChildItem $installDir -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
Get-ChildItem $Source -Force | Where-Object {
    $_.Name -notin @('Install-QuickShelf.ps1', 'Uninstall-QuickShelf.ps1', 'SHA256SUMS.txt')
} | Copy-Item -Destination $installDir -Recurse -Force

$exe = Join-Path $installDir 'QuickShelf.exe'
if (-not (Test-Path $exe)) {
    throw "QuickShelf.exe was not found after installation."
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($startMenuShortcut)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = "$exe,0"
$shortcut.Description = 'QuickShelf'
$shortcut.Save()

Remove-Item $legacyStartupShortcut -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path $runKey -Name $runValueName -ErrorAction SilentlyContinue
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$action = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $installDir
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $identity
$trigger.Delay = 'PT10S'
$principal = New-ScheduledTaskPrincipal -UserId $identity -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Days 3650)
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description 'Start QuickShelf after user logon.' | Out-Null

Start-Process $exe
Write-Host "QuickShelf installed to $installDir"
Write-Host "Windows logon startup task registered: $taskName"

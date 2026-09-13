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

if ([string]::IsNullOrWhiteSpace($Source)) {
    if (Test-Path (Join-Path $PSScriptRoot 'QuickShelf.exe')) {
        # Release ZIP layout: installer sits beside QuickShelf.exe.
        $Source = $PSScriptRoot
    }
    else {
        # Repository layout.
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

# If installing from a Release ZIP, do not copy the installer/uninstaller files
# into the app directory; copy the application payload only.
Get-ChildItem $Source -Force | Where-Object {
    $_.Name -notin @('Install-QuickShelf.ps1', 'Uninstall-QuickShelf.ps1', 'SHA256SUMS.txt')
} | Copy-Item -Destination $installDir -Recurse -Force

$exe = Join-Path $installDir 'QuickShelf.exe'
if (-not (Test-Path $exe)) {
    throw "QuickShelf.exe was not found after installation."
}

# Start menu shortcut.
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($startMenuShortcut)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = "$exe,0"
$shortcut.Description = 'QuickShelf'
$shortcut.Save()

# Register per-user logon startup in HKCU Run. This proved more reliable than
# a Startup-folder shortcut on some Windows 11 configurations.
New-Item -Path $runKey -Force | Out-Null
Set-ItemProperty -Path $runKey -Name $runValueName -Value ('"{0}"' -f $exe)

# Remove the old Startup-folder shortcut from v0.1.0 installs to avoid
# duplicate launches after upgrading.
Remove-Item $legacyStartupShortcut -Force -ErrorAction SilentlyContinue

Start-Process $exe
Write-Host "QuickShelf installed to $installDir"
Write-Host "Windows logon startup registered in HKCU Run: $runValueName"

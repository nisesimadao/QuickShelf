$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts\publish'

if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

Push-Location $root
try {
    dotnet publish .\QuickShelf.csproj -c Release -r win-x64 --self-contained false -o $out
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host "Published QuickShelf to $out"

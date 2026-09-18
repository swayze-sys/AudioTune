$ErrorActionPreference = 'Stop'

# Keep .NET/PowerShell output readable in Windows PowerShell 5.1.
try {
    $Utf8 = New-Object System.Text.UTF8Encoding($false)
    [Console]::OutputEncoding = $Utf8
    $OutputEncoding = $Utf8
}
catch {
    # Encoding setup is non-critical; continue with the host defaults.
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'The .NET SDK was not found.' -ForegroundColor Red
    Write-Host 'Install the .NET 10 SDK, then run this script again.' -ForegroundColor Yellow
    exit 1
}

Write-Host 'Restoring AudioTune...' -ForegroundColor Cyan
dotnet restore .\AudioTune.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Building AudioTune (Debug)...' -ForegroundColor Cyan
dotnet build .\AudioTune.sln -c Debug --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ''
Write-Host 'Build completed.' -ForegroundColor Green
Write-Host 'Executable output is under AudioTune\bin\Debug\net10.0-windows\' -ForegroundColor Green

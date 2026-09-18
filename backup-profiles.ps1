#requires -version 5.1

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

$Source = Join-Path $env:LOCALAPPDATA 'AudioTune\Profiles'
$Timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$Destination = Join-Path $PSScriptRoot "AudioTune-ProfileBackup-$Timestamp"

if (-not (Test-Path -LiteralPath $Source)) {
    Write-Host "Kein AudioTune-Profilordner gefunden: $Source" -ForegroundColor Yellow
    exit 0
}

New-Item -ItemType Directory -Path $Destination -Force | Out-Null
Copy-Item -Path (Join-Path $Source '*') -Destination $Destination -Recurse -Force

Write-Host "AudioTune-Profile wurden gesichert:" -ForegroundColor Green
Write-Host $Destination -ForegroundColor Cyan

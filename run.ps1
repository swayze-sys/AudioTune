$ErrorActionPreference = 'Stop'

# PowerShell 5.1 compatible UTF-8 console output.
try {
    $Utf8 = New-Object System.Text.UTF8Encoding($false)
    [Console]::OutputEncoding = $Utf8
    $OutputEncoding = $Utf8
}
catch {
    # Non-critical.
}

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $Root 'AudioTune\AudioTune.csproj'
$Exe = Join-Path $Root 'AudioTune\bin\Debug\net10.0-windows\AudioTune.exe'
$ExeDir = Split-Path -Parent $Exe

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'The .NET SDK was not found. Install the .NET 10 SDK first.' -ForegroundColor Red
    exit 1
}

# Build only when the executable does not exist. Use build.ps1 explicitly when
# you want to rebuild after source changes.
if (-not (Test-Path $Exe)) {
    Write-Host 'AudioTune executable not found. Building Debug version first...' -ForegroundColor Cyan
    & dotnet build $Project -c Debug
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

# Start the WPF WinExe as an independent process. The PowerShell window may
# close immediately without terminating AudioTune.
Start-Process -FilePath $Exe -WorkingDirectory $ExeDir
exit 0

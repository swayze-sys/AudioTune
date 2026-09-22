param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipComparison
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 compatible.
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$VsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$MsBuild = $null

if (Test-Path -LiteralPath $VsWhere) {
    $InstallationPath = & $VsWhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    if ($InstallationPath) {
        $Candidate = Join-Path $InstallationPath 'MSBuild\Current\Bin\MSBuild.exe'
        if (Test-Path -LiteralPath $Candidate) {
            $MsBuild = $Candidate
        }
    }
}

if (-not $MsBuild) {
    $Candidates = @(
        'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe',
        'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    )
    $MsBuild = $Candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $MsBuild) {
    Write-Host 'Visual Studio Build Tools with Desktop development with C++ were not found.' -ForegroundColor Red
    Write-Host 'Install the workload, then run build-native-fxsound.ps1 again.' -ForegroundColor Yellow
    exit 1
}

$Project = Join-Path $Root 'Native\AudioTune.FxSound.Compare\AudioTune.FxSound.Compare.vcxproj'
Write-Host "Building isolated FxSound native module ($Configuration, x64)..." -ForegroundColor Cyan
& $MsBuild $Project /t:Build /p:Configuration=$Configuration /p:Platform=x64 /m /v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$ApoTestProject = Join-Path $Root 'Native\AudioTune.FxSound.Apo.Test\AudioTune.FxSound.Apo.Test.vcxproj'
Write-Host "Building Equalizer APO native host ($Configuration, x64)..." -ForegroundColor Cyan
& $MsBuild $ApoTestProject /t:Build /p:Configuration=$Configuration /p:Platform=x64 /m /v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$NativeDll = Join-Path $Root "Native\bin\$Configuration-x64\AudioTune.FxSound.Native.dll"
if (-not (Test-Path -LiteralPath $NativeDll -PathType Leaf)) {
    Write-Host "Native build validation failed: $NativeDll is missing." -ForegroundColor Red
    exit 2
}
$ApoDll = Join-Path $Root "Native\bin\$Configuration-x64\AudioTune.FxSound.Apo.dll"
if (-not (Test-Path -LiteralPath $ApoDll -PathType Leaf)) {
    Write-Host "Native APO host validation failed: $ApoDll is missing." -ForegroundColor Red
    exit 3
}

if (-not $SkipComparison) {
    $Comparison = Join-Path $Root "Native\bin\$Configuration-x64\AudioTune.FxSound.Compare.exe"
    Write-Host 'Comparing direct FxSound output with the AudioTune adapter...' -ForegroundColor Cyan
    & $Comparison
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $ApoTest = Join-Path $Root "Native\bin\$Configuration-x64\AudioTune.FxSound.Apo.Test.exe"
    Write-Host 'Validating Equalizer APO ABI, bypass and engine output...' -ForegroundColor Cyan
    & $ApoTest
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host 'Native FxSound module validation passed.' -ForegroundColor Green

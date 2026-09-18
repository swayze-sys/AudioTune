$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 compatible.
try {
    $Utf8 = New-Object System.Text.UTF8Encoding($false)
    [Console]::OutputEncoding = $Utf8
    $OutputEncoding = $Utf8
}
catch { }

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'The .NET 10 SDK was not found.' -ForegroundColor Red
    exit 1
}

$Output = Join-Path $Root 'publish\win-x64'
if (Test-Path $Output) {
    Remove-Item $Output -Recurse -Force
}
New-Item -ItemType Directory -Path $Output -Force | Out-Null

Write-Host 'Publishing AudioTune v0.4.16-alpha for Windows x64...' -ForegroundColor Cyan
& dotnet publish '.\AudioTune\AudioTune.csproj' `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $Output

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# The application payload must never carry a private .NET runtime. The setup
# installs the official Windows Desktop Runtime globally when it is missing.
$ForbiddenRuntimeFiles = @(
    'coreclr.dll',
    'clrjit.dll',
    'clrgc.dll',
    'clrgcexp.dll',
    'clretwrc.dll',
    'hostfxr.dll',
    'hostpolicy.dll',
    'System.Private.CoreLib.dll',
    'createdump.exe',
    'mscordaccore.dll',
    'mscordbi.dll',
    'mscorlib.dll',
    'mscorrc.dll'
)

$RuntimeHits = Get-ChildItem -LiteralPath $Output -Recurse -File | Where-Object {
    $ForbiddenRuntimeFiles -contains $_.Name
}

if ($RuntimeHits) {
    Write-Host ''
    Write-Host 'Publish validation failed: bundled .NET runtime files were found.' -ForegroundColor Red
    $RuntimeHits | ForEach-Object { Write-Host $_.FullName -ForegroundColor Red }
    exit 3
}

$RequiredFiles = @(
    (Join-Path $Output 'AudioTune.exe'),
    (Join-Path $Output 'Data\Headphones\beyerdynamic-amiron-home.sources.json'),
    (Join-Path $Output 'Data\Psychoacoustics\iso226.sources.json')
)

$MissingFiles = $RequiredFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }
if ($MissingFiles) {
    Write-Host ''
    Write-Host 'Publish validation failed: required application files are missing.' -ForegroundColor Red
    $MissingFiles | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    exit 4
}

Write-Host ''
Write-Host 'Publish completed:' -ForegroundColor Green
Write-Host $Output -ForegroundColor Green
Write-Host 'Validation passed: no private .NET runtime is present in the application payload.' -ForegroundColor Green

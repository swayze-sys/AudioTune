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

& (Join-Path $Root 'publish.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$ToolsDir = Join-Path $Root '.tools'
$InnoDir = Join-Path $ToolsDir 'InnoSetup6'
$InnoBootstrapVersion = '6.7.3'
$InnoBootstrapUrl = "https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-$InnoBootstrapVersion.exe"

$Candidates = @(
    (Join-Path $InnoDir 'ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$ISCC = $Candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $ISCC) {
    New-Item -ItemType Directory -Path $ToolsDir -Force | Out-Null
    $InnoBootstrap = Join-Path $ToolsDir "innosetup-$InnoBootstrapVersion.exe"

    Write-Host "Inno Setup 6 was not found. Downloading the official signed $InnoBootstrapVersion build tool..." -ForegroundColor Cyan
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $InnoBootstrapUrl -OutFile $InnoBootstrap -UseBasicParsing

    $Signature = Get-AuthenticodeSignature -LiteralPath $InnoBootstrap
    if ($Signature.Status -ne 'Valid' -or $Signature.SignerCertificate.Subject -notmatch 'Pyrsys B\.V\.') {
        Remove-Item -LiteralPath $InnoBootstrap -Force -ErrorAction SilentlyContinue
        Write-Host 'The downloaded Inno Setup installer did not have the expected valid Pyrsys B.V. signature.' -ForegroundColor Red
        exit 2
    }

    Write-Host 'Installing the Inno Setup compiler into the repository-local tool cache...' -ForegroundColor Cyan
    $InnoInstall = Start-Process -FilePath $InnoBootstrap -ArgumentList @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        '/CURRENTUSER',
        "/DIR=`"$InnoDir`""
    ) -Wait -PassThru
    if ($InnoInstall.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $InnoDir 'ISCC.exe'))) {
        Write-Host "Inno Setup installation failed with exit code $($InnoInstall.ExitCode)." -ForegroundColor Red
        exit 2
    }

    $ISCC = Join-Path $InnoDir 'ISCC.exe'
}

$PrerequisiteDir = Join-Path $Root 'Installer\Prerequisites'
$DotNetRuntimeVersion = '10.0.12'
$DotNetRuntimeName = "windowsdesktop-runtime-$DotNetRuntimeVersion-win-x64.exe"
$DotNetRuntimePath = Join-Path $PrerequisiteDir $DotNetRuntimeName
$DotNetRuntimeUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$DotNetRuntimeVersion/$DotNetRuntimeName"
$DotNetRuntimeSha512 = '0b907e9312867172a4eb82f4b5ab3f7c2d25e27d8349546d77eeb5d5b8cbecab9ebebfed189b13e9669dc578d892756c548366da539d47b3ddac5efcb7ae72fe'

New-Item -ItemType Directory -Path $PrerequisiteDir -Force | Out-Null
$DownloadRuntime = $true
if (Test-Path -LiteralPath $DotNetRuntimePath -PathType Leaf) {
    $ExistingHash = (Get-FileHash -LiteralPath $DotNetRuntimePath -Algorithm SHA512).Hash.ToLowerInvariant()
    $DownloadRuntime = $ExistingHash -ne $DotNetRuntimeSha512
}

if ($DownloadRuntime) {
    $RuntimeDownload = "$DotNetRuntimePath.download"
    Remove-Item -LiteralPath $RuntimeDownload -Force -ErrorAction SilentlyContinue
    Write-Host "Downloading official .NET Windows Desktop Runtime $DotNetRuntimeVersion..." -ForegroundColor Cyan
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $DotNetRuntimeUrl -OutFile $RuntimeDownload -UseBasicParsing
    $RuntimeHash = (Get-FileHash -LiteralPath $RuntimeDownload -Algorithm SHA512).Hash.ToLowerInvariant()
    if ($RuntimeHash -ne $DotNetRuntimeSha512) {
        Remove-Item -LiteralPath $RuntimeDownload -Force -ErrorAction SilentlyContinue
        Write-Host 'The downloaded .NET Windows Desktop Runtime failed SHA-512 verification.' -ForegroundColor Red
        exit 3
    }
    Move-Item -LiteralPath $RuntimeDownload -Destination $DotNetRuntimePath -Force
}

$VerifiedRuntimeHash = (Get-FileHash -LiteralPath $DotNetRuntimePath -Algorithm SHA512).Hash.ToLowerInvariant()
if ($VerifiedRuntimeHash -ne $DotNetRuntimeSha512) {
    Write-Host 'The cached .NET Windows Desktop Runtime failed SHA-512 verification.' -ForegroundColor Red
    exit 3
}

$OutputDir = Join-Path $Root 'dist'
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host 'Building AudioTune Windows installer...' -ForegroundColor Cyan
& $ISCC (Join-Path $Root 'Installer\AudioTune.iss')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ''
Write-Host 'Installer completed:' -ForegroundColor Green
Write-Host (Join-Path $OutputDir 'AudioTuneSetup-0.4.17-alpha.exe') -ForegroundColor Green
Write-Host ".NET Windows Desktop Runtime $DotNetRuntimeVersion is embedded and installs only when required." -ForegroundColor Green

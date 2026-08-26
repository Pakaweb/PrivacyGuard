#Requires -Version 5.1
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $root "src\PrivacyGuard\PrivacyGuard.csproj"
$outDir = Join-Path $root "artifacts\$Runtime"

Write-Host "Publishing PrivacyGuard ($Configuration, $Runtime)..."
dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Platform=x64 `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishReadyToRun=false `
    -o $outDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

Write-Host "Published to $outDir"

if ($SkipInstaller) {
    Write-Host "Skipping installer (-SkipInstaller)."
    exit 0
}

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Warning "Inno Setup 6 not found. Install from https://jrsoftware.org/isinfo.php then re-run, or pass -SkipInstaller."
    exit 0
}

$iss = Join-Path $root "setup\PrivacyGuard.iss"
& $iscc $iss
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compile failed."
}

Write-Host "Installer written to $(Join-Path $root 'artifacts\installer')"

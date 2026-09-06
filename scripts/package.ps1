# Radial Launcher Packaging Script v1.0.0
# Generates published directory, portable zip, and checksums

$ErrorActionPreference = "Stop"

$rootDir = Resolve-Path "$PSScriptRoot\.."
$publishDir = "$rootDir\publish\RadialLauncher-1.0.0-win-x64"
$artifactsDir = "$rootDir\artifacts"
$zipFile = "$artifactsDir\RadialLauncher-1.0.0-win-x64.zip"

Write-Host "=== Building & Publishing Radial Launcher (Release win-x64) ===" -ForegroundColor Cyan

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
if (!(Test-Path $artifactsDir)) {
    New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
}

dotnet publish "$rootDir\RadialLauncher.csproj" -c Release -r win-x64 --self-contained false -o $publishDir

# Remove pdb from publish package for clean distribution
if (Test-Path "$publishDir\RadialLauncher.pdb") {
    Remove-Item "$publishDir\RadialLauncher.pdb" -Force
}

# Copy Readme to published folder
if (Test-Path "$rootDir\README.md") {
    Copy-Item "$rootDir\README.md" "$publishDir\README.md" -Force
}

Write-Host "=== Creating Portable Zip Package ===" -ForegroundColor Cyan
if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}

Compress-Archive -Path "$publishDir\*" -DestinationPath $zipFile -CompressionLevel Optimal

Write-Host "=== Generating SHA256 Checksums ===" -ForegroundColor Cyan
$checksums = @()
Get-ChildItem -Path $artifactsDir -Filter *.zip | ForEach-Object {
    $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash
    $checksums += "$hash  $($_.Name)"
}

$checksumFile = "$artifactsDir\SHA256SUMS.txt"
$checksums | Set-Content -Path $checksumFile -Encoding utf8

Write-Host "=== Packaging Complete! ===" -ForegroundColor Green
Write-Host "Portable package: $zipFile"
Write-Host "Checksum file:    $checksumFile"

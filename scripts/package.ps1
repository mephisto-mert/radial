# Radial Launcher Automated Release Packaging Pipeline v1.0.0
# Builds clean production binaries, standalone setup wizard, portable zip, and verification checksums

$ErrorActionPreference = "Stop"

$rootDir = Resolve-Path "$PSScriptRoot\.."
$publishDir = "$rootDir\publish\RadialLauncher-1.0.0-win-x64"
$installerProjectDir = "$rootDir\installer\RadialLauncher.Installer"
$artifactsDir = "$rootDir\artifacts"
$zipFile = "$artifactsDir\RadialLauncher-1.0.0-win-x64.zip"
$setupExeTarget = "$artifactsDir\RadialLauncher-Setup-v1.0.0.exe"
$payloadZip = "$installerProjectDir\payload.zip"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   RADIAL LAUNCHER - v1.0.0 RELEASE PACKAGING PIPELINE" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Clean output directories
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
if (!(Test-Path $artifactsDir)) {
    New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
}

# 2. Publish clean Release win-x64
Write-Host "`n[1/5] Publishing clean Release binaries (win-x64)..." -ForegroundColor Yellow
dotnet publish "$rootDir\RadialLauncher.csproj" -c Release -r win-x64 --self-contained false -o $publishDir

# Remove any PDB or debug/developer files from publish
Get-ChildItem -Path $publishDir -Include *.pdb,*.db,*.sqlite,settings.json,*.log -Recurse | Remove-Item -Force

if (Test-Path "$rootDir\README.md") {
    Copy-Item "$rootDir\README.md" "$publishDir\README.md" -Force
}
if (Test-Path "$rootDir\app.ico") {
    Copy-Item "$rootDir\app.ico" "$publishDir\app.ico" -Force
}

# 3. Create Payload Zip for Installer & Standalone Portable Zip
Write-Host "`n[2/5] Creating clean distribution ZIP packages..." -ForegroundColor Yellow
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force }
if (Test-Path $zipFile) { Remove-Item $zipFile -Force }

Compress-Archive -Path "$publishDir\*" -DestinationPath $payloadZip -CompressionLevel Optimal
Copy-Item $payloadZip $zipFile -Force

# 4. Compile Standalone Single-File Setup Wizard
Write-Host "`n[3/5] Compiling Standalone Setup Wizard (RadialLauncher-Setup-v1.0.0.exe)..." -ForegroundColor Yellow
$installerPublishTemp = "$rootDir\installer\publish_temp"
if (Test-Path $installerPublishTemp) { Remove-Item -Recurse -Force $installerPublishTemp }

dotnet publish "$installerProjectDir\RadialLauncher.Installer.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $installerPublishTemp

if (Test-Path "$installerPublishTemp\RadialLauncher.Installer.exe") {
    Copy-Item "$installerPublishTemp\RadialLauncher.Installer.exe" $setupExeTarget -Force
}
Remove-Item -Recurse -Force $installerPublishTemp

# 5. Generate SHA-256 Checksums
Write-Host "`n[4/5] Generating SHA256 verification checksums..." -ForegroundColor Yellow
$checksums = @()
Get-ChildItem -Path $artifactsDir -Include *.zip,*.exe -Recurse | ForEach-Object {
    $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash
    $checksums += "$hash  $($_.Name)"
    Write-Host "  $($_.Name) -> $hash" -ForegroundColor Gray
}

$checksumFile = "$artifactsDir\SHA256SUMS.txt"
$checksums | Set-Content -Path $checksumFile -Encoding utf8

Write-Host "`n[5/5] Packaging Completed Successfully!" -ForegroundColor Green
Write-Host "----------------------------------------------------------" -ForegroundColor Green
Write-Host "Installer:    $setupExeTarget" -ForegroundColor White
Write-Host "Portable ZIP: $zipFile" -ForegroundColor White
Write-Host "Checksums:    $checksumFile" -ForegroundColor White
Write-Host "----------------------------------------------------------" -ForegroundColor Green

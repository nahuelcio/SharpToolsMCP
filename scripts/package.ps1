# SharpTools Package Creator for Windows
# Creates zip packages for GitHub Releases

param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SharpTools Package Creator" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ProjectRoot = Resolve-Path "$PSScriptRoot\.."
$PublishDir = "$ProjectRoot\publish"
$PackagesDir = "$ProjectRoot\packages"

# Clean packages directory
Write-Host "Cleaning packages directory..." -ForegroundColor Yellow
if (Test-Path $PackagesDir) {
    Remove-Item -Recurse -Force $PackagesDir
}
New-Item -ItemType Directory -Path $PackagesDir | Out-Null

# Check if publish directory exists
if (-not (Test-Path $PublishDir)) {
    Write-Host "Error: Publish directory not found. Run build.ps1 first." -ForegroundColor Red
    exit 1
}

# Get version from git tag or use default
if ([string]::IsNullOrEmpty($Version)) {
    try {
        $Version = git describe --tags --always --dirty 2>$null
        if ([string]::IsNullOrEmpty($Version)) {
            $Version = "dev"
        }
    } catch {
        $Version = "dev"
    }
}

Write-Host "Version: $Version" -ForegroundColor Green
Write-Host ""

$Runtimes = @("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")

foreach ($runtime in $Runtimes) {
    $srcDir = "$PublishDir\stdio-$runtime"
    
    if (-not (Test-Path $srcDir)) {
        Write-Host "Warning: $srcDir not found, skipping..." -ForegroundColor Yellow
        continue
    }
    
    Write-Host "Creating package: stdio-$runtime..." -ForegroundColor Yellow
    
    $packageName = "sharptools-stdio-$runtime-$Version.zip"
    
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipPath = "$PackagesDir\$packageName"
    [System.IO.Compression.ZipFile]::CreateFromDirectory($srcDir, $zipPath)
    
    Write-Host "  Created: $packageName" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Packages Created Successfully!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output directory: $PackagesDir" -ForegroundColor Green
Write-Host ""
Get-ChildItem $PackagesDir | Select-Object Name, @{Name="Size";Expression={"{0:N2} KB" -f ($_.Length/1KB)}}

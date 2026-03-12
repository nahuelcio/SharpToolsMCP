# SharpTools Build Script for Windows
# Builds the stdio server for all platforms

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "$PSScriptRoot\..\publish"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SharpTools Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Clean output directory
Write-Host "Cleaning output directory..." -ForegroundColor Yellow
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

$ProjectRoot = Resolve-Path "$PSScriptRoot\.."
$Runtimes = @("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")

# Build solution first
Write-Host "Building solution..." -ForegroundColor Green
dotnet build "$ProjectRoot\SharpTools.sln" -c $Configuration

Write-Host ""
Write-Host "Publishing for all platforms..." -ForegroundColor Green
Write-Host ""

foreach ($runtime in $Runtimes) {
    Write-Host "----------------------------------------" -ForegroundColor Cyan
    Write-Host "Publishing for: $runtime" -ForegroundColor Cyan
    Write-Host "----------------------------------------" -ForegroundColor Cyan
    
    Write-Host "  Building Stdio Server..." -ForegroundColor Yellow
    dotnet publish "$ProjectRoot\SharpTools.StdioServer\SharpTools.StdioServer.csproj" `
        -c $Configuration `
        -r $runtime `
        --self-contained `
        -o "$OutputDir\stdio-$runtime" `
        /p:PublishSingleFile=false `
        /p:PublishTrimmed=false
    
    Write-Host "  Done: $runtime" -ForegroundColor Green
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output directory: $OutputDir" -ForegroundColor Green
Write-Host ""
Write-Host "Created publish directories:" -ForegroundColor Green
Get-ChildItem $OutputDir | Select-Object Name

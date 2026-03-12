# SharpTools Stdio Installer for Windows
# Downloads and installs the latest SharpTools Stdio server

param(
    [string]$InstallDir = "$env:LOCALAPPDATA\SharpTools"
)

$ErrorActionPreference = "Stop"

# Colors (PowerShell 5.1+)
function Write-Color {
    param([string]$Text, [string]$Color)
    Write-Host $Text -ForegroundColor $Color
}

Write-Color "========================================" "Cyan"
Write-Color "SharpTools Stdio Installer" "Cyan"
Write-Color "========================================" "Cyan"
Write-Host ""

# Configuration
$Repo = "nahuelcio/SharpToolsMCP"
$BinDir = "$env:LOCALAPPDATA\Microsoft\WindowsApps"
$ServerType = "stdio"

# Detect platform
function Get-Platform {
    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    switch ($architecture) {
        "X64" {
            return "win-x64"
        }
        "Arm64" {
            return "win-arm64"
        }
        default {
            throw "Unsupported architecture: $architecture"
        }
    }
}

$Platform = Get-Platform
Write-Host "Detected platform: $Platform"
Write-Host ""

# Create temporary directory
$TempDir = Join-Path $env:TEMP "sharptools-install-$(Get-Random)"
New-Item -ItemType Directory -Path $TempDir | Out-Null

# Cleanup on exit
trap {
    if (Test-Path $TempDir) {
        Remove-Item -Recurse -Force $TempDir
    }
} 1,2,3,15

# Get latest release from GitHub API
Write-Host "Fetching latest release information..." -ForegroundColor Yellow
try {
    $LatestRelease = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest"
} catch {
    Write-Host "Error: Could not fetch latest release" -ForegroundColor Red
    exit 1
}

$Version = $LatestRelease.tag_name
Write-Host "Latest version: $Version" -ForegroundColor Green

# Find the appropriate asset
$Asset = $LatestRelease.assets | Where-Object { $_.name -like "*stdio*$Platform*.zip" } | Select-Object -First 1

if (-not $Asset) {
    Write-Host "Error: No release asset found for $Platform" -ForegroundColor Red
    Write-Host "Available assets:"
    $LatestRelease.assets | ForEach-Object { Write-Host "  $($_.name)" }
    exit 1
}

$DownloadUrl = $Asset.browser_download_url
Write-Host "Downloading: $DownloadUrl" -ForegroundColor Yellow
Write-Host ""

# Download
$ZipPath = Join-Path $TempDir "sharptools.zip"
Invoke-WebRequest -Uri $DownloadUrl -OutFile $ZipPath -UseBasicParsing

# Extract
Write-Host "Extracting..." -ForegroundColor Yellow
$ExtractDir = Join-Path $TempDir "extracted"
New-Item -ItemType Directory -Path $ExtractDir | Out-Null
Expand-Archive -Path $ZipPath -DestinationPath $ExtractDir -Force

# Install
Write-Host ""
Write-Host "Installing to: $InstallDir\$ServerType" -ForegroundColor Yellow

# Create install directory
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir | Out-Null
}

# Remove old version
$ServerInstallDir = Join-Path $InstallDir $ServerType
if (Test-Path $ServerInstallDir) {
    Remove-Item -Recurse -Force $ServerInstallDir
}
New-Item -ItemType Directory -Path $ServerInstallDir | Out-Null

# Copy new version
Copy-Item -Path "$ExtractDir\*" -Destination $ServerInstallDir -Recurse -Force

# Find executable
$Executable = Join-Path $ServerInstallDir "stserver.exe"
if (-not (Test-Path $Executable)) {
    $Executable = Join-Path $ServerInstallDir "SharpTools.StdioServer.exe"
}

if (-not (Test-Path $Executable)) {
    throw "Could not find server executable in $ServerInstallDir"
}

$CommandShimPath = Join-Path $InstallDir "sharptools.cmd"
$CommandShimContent = "@echo off`r`n\"$Executable\" %*"
Set-Content -Path $CommandShimPath -Value $CommandShimContent -Encoding ASCII

$CommandPath = $CommandShimPath

# Create symlink in WindowsApps folder (requires developer mode or admin)
$LinkPath = Join-Path $BinDir "sharptools.exe"

try {
    if (Test-Path $LinkPath) {
        Remove-Item $LinkPath -Force
    }
    New-Item -ItemType SymbolicLink -Path $LinkPath -Target $Executable -Force | Out-Null
    $CommandPath = $LinkPath
} catch {
    Write-Host "Warning: Could not create symlink in $BinDir" -ForegroundColor Yellow
    Write-Host "Using fallback command shim: $CommandShimPath" -ForegroundColor Yellow
    Write-Host "Add $InstallDir to PATH to run 'sharptools'" -ForegroundColor Yellow
}

Write-Host ""
Write-Color "========================================" "Green"
Write-Color "Installation Complete!" "Green"
Write-Color "========================================" "Green"
Write-Host ""
Write-Host "Installed: sharptools"
Write-Host "Location: $Executable"
Write-Host "Command: $CommandPath"
Write-Host ""
Write-Host "Usage:" -ForegroundColor Cyan
Write-Host "  sharptools --help"
Write-Host ""
Write-Host "For MCP configuration (VS Code Copilot):" -ForegroundColor Cyan
Write-Host '  "mcp": {'
Write-Host '    "servers": {'
Write-Host '      "SharpTools": {'
Write-Host '        "type": "stdio",'
Write-Host '        "command": "sharptools"'
Write-Host '      }'
Write-Host '    }'
Write-Host '  }'
Write-Host ""

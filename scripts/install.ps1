# SharpTools Installer for Windows
# Downloads and installs the latest SharpTools release

param(
    [string]$ServerType = "sse",  # sse or stdio
    [string]$InstallDir = "$env:LOCALAPPDATA\SharpTools"
)

$ErrorActionPreference = "Stop"

# Colors (PowerShell 5.1+)
function Write-Color {
    param([string]$Text, [string]$Color)
    Write-Host $Text -ForegroundColor $Color
}

Write-Color "========================================" "Cyan"
Write-Color "SharpTools Installer" "Cyan"
Write-Color "========================================" "Cyan"
Write-Host ""

# Configuration
$Repo = "nahuelcio/SharpToolsMCP"
$BinDir = "$env:LOCALAPPDATA\Microsoft\WindowsApps"

# Detect platform
function Get-Platform {
    $arch = (Get-CimInstance Win32_Processor).AddressWidth | Select-Object -First 1
    if ($arch -eq 64) {
        return "win-x64"
    } elseif ($arch -eq 32) {
        return "win-arm64"  # Assuming ARM for 32-bit
    } else {
        throw "Unsupported architecture: $arch"
    }
}

$Platform = Get-Platform
Write-Host "Detected platform: $Platform"
Write-Host "Server type: $ServerType"
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
$AssetName = "sharptools-$ServerType-$Platform"
$Asset = $LatestRelease.assets | Where-Object { $_.name -like "*$Platform*.zip" } | Select-Object -First 1

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
Expand-Archive -Path $ZipPath -DestinationPath $TempDir -Force

# Install
Write-Host ""
Write-Host "Installing to: $InstallDir" -ForegroundColor Yellow

# Create install directory
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir | Out-Null
}

# Remove old version
$ServerInstallDir = Join-Path $InstallDir $ServerType
if (Test-Path $ServerInstallDir) {
    Remove-Item -Recurse -Force $ServerInstallDir
}

# Copy new version
Move-Item -Path "$TempDir\*" -Destination $ServerInstallDir -Force

# Create symlink in WindowsApps folder (requires developer mode or admin)
$Executable = Join-Path $ServerInstallDir "stserver.exe"
$LinkPath = Join-Path $BinDir "sharptools-$ServerType.exe"

# Make executable (ensure .exe extension)
if (-not (Test-Path $Executable)) {
    # Try alternative name
    $Executable = Join-Path $ServerInstallDir "SharpTools.SseServer.exe"
    if (-not (Test-Path $Executable)) {
        $Executable = Join-Path $ServerInstallDir "SharpTools.StdioServer.exe"
    }
}

# Create symlink (may require admin)
try {
    if (Test-Path $LinkPath) {
        Remove-Item $LinkPath -Force
    }
    New-Item -ItemType SymbolicLink -Path $LinkPath -Target $Executable -Force | Out-Null
} catch {
    Write-Host "Warning: Could not create symlink in $BinDir" -ForegroundColor Yellow
    Write-Host "You may need to run as Administrator or add $ServerInstallDir to PATH manually" -ForegroundColor Yellow
}

Write-Host ""
Write-Color "========================================" "Green"
Write-Color "Installation Complete!" "Green"
Write-Color "========================================" "Green"
Write-Host ""
Write-Host "Installed: sharptools-$ServerType"
Write-Host "Location: $Executable"
Write-Host ""
Write-Host "Usage:" -ForegroundColor Cyan
Write-Host "  sharptools-$ServerType --help"
Write-Host ""
Write-Host "For SSE server:" -ForegroundColor Cyan
Write-Host "  sharptools-sse --port 3001"
Write-Host ""
Write-Host "For Stdio server (MCP):" -ForegroundColor Cyan
Write-Host "  sharptools-stdio"
Write-Host ""

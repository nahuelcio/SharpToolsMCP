#!/usr/bin/env bash
# SharpTools Stdio Installer for Linux/macOS
# Downloads and installs the latest SharpTools Stdio server

set -e

# Configuration
REPO="nahuelcio/SharpToolsMCP"
INSTALL_DIR="${INSTALL_DIR:-$HOME/.sharptools}"
BIN_DIR="${BIN_DIR:-$HOME/.local/bin}"
SERVER_TYPE="stdio"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}SharpTools Stdio Installer${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --install-dir)
            INSTALL_DIR="$2"
            shift 2
            ;;
        --bin-dir)
            BIN_DIR="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --install-dir DIR Installation directory (default: ~/.sharptools)"
            echo "  --bin-dir DIR     Bin directory for symlinks (default: ~/.local/bin)"
            echo "  -h, --help        Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Detect OS and architecture
detect_platform() {
    local os=$(uname -s | tr '[:upper:]' '[:lower:]')
    local arch=$(uname -m)
    
    case "$os" in
        linux)
            case "$arch" in
                x86_64) echo "linux-x64" ;;
                aarch64|arm64) echo "linux-arm64" ;;
                *) echo "Unsupported architecture: $arch" >&2; exit 1 ;;
            esac
            ;;
        darwin)
            case "$arch" in
                x86_64) echo "osx-x64" ;;
                arm64) echo "osx-arm64" ;;
                *) echo "Unsupported architecture: $arch" >&2; exit 1 ;;
            esac
            ;;
        *)
            echo "Unsupported OS: $os" >&2
            exit 1
            ;;
    esac
}

PLATFORM=$(detect_platform)
echo "Detected platform: $PLATFORM"
echo ""

# Create temporary directory
TEMP_DIR=$(mktemp -d)
trap "rm -rf $TEMP_DIR" EXIT

# Get latest release from GitHub API
echo "Fetching latest release information..."
LATEST_RELEASE=$(curl -s https://api.github.com/repos/$REPO/releases/latest)

if [ -z "$LATEST_RELEASE" ]; then
    echo -e "${RED}Error: Could not fetch latest release${NC}" >&2
    exit 1
fi

VERSION=$(echo "$LATEST_RELEASE" | grep -o '"tag_name": ".*"' | cut -d'"' -f4)
echo "Latest version: $VERSION"

# Find the appropriate asset
echo "Searching for package: sharptools-stdio-$PLATFORM..."
DOWNLOAD_URL=$(echo "$LATEST_RELEASE" | grep -o "\"browser_download_url\": \"[^\"]*stdio[^\"]*${PLATFORM}[^\"]*\.tar\.gz\"" | cut -d'"' -f4 | head -1)

if [ -z "$DOWNLOAD_URL" ]; then
    echo -e "${RED}Error: No release asset found for $PLATFORM${NC}" >&2
    echo "Available assets:"
    echo "$LATEST_RELEASE" | grep -o "\"browser_download_url\": \"[^\"]*\"" | cut -d'"' -f4
    exit 1
fi

echo "Downloading: $DOWNLOAD_URL"
echo ""

# Download and extract
curl -L -o "$TEMP_DIR/sharptools.tar.gz" "$DOWNLOAD_URL"

echo "Extracting..."
tar -xzf "$TEMP_DIR/sharptools.tar.gz" -C "$TEMP_DIR"

# Install
echo ""
echo "Installing to: $INSTALL_DIR/$SERVER_TYPE"

# Create install directory
mkdir -p "$INSTALL_DIR/$SERVER_TYPE"

# Remove old version
rm -rf "$INSTALL_DIR/$SERVER_TYPE"/*

# Copy new version
cp -r "$TEMP_DIR"/* "$INSTALL_DIR/$SERVER_TYPE/"

# Create bin directory if it doesn't exist
mkdir -p "$BIN_DIR"

# Find executable
EXECUTABLE="$INSTALL_DIR/$SERVER_TYPE/stserver"
if [ ! -f "$EXECUTABLE" ]; then
    EXECUTABLE="$INSTALL_DIR/$SERVER_TYPE/SharpTools.StdioServer"
fi

# Make executable
chmod +x "$EXECUTABLE"

# Remove old symlink if exists
rm -f "$BIN_DIR/sharptools"

# Create new symlink
ln -s "$EXECUTABLE" "$BIN_DIR/sharptools"

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Installation Complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "Installed: sharptools"
echo "Location: $BIN_DIR/sharptools"
echo ""

# Check if bin directory is in PATH
if [[ ":$PATH:" != *":$BIN_DIR:"* ]]; then
    echo -e "${YELLOW}Warning: $BIN_DIR is not in your PATH${NC}"
    echo "Add this to your ~/.bashrc or ~/.zshrc:"
    echo "  export PATH=\"$BIN_DIR:\$PATH\""
    echo ""
fi

echo "Usage:"
echo "  sharptools --help"
echo ""
echo "For MCP configuration (VS Code Copilot):"
echo '  "mcp": {'
echo '    "servers": {'
echo '      "SharpTools": {'
echo '        "type": "stdio",'
echo '        "command": "sharptools"'
echo '      }'
echo '    }'
echo '  }'
echo ""

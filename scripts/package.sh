#!/bin/bash
# SharpTools Package Creator for Linux/macOS
# Creates zip/tar.gz packages for GitHub Releases

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
PUBLISH_DIR="$PROJECT_ROOT/publish"
PACKAGES_DIR="$PROJECT_ROOT/packages"

echo "========================================"
echo "SharpTools Package Creator"
echo "========================================"
echo ""

# Clean packages directory
echo "Cleaning packages directory..."
rm -rf "$PACKAGES_DIR"
mkdir -p "$PACKAGES_DIR"

# Check if publish directory exists
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: Publish directory not found. Run build.sh first."
    exit 1
fi

# Get version from git tag or use default
VERSION=$(git describe --tags --always --dirty 2>/dev/null || echo "dev")
echo "Version: $VERSION"
echo ""

# Create packages for each runtime
RUNTIMES=("win-x64" "win-arm64" "linux-x64" "linux-arm64" "osx-x64" "osx-arm64")
SERVERS=("sse" "stdio")

for runtime in "${RUNTIMES[@]}"; do
    for server in "${SERVERS[@]}"; do
        src_dir="$PUBLISH_DIR/$server-$runtime"
        
        if [ ! -d "$src_dir" ]; then
            echo "Warning: $src_dir not found, skipping..."
            continue
        fi
        
        echo "Creating package: $server-$runtime..."
        
        if [[ "$runtime" == win-* ]]; then
            # Windows - create ZIP
            package_name="sharptools-$server-$runtime-$VERSION.zip"
            (cd "$src_dir" && zip -r "$PACKAGES_DIR/$package_name" .)
        else
            # Unix - create tar.gz
            package_name="sharptools-$server-$runtime-$VERSION.tar.gz"
            tar -czf "$PACKAGES_DIR/$package_name" -C "$src_dir" .
        fi
        
        echo "  Created: $package_name"
    done
done

echo ""
echo "========================================"
echo "Packages Created Successfully!"
echo "========================================"
echo ""
echo "Output directory: $PACKAGES_DIR"
echo ""
ls -lh "$PACKAGES_DIR"

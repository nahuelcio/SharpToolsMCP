#!/bin/bash
# SharpTools Build Script for Linux/macOS
# Builds self-contained executables for all platforms

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
OUTPUT_DIR="$PROJECT_ROOT/publish"

echo "========================================"
echo "SharpTools Build Script"
echo "========================================"
echo ""

# Clean output directory
echo "Cleaning output directory..."
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Build configurations
CONFIGURATIONS=("Release")
RUNTIMES=("win-x64" "win-arm64" "linux-x64" "linux-arm64" "osx-x64" "osx-arm64")

# Build solution first
echo "Building solution..."
dotnet build "$PROJECT_ROOT/SharpTools.sln" -c Release

echo ""
echo "Publishing for all platforms..."
echo ""

for runtime in "${RUNTIMES[@]}"; do
    echo "----------------------------------------"
    echo "Publishing for: $runtime"
    echo "----------------------------------------"
    
    # SSE Server
    echo "  Building SSE Server..."
    dotnet publish "$PROJECT_ROOT/SharpTools.SseServer/SharpTools.SseServer.csproj" \
        -c Release \
        -r "$runtime" \
        --self-contained \
        -o "$OUTPUT_DIR/sse-$runtime" \
        /p:PublishSingleFile=false \
        /p:PublishTrimmed=false
    
    # Stdio Server
    echo "  Building Stdio Server..."
    dotnet publish "$PROJECT_ROOT/SharpTools.StdioServer/SharpTools.StdioServer.csproj" \
        -c Release \
        -r "$runtime" \
        --self-contained \
        -o "$OUTPUT_DIR/stdio-$runtime" \
        /p:PublishSingleFile=false \
        /p:PublishTrimmed=false
    
    echo "  Done: $runtime"
    echo ""
done

echo "========================================"
echo "Build Complete!"
echo "========================================"
echo ""
echo "Output directory: $OUTPUT_DIR"
echo ""
echo "Created packages:"
ls -la "$OUTPUT_DIR"

# SharpTools Scripts

This directory contains build, packaging, and installation scripts for SharpTools.

## Build Scripts (For Developers)

### Windows
```powershell
.\scripts\build.ps1
```

### Linux/macOS
```bash
./scripts/build.sh
```

These scripts build self-contained executables for all platforms (Windows, Linux, macOS) and output them to the `publish/` directory.

## Package Scripts (For Release)

### Windows
```powershell
.\scripts\package.ps1
```

### Linux/macOS
```bash
./scripts/package.sh
```

These scripts create distributable packages (`.zip` for Windows, `.tar.gz` for Unix) from the `publish/` directory and output them to the `packages/` directory.

## Install Scripts (For End Users)

### Windows
```powershell
.\scripts\install.ps1 -ServerType sse
.\scripts\install.ps1 -ServerType stdio
```

### Linux/macOS
```bash
./scripts/install.sh --server sse
./scripts/install.sh --server stdio
```

These scripts download the latest release from GitHub and install it to your system.

## Full Release Process

1. **Build all platforms:**
   ```bash
   ./scripts/build.sh  # or build.ps1 on Windows
   ```

2. **Create packages:**
   ```bash
   ./scripts/package.sh  # or package.ps1 on Windows
   ```

3. **Create a git tag:**
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

4. **GitHub Actions will automatically:**
   - Build for all platforms
   - Create a GitHub release
   - Upload all packages
   - Publish to NuGet (if tagged)

## Manual Installation from Local Build

If you want to install from a local build instead of downloading from GitHub:

### Windows
```powershell
# After running build.ps1
$installDir = "$env:LOCALAPPDATA\SharpTools\sse"
New-Item -ItemType Directory -Path $installDir -Force
Copy-Item -Recurse -Force publish\sse-win-x64\* $installDir
```

### Linux/macOS
```bash
# After running build.sh
mkdir -p ~/.sharptools/sse
cp -r publish/sse-linux-x64/* ~/.sharptools/sse/
chmod +x ~/.sharptools/sse/stserver
ln -s ~/.sharptools/sse/stserver ~/.local/bin/sharptools-sse
```

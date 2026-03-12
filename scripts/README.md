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

These scripts build the stdio server for all platforms (Windows, Linux, macOS) and output it to the `publish/` directory.

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
.\scripts\install.ps1
```

### Linux/macOS
```bash
./scripts/install.sh
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
$installDir = "$env:LOCALAPPDATA\SharpTools\stdio"
New-Item -ItemType Directory -Path $installDir -Force
Copy-Item -Recurse -Force publish\stdio-win-x64\* $installDir
```

### Linux/macOS
```bash
# After running build.sh
mkdir -p ~/.sharptools/stdio
cp -r publish/stdio-linux-x64/* ~/.sharptools/stdio/
chmod +x ~/.sharptools/stdio/SharpTools.StdioServer
ln -s ~/.sharptools/stdio/SharpTools.StdioServer ~/.local/bin/sharptools
```

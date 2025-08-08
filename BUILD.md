# Building Peglin Save Explorer

This document describes how to build Peglin Save Explorer for distribution across multiple platforms.

## Prerequisites

- .NET 9.0 SDK
- Node.js 20 or later
- npm

## Local Development Build

For local development and testing:

```bash
# Install build dependencies
npm install

# Build for all platforms (development version)
npm run build:dev

# Build for all platforms with a specific version
npm run build v1.0.0
```

This will create:

- `dist/win-x64/` - Windows x64 build
- `dist/linux-x64/` - Linux x64 build
- `dist/osx-x64/` - macOS Intel build
- `dist/osx-arm64/` - macOS Apple Silicon build

Each directory contains:

- The standalone executable (`peglin-save-explorer` or `peglin-save-explorer.exe`)
- Web frontend assets in `wwwroot/`
- Platform-specific scripts in `scripts/`
- Documentation (`README.txt`, `VERSION`, `BUILD_INFO`)

## Automated Builds (GitHub Actions)

The repository includes a GitHub Actions workflow that automatically builds and releases the application when you push a version tag:

```bash
# Create and push a version tag
git tag v1.0.0
git push origin v1.0.0
```

This will trigger the build workflow which:

1. Builds all platform variants
2. Creates distribution archives (.zip for Windows, .tar.gz for others)
3. Creates a GitHub release with all archives attached

## Manual Release Process

If you prefer to build and release manually:

1. **Build locally:**

   ```bash
   npm run build v1.0.0
   ```

2. **Test the builds** on their target platforms

3. **Create release archives** (optional, the build script creates them automatically):

   ```bash
   cd dist
   zip -r peglin-save-explorer-win-x64.zip win-x64/
   tar -czf peglin-save-explorer-linux-x64.tar.gz linux-x64/
   tar -czf peglin-save-explorer-osx-x64.tar.gz osx-x64/
   tar -czf peglin-save-explorer-osx-arm64.tar.gz osx-arm64/
   ```

4. **Upload to GitHub releases** or distribute as needed

## Distribution Structure

Each platform archive contains:

```
peglin-save-explorer-{platform}/
├── peglin-save-explorer(.exe)     # Main executable
├── wwwroot/                       # Web frontend assets
├── scripts/
│   ├── open-web.{sh|bat}         # Launch web interface
│   └── install-to-path.{sh|bat}  # Install to system PATH
├── README.txt                    # User documentation
├── VERSION                       # Version string
└── BUILD_INFO                    # Build metadata
```

## User Installation

Users can either:

1. **Extract and run directly:**

   - Extract the archive
   - Run `./peglin-save-explorer` (or `peglin-save-explorer.exe`)
   - Use `./scripts/open-web` for the web interface

2. **Install to system PATH:**
   - Extract the archive
   - Run `./scripts/install-to-path` script
   - Then use `peglin-save-explorer` from anywhere

## Build Configuration

The build process:

- Creates self-contained executables (no .NET runtime required)
- Includes all dependencies in a single file
- Compresses the executable for smaller distribution size
- Excludes Peglin game DLLs (these are development-only dependencies)
- Builds the React frontend and includes it in the distribution

## Troubleshooting

**Build fails with missing Peglin DLLs:**

- This is normal for distribution builds
- The Peglin DLL references are only used during development
- The build script sets `-p:PeglinDllPath=` to disable these references

**Frontend build fails:**

- Ensure Node.js 20+ is installed
- Run `npm install` in the `web-frontend` directory
- Check that the build script can find the web frontend

**Platform-specific issues:**

- Ensure you have the target platform SDK installed
- For cross-compilation, .NET should handle this automatically
- Test on the actual target platform when possible

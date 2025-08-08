#!/bin/bash

# Build script for Peglin Save Explorer
# This script builds the application for all supported platforms

set -e

echo "🎮 Building Peglin Save Explorer for all platforms..."

# Configuration
PROJECT_DIR="peglin-save-explorer"
OUTPUT_DIR="dist"
VERSION=${1:-"dev"}

# Clean previous builds
echo "🧹 Cleaning previous builds..."
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Build frontend
echo "🌐 Building web frontend..."
cd web-frontend
npm install
npm run build
cd ..

# Define platforms
platforms=("win-x64" "linux-x64" "osx-x64" "osx-arm64")

# Build for each platform
for platform in "${platforms[@]}"; do
    echo "🔨 Building for $platform..."
    
    cd "$PROJECT_DIR"
    dotnet publish \
        --configuration Release \
        --runtime "$platform" \
        --self-contained true \
        --output "../$OUTPUT_DIR/$platform" \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:PublishTrimmed=false \
        -p:EnableCompressionInSingleFile=true \
        -p:PeglinDllPath=""
    
    cd ..
    
    # Copy web frontend
    echo "   📁 Copying web assets for $platform..."
    cp -r web-frontend/dist "$OUTPUT_DIR/$platform/wwwroot/"
    
    # Create scripts directory
    mkdir -p "$OUTPUT_DIR/$platform/scripts"
    
    # Create platform-specific scripts
    if [[ "$platform" == "win-x64" ]]; then
        # Windows batch files
        cat > "$OUTPUT_DIR/$platform/scripts/open-web.bat" << 'EOF'
@echo off
echo Starting Peglin Save Explorer Web Interface...
cd /d "%~dp0\.."
start "" "peglin-save-explorer.exe" web --open
EOF
        
        cat > "$OUTPUT_DIR/$platform/scripts/install-to-path.bat" << 'EOF'
@echo off
echo Installing Peglin Save Explorer to PATH...
set "TARGET_DIR=%USERPROFILE%\.peglin-save-explorer"
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"
copy "%~dp0\..\peglin-save-explorer.exe" "%TARGET_DIR%\"

:: Add to user PATH
for /f "tokens=2*" %%A in ('reg query HKCU\Environment /v PATH 2^>nul') do set "USER_PATH=%%B"
if not defined USER_PATH set "USER_PATH="
echo %USER_PATH% | find /i "%TARGET_DIR%" >nul
if errorlevel 1 (
    reg add HKCU\Environment /v PATH /t REG_EXPAND_SZ /d "%USER_PATH%;%TARGET_DIR%" /f
    echo Added %TARGET_DIR% to PATH
    echo Please restart your command prompt or terminal
) else (
    echo %TARGET_DIR% is already in PATH
)
pause
EOF
    else
        # Unix shell scripts
        cat > "$OUTPUT_DIR/$platform/scripts/open-web.sh" << 'EOF'
#!/bin/bash
echo "Starting Peglin Save Explorer Web Interface..."
cd "$(dirname "$0")/.."
./peglin-save-explorer web --open
EOF
        chmod +x "$OUTPUT_DIR/$platform/scripts/open-web.sh"
        
        cat > "$OUTPUT_DIR/$platform/scripts/install-to-path.sh" << 'EOF'
#!/bin/bash
echo "Installing Peglin Save Explorer to PATH..."

TARGET_DIR="$HOME/.local/bin"
BINARY_NAME="peglin-save-explorer"
SOURCE_BINARY="$(dirname "$0")/../peglin-save-explorer"

# Create target directory if it doesn't exist
mkdir -p "$TARGET_DIR"

# Copy binary
cp "$SOURCE_BINARY" "$TARGET_DIR/$BINARY_NAME"
chmod +x "$TARGET_DIR/$BINARY_NAME"

echo "Installed $BINARY_NAME to $TARGET_DIR"

# Check if directory is in PATH
if [[ ":$PATH:" != *":$TARGET_DIR:"* ]]; then
    echo "Adding $TARGET_DIR to PATH..."
    
    # Determine shell configuration file
    if [[ "$SHELL" == *"zsh"* ]]; then
        SHELL_RC="$HOME/.zshrc"
    elif [[ "$SHELL" == *"bash"* ]]; then
        SHELL_RC="$HOME/.bashrc"
    else
        SHELL_RC="$HOME/.profile"
    fi
    
    echo "export PATH=\"\$PATH:$TARGET_DIR\"" >> "$SHELL_RC"
    echo "Added $TARGET_DIR to PATH in $SHELL_RC"
    echo "Please restart your terminal or run: source $SHELL_RC"
else
    echo "$TARGET_DIR is already in PATH"
fi
EOF
        chmod +x "$OUTPUT_DIR/$platform/scripts/install-to-path.sh"
    fi
    
    # Create README
    cat > "$OUTPUT_DIR/$platform/README.txt" << EOF
Peglin Save Explorer - Standalone Distribution
=============================================

Version: $VERSION
Platform: $platform
Built: $(date)

This package contains the Peglin Save Explorer CLI tool and web interface.

QUICK START:
-----------
1. Extract this archive to a folder of your choice
2. Run the executable directly: ./peglin-save-explorer (or peglin-save-explorer.exe on Windows)
3. For web interface: ./scripts/open-web.sh (or scripts/open-web.bat on Windows)

INSTALLATION TO PATH:
--------------------
To use 'peglin-save-explorer' from anywhere in your terminal:
- Linux/Mac: Run ./scripts/install-to-path.sh
- Windows: Run scripts/install-to-path.bat as Administrator

USAGE:
------
CLI Commands:
  peglin-save-explorer --help                    # Show all commands
  peglin-save-explorer analyze <save-file>       # Analyze save file
  peglin-save-explorer extract <peglin-path>     # Extract game data
  peglin-save-explorer web                       # Start web interface
  peglin-save-explorer web --open                # Start web interface and open browser

Web Interface:
  Access at http://localhost:5000 when running 'web' command
  Use scripts/open-web to automatically start and open in browser

REQUIREMENTS:
------------
- No additional dependencies required (self-contained)
- For game data extraction: Peglin installation required

SUPPORT:
--------
GitHub: https://github.com/AgentEnder/peglin-save-explorer
Issues: https://github.com/AgentEnder/peglin-save-explorer/issues
EOF

    # Create version file
    echo "$VERSION" > "$OUTPUT_DIR/$platform/VERSION"
    
    echo "✅ Built $platform successfully"
done

echo ""
echo "🎉 Build completed! Archives created in $OUTPUT_DIR/"
echo ""
echo "📦 Available builds:"
for platform in "${platforms[@]}"; do
    size=$(du -sh "$OUTPUT_DIR/$platform" | cut -f1)
    echo "  - $platform ($size)"
done

echo ""
echo "💡 To create archives:"
echo "  cd $OUTPUT_DIR"
for platform in "${platforms[@]}"; do
    if [[ "$platform" == "win-x64" ]]; then
        echo "  zip -r peglin-save-explorer-$platform.zip $platform/"
    else
        echo "  tar -czf peglin-save-explorer-$platform.tar.gz $platform/"
    fi
done

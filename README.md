# Peglin Save Explorer

A comprehensive CLI tool and web interface for exploring and analyzing Peglin save files, with built-in game data extraction capabilities.

## Installation

### Option 1: Download Pre-built Binaries (Recommended)

Download the latest release for your platform from the [Releases page](https://github.com/AgentEnder/peglin-save-explorer/releases):

- **Windows**: `peglin-save-explorer-win-x64.zip`
- **Linux**: `peglin-save-explorer-linux-x64.tar.gz`
- **macOS Intel**: `peglin-save-explorer-osx-x64.tar.gz`
- **macOS Apple Silicon**: `peglin-save-explorer-osx-arm64.tar.gz`

#### Quick Setup:
1. Extract the archive to your preferred location
2. Run the executable:
   - **Windows**: `peglin-save-explorer.exe`
   - **Linux/macOS**: `./peglin-save-explorer`
3. (Optional) Add to PATH for global access:
   - **Windows**: Run `scripts\install-to-path.bat` as Administrator
   - **Linux/macOS**: Run `./scripts/install-to-path.sh`

### Option 2: Build from Source

#### Prerequisites
- .NET 9.0 SDK
- Node.js 20+ (for web interface)
- Peglin installed via Steam (for game data extraction)

#### Build Steps:
```bash
# Clone the repository
git clone --recursive https://github.com/AgentEnder/peglin-save-explorer.git
cd peglin-save-explorer

# Build all platforms
npm install
npm run build

# Or build for development/current platform only
cd peglin-save-explorer
dotnet build
```

## Features

- 🎮 **Save File Analysis**: Detailed statistics and orb performance metrics
- 🌐 **Web Interface**: Interactive browser-based exploration of save data
- 📊 **Game Data Extraction**: Extract sprites, relics, and game assets
- 🔍 **Search Functionality**: Find specific data within saves
- 💾 **Multiple Output Formats**: JSON, plain text, and formatted views
- 🖥️ **Cross-Platform**: Works on Windows, Linux, and macOS

## Usage

### Quick Start with Web Interface
```bash
# Start web interface and open browser
peglin-save-explorer web --open

# Or use the helper script (from extracted archive)
./scripts/open-web.sh    # Linux/macOS
scripts\open-web.bat     # Windows
```

### Command Line Interface

```bash
# Show help and available commands
peglin-save-explorer --help

# Analyze a save file (auto-detects default location)
peglin-save-explorer analyze

# Specify a custom save file
peglin-save-explorer analyze --save-file "path/to/save.data"

# Extract game data (sprites, relics, etc.)
peglin-save-explorer extract

# View specific run details
peglin-save-explorer view-run --run-id <id>

# Start web interface
peglin-save-explorer web --port 5000 --open
```

#### Available Commands:

- **analyze** - Comprehensive save file analysis
- **extract** - Extract game assets and data
- **view-run** - View detailed run information
- **web** - Start interactive web interface
- **extract-sprites** - Extract sprite assets
- **extract-relics** - Extract relic data
- **analyze-assembly** - Analyze game assembly structure

### Default File Locations

The tool automatically detects these locations:

**Save Files:**
- Windows: `%USERPROFILE%\AppData\LocalLow\Red Nexus Games Inc\Peglin\Save_0.data`
- Linux: `~/.config/unity3d/Red Nexus Games Inc/Peglin/Save_0.data`
- macOS: `~/Library/Application Support/Red Nexus Games Inc/Peglin/Save_0.data`

**Peglin Installation:**
- Steam (Windows): `C:\Program Files (x86)\Steam\steamapps\common\Peglin`
- Steam (Linux): `~/.steam/steam/steamapps/common/Peglin`
- Steam (macOS): `~/Library/Application Support/Steam/steamapps/common/Peglin`

## Web Interface

The web interface provides an intuitive way to explore save data:

- **Run History**: Browse all your runs with filtering and sorting
- **Detailed Statistics**: View comprehensive stats and charts
- **Orb Performance**: Analyze individual orb effectiveness
- **Relic Browser**: Explore all relics and their effects
- **Asset Viewer**: Browse extracted game sprites and assets

Access at `http://localhost:5000` when running the web command.

## Technical Details

- **Serialization**: Uses OdinSerializer for accurate save file parsing
- **Runtime Loading**: Dynamically loads Peglin's assemblies for proper deserialization
- **Asset Extraction**: Utilizes AssetRipper for Unity asset extraction
- **Web Stack**: React frontend with ASP.NET Core backend
- **Cross-Platform**: .NET 9.0 single-file deployments

## Building from Source

### Requirements
- .NET 9.0 SDK
- Node.js 20+
- Git (with submodules support)

### Build Process
```bash
# Clone with submodules
git clone --recursive https://github.com/AgentEnder/peglin-save-explorer.git
cd peglin-save-explorer

# Build for all platforms
npm install
npm run build

# The dist/ folder will contain platform-specific archives
```

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## License

This tool is for personal use only. It loads Peglin's proprietary DLL files from the user's installation at runtime. Do not distribute Peglin's game files with this tool.
# Peglin Save Explorer

A CLI tool for exploring and analyzing Peglin save files. This tool loads Peglin's game DLLs to properly deserialize save data using OdinSerializer.

## Prerequisites

- .NET 8.0 SDK
- Peglin installed via Steam (the tool will automatically find the game installation)

## Features

- Extract player statistics (total runs, wins, damage dealt, coins earned)
- Analyze orb usage and performance metrics
- View detailed combat and economy statistics
- Interactive Terminal.Gui interface for browsing save data
- Search functionality to find specific data within saves
- Support for multiple output formats

## Usage

### Build the project
```bash
cd peglin-save-explorer
dotnet build
```

### Available Commands

1. **Summary** - Show player statistics summary
   ```bash
   dotnet run -- summary -f "C:\Users\{username}\AppData\LocalLow\Red Nexus Games Inc\Peglin\Save_0.data"
   ```

2. **Orbs** - Analyze orb usage and performance
   ```bash
   dotnet run -- orbs -f "path/to/save.data"
   ```

3. **Stats** - Show detailed player statistics
   ```bash
   dotnet run -- stats -f "path/to/save.data"
   ```

4. **Search** - Search for specific data in save file
   ```bash
   dotnet run -- search -f "path/to/save.data" -q "search term"
   ```

5. **Interactive** - Start Terminal.Gui interactive mode
   ```bash
   dotnet run -- interactive -f "path/to/save.data"
   ```
   Use the menu or keyboard shortcuts (F1-F3) to navigate between views.

6. **Dump** - Dump raw save file structure (for debugging)
   ```bash
   dotnet run -- dump -f "path/to/save.data"
   ```

### Save File Location

Peglin save files are typically located at:
```
%USERPROFILE%\AppData\LocalLow\Red Nexus Games Inc\Peglin\Save_0.data
```

## How It Works

The tool uses the official OdinSerializer library (integrated as a git submodule) and loads Peglin's Assembly-CSharp.dll from your game installation to properly deserialize save data. This ensures accurate parsing of all game data structures.

The tool will automatically search for Peglin in common Steam installation locations:
- `G:\SteamLibrary\steamapps\common\Peglin`
- `C:\Program Files (x86)\Steam\steamapps\common\Peglin`
- `C:\Program Files\Steam\steamapps\common\Peglin`

## Technical Details

- Uses OdinSerializer for binary deserialization
- Loads game types from Peglin's Assembly-CSharp.dll at runtime
- Handles missing Unity dependencies gracefully
- Terminal.Gui v2 for the interactive interface

## Distribution Note

When distributing this tool, do not include Peglin's DLL files as they are proprietary. The tool will load them from the user's Peglin installation at runtime.
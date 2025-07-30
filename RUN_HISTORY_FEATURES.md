# Run History Features

## ✅ Implemented Features

### 1. Run History Analysis & Extraction
**RunHistoryManager.cs** - Comprehensive run data parsing:
- Searches multiple potential locations in save files for run history data
- Supports various data formats and structures
- Handles missing data gracefully with fallback reconstruction
- Extracts detailed run information including:
  - Timestamp, win/loss status, score, damage dealt
  - Character class, duration, final level, coins earned
  - Orbs and relics used in each run
  - Game seed (when available)

### 2. Terminal.Gui Integration
**Enhanced TerminalGuiSession.cs**:
- **Run History Viewer**: New "Run History" menu item under View
- **Export Dialog**: File → Export Run History with default file naming
- **Import Dialog**: File → Import Run History with validation
- **Responsive Layout**: All dialogs adapt to terminal size
- **Data Display**: Formatted run listings with sorting indicators

### 3. CLI Commands
**New `runs` command** in Program.cs:
```bash
# View run history
dotnet run runs

# Export run history to file
dotnet run runs --export runs_backup.json

# Import run history from file  
dotnet run runs --import runs_backup.json

# Import with save file update attempt (experimental)
dotnet run runs --import runs_backup.json --update-save
```

### 4. Data Models
**RunRecord Class** with comprehensive fields:
- **Core Data**: ID, timestamp, won/lost, score, damage
- **Gameplay Stats**: pegs hit, duration, character class, final level
- **Economy**: coins earned
- **Items**: orbs used, relics used
- **Meta**: seed, reconstruction flag

**RunHistoryExport Class** for structured JSON export/import

## Technical Implementation

### Save File Analysis Approach
```csharp
// Multiple search locations for run data
var runSources = new[]
{
    data["runHistory"],
    data["completedRuns"], 
    data["gameHistory"],
    data["sessionHistory"],
    data["runs"],
    data["PermanentStats"]?["Value"]?["runHistory"],
    // ... additional locations
};
```

### Fallback Reconstruction
When detailed run history isn't available, the system can reconstruct basic records from aggregate statistics:
```csharp
// Create placeholder records based on total runs/wins
for (int i = 0; i < totalRuns; i++)
{
    runs.Add(new RunRecord
    {
        Id = $"reconstructed_{i}",
        Won = i < totalWins, // Assume recent runs were wins
        IsReconstructed = true
    });
}
```

### Export/Import Format
JSON structure with metadata:
```json
{
  "ExportedAt": "2025-01-29T...",
  "TotalRuns": 42,
  "Runs": [
    {
      "Id": "run_001",
      "Timestamp": "2025-01-28T...",
      "Won": true,
      "Score": 1500000,
      "DamageDealt": 25000000,
      "CharacterClass": "Peglin",
      "OrbsUsed": ["Stone", "Bramble", "Dagger"],
      "RelicsUsed": ["Lucky Charm", "Orb Refresh"]
    }
  ]
}
```

## User Experience Features

### Terminal.Gui Interface
- **Adaptive Display**: Run information formatted for available screen width
- **Status Indicators**: Clear WIN/LOSS status with colored formatting
- **Smart Truncation**: Long names truncated with "..." when space limited
- **Sort Instructions**: Built-in help for sorting by date, win, score, time, class
- **Error Handling**: Graceful handling of missing or corrupted data

### CLI Integration
- **Default Behavior**: `runs` command shows recent run history
- **Export Options**: Flexible file path specification with defaults
- **Import Validation**: File existence and format validation
- **Status Feedback**: Clear success/error messages with details

### Data Resilience
- **Multiple Format Support**: Handles various save file versions
- **Graceful Degradation**: Works even with incomplete data
- **Reconstruction Mode**: Creates usable records from aggregate stats
- **Format Detection**: Automatically handles different timestamp formats

## Current Limitations & Future Enhancements

### ⚠️ Known Limitations
1. **Save File Updating**: Import-to-save functionality not yet implemented
2. **Data Discovery**: May not find run history in all save file formats
3. **Partial Reconstruction**: Reconstructed runs have limited detail

### 🔮 Future Enhancements
1. **Save File Writing**: Complete implementation of save file updates
2. **Advanced Filtering**: Filter runs by date, class, win/loss status
3. **Statistics Analysis**: Trends, win rate over time, performance metrics
4. **Run Comparison**: Side-by-side comparison of different runs
5. **Data Visualization**: Charts and graphs for run history trends

## Testing Recommendations

### Terminal.Gui Testing
1. **Run History View**: Access via View → Run History
2. **Export Feature**: File → Export Run History, test file creation
3. **Import Feature**: File → Import Run History, test with exported file
4. **Responsive Layout**: Test with different terminal sizes

### CLI Testing  
1. **Basic View**: `dotnet run runs`
2. **Export Test**: `dotnet run runs --export my_runs.json`
3. **Import Test**: `dotnet run runs --import my_runs.json`
4. **Error Handling**: Test with invalid files and paths

### Data Format Testing
1. **Different Save Files**: Test with various Peglin save file versions
2. **Missing Data**: Test behavior with incomplete save files
3. **Large Datasets**: Test performance with many runs
4. **Edge Cases**: Empty files, corrupted data, unusual formats

The run history system provides a solid foundation for tracking and managing Peglin gameplay data, with room for future enhancements in save file modification and advanced analytics.
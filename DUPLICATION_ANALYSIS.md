# Code Duplication Analysis

## Executive Summary

After analyzing the run details command (`ViewRunCommand`), run history manager (`RunHistoryManager`), and interactive console session (`ConsoleSession`), I've identified significant duplication across several key areas that can be abstracted into shared components.

## Key Areas of Duplication

### 1. **Run Data Loading & File Path Resolution** 
**Locations:**
- `ViewRunCommand.LoadRunHistoryData()` (lines 522-577)
- `RunHistoryCommand.LoadRunHistoryData()` (lines 316-338)
- `ConsoleSession.LoadRunHistoryData()` (lines 445-488)
- `CommandHandlers.HandleRunHistory()` (lines 197-242)

**Duplicated Logic:**
- Stats file path calculation from save file path (`Save_X.data` → `Stats_X.data`)
- File existence checking and error handling
- Save file reading with OdinSerializer
- JSON parsing and run history extraction via RunHistoryManager
- Debug logging and error reporting

**Abstraction Opportunity:** Create a `RunDataService` class to centralize all run data loading logic.

### 2. **Run Display Formatting**
**Locations:**
- `ViewRunCommand.Execute()` (lines 89-500+) - Comprehensive console display
- `ConsoleSession.ShowRunDetails()` (lines 581-715) - Widget-based display  
- `RunHistoryCommand.DisplayRunHistorySummary()` (lines 210-266) - Summary table
- `CommandHandlers.HandleRunHistory()` (lines 217-240) - Simple list format

**Duplicated Logic:**
- Run status formatting ("WIN"/"LOSS", "WON"/"LOST")
- Character class name truncation/formatting
- Duration formatting (minutes, "Unknown", "--")
- Damage number formatting with thousands separators
- Health display (`FinalHp/MaxHp` formatting)
- Date/time formatting consistency

**Abstraction Opportunity:** Create a `RunDisplayFormatter` utility class with methods for consistent formatting.

### 3. **Game Data Integration**
**Locations:**
- `ViewRunCommand.Execute()` (lines 54-68) - GameDataMappings + RelicCache setup
- `RunHistoryCommand.Execute()` (lines 79-95) - Similar setup pattern
- `ConsoleSession.ShowRunDetails()` (lines 581+) - Inline boss/room name resolution

**Duplicated Logic:**
- Peglin path configuration loading
- GameDataMappings initialization with error handling
- RelicMappingCache loading and updating
- Boss/room name resolution with fallbacks
- Warning/debug logging for data loading failures

**Abstraction Opportunity:** Create a `GameDataService` that handles all game data initialization and provides clean APIs for name resolution.

### 4. **Run Statistics Display Sections**
**Locations:**
- `ViewRunCommand.Execute()` (lines 100-320) - Console sections
- `ConsoleSession.ShowRunDetails()` (lines 600-715) - Widget sections

**Duplicated Sections:**
- Basic run info (date, result, class, cruciball level)
- Combat statistics (damage, shots, bombs, pegs)
- Health and duration display
- Relics collection formatting
- Bosses defeated listing
- Room statistics and timeline
- Status effects display
- Orbs used listing

**Abstraction Opportunity:** Create modular `RunSectionRenderer` classes that can output to both console and widget formats.

### 5. **Error Handling & Logging Patterns**
**Locations:**
- All files show similar try/catch patterns
- Consistent Logger.Debug/Warning/Error usage
- Similar "file not found" and "no data" messaging

**Duplicated Patterns:**
- File loading error handling with user-friendly messages
- Assembly loading warnings (for game data)
- Missing data graceful fallbacks
- Debug logging for data parsing steps

## Proposed Abstraction Architecture

### Core Services Layer

```csharp
// Centralized run data loading
public class RunDataService
{
    public static List<RunRecord> LoadRunHistory(FileInfo? file, ConfigurationManager config)
    public static string GetStatsFilePath(string saveFilePath)
    public static RunRecord? GetRunByIndex(List<RunRecord> runs, int index)
}

// Game data management
public class GameDataService
{
    public static void InitializeGameData(string? peglinPath)
    public static void EnsureRelicCache(string? peglinPath)
    public static RelicMappingCache LoadRelicCache()
}

// Display formatting utilities
public class RunDisplayFormatter
{
    public static string FormatRunStatus(bool won)
    public static string FormatDuration(TimeSpan duration)
    public static string FormatHealth(int finalHp, int maxHp)
    public static string FormatCharacterClass(string className, int maxLength = 10)
    public static string FormatDamage(long damage)
    public static string FormatDateTime(DateTime timestamp)
    public static string FormatCruciballLevel(int level)
}
```

### Rendering System

```csharp
// Abstract base for run display
public abstract class RunRenderer
{
    protected abstract void WriteHeader(string title);
    protected abstract void WriteSection(string sectionTitle);
    protected abstract void WriteItem(string label, string value);
    protected abstract void WriteList(string title, IEnumerable<string> items);
    
    public void RenderRun(RunRecord run)
    {
        RenderBasicInfo(run);
        RenderCombatStats(run);
        RenderCollections(run);
        // ... etc
    }
}

// Console implementation
public class ConsoleRunRenderer : RunRenderer { ... }

// Widget implementation  
public class WidgetRunRenderer : RunRenderer { ... }
```

### Configuration Integration

```csharp
// Centralized configuration for run display
public class RunDisplayConfig
{
    public bool ShowEnhancedStats { get; set; } = true;
    public bool ShowDebugInfo { get; set; } = false;
    public int MaxRelicsToShow { get; set; } = 50;
    public int MaxOrbsToShow { get; set; } = 50;
    public TimeFormat PreferredTimeFormat { get; set; } = TimeFormat.Short;
}
```

## Concrete Examples of Duplication

### Example 1: GetStatsFilePath Method (Exact Duplication)

**Found in 3 locations:**
- `ViewRunCommand.cs` lines 566-577
- `RunHistoryCommand.cs` lines 323-335  
- `ConsoleSession.cs` lines 469-481

**Duplicated Code:**
```csharp
private static string GetStatsFilePath(string saveFilePath)
{
    // Stats file has same name pattern but with Stats_ prefix
    var saveFileName = Path.GetFileName(saveFilePath);
    if (saveFileName.StartsWith("Save_") && saveFileName.EndsWith(".data"))
    {
        var saveNumber = saveFileName.Substring(5, saveFileName.Length - 10);
        var statsFileName = $"Stats_{saveNumber}.data";
        var saveDir = Path.GetDirectoryName(saveFilePath);
        return Path.Combine(saveDir ?? "", statsFileName);
    }
    return "";
}
```

### Example 2: Run Status Formatting (Consistent Pattern)

**Found in 4+ locations with identical logic:**
```csharp
// Pattern repeated everywhere:
var status = run.Won ? "WIN" : "LOSS";
// or
run.Won ? "WIN" : "LOSS"
```

### Example 3: Game Data Service Initialization (Near-Identical)

**Pattern repeated in ViewRunCommand, RunHistoryCommand, ConsoleSession:**
```csharp
// Load game data mappings
var peglinPath = configManager.GetEffectivePeglinPath();
if (!string.IsNullOrEmpty(peglinPath))
{
    Logger.Debug($"Loading game data from: {peglinPath}");
    GameDataMappings.LoadGameDataMappings(peglinPath);
}
else
{
    Logger.Warning("No Peglin path configured, using fallback mappings");
    GameDataMappings.LoadGameDataMappings(null);
}

// Ensure relic cache is up to date
try
{
    if (!string.IsNullOrEmpty(peglinPath))
    {
        RelicMappingCache.EnsureCacheFromAssetRipper(peglinPath);
        Logger.Debug("Relic cache updated for name resolution.");
    }
}
catch (Exception ex)
{
    Logger.Warning($"Could not update relic cache: {ex.Message}");
}
```

### Example 4: Run Data Loading Pattern (Structural Duplication)

**Similar structure in all 4 locations:**
```csharp
// 1. Get save file path
// 2. Calculate stats file path  
var statsFilePath = GetStatsFilePath(saveFilePath);
// 3. Check file existence
if (string.IsNullOrEmpty(statsFilePath))
{
    Logger.Error("Could not determine stats file path...");
    return new List<RunRecord>();
}
if (!File.Exists(statsFilePath))
{
    Logger.Error($"Stats file not found: {statsFilePath}");
    return new List<RunRecord>();
}
// 4. Load and parse file
Logger.Debug($"Loading run history from: {statsFilePath}");
var statsBytes = File.ReadAllBytes(statsFilePath);
var dumper = new SaveFileDumper(configManager);
var statsJson = dumper.DumpSaveFile(statsBytes);
var statsData = JObject.Parse(statsJson);
var runs = runHistoryManager.ExtractRunHistory(statsData);
```

## Implementation Priority

1. **High Priority - RunDataService**: Eliminate the most critical duplication in file loading
2. **Medium Priority - RunDisplayFormatter**: Standardize formatting across all displays
3. **Medium Priority - GameDataService**: Centralize game data management
4. **Low Priority - RunRenderer**: Abstract display logic (larger refactor)

## Benefits

- **Consistency**: All run displays will have identical formatting
- **Maintainability**: Single location for data loading and display logic
- **Testing**: Isolated services are easier to unit test
- **Extensibility**: New display formats can reuse existing services
- **Error Handling**: Centralized error patterns improve reliability

## Files to Refactor

1. `src/Commands/ViewRunCommand.cs` - Use new services
2. `src/Commands/RunHistoryCommand.cs` - Use new services
3. `src/UI/ConsoleSession.cs` - Use new services
4. `CommandHandlers.cs` - Use new services (if still in use)
5. Create new files:
   - `src/Services/RunDataService.cs`
   - `src/Services/GameDataService.cs` 
   - `src/Utils/RunDisplayFormatter.cs`
   - `src/Rendering/RunRenderer.cs` (future)

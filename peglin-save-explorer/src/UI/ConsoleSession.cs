using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using peglin_save_explorer.Core;
using peglin_save_explorer.Data;

namespace peglin_save_explorer.UI
{
    public class ConsoleSession
    {
        private JObject? saveData;
        private FileInfo? fileInfo;
        private string fileName;
        private JObject? data;
        private JObject? statsData;
        private JArray? orbData;
        private readonly ConfigurationManager configManager;
        private readonly RunHistoryManager runHistoryManager;
        private readonly RelicMappingCache relicCache;
        private bool isRunning = true;

        public ConsoleSession(JObject? saveData, FileInfo? fileInfo)
        {
            this.saveData = saveData;
            this.fileInfo = fileInfo;
            this.fileName = fileInfo?.Name ?? "Unknown";
            this.configManager = new ConfigurationManager();
            this.runHistoryManager = new RunHistoryManager(configManager);
            this.relicCache = new RelicMappingCache();

            // Load relic cache for name resolution
            try
            {
                // First try to get/regenerate the cache from AssetRipper extraction
                var peglinPath = this.configManager.GetEffectivePeglinPath();
                if (!string.IsNullOrEmpty(peglinPath))
                {
                    RelicMappingCache.EnsureCacheFromAssetRipper(peglinPath);
                }
                
                // Now load the refreshed cache into our instance
                this.relicCache.LoadFromDisk();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load relic cache: {ex.Message}");
            }

            if (saveData != null)
            {
                // Try to extract data using both possible paths for compatibility
                this.data = saveData["peglinData"] as JObject ?? saveData["data"] as JObject;
                this.statsData = this.data?["PermanentStats"]?["Value"] as JObject;
                this.orbData = this.statsData?["historicOrbPlayData"] as JArray;
            }
        }

        public void Run()
        {
            // Load save data if we haven't already
            if (saveData == null)
            {
                // If no fileInfo provided, get the effective save file path and create FileInfo
                if (fileInfo == null)
                {
                    var defaultPath = configManager.GetEffectiveSaveFilePath();
                    if (!string.IsNullOrEmpty(defaultPath) && File.Exists(defaultPath))
                    {
                        fileInfo = new FileInfo(defaultPath);
                    }
                }

                saveData = SaveDataLoader.LoadSaveData(fileInfo);
                if (saveData == null)
                {
                    Console.WriteLine("Failed to load save data.");
                    return;
                }

                // Update fileName from fileInfo
                if (fileInfo != null)
                {
                    fileName = fileInfo.Name;
                }

                // Extract data after loading
                this.data = saveData["peglinData"] as JObject ?? saveData["data"] as JObject;
                this.statsData = this.data?["PermanentStats"]?["Value"] as JObject;
                this.orbData = this.statsData?["historicOrbPlayData"] as JArray;
            }

            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // Console.Clear() can fail in some environments (like when input is piped)
                // Just continue without clearing
            }

            while (isRunning)
            {
                ShowMainMenu();
            }
        }


        private void ShowMainMenu()
        {
            AutocompleteMenuItem? selectedItem = null;

            try
            {
                // Try to use the advanced widget system first
                using var widgetManager = new WidgetManager();

                // Add header widget
                var header = new HeaderWidget(fileName, DateTime.Now);
                header.X = 0;
                header.Y = 0;
                widgetManager.AddWidget(header);

                // Add autocomplete widget
                var menuItems = new List<AutocompleteMenuItem>
                {
                    new("Show Summary - Player statistics overview", "summary"),
                    new("Analyze Orbs - Orb usage and performance data", "orbs"),
                    new("View Statistics - Detailed game statistics", "stats"),
                    new("Run History - View and analyze past runs", "runhistory"),
                    new("Search Data - Search through save file data", "search"),
                    new("Settings - Configure application settings", "settings"),
                    new("Exit - Quit the application", "exit")
                };

                var menu = new AutocompleteWidget(menuItems, "Main Menu - Select an option:");
                menu.X = 0;
                menu.Y = 8; // After header
                widgetManager.AddWidget(menu);

                // Run the widget system
                widgetManager.Run();

                selectedItem = menu.GetSelectedItem();
                if (selectedItem == null)
                {
                    // User pressed Escape, treat as exit
                    isRunning = false;
                    return;
                }
            }
            catch (Exception ex)
            {
                // Widget system failed, fall back to simple menu
                Console.WriteLine($"Widget system failed: {ex.Message}");
                // exit with code 1 to indicate failure
                System.Environment.Exit(1);
            }

            switch (selectedItem.Value)
            {
                case "summary":
                    ShowSummary();
                    break;
                case "orbs":
                    ShowOrbs();
                    break;
                case "stats":
                    ShowStats();
                    break;
                case "runhistory":
                    ShowRunHistory();
                    break;
                case "search":
                    ShowSearch();
                    break;
                case "settings":
                    ShowSettings();
                    break;
                case "exit":
                    isRunning = false;
                    break;
                default:
                    Console.WriteLine("Invalid selection. Please try again.");
                    break;
            }
        }

        private void ShowSummary()
        {
            using var widgetManager = new WidgetManager();

            // Create data display widget for summary (no separate header to save space)
            var dataWidget = new DataDisplayWidget($"Player Summary - {fileName}");
            dataWidget.X = 0;
            dataWidget.Y = 0; // Start at top to maximize space

            if (statsData != null)
            {
                dataWidget.AddSection("Player Statistics:");
                dataWidget.AddItem("  Total Runs", statsData["totalRunsStarted"]?.Value<int>() ?? 0);
                dataWidget.AddItem("  Wins", statsData["totalWins"]?.Value<int>() ?? 0);
                dataWidget.AddItem("  Total Damage Dealt", (statsData["totalDamageDealt"]?.Value<long>() ?? 0).ToString("N0"));
                dataWidget.AddItem("  Total Enemies Defeated", statsData["totalEnemiesDefeated"]?.Value<int>() ?? 0);
                dataWidget.AddItem("  Total Pegs Hit", statsData["totalPegsHit"]?.Value<int>() ?? 0);
                dataWidget.AddItem("  Orbs Collected", statsData["totalOrbsCollected"]?.Value<int>() ?? 0);
            }
            else
            {
                dataWidget.AddItem("No statistical data found in save file.", "");
            }

            widgetManager.AddWidget(dataWidget);

            // Run the widget system
            widgetManager.Run();
        }

        private void ShowOrbs()
        {
            using var widgetManager = new WidgetManager();

            // Create data display widget for orbs (no separate header to save space)
            var dataWidget = new DataDisplayWidget($"Orb Analysis - {fileName}");
            dataWidget.X = 0;
            dataWidget.Y = 0; // Start at top to maximize space

            if (orbData != null && orbData.Count > 0)
            {
                dataWidget.AddSection("Orb Usage Statistics:");
                dataWidget.AddEmptyLine();

                foreach (var orb in orbData.Take(20)) // Limit to first 20 orbs
                {
                    var orbName = orb["name"]?.Value<string>() ?? "Unknown";
                    var damageDealt = orb["damageDealt"]?.Value<long>() ?? 0;
                    var timesFired = orb["timesFired"]?.Value<int>() ?? 0;
                    var timesDiscarded = orb["timesDiscarded"]?.Value<int>() ?? 0;
                    var timesRemoved = orb["timesRemoved"]?.Value<int>() ?? 0;
                    var highestCruciballBeat = orb["highestCruciballBeat"]?.Value<int>() ?? 0;
                    var amountInDeck = orb["amountInDeck"]?.Value<int>() ?? 0;

                    dataWidget.AddSection($"{orbName}");
                    dataWidget.AddItem($"    Damage Dealt", damageDealt.ToString("N0"));
                    dataWidget.AddItem($"    Times Fired", timesFired.ToString("N0"));
                    dataWidget.AddItem($"    Times Discarded", timesDiscarded.ToString("N0"));
                    dataWidget.AddItem($"    Times Removed", timesRemoved.ToString("N0"));
                    dataWidget.AddItem($"    Highest Cruciball Beat", highestCruciballBeat.ToString());
                    dataWidget.AddItem($"    Amount in Deck", amountInDeck.ToString());
                    dataWidget.AddEmptyLine();
                }
            }
            else
            {
                dataWidget.AddItem("No orb data found in save file.", "");
            }

            widgetManager.AddWidget(dataWidget);

            // Run the widget system
            widgetManager.Run();
        }

        private void ShowStats()
        {
            using var widgetManager = new WidgetManager();

            // Create data display widget for detailed stats (no separate header to save space)
            var dataWidget = new DataDisplayWidget($"Detailed Statistics - {fileName}");
            dataWidget.X = 0;
            dataWidget.Y = 0; // Start at top to maximize space

            if (statsData != null)
            {
                dataWidget.AddSection("Detailed Game Statistics:");
                dataWidget.AddEmptyLine();

                // Show all available stats
                foreach (var stat in statsData.Properties().Take(30)) // Limit output
                {
                    var value = stat.Value;
                    if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
                    {
                        dataWidget.AddItem($"  {stat.Name}", value.ToString());
                    }
                    else if (value.Type == JTokenType.String && !string.IsNullOrEmpty(value.Value<string>()))
                    {
                        dataWidget.AddItem($"  {stat.Name}", value.ToString());
                    }
                }
            }
            else
            {
                dataWidget.AddItem("No statistical data found in save file.", "");
            }

            widgetManager.AddWidget(dataWidget);

            // Run the widget system
            widgetManager.Run();
        }

        private void ShowRunHistory()
        {
            var runs = LoadRunHistoryData();

            if (runs.Count == 0)
            {
                using var widgetManager = new WidgetManager();

                // Create data display widget for no runs message (no separate header to save space)
                var dataWidget = new DataDisplayWidget($"Run History - {fileName}");
                dataWidget.X = 0;
                dataWidget.Y = 0; // Start at top to maximize space
                dataWidget.AddItem("No run history data found.", "");
                dataWidget.AddItem("This might be normal for older save formats or if no stats file is available.", "");

                widgetManager.AddWidget(dataWidget);
                widgetManager.Run();
            }
            else
            {
                // Create menu items for runs and options
                var menuItems = new List<AutocompleteMenuItem>();

                // Add individual runs
                foreach (var run in runs.Take(20))
                {
                    var status = run.Won ? "WIN" : "LOSS";
                    var className = run.CharacterClass.Length > 10 ? run.CharacterClass.Substring(0, 10) : run.CharacterClass;
                    var duration = run.Duration.TotalMinutes > 0 ? $"{run.Duration.TotalMinutes:F0}m" : "--";
                    var runDisplay = $"{run.Timestamp:MM/dd HH:mm} | {status.PadRight(4)} | {className.PadRight(10)} | {run.DamageDealt.ToString("N0").PadLeft(8)} dmg | {duration.PadLeft(3)}";

                    menuItems.Add(new AutocompleteMenuItem(runDisplay, "run", run));
                }

                // Add separator and filter options
                menuItems.Add(new AutocompleteMenuItem("--- View Options ---", "separator"));
                menuItems.Add(new AutocompleteMenuItem("Show All Runs - View complete run history", "all-runs"));
                menuItems.Add(new AutocompleteMenuItem("Filter by Wins Only - Show only successful runs", "wins-only"));
                menuItems.Add(new AutocompleteMenuItem("Filter by Losses Only - Show only failed runs", "losses-only"));
                menuItems.Add(new AutocompleteMenuItem("Return to Main Menu - Go back to main menu", "return"));

                var selectedItem = ShowWidgetMenu(menuItems, $"Run History ({runs.Count} runs) - Select an option:");

                if (selectedItem == null || selectedItem.Value == "return")
                {
                    // User pressed Escape or selected return
                    return; // Exit the method, which will return to main menu
                }

                switch (selectedItem.Value)
                {
                    case "run":
                        if (selectedItem.Data is RunRecord selectedRun)
                        {
                            ShowRunDetails(selectedRun);
                        }
                        break;
                    case "all-runs":
                        ShowAllRuns(runs);
                        break;
                    case "wins-only":
                        ShowFilteredRuns(runs.Where(r => r.Won).ToList(), "Wins");
                        break;
                    case "losses-only":
                        ShowFilteredRuns(runs.Where(r => !r.Won).ToList(), "Losses");
                        break;
                    case "separator":
                        // Ignore separator selection
                        break;
                }
            }
        }

        private void ShowAllRuns(List<RunRecord> runs)
        {
            ShowRunListWidget(runs, $"All Runs ({runs.Count} total)");
        }

        private void ShowFilteredRuns(List<RunRecord> runs, string filterName)
        {
            ShowRunListWidget(runs, $"{filterName} ({runs.Count} total)");
        }

        private void ShowRunListWidget(List<RunRecord> runs, string title)
        {
            using var widgetManager = new WidgetManager();

            // Create text display widget for run list (no separate header to save space)
            var textWidget = new TextDisplayWidget($"{title} - {fileName}", new List<string>());
            textWidget.X = 0;
            textWidget.Y = 0; // Start at top to maximize space

            // Add header row
            textWidget.AddLine("Date/Time        | Result | Class      | Cruci | Damage     | HP    | Duration");
            textWidget.AddLine("─────────────────┼────────┼────────────┼───────┼────────────┼───────┼─────────");

            // Add run data
            foreach (var run in runs)
            {
                var status = run.Won ? "WIN" : "LOSS";
                var className = run.CharacterClass.Length > 10 ? run.CharacterClass.Substring(0, 10) : run.CharacterClass;
                var cruci = run.CruciballLevel > 0 ? $"C{run.CruciballLevel}" : "C0";
                var duration = run.Duration.TotalMinutes > 0 ? $"{run.Duration.TotalMinutes:F0}m" : "--";
                var hp = run.MaxHp > 0 ? $"{run.FinalHp}/{run.MaxHp}" : "--";

                textWidget.AddLine($"{run.Timestamp:MM/dd HH:mm:ss} | {status.PadRight(6)} | {className.PadRight(10)} | {cruci.PadRight(5)} | {run.DamageDealt.ToString("N0").PadLeft(10)} | {hp.PadLeft(5)} | {duration.PadLeft(7)}");
            }

            widgetManager.AddWidget(textWidget);

            // Run the widget system
            widgetManager.Run();
        }

        private List<RunRecord> LoadRunHistoryData()
        {
            var debugInfo = new List<string>();

            if (saveData == null || fileInfo == null)
            {
                debugInfo.Add("No save data or file info available");
                return new List<RunRecord>();
            }

            try
            {
                var statsFilePath = GetStatsFilePath(fileInfo.FullName);
                debugInfo.Add($"Looking for stats file at: {statsFilePath}");

                if (File.Exists(statsFilePath))
                {
                    debugInfo.Add("Stats file found, reading...");
                    var statsBytes = File.ReadAllBytes(statsFilePath);
                    debugInfo.Add($"Read {statsBytes.Length} bytes from stats file");

                    var dumper = new SaveFileDumper(configManager);
                    var statsJson = dumper.DumpSaveFile(statsBytes);
                    debugInfo.Add($"Converted to JSON, length: {statsJson.Length}");

                    var statsData = JObject.Parse(statsJson);
                    var runs = runHistoryManager.ExtractRunHistory(statsData);
                    debugInfo.Add($"Extracted {runs.Count} runs from stats data");

                    return runs;
                }
                else
                {
                    debugInfo.Add("Stats file not found");
                }
            }
            catch (Exception ex)
            {
                debugInfo.Add($"Error loading run history: {ex.Message}");
                Console.WriteLine("DEBUG: Run history loading failed:");
                foreach (var info in debugInfo)
                {
                    Console.WriteLine($"  {info}");
                }
                Console.WriteLine();
            }

            return new List<RunRecord>();
        }

        private string GetStatsFilePath(string saveFilePath)
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

        private void ShowSearch()
        {
            using var widgetManager = new WidgetManager();

            // Create data display widget for search message (no separate header to save space)
            var dataWidget = new DataDisplayWidget($"Search Data - {fileName}");
            dataWidget.X = 0;
            dataWidget.Y = 0; // Start at top to maximize space
            dataWidget.AddItem("Search functionality not yet implemented in console version.", "");

            widgetManager.AddWidget(dataWidget);

            // Run the widget system
            widgetManager.Run();
        }

        private void ShowSettings()
        {
            var currentPath = configManager.Config.DefaultPeglinInstallPath ?? "Not Set";
            var currentSaveFile = fileInfo?.FullName ?? "Default save file";

            var menuItems = new List<AutocompleteMenuItem>
            {
                new($"Current Peglin Path: {currentPath}", "info"),
                new($"Current Save File: {currentSaveFile}", "info"),
                new("--- Actions ---", "separator"),
                new("Change Peglin Installation Path - Set path to Peglin game files", "peglin-path"),
                new("Load Different Save File - Switch to a different save file", "load-file"),
                new("Return to Main Menu - Go back to main menu", "return")
            };

            var selectedItem = ShowWidgetMenu(menuItems, "Settings - Select an option:");

            if (selectedItem == null || selectedItem.Value == "return")
            {
                // User pressed Escape or selected return
                return; // Will go back to main menu
            }

            switch (selectedItem.Value)
            {
                case "peglin-path":
                    // For now, show a message that this feature needs console input
                    ShowSettingsMessage("Peglin Path Configuration",
                        "This feature requires console input and is not yet implemented in widget mode.\n" +
                        "Please use the fallback console version if you need to change the Peglin path.");
                    break;

                case "load-file":
                    LoadNewSaveFile();
                    return; // LoadNewSaveFile handles its own return to main menu

                case "info":
                case "separator":
                    // Ignore these selections
                    break;
            }
        }

        private void ShowSettingsMessage(string title, string message)
        {
            using var widgetManager = new WidgetManager();

            // Create data display widget for message (no separate header to save space)
            var dataWidget = new DataDisplayWidget($"{title} - {fileName}");
            dataWidget.X = 0;
            dataWidget.Y = 0; // Start at top to maximize space
            dataWidget.AddItem(message, "");

            widgetManager.AddWidget(dataWidget);

            // Run the widget system
            widgetManager.Run();
        }

        private void LoadNewSaveFile()
        {
            // For now, show a message that this feature requires console input
            ShowSettingsMessage("Load Save File",
                "This feature requires console input and is not yet fully implemented in widget mode.\n" +
                "Please restart the application with a different save file path if needed.");
        }

        private AutocompleteMenuItem? ShowWidgetMenu(List<AutocompleteMenuItem> menuItems, string prompt)
        {
            using var widgetManager = new WidgetManager();

            var menu = new AutocompleteWidget(menuItems, prompt);
            menu.X = 0;
            menu.Y = 2;
            widgetManager.AddWidget(menu);

            widgetManager.Run();
            return menu.GetSelectedItem();
        }

        private void ShowRunDetails(RunRecord run)
        {
            using var widgetManager = new WidgetManager();

            // Create data display widget for run details (no separate header widget to save space)
            var dataWidget = new DataDisplayWidget($"Run Details - {fileName}");
            dataWidget.X = 0;
            dataWidget.Y = 0; // Start at top to maximize space

            // Basic run info
            dataWidget.AddItem("Date/Time", run.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            dataWidget.AddItem("Result", run.Won ? "WIN" : "LOSS");
            dataWidget.AddItem("Character Class", run.CharacterClass);
            dataWidget.AddItem("Cruciball Level", run.CruciballLevel.ToString());
            if (!string.IsNullOrEmpty(run.Seed))
            {
                dataWidget.AddItem("Seed", run.Seed);
            }
            dataWidget.AddEmptyLine();

            // Combat statistics section
            dataWidget.AddSection("Combat Statistics:");
            dataWidget.AddItem("  Damage Dealt", run.DamageDealt.ToString("N0"));
            dataWidget.AddItem("  Most Damage in Single Attack", run.MostDamageDealtWithSingleAttack.ToString("N0"));
            dataWidget.AddItem("  Final HP", $"{run.FinalHp}/{run.MaxHp}");
            dataWidget.AddItem("  Damage Negated", run.TotalDamageNegated.ToString("N0"));
            dataWidget.AddItem("  Duration", run.Duration.TotalMinutes > 0 ? $"{run.Duration.TotalMinutes:F1} minutes" : "Unknown");
            dataWidget.AddEmptyLine();

            // Detailed game statistics section
            dataWidget.AddSection("Detailed Statistics:");
            dataWidget.AddItem("  Pegs Hit", $"{run.PegsHit:N0} (Crit: {run.PegsHitCrit}, Refresh: {run.PegsHitRefresh})");
            dataWidget.AddItem("  Shots Taken", $"{run.ShotsTaken} (Crit: {run.CritShotsTaken})");
            dataWidget.AddItem("  Bombs Thrown", $"{run.BombsThrown} (Rigged: {run.BombsThrownRigged})");
            dataWidget.AddItem("  Most Pegs in One Turn", run.MostPegsHitInOneTurn.ToString("N0"));
            dataWidget.AddItem("  Coins", $"Earned: {run.CoinsEarned:N0}, Spent: {Math.Abs(run.CoinsSpent):N0}");
            dataWidget.AddEmptyLine();

            // Relics section
            if (run.RelicNames.Count > 0)
            {
                dataWidget.AddSection("Relics Collected:");
                foreach (var relic in run.RelicNames)
                {
                    // Try to resolve relic name if it's in "Unknown Relic" format
                    var resolvedName = relicCache.ResolveRelicName(relic);
                    dataWidget.AddItem("  • " + resolvedName, "");
                }
                dataWidget.AddEmptyLine();
            }

            // Bosses section
            if (run.BossNames.Count > 0)
            {
                dataWidget.AddSection("Bosses Defeated:");
                foreach (var boss in run.BossNames)
                {
                    dataWidget.AddItem("  • " + boss, "");
                }
                dataWidget.AddEmptyLine();
            }

            // Room statistics section
            if (run.RoomTypeStatistics.Count > 0)
            {
                dataWidget.AddSection("Room Visits:");
                foreach (var kvp in run.RoomTypeStatistics.OrderByDescending(x => x.Value))
                {
                    dataWidget.AddItem($"  {kvp.Key}", kvp.Value.ToString());
                }
                dataWidget.AddEmptyLine();
            }

            // Status effects section
            if (run.ActiveStatusEffects.Count > 0)
            {
                dataWidget.AddSection("Status Effects (Final):");
                foreach (var effect in run.ActiveStatusEffects)
                {
                    dataWidget.AddItem("  • " + effect, "");
                }
                dataWidget.AddEmptyLine();
            }

            // Slime pegs section
            if (run.ActiveSlimePegs.Count > 0)
            {
                dataWidget.AddSection("Slime Pegs (Final):");
                foreach (var slime in run.ActiveSlimePegs)
                {
                    dataWidget.AddItem("  • " + slime, "");
                }
                dataWidget.AddEmptyLine();
            }

            // Orbs used section
            if (run.OrbsUsed.Count > 0)
            {
                dataWidget.AddSection("Orbs Used:");
                foreach (var orb in run.OrbsUsed)
                {
                    dataWidget.AddItem("  • " + orb, "");
                }
                dataWidget.AddEmptyLine();
            }

            // Additional info
            if (!run.Won && !string.IsNullOrEmpty(run.DefeatedBy))
            {
                dataWidget.AddItem("Defeated By", run.DefeatedBy);
                dataWidget.AddEmptyLine();
            }

            if (run.IsCustomRun)
            {
                dataWidget.AddItem("Special", "Custom Run");
            }

            widgetManager.AddWidget(dataWidget);

            // Run the widget system
            widgetManager.Run();
        }
    }
}
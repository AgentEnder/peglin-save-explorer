using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using peglin_save_explorer.Core;
using peglin_save_explorer.Data;
using peglin_save_explorer.Services;
using peglin_save_explorer.UI;
using peglin_save_explorer.Utils;

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

            // Initialize game data using centralized service
            GameDataService.InitializeGameData(configManager);
            
            // Load relic cache using centralized service
            this.relicCache = GameDataService.LoadRelicCache();

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
                dataWidget.AddItem("  Total Damage Dealt", RunDisplayFormatter.FormatDamage(statsData["totalDamageDealt"]?.Value<long>() ?? 0));
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
                    dataWidget.AddItem($"    Damage Dealt", RunDisplayFormatter.FormatDamage(damageDealt));
                    dataWidget.AddItem($"    Times Fired", RunDisplayFormatter.FormatNumber(timesFired));
                    dataWidget.AddItem($"    Times Discarded", RunDisplayFormatter.FormatNumber(timesDiscarded));
                    dataWidget.AddItem($"    Times Removed", RunDisplayFormatter.FormatNumber(timesRemoved));
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
                    var runDisplay = RunDisplayFormatter.FormatRunSummaryLine(run);
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
                            ShowRunDetailsWithActions(selectedRun);
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
            textWidget.AddLine(RunDisplayFormatter.GetDetailedRunListHeader());
            textWidget.AddLine(RunDisplayFormatter.GetDetailedRunListSeparator());

            // Add run data
            foreach (var run in runs)
            {
                textWidget.AddLine(RunDisplayFormatter.FormatRunTableRow(run));
            }

            widgetManager.AddWidget(textWidget);

            // Run the widget system
            widgetManager.Run();
        }

        private List<RunRecord> LoadRunHistoryData()
        {
            return RunDataService.LoadRunHistoryWithDebug(fileInfo, configManager);
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
            dataWidget.AddItem("Result", RunDisplayFormatter.FormatRunStatus(run.Won));
            dataWidget.AddItem("Character Class", run.CharacterClass);
            dataWidget.AddItem("Cruciball Level", run.CruciballLevel.ToString());
            if (!string.IsNullOrEmpty(run.Seed))
            {
                dataWidget.AddItem("Seed", run.Seed);
            }
            dataWidget.AddEmptyLine();

            // Combat statistics section
            dataWidget.AddSection("Combat Statistics:");
            dataWidget.AddItem("  Damage Dealt", RunDisplayFormatter.FormatDamage(run.DamageDealt));
            dataWidget.AddItem("  Most Damage in Single Attack", RunDisplayFormatter.FormatDamage(run.MostDamageDealtWithSingleAttack));
            dataWidget.AddItem("  Final HP", $"{run.FinalHp}/{run.MaxHp}");
            dataWidget.AddItem("  Damage Negated", RunDisplayFormatter.FormatDamage(run.TotalDamageNegated));
            dataWidget.AddItem("  Duration", RunDisplayFormatter.FormatDuration(run.Duration));
            dataWidget.AddEmptyLine();

            // Detailed game statistics section
            dataWidget.AddSection("Detailed Statistics:");
            dataWidget.AddItem("  Pegs Hit", $"{RunDisplayFormatter.FormatNumber(run.PegsHit)} (Crit: {run.PegsHitCrit}, Refresh: {run.PegsHitRefresh})");
            dataWidget.AddItem("  Shots Taken", $"{run.ShotsTaken} (Crit: {run.CritShotsTaken})");
            dataWidget.AddItem("  Bombs Thrown", $"{run.BombsThrown} (Rigged: {run.BombsThrownRigged})");
            dataWidget.AddItem("  Most Pegs in One Turn", RunDisplayFormatter.FormatNumber(run.MostPegsHitInOneTurn));
            dataWidget.AddItem("  Coins", $"Earned: {RunDisplayFormatter.FormatNumber(run.CoinsEarned)}, Spent: {RunDisplayFormatter.FormatNumber(Math.Abs(run.CoinsSpent))}");
            dataWidget.AddEmptyLine();

            // Relics section - use GameDataService for centralized relic mappings
            if (run.RelicNames.Count > 0)
            {
                dataWidget.AddSection($"Relics Collected ({run.RelicNames.Count}):");
                
                foreach (var relicName in run.RelicNames)
                {
                    dataWidget.AddItem("  • " + relicName, "");
                }
                dataWidget.AddEmptyLine();
            }

            // Bosses section
            if (run.BossNames.Count > 0)
            {
                dataWidget.AddSection("Bosses Defeated:");
                foreach (var boss in run.BossNames)
                {
                    // Provide better fallback names for common boss IDs
                    var displayName = boss;
                    if (boss.StartsWith("Unknown Boss (") && boss.EndsWith(")"))
                    {
                        var idStr = boss.Substring(14, boss.Length - 15);
                        if (int.TryParse(idStr, out int bossId))
                        {
                            displayName = GetBetterBossName(bossId);
                        }
                    }
                    dataWidget.AddItem("  • " + displayName, "");
                }
                dataWidget.AddEmptyLine();
            }

            // Room statistics section
            if (run.RoomTypeStatistics.Count > 0)
            {
                dataWidget.AddSection("Room Visits:");
                foreach (var kvp in run.RoomTypeStatistics.OrderByDescending(x => x.Value))
                {
                    // Provide better fallback names for room types
                    var displayName = kvp.Key;
                    if (kvp.Key.StartsWith("Unknown Room (") && kvp.Key.EndsWith(")"))
                    {
                        var idStr = kvp.Key.Substring(14, kvp.Key.Length - 15);
                        if (int.TryParse(idStr, out int roomId))
                        {
                            displayName = GetBetterRoomName(roomId);
                        }
                    }
                    dataWidget.AddItem($"  {displayName}", kvp.Value.ToString());
                }
                dataWidget.AddEmptyLine();
            }

            // Room Timeline section - simple list format
            if (run.VisitedRooms.Length > 0)
            {
                dataWidget.AddSection($"Room Timeline ({run.VisitedRooms.Length} rooms):");
                
                // Simple room list without fancy symbols or formatting
                for (int i = 0; i < Math.Min(run.VisitedRooms.Length, 20); i++)
                {
                    var roomId = run.VisitedRooms[i];
                    var roomName = GameDataMappings.GetRoomName(roomId);
                    dataWidget.AddItem($"  {i + 1}. {roomName}", "");
                }
                
                if (run.VisitedRooms.Length > 20)
                {
                    dataWidget.AddItem($"  ... and {run.VisitedRooms.Length - 20} more rooms", "");
                }
                
                dataWidget.AddEmptyLine();
            }

            // Status effects section - already filtered in GameDataMappings
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

            // Orbs section - show detailed stats if available, fallback to simple list with counts
            if (run.OrbStats.Any())
            {
                dataWidget.AddSection($"Orbs Used ({run.OrbStats.Count}):");
                var sortedOrbs = run.OrbStats.OrderByDescending(kvp => kvp.Value.DamageDealt).ThenBy(kvp => kvp.Value.Name);
                
                foreach (var orbStat in sortedOrbs)
                {
                    var orb = orbStat.Value;
                    dataWidget.AddItem($"  • {orb.Name}", "");
                    if (orb.DamageDealt > 0) dataWidget.AddItem($"    Damage Dealt", RunDisplayFormatter.FormatDamage(orb.DamageDealt));
                    if (orb.TimesFired > 0) dataWidget.AddItem($"    Times Fired", RunDisplayFormatter.FormatNumber(orb.TimesFired));
                    if (orb.AmountInDeck > 0) dataWidget.AddItem($"    Amount in Deck", orb.AmountInDeck.ToString());
                    if (orb.LevelInstances != null && orb.LevelInstances.Any(l => l > 0))
                    {
                        var levelInfo = new List<string>();
                        for (int i = 0; i < orb.LevelInstances.Length; i++)
                        {
                            if (orb.LevelInstances[i] > 0)
                            {
                                levelInfo.Add($"Lvl{i + 1}: {orb.LevelInstances[i]}");
                            }
                        }
                        if (levelInfo.Any())
                        {
                            dataWidget.AddItem($"    Level Distribution", string.Join(", ", levelInfo));
                        }
                    }
                    if (orb.TimesDiscarded > 0) dataWidget.AddItem($"    Times Discarded", RunDisplayFormatter.FormatNumber(orb.TimesDiscarded));
                    if (orb.TimesRemoved > 0) dataWidget.AddItem($"    Times Removed", RunDisplayFormatter.FormatNumber(orb.TimesRemoved));
                    if (orb.Starting) dataWidget.AddItem($"    Starting Orb", "Yes");
                    if (orb.HighestCruciballBeat > 0) dataWidget.AddItem($"    Highest Cruciball Beat", orb.HighestCruciballBeat.ToString());
                }
                dataWidget.AddEmptyLine();
            }
            else if (run.OrbsUsed.Count > 0)
            {
                dataWidget.AddSection($"Orbs Used ({run.OrbsUsed.Count}):");
                // Group orbs by name and count usage - exactly like ViewRunCommand
                var orbUsage = run.OrbsUsed
                    .GroupBy(orb => orb)
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key);
                    
                foreach (var group in orbUsage)
                {
                    var orbName = group.Key;
                    var count = group.Count();
                    if (count > 1)
                    {
                        dataWidget.AddItem($"  • {orbName}", $"(used {count} times)");
                    }
                    else
                    {
                        dataWidget.AddItem($"  • {orbName}", "");
                    }
                }
                dataWidget.AddEmptyLine();
            }
            else
            {
                dataWidget.AddSection("Orbs Used (0):");
                dataWidget.AddItem("  None", "");
                dataWidget.AddEmptyLine();
            }

            // Enhanced Status Effects section with stacks
            if (run.StacksPerStatusEffect.Any())
            {
                dataWidget.AddSection($"Detailed Status Effects ({run.StacksPerStatusEffect.Count}):");
                var sortedEffects = run.StacksPerStatusEffect.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key);
                
                foreach (var effect in sortedEffects)
                {
                    dataWidget.AddItem($"  • {effect.Key}", $"{effect.Value} stacks");
                }
                dataWidget.AddEmptyLine();
            }

            // Enemy Combat Data section  
            if (run.EnemyData.Any())
            {
                dataWidget.AddSection($"Enemy Combat Data ({run.EnemyData.Count}):");
                var sortedEnemies = run.EnemyData.OrderByDescending(kvp => kvp.Value.AmountFought).ThenBy(kvp => kvp.Value.Name);
                
                foreach (var enemyStat in sortedEnemies)
                {
                    var enemy = enemyStat.Value;
                    dataWidget.AddItem($"  • {enemy.Name}", "");
                    if (enemy.AmountFought > 0) dataWidget.AddItem($"    Times Fought", enemy.AmountFought.ToString());
                    if (enemy.MeleeDamageReceived > 0) dataWidget.AddItem($"    Melee Damage Received", RunDisplayFormatter.FormatDamage(enemy.MeleeDamageReceived));
                    if (enemy.RangedDamageReceived > 0) dataWidget.AddItem($"    Ranged Damage Received", RunDisplayFormatter.FormatDamage(enemy.RangedDamageReceived));
                    if (enemy.DefeatedBy) dataWidget.AddItem($"    Defeated By This Enemy", "Yes");
                }
                dataWidget.AddEmptyLine();
            }

            // Slime Peg Statistics
            if (run.SlimePegsPerSlimeType.Any())
            {
                dataWidget.AddSection($"Slime Peg Statistics ({run.SlimePegsPerSlimeType.Count} types):");
                var sortedSlimes = run.SlimePegsPerSlimeType.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key);
                
                foreach (var slime in sortedSlimes)
                {
                    dataWidget.AddItem($"  • {slime.Key}", $"{slime.Value} pegs");
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

        private void ShowRunDetailsWithActions(RunRecord run)
        {
            while (true)
            {
                // First show the run details
                ShowRunDetails(run);

                // Then show action menu
                var menuItems = new List<AutocompleteMenuItem>
                {
                    new("Export Run - Export this run to a JSON file", "export"),
                    new("Import from Save - Import this run from save file to stats", "import-save"),
                    new("Import from File - Import a run from a JSON file", "import-file"),
                    new("Return - Go back to run history", "return")
                };

                var selectedItem = ShowWidgetMenu(menuItems, $"Actions for Run {run.Id?.Substring(0, 8)}:");

                if (selectedItem == null || selectedItem.Value == "return")
                {
                    break;
                }

                switch (selectedItem.Value)
                {
                    case "export":
                        HandleRunExport(run);
                        break;
                    case "import-save":
                        HandleRunImportFromSave(run);
                        break;
                    case "import-file":
                        HandleRunImportFromFile();
                        break;
                }
            }
        }

        private void HandleRunExport(RunRecord run)
        {
            try
            {
                // Prompt for export path
                Console.Clear();
                Console.WriteLine("=== Export Run ===");
                Console.WriteLine($"Exporting run: {run.Id?.Substring(0, 8)} ({run.Timestamp:yyyy-MM-dd HH:mm:ss})");
                Console.WriteLine();
                Console.Write("Enter export file path (or press Enter for default): ");
                
                var exportPath = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(exportPath))
                {
                    exportPath = $"run-export-{run.Id?.Substring(0, 8)}-{DateTime.Now:yyyyMMdd-HHmmss}.json";
                }

                // Get raw data for this specific run
                var rawRunData = GetRawRunDataForSingleRun(run);

                // Create enhanced export format for single run
                var exportData = new
                {
                    exported = DateTime.UtcNow,
                    runs = new[] { run }, // Single run in array format for consistency
                    raw = rawRunData != null ? new[] { rawRunData } : null
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(exportPath, json);
                
                Console.WriteLine();
                Console.WriteLine($"✓ Run exported successfully to: {exportPath}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"✗ Error exporting run: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }

        private void HandleRunImportFromSave(RunRecord run)
        {
            try
            {
                Console.Clear();
                Console.WriteLine("=== Import Run from Save File ===");
                Console.WriteLine($"This will import run: {run.Id?.Substring(0, 8)} from the save file to your stats database");
                Console.WriteLine();
                Console.Write("Are you sure? (y/N): ");
                
                var confirmation = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (confirmation != "y" && confirmation != "yes")
                {
                    Console.WriteLine("Import cancelled.");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                    return;
                }

                // Create a single-run list for import
                var importedRuns = new List<RunRecord> { run };

                // Always update the save file when importing from save
                var runHistoryManager = new RunHistoryManager(configManager);
                HandleSaveFileUpdate(importedRuns, runHistoryManager);

                Console.WriteLine();
                Console.WriteLine($"✓ Successfully imported run '{run.Id?.Substring(0, 8)}' from save file and added to stats database");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"✗ Error importing run from save file: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }

        private void HandleRunImportFromFile()
        {
            try
            {
                Console.Clear();
                Console.WriteLine("=== Import Run from File ===");
                Console.WriteLine("Import a previously exported run from a JSON file");
                Console.WriteLine();
                Console.Write("Enter import file path: ");
                
                var importPath = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(importPath))
                {
                    Console.WriteLine("Import cancelled - no file path provided.");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                    return;
                }

                if (!File.Exists(importPath))
                {
                    Console.WriteLine($"✗ Import file not found: {importPath}");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                    return;
                }

                var jsonContent = File.ReadAllText(importPath);
                var importData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonContent);
                
                var runs = new List<RunRecord>();
                if (importData?.runs != null)
                {
                    foreach (var runToken in importData.runs)
                    {
                        var runRecord = Newtonsoft.Json.JsonConvert.DeserializeObject<RunRecord>(runToken.ToString());
                        if (runRecord != null)
                        {
                            runs.Add(runRecord);
                        }
                    }
                }

                if (runs.Count == 0)
                {
                    Console.WriteLine("✗ No valid runs found in import file.");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"Found {runs.Count} run(s) in import file");
                Console.Write("Import these runs to your stats database? (y/N): ");
                
                var confirmation = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (confirmation != "y" && confirmation != "yes")
                {
                    Console.WriteLine("Import cancelled.");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                    return;
                }

                var runHistoryManager = new RunHistoryManager(configManager);
                HandleSaveFileUpdate(runs, runHistoryManager);

                Console.WriteLine();
                Console.WriteLine($"✓ Successfully imported {runs.Count} run(s) from {importPath}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"✗ Error importing run from file: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }

        private JObject? GetRawRunDataForSingleRun(RunRecord run)
        {
            try
            {
                // For now, we'll export without raw data to keep things simple
                // The run data itself contains all the important information
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not get raw run data: {ex.Message}");
                return null;
            }
        }

        private void HandleSaveFileUpdate(List<RunRecord> runs, RunHistoryManager runHistoryManager)
        {
            try
            {
                // Get the stats file path and update it
                var statsFilePath = RunDataService.GetStatsFilePath(fileInfo?.FullName);
                runHistoryManager.UpdateSaveFileWithRuns(statsFilePath, runs);
                
                Console.WriteLine($"Updated stats file with {runs.Count} run(s)");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update save file: {ex.Message}", ex);
            }
        }

        private string GetBetterBossName(int bossId)
        {
            // Common boss mappings based on typical game progression
            return bossId switch
            {
                0 => "Training Dummy",
                1 => "Forest Guardian",
                2 => "Slime King",
                3 => "Minotaur",
                4 => "Desert Sphinx",
                5 => "Crystal Golem",
                6 => "Shadow Beast",
                7 => "Fire Dragon",
                8 => "Ice Dragon",
                9 => "Ancient Dragon",
                10 => "Final Boss",
                _ => $"Boss {bossId}"
            };
        }

        private string GetBetterRoomName(int roomId)
        {
            // Common room type mappings
            return roomId switch
            {
                0 => "Starting Area",
                1 => "Forest",
                2 => "Mines",
                3 => "Desert",
                4 => "Castle",
                5 => "Caverns",
                6 => "Swamp",
                7 => "Mountain",
                8 => "Final Area",
                _ => $"Area {roomId}"
            };
        }
    }
}
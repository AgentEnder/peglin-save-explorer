using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace peglin_save_explorer
{
    public static class CommandHandlers
    {
        public static void ShowSummary(FileInfo? file)
        {
            var saveData = Program.LoadSaveData(file);
            
            DisplayHelper.PrintFileInfo(file?.Name ?? "Default Save");
            Console.WriteLine();

            try
            {
                if (saveData["peglinDeserializationSuccess"]?.Value<bool>() == true && saveData["peglinData"] != null)
                {
                    var data = saveData["peglinData"] as JObject;
                    DisplayHelper.PrintSectionHeader("PLAYER STATISTICS");
                    
                    PrintBasicStats(data);
                    PrintClassPerformance(data);
                    PrintSaveInfo(data);
                }
                else
                {
                    DisplayHelper.PrintError("Unable to parse save data");
                    return;
                }
            }
            catch (Exception ex)
            {
                DisplayHelper.PrintError($"Error analyzing save data: {ex.Message}");
                return;
            }

            DisplayHelper.PrintInfo("Use 'orbs', 'stats', or 'search' commands for detailed analysis!");
        }

        public static void AnalyzeOrbs(FileInfo? file, int topCount, string sortBy)
        {
            var saveData = Program.LoadSaveData(file);
            var orbData = ExtractOrbData(saveData);
            
            if (orbData == null || !orbData.HasValues)
            {
                DisplayHelper.PrintError("No orb data found in save file");
                return;
            }

            DisplayHelper.PrintOrbAnalysis(topCount, sortBy);
            Console.WriteLine();

            var orbs = ParseOrbStatistics(orbData);
            var sortedOrbs = SortOrbs(orbs, sortBy).Take(topCount);

            foreach (var orb in sortedOrbs)
            {
                var damage = orb.Value<long>("totalDamage");
                var fired = orb.Value<long>("timesFired");
                var efficiency = fired > 0 ? damage / fired : 0;
                var cruciball = orb.Value<int>("highestCruciball");
                var discarded = orb.Value<long>("timesDiscarded");
                var removed = orb.Value<long>("timesRemoved");

                Console.WriteLine($"  {orb.Value<string>("name")}:");
                Console.WriteLine($"    {DisplayHelper.DAMAGE_ICON} Damage: {damage:N0}");
                Console.WriteLine($"    {DisplayHelper.HITS_ICON} Times Fired: {fired:N0}");
                Console.WriteLine($"    {DisplayHelper.EFFICIENCY_ICON} Efficiency: {efficiency:N0} dmg/shot");
                Console.WriteLine($"    {DisplayHelper.TOP_ICON} Highest Cruciball: {cruciball}");
                Console.WriteLine($"    {DisplayHelper.TRASH_ICON} Discarded: {discarded:N0} | Removed: {removed:N0}");

                var recommendation = GetOrbRecommendation(damage, fired, efficiency, cruciball);
                if (!string.IsNullOrEmpty(recommendation))
                {
                    Console.WriteLine($"    {DisplayHelper.TIP_ICON} {recommendation}");
                }
                Console.WriteLine();
            }

            PrintTopEfficiencyOrbs(orbs);
        }

        public static void ShowDetailedStats(FileInfo? file)
        {
            var saveData = Program.LoadSaveData(file);
            var data = saveData["peglinData"] as JObject;
            
            if (data == null)
            {
                DisplayHelper.PrintError("No statistics data found in save file");
                return;
            }

            DisplayHelper.PrintSectionHeader("DETAILED PLAYER STATISTICS");
            Console.WriteLine();

            DisplayHelper.PrintSubHeader("GAMEPLAY STATS");
            PrintGameplayStats(data);

            DisplayHelper.PrintSubHeader("COMBAT STATS");
            PrintCombatStats(data);

            DisplayHelper.PrintSubHeader("PEG STATS");
            PrintPegStats(data);

            DisplayHelper.PrintSubHeader("ECONOMY STATS");
            PrintEconomyStats(data);
        }

        public static void SearchSaveData(FileInfo? file, string query)
        {
            var saveData = Program.LoadSaveData(file);
            DisplayHelper.PrintSearchHeader(query);

            var results = SearchInJToken(saveData, query.ToLower());
            
            if (results.Count == 0)
            {
                DisplayHelper.PrintError($"No results found for '{query}'");
                return;
            }
            else
            {
                DisplayHelper.PrintSuccess($"Found {results.Count} result(s):\n");
            }

            foreach (var result in results.Take(20))
            {
                Console.WriteLine($"Path: {result.Path}");
                Console.WriteLine($"Value: {result.Value}");
                Console.WriteLine($"Type: {result.Type}");
                Console.WriteLine();
            }

            if (results.Count > 20)
            {
                Console.WriteLine($"... and {results.Count - 20} more results (showing first 20)");
            }
        }

        public static void HandleRunHistory(FileInfo? file, string? exportPath, string? importPath, bool updateSave)
        {
            var configManager = new ConfigurationManager();
            var runHistoryManager = new RunHistoryManager(configManager);
            
            // Handle export
            if (!string.IsNullOrEmpty(exportPath))
            {
                var saveData = Program.LoadSaveData(file);
                if (saveData == null) return;
                
                try
                {
                    var runs = runHistoryManager.ExtractRunHistory(saveData);
                    runHistoryManager.ExportRunHistory(runs, exportPath);
                    DisplayHelper.PrintSuccess($"Exported {runs.Count} run(s) to: {exportPath}");
                }
                catch (Exception ex)
                {
                    DisplayHelper.PrintError($"Export failed: {ex.Message}");
                }
                return;
            }
            
            // Handle import
            if (!string.IsNullOrEmpty(importPath))
            {
                try
                {
                    var importedRuns = runHistoryManager.ImportRunHistory(importPath);
                    DisplayHelper.PrintSuccess($"Imported {importedRuns.Count} run(s) from: {importPath}");
                    
                    if (updateSave)
                    {
                        DisplayHelper.PrintError("Save file updating is not yet implemented.");
                        DisplayHelper.PrintInfo("Imported runs are available for viewing only.");
                    }
                    else
                    {
                        DisplayHelper.PrintInfo("Imported runs are available for viewing only.");
                        DisplayHelper.PrintInfo("Use --update-save flag to attempt save file update (experimental).");
                    }
                }
                catch (Exception ex)
                {
                    DisplayHelper.PrintError($"Import failed: {ex.Message}");
                }
                return;
            }
            
            // Default: show run history
            var saveDataForViewing = Program.LoadSaveData(file);
            if (saveDataForViewing == null) return;
            
            try
            {
                var runs = runHistoryManager.ExtractRunHistory(saveDataForViewing);
                
                if (runs.Count == 0)
                {
                    DisplayHelper.PrintInfo("No run history found in save file.");
                    DisplayHelper.PrintInfo("This might be normal for older save formats.");
                    return;
                }
                
                DisplayHelper.PrintSectionHeader("RUN HISTORY");
                Console.WriteLine($"Found {runs.Count} run(s):\n");
                
                foreach (var run in runs.Take(20)) // Show first 20
                {
                    var status = run.Won ? "WIN" : "LOSS";
                    var duration = run.Duration.TotalMinutes > 0 ? $"{run.Duration.TotalMinutes:F0}m" : "--";
                    
                    if (run.IsReconstructed)
                    {
                        Console.WriteLine($"  [Reconstructed] {status} | {run.CharacterClass} | Score: {run.Score:N0}");
                    }
                    else
                    {
                        Console.WriteLine($"  {run.Timestamp:MM/dd HH:mm} | {status} | {run.CharacterClass} | Score: {run.Score:N0} | {duration} | Damage: {run.DamageDealt:N0}");
                        
                        if (run.OrbsUsed.Count > 0)
                        {
                            Console.WriteLine($"    Orbs: {string.Join(", ", run.OrbsUsed.Take(5))}{(run.OrbsUsed.Count > 5 ? "..." : "")}");
                        }
                    }
                    Console.WriteLine();
                }
                
                if (runs.Count > 20)
                {
                    Console.WriteLine($"... and {runs.Count - 20} more runs (showing first 20)");
                }
                
                DisplayHelper.PrintInfo("Use 'interactive' mode for full run history browsing.");
                DisplayHelper.PrintInfo("Use --export to save run history to a file.");
            }
            catch (Exception ex)
            {
                DisplayHelper.PrintError($"Error loading run history: {ex.Message}");
            }
        }
        
        public static void DumpSaveFile(FileInfo? file, string? outputPath)
        {
            var saveData = Program.LoadSaveData(file);
            if (saveData == null) return;
            
            var dumpContent = saveData.ToString();

            if (string.IsNullOrEmpty(outputPath))
            {
                Console.WriteLine(dumpContent);
            }
            else
            {
                var dumpFile = Path.GetFullPath(outputPath);
                File.WriteAllText(dumpFile, dumpContent);
                DisplayHelper.PrintSaveInfo($"Save file dumped to: {dumpFile}");
            }
        }

        private static JObject LoadSaveData(FileInfo file)
        {
            if (!file.Exists)
            {
                throw new FileNotFoundException($"Save file not found: {file.FullName}");
            }

            var dumper = new SaveFileDumper();
            var saveBytes = File.ReadAllBytes(file.FullName);
            var dumpJson = dumper.DumpSaveFile(saveBytes);
            return JObject.Parse(dumpJson);
        }

        private static void PrintBasicStats(JObject? data)
        {
            if (data == null) return;

            var stats = new[]
            {
                ("Games Won", "gamesWon"),
                ("Games Lost", "gamesLost"), 
                ("Total Gold Earned", "goldEarned"),
                ("Total Damage Dealt", "totalDamageDealt"),
                ("Pegs Hit", "pegsHit"),
                ("Shots Fired", "shotsFired")
            };

            foreach (var (label, key) in stats)
            {
                var value = GetNestedValue(data, key);
                if (value != null)
                {
                    Console.WriteLine($"  {label}: {value:N0}");
                }
            }
        }

        private static void PrintClassPerformance(JObject? data)
        {
            if (data == null) return;

            var classStats = GetNestedValue(data, "classStats") as JObject;
            if (classStats != null)
            {
                DisplayHelper.PrintSubHeader("CLASS PERFORMANCE");
                foreach (var kvp in classStats)
                {
                    var className = kvp.Key;
                    var stats = kvp.Value as JObject;
                    if (stats != null)
                    {
                        var wins = stats["wins"]?.Value<int>() ?? 0;
                        var losses = stats["losses"]?.Value<int>() ?? 0;
                        var winRate = wins + losses > 0 ? (double)wins / (wins + losses) * 100 : 0;
                        Console.WriteLine($"  {className}: {wins}W/{losses}L ({winRate:F1}% win rate)");
                    }
                }
            }
        }

        private static void PrintSaveInfo(JObject? data)
        {
            if (data == null) return;

            DisplayHelper.PrintSubHeader("SAVE INFO");
            var saveInfo = new[]
            {
                ("Save Version", "version"),
                ("Last Played", "lastPlayed"),
                ("Playtime Hours", "playtimeHours")
            };

            foreach (var (label, key) in saveInfo)
            {
                var value = GetNestedValue(data, key);
                if (value != null)
                {
                    Console.WriteLine($"  {label}: {value}");
                }
            }
        }

        private static JToken? ExtractOrbData(JObject saveData)
        {
            return saveData["peglinData"]?["orbStats"] ?? saveData["peglinData"]?["orbs"];
        }

        private static IEnumerable<JObject> ParseOrbStatistics(JToken orbData)
        {
            var orbs = new List<JObject>();
            
            if (orbData is JObject orbObject)
            {
                foreach (var kvp in orbObject)
                {
                    if (kvp.Value is JObject orbStats)
                    {
                        orbStats["name"] = kvp.Key;
                        orbs.Add(orbStats);
                    }
                }
            }
            
            return orbs;
        }

        private static IEnumerable<JObject> SortOrbs(IEnumerable<JObject> orbs, string sortBy)
        {
            return sortBy.ToLower() switch
            {
                "damage" => orbs.OrderByDescending(o => o.Value<long>("totalDamage")),
                "usage" => orbs.OrderByDescending(o => o.Value<long>("timesFired")),
                "efficiency" => orbs.OrderByDescending(o => 
                {
                    var damage = o.Value<long>("totalDamage");
                    var fired = o.Value<long>("timesFired");
                    return fired > 0 ? damage / fired : 0;
                }),
                "cruciball" => orbs.OrderByDescending(o => o.Value<int>("highestCruciball")),
                _ => orbs.OrderByDescending(o => o.Value<long>("totalDamage"))
            };
        }

        private static string GetOrbRecommendation(long damage, long fired, long efficiency, int cruciball)
        {
            if (fired < 10) return "Low usage - try using more often";
            if (efficiency > 1000) return "Highly efficient orb - excellent choice!";
            if (cruciball >= 10) return "Master-level cruciball usage!";
            if (damage > 100000) return "Heavy damage dealer!";
            return "";
        }

        private static void PrintTopEfficiencyOrbs(IEnumerable<JObject> orbs)
        {
            var efficientOrbs = orbs
                .Where(o => o.Value<long>("timesFired") >= 20)
                .OrderByDescending(o => 
                {
                    var damage = o.Value<long>("totalDamage");
                    var fired = o.Value<long>("timesFired");
                    return fired > 0 ? damage / fired : 0;
                })
                .Take(3);

            if (efficientOrbs.Any())
            {
                Console.WriteLine($"{DisplayHelper.STAR_ICON} Most efficient orbs (with significant usage):");
                foreach (var orb in efficientOrbs)
                {
                    var name = orb.Value<string>("name");
                    var damage = orb.Value<long>("totalDamage");
                    var fired = orb.Value<long>("timesFired");
                    var efficiency = fired > 0 ? damage / fired : 0;
                    Console.WriteLine($"  {name}: {efficiency:N0} damage per shot");
                }
                Console.WriteLine();
            }
        }

        private static void PrintGameplayStats(JObject? data)
        {
            if (data == null) return;

            var gameplayStats = new[]
            {
                ("Games Played", "gamesPlayed"),
                ("Hours Played", "hoursPlayed"),
                ("Levels Completed", "levelsCompleted"),
                ("Bosses Defeated", "bossesDefeated")
            };

            foreach (var (label, key) in gameplayStats)
            {
                var value = GetNestedValue(data, key);
                if (value != null)
                {
                    Console.WriteLine($"  {label}: {value:N0}");
                }
            }
        }

        private static void PrintCombatStats(JObject? data)
        {
            if (data == null) return;

            var combatStats = new[]
            {
                ("Total Damage", "totalDamage"),
                ("Critical Hits", "criticalHits"),
                ("Enemies Defeated", "enemiesDefeated"),
                ("Highest Single Hit", "highestHit")
            };

            foreach (var (label, key) in combatStats)
            {
                var value = GetNestedValue(data, key);
                if (value != null)
                {
                    Console.WriteLine($"  {label}: {value:N0}");
                }
            }
        }

        private static void PrintPegStats(JObject? data)
        {
            if (data == null) return;

            var pegStats = new[]
            {
                ("Pegs Hit", "pegsHit"),
                ("Perfect Shots", "perfectShots"),
                ("Bank Shots", "bankShots"),
                ("Multiball Activations", "multiballActivations")
            };

            foreach (var (label, key) in pegStats)
            {
                var value = GetNestedValue(data, key);
                if (value != null)
                {
                    Console.WriteLine($"  {label}: {value:N0}");
                }
            }
        }

        private static void PrintEconomyStats(JObject? data)
        {
            if (data == null) return;

            var economyStats = new[]
            {
                ("Gold Earned", "goldEarned"),
                ("Gold Spent", "goldSpent"),
                ("Items Bought", "itemsBought"),
                ("Bombs Used", "bombsUsed")
            };

            foreach (var (label, key) in economyStats)
            {
                var value = GetNestedValue(data, key);
                if (value != null)
                {
                    Console.WriteLine($"  {label}: {value:N0}");
                }
            }
        }

        private static object? GetNestedValue(JObject data, string path)
        {
            try
            {
                var token = data.SelectToken(path);
                return token?.Value<object>();
            }
            catch
            {
                return null;
            }
        }

        private static List<(string Path, object Value, string Type)> SearchInJToken(JToken token, string searchTerm)
        {
            var results = new List<(string Path, object Value, string Type)>();
            SearchInJTokenRecursive(token, searchTerm, "", results);
            return results;
        }

        private static void SearchInJTokenRecursive(JToken token, string searchTerm, string currentPath, List<(string Path, object Value, string Type)> results)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = (JObject)token;
                    foreach (var property in obj.Properties())
                    {
                        var newPath = string.IsNullOrEmpty(currentPath) ? property.Name : $"{currentPath}.{property.Name}";
                        
                        if (property.Name.ToLower().Contains(searchTerm))
                        {
                            results.Add((newPath, property.Value?.ToString() ?? "null", property.Value?.Type.ToString() ?? "null"));
                        }
                        
                        SearchInJTokenRecursive(property.Value, searchTerm, newPath, results);
                    }
                    break;
                    
                case JTokenType.Array:
                    var array = (JArray)token;
                    for (int i = 0; i < array.Count; i++)
                    {
                        var newPath = $"{currentPath}[{i}]";
                        SearchInJTokenRecursive(array[i], searchTerm, newPath, results);
                    }
                    break;
                    
                case JTokenType.String:
                    var stringValue = token.Value<string>();
                    if (stringValue != null && stringValue.ToLower().Contains(searchTerm))
                    {
                        results.Add((currentPath, stringValue, "String"));
                    }
                    break;
            }
        }
        
        public static void HandleRunHistory(FileInfo? file, string export, string import, bool updateSave, string dumpRaw)
        {
            var saveData = Program.LoadSaveData(file);
            if (saveData == null) return;
            
            var configManager = new ConfigurationManager();
            var runHistoryManager = new RunHistoryManager(configManager);
            
            if (!string.IsNullOrEmpty(dumpRaw))
            {
                Console.WriteLine($"Dumping raw run history data to: {dumpRaw}");
                try
                {
                    runHistoryManager.DumpRawRunHistoryData(saveData, dumpRaw);
                    Console.WriteLine("Raw run history data dumped successfully!");
                    Console.WriteLine($"Main dump file: {dumpRaw}");
                    
                    var rawDataPath = Path.Combine(
                        Path.GetDirectoryName(dumpRaw) ?? "",
                        Path.GetFileNameWithoutExtension(dumpRaw) + "_raw.json"
                    );
                    Console.WriteLine($"Raw data file: {rawDataPath}");
                    Console.WriteLine("\nThe dump includes:");
                    Console.WriteLine("- All possible run history locations in the save file");
                    Console.WriteLine("- Permanent stats overview");
                    Console.WriteLine("- Normalized run data with all available fields");
                    Console.WriteLine("- Additional findings of run-related data");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error dumping raw data: {ex.Message}");
                }
                return;
            }
            
            // Extract run history
            var runs = runHistoryManager.ExtractRunHistory(saveData);
            
            if (!string.IsNullOrEmpty(export))
            {
                try
                {
                    runHistoryManager.ExportRunHistory(runs, export);
                    Console.WriteLine($"Run history exported to: {export}");
                    Console.WriteLine($"Total runs: {runs.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting run history: {ex.Message}");
                }
            }
            else if (!string.IsNullOrEmpty(import))
            {
                try
                {
                    var importedRuns = runHistoryManager.ImportRunHistory(import);
                    Console.WriteLine($"Imported {importedRuns.Count} runs from: {import}");
                    
                    if (updateSave && file != null)
                    {
                        Console.WriteLine("Updating save file with imported runs...");
                        runHistoryManager.UpdateSaveFileWithRuns(file.FullName, importedRuns);
                        Console.WriteLine("Save file updated successfully!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error importing run history: {ex.Message}");
                }
            }
            else
            {
                // Default: show run history
                Console.WriteLine($"\n=== RUN HISTORY ({runs.Count} runs) ===\n");
                
                if (runs.Count == 0)
                {
                    Console.WriteLine("No run history found.");
                    return;
                }
                
                // Show recent runs
                var recentRuns = runs.Take(10).ToList();
                foreach (var run in recentRuns)
                {
                    var status = run.Won ? "WON" : "LOST";
                    var reconstructed = run.IsReconstructed ? " [Reconstructed]" : "";
                    Console.WriteLine($"{run.Timestamp:yyyy-MM-dd HH:mm} - {status} - Level {run.FinalLevel} - {run.Score:N0} score{reconstructed}");
                    Console.WriteLine($"  Class: {run.CharacterClass} | Damage: {run.DamageDealt:N0} | Pegs: {run.PegsHit:N0}");
                    Console.WriteLine($"  Duration: {run.Duration} | Coins: {run.CoinsEarned:N0}");
                    if (run.OrbsUsed.Count > 0)
                    {
                        Console.WriteLine($"  Orbs: {string.Join(", ", run.OrbsUsed.Take(5))}{(run.OrbsUsed.Count > 5 ? "..." : "")}");
                    }
                    Console.WriteLine();
                }
                
                if (runs.Count > 10)
                {
                    Console.WriteLine($"... and {runs.Count - 10} more runs");
                }
            }
        }
    }
}
using System.CommandLine;
using peglin_save_explorer.Core;
using peglin_save_explorer.Data;
using peglin_save_explorer.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace peglin_save_explorer.Commands
{
    public class RunHistoryCommand : ICommand
    {
        public Command CreateCommand()
        {
            var fileOption = new Option<FileInfo?>(
                new[] { "--file", "-f" },
                description: "Path to the Peglin save file")
            {
                IsRequired = false
            };

            var exportOption = new Option<string?>(
                new[] { "--export", "-e" },
                description: "Export run history to file");

            var importOption = new Option<string?>(
                new[] { "--import", "-i" },
                description: "Import run history from file");

            var updateSaveOption = new Option<bool>(
                new[] { "--update-save", "-u" },
                description: "Update save file with imported runs (experimental)",
                getDefaultValue: () => false);

            var dumpRawOption = new Option<string?>(
                new[] { "--dump-raw", "-d" },
                description: "Dump raw run history data to file for analysis");

            var command = new Command("runs", "View and manage run history")
            {
                fileOption,
                exportOption,
                importOption,
                updateSaveOption,
                dumpRawOption
            };

            command.SetHandler(Execute,
                fileOption, exportOption, importOption, updateSaveOption, dumpRawOption);

            return command;
        }

        private static void Execute(FileInfo? file, string? export, string? import, bool updateSave, string? dumpRaw)
        {
            var configManager = new ConfigurationManager();
            var runHistoryManager = new RunHistoryManager(configManager);

            try
            {
                // Handle import first if specified
                if (!string.IsNullOrEmpty(import))
                {
                    HandleImport(import, file, updateSave, configManager, runHistoryManager);
                    return;
                }

                // Load run history from stats file (not save file)
                var runs = LoadRunHistoryData(file, configManager, runHistoryManager);

                if (runs.Count == 0)
                {
                    Logger.Info("No run history found. Make sure you have a corresponding Stats file (e.g., Stats_0.data).");
                    return;
                }

                // Ensure relic cache is up to date (GameDataMappings will use it automatically)
                try
                {
                    var peglinPath = configManager.GetEffectivePeglinPath();
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

                // Handle export if specified
                if (!string.IsNullOrEmpty(export))
                {
                    HandleExport(runs, export);
                    return;
                }

                // Handle raw dump if specified
                if (!string.IsNullOrEmpty(dumpRaw))
                {
                    HandleRawDump(file, dumpRaw, configManager);
                    return;
                }

                // Default: Display run history summary
                DisplayRunHistorySummary(runs);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing run history: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Logger.Error($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private static void HandleImport(string importPath, FileInfo? saveFile, bool updateSave, ConfigurationManager configManager, RunHistoryManager runHistoryManager)
        {
            try
            {
                if (!File.Exists(importPath))
                {
                    Logger.Error($"Import file not found: {importPath}");
                    return;
                }

                var jsonContent = File.ReadAllText(importPath);
                var importedRuns = JsonConvert.DeserializeObject<List<RunRecord>>(jsonContent);

                if (importedRuns == null || importedRuns.Count == 0)
                {
                    Logger.Info("No runs found in import file.");
                    return;
                }

                Logger.Info($"Imported {importedRuns.Count} runs from {importPath}");

                if (updateSave)
                {
                    Logger.Warning("Save file updating is experimental and not yet implemented.");
                }

                // Display imported runs summary
                DisplayRunHistorySummary(importedRuns);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error importing runs: {ex.Message}");
            }
        }

        private static void HandleExport(List<RunRecord> runs, string exportPath)
        {
            try
            {
                var json = JsonConvert.SerializeObject(runs, Formatting.Indented);
                File.WriteAllText(exportPath, json);
                Logger.Info($"Exported {runs.Count} runs to {exportPath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error exporting runs: {ex.Message}");
            }
        }

        private static void HandleRawDump(FileInfo? file, string dumpPath, ConfigurationManager configManager)
        {
            try
            {
                // Determine save file path
                string saveFilePath;
                if (file != null && file.Exists)
                {
                    saveFilePath = file.FullName;
                }
                else
                {
                    var defaultPath = configManager.GetEffectiveSaveFilePath();
                    if (string.IsNullOrEmpty(defaultPath) || !File.Exists(defaultPath))
                    {
                        Logger.Error("No save file specified and no default save file found.");
                        return;
                    }
                    saveFilePath = defaultPath;
                }

                var statsFilePath = GetStatsFilePath(saveFilePath);
                if (string.IsNullOrEmpty(statsFilePath) || !File.Exists(statsFilePath))
                {
                    Console.WriteLine($"Stats file not found: {statsFilePath}");
                    return;
                }

                var statsBytes = File.ReadAllBytes(statsFilePath);
                var dumper = new SaveFileDumper(configManager);
                var statsJson = dumper.DumpSaveFile(statsBytes);
                
                File.WriteAllText(dumpPath, statsJson);
                Logger.Info($"Raw stats data dumped to {dumpPath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error dumping raw data: {ex.Message}");
            }
        }

        private static void DisplayRunHistorySummary(List<RunRecord> runs)
        {
            Console.WriteLine($"\nRun History Summary ({runs.Count} runs):");
            Console.WriteLine("================================================");

            var wins = runs.Count(r => r.Won);
            var winRate = runs.Count > 0 ? (double)wins / runs.Count * 100 : 0;

            Console.WriteLine($"Total Runs: {runs.Count}");
            Console.WriteLine($"Wins: {wins} ({winRate:F1}%)");
            Console.WriteLine($"Losses: {runs.Count - wins}");

            if (runs.Count > 0)
            {
                var totalDamage = runs.Sum(r => r.DamageDealt);
                var avgDamage = totalDamage / runs.Count;
                var bestRun = runs.OrderByDescending(r => r.DamageDealt).First();

                Console.WriteLine($"Total Damage Dealt: {totalDamage:N0}");
                Console.WriteLine($"Average Damage per Run: {avgDamage:N0}");
                Console.WriteLine($"Best Run: {bestRun.DamageDealt:N0} damage ({bestRun.Timestamp:yyyy-MM-dd})");

                Console.WriteLine("\nRecent Runs:");
                Console.WriteLine("Date/Time            | Result | Class      | Damage     | Duration");
                Console.WriteLine("────────────────────┼────────┼────────────┼────────────┼─────────");

                foreach (var run in runs.Take(15))
                {
                    var status = run.Won ? "WIN" : "LOSS";
                    var className = run.CharacterClass.Length > 10 ? run.CharacterClass.Substring(0, 10) : run.CharacterClass;
                    var duration = run.Duration.TotalMinutes > 0 ? $"{run.Duration.TotalMinutes:F0}m" : "--";

                    Console.WriteLine($"{run.Timestamp:yyyy-MM-dd HH:mm:ss} | {status.PadRight(6)} | {className.PadRight(10)} | {run.DamageDealt.ToString("N0").PadLeft(10)} | {duration.PadLeft(7)}");
                }

                // Show most used relics
                var relicUsage = new Dictionary<string, int>();
                foreach (var run in runs)
                {
                    foreach (var relic in run.RelicNames)
                    {
                        if (!relicUsage.ContainsKey(relic))
                            relicUsage[relic] = 0;
                        relicUsage[relic]++;
                    }
                }


                if (relicUsage.Count > 0)
                {
                    Console.WriteLine("\nMost Used Relics:");
                    Console.WriteLine("──────────────────────────────────────────────");
                    var topRelics = relicUsage.OrderByDescending(kvp => kvp.Value).Take(10);
                    foreach (var kvp in topRelics)
                    {
                        var percentage = (double)kvp.Value / runs.Count * 100;
                        Console.WriteLine($"{kvp.Key.PadRight(30)} | {kvp.Value.ToString().PadLeft(3)} runs ({percentage:F1}%)");
                    }
                }
            }
        }

        private static List<RunRecord> LoadRunHistoryData(FileInfo? file, ConfigurationManager configManager, RunHistoryManager runHistoryManager)
        {
            try
            {
                // Determine save file path
                string saveFilePath;
                if (file != null && file.Exists)
                {
                    saveFilePath = file.FullName;
                }
                else
                {
                    // Use default save file path
                    var defaultPath = configManager.GetEffectiveSaveFilePath();
                    if (string.IsNullOrEmpty(defaultPath) || !File.Exists(defaultPath))
                    {
                        Logger.Error("No save file specified and no default save file found.");
                        return new List<RunRecord>();
                    }
                    saveFilePath = defaultPath;
                }

                var statsFilePath = GetStatsFilePath(saveFilePath);
                if (string.IsNullOrEmpty(statsFilePath))
                {
                    Logger.Error("Could not determine stats file path. Save file should be named like 'Save_0.data'.");
                    return new List<RunRecord>();
                }

                if (!File.Exists(statsFilePath))
                {
                    Logger.Error($"Stats file not found: {statsFilePath}");
                    Logger.Info("Run history is stored in the Stats file, not the Save file.");
                    return new List<RunRecord>();
                }

                Logger.Debug($"Loading run history from: {statsFilePath}");
                var statsBytes = File.ReadAllBytes(statsFilePath);
                var dumper = new SaveFileDumper(configManager);
                var statsJson = dumper.DumpSaveFile(statsBytes);
                var statsData = JObject.Parse(statsJson);
                var runs = runHistoryManager.ExtractRunHistory(statsData);

                Logger.Debug($"Successfully loaded {runs.Count} runs from stats file.");
                return runs;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading run history: {ex.Message}");
                return new List<RunRecord>();
            }
        }

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
    }
}
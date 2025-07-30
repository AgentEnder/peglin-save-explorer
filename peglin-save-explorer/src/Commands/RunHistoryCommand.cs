using System.CommandLine;
using peglin_save_explorer.Core;
using peglin_save_explorer.Data;
using peglin_save_explorer.Services;
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

                // Load run history using centralized service
                var runs = RunDataService.LoadRunHistory(file, configManager);

                if (runs.Count == 0)
                {
                    Logger.Info("No run history found. Make sure you have a corresponding Stats file (e.g., Stats_0.data).");
                    return;
                }

                // Initialize game data using centralized service
                GameDataService.InitializeGameData(configManager);

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

                var statsFilePath = RunDataService.GetStatsFilePath(saveFilePath);
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
            var winRate = RunDisplayFormatter.FormatWinRate(wins, runs.Count);

            Console.WriteLine($"Total Runs: {runs.Count}");
            Console.WriteLine($"Wins: {wins} ({winRate})");
            Console.WriteLine($"Losses: {runs.Count - wins}");

            if (runs.Count > 0)
            {
                var totalDamage = runs.Sum(r => r.DamageDealt);
                var avgDamage = totalDamage / runs.Count;
                var bestRun = runs.OrderByDescending(r => r.DamageDealt).First();

                Console.WriteLine($"Total Damage Dealt: {RunDisplayFormatter.FormatNumber(totalDamage)}");
                Console.WriteLine($"Average Damage per Run: {RunDisplayFormatter.FormatNumber(avgDamage)}");
                Console.WriteLine($"Best Run: {RunDisplayFormatter.FormatNumber(bestRun.DamageDealt)} damage ({bestRun.Timestamp:yyyy-MM-dd})");

                Console.WriteLine("\nRecent Runs:");
                Console.WriteLine(RunDisplayFormatter.GetRunListHeader());
                Console.WriteLine(RunDisplayFormatter.GetRunListSeparator());

                foreach (var run in runs.Take(15))
                {
                    var status = RunDisplayFormatter.FormatRunStatus(run.Won);
                    var className = RunDisplayFormatter.FormatCharacterClass(run.CharacterClass, 10);
                    var duration = RunDisplayFormatter.FormatDuration(run.Duration);
                    var damage = RunDisplayFormatter.FormatDamage(run.DamageDealt);

                    Console.WriteLine($"{run.Timestamp:yyyy-MM-dd HH:mm:ss} | {status.PadRight(6)} | {className.PadRight(10)} | {damage.PadLeft(10)} | {duration.PadLeft(7)}");
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
    }
}
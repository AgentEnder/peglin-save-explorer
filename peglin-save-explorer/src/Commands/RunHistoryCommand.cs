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


            var dumpRawOption = new Option<string?>(
                new[] { "--dump-raw", "-d" },
                description: "Dump raw run history data to file for analysis");

            var command = new Command("runs", "View and manage run history")
            {
                fileOption,
                dumpRawOption
            };

            command.SetHandler(Execute,
                fileOption, dumpRawOption);

            return command;
        }

        private static void Execute(FileInfo? file, string? dumpRaw)
        {
            var configManager = new ConfigurationManager();

            try
            {
                // Load run history using centralized service
                var runs = RunDataService.LoadRunHistory(file, configManager);

                if (runs.Count == 0)
                {
                    Logger.Info("No run history found. Make sure you have a corresponding Stats file (e.g., Stats_0.data).");
                    return;
                }

                // Initialize game data using centralized service
                GameDataService.InitializeGameData(configManager);

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
            Console.WriteLine($"\nRun History Summary ({runs.Count} runs:");
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

                // Show most used relics with win rates
                var relicUsage = new Dictionary<string, (int totalUses, int relicWins)>();
                foreach (var run in runs)
                {
                    foreach (var relicName in run.RelicNames)
                    {
                        if (!relicUsage.ContainsKey(relicName))
                            relicUsage[relicName] = (0, 0);
                        
                        var current = relicUsage[relicName];
                        relicUsage[relicName] = (current.totalUses + 1, current.relicWins + (run.Won ? 1 : 0));
                    }
                }

                if (relicUsage.Count > 0)
                {
                    Console.WriteLine("\nMost Used Relics:");
                    Console.WriteLine("────────────────────────────────────────────────────────────────");
                    var topRelics = relicUsage.OrderByDescending(kvp => kvp.Value.totalUses).Take(10);
                    foreach (var kvp in topRelics)
                    {
                        var relicName = kvp.Key;
                        var totalUses = kvp.Value.totalUses;
                        var relicWins = kvp.Value.relicWins;
                        var usagePercentage = (double)totalUses / runs.Count * 100;
                        var relicWinRate = totalUses > 0 ? (double)relicWins / totalUses * 100 : 0;
                        
                        Console.WriteLine($"{relicName.PadRight(30)} | {totalUses.ToString().PadLeft(3)} runs ({usagePercentage:F1}%) | {relicWinRate:F1}% win rate");
                    }
                }
            }
        }
    }
}
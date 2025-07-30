using System.CommandLine;
using peglin_save_explorer.Core;
using peglin_save_explorer.Utils;
using Newtonsoft.Json.Linq;

namespace peglin_save_explorer.Commands
{
    public class SummaryCommand : ICommand
    {
        public Command CreateCommand()
        {
            var fileOption = new Option<FileInfo?>(
                new[] { "--file", "-f" },
                description: "Path to the Peglin save file")
            {
                IsRequired = false
            };

            var command = new Command("summary", "Show player statistics summary")
            {
                fileOption
            };

            command.SetHandler((FileInfo? file) => Execute(file), fileOption);
            return command;
        }

        private static void Execute(FileInfo? file)
        {
            var saveData = SaveDataLoader.LoadSaveData(file);

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
    }
}
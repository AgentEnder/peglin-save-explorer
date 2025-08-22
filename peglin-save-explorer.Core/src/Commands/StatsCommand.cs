using System.CommandLine;
using peglin_save_explorer.Core;
using peglin_save_explorer.Utils;
using Newtonsoft.Json.Linq;

namespace peglin_save_explorer.Commands
{
    public class StatsCommand : ICommand
    {
        public Command CreateCommand()
        {
            var fileOption = new Option<FileInfo?>(
                new[] { "--file", "-f" },
                description: "Path to the Peglin save file")
            {
                IsRequired = false
            };

            var command = new Command("stats", "Show detailed player statistics")
            {
                fileOption
            };

            command.SetHandler((FileInfo? file) => Execute(file), fileOption);
            return command;
        }

        private static void Execute(FileInfo? file)
        {
            var saveData = SaveDataLoader.LoadSaveData(file);
            var data = saveData?["peglinData"] as JObject;

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
    }
}
using System.CommandLine;
using peglin_save_explorer.Core;
using peglin_save_explorer.Utils;
using Newtonsoft.Json.Linq;

namespace peglin_save_explorer.Commands
{
    public class OrbsCommand : ICommand
    {
        public Command CreateCommand()
        {
            var fileOption = new Option<FileInfo?>(
                new[] { "--file", "-f" },
                description: "Path to the Peglin save file")
            {
                IsRequired = false
            };

            var topCountOption = new Option<int>(
                new[] { "--top", "-t" },
                description: "Show top N orbs by criteria",
                getDefaultValue: () => 10);

            var sortByOption = new Option<string>(
                new[] { "--sort", "-s" },
                description: "Sort by: damage, usage, efficiency, cruciball",
                getDefaultValue: () => "damage");

            var command = new Command("orbs", "Analyze orb usage and performance")
            {
                fileOption,
                topCountOption,
                sortByOption
            };

            command.SetHandler((FileInfo? file, int top, string sortBy) => Execute(file, top, sortBy), 
                fileOption, topCountOption, sortByOption);

            return command;
        }

        private static void Execute(FileInfo? file, int topCount, string sortBy)
        {
            var saveData = SaveDataLoader.LoadSaveData(file);
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

        private static JToken? ExtractOrbData(JObject? saveData)
        {
            return saveData?["peglinData"]?["orbStats"] ?? saveData?["peglinData"]?["orbs"];
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
    }
}
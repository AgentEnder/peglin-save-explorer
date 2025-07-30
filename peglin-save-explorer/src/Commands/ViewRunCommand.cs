using System.CommandLine;
using peglin_save_explorer.Core;
using peglin_save_explorer.Data;
using peglin_save_explorer.Services;
using peglin_save_explorer.Utils;
using Newtonsoft.Json.Linq;

namespace peglin_save_explorer.Commands
{
    public class ViewRunCommand : ICommand
    {
        public Command CreateCommand()
        {
            var fileOption = new Option<FileInfo?>(
                new[] { "--file", "-f" },
                description: "Path to the Peglin save file")
            {
                IsRequired = false
            };

            var runIndexOption = new Option<int>(
                new[] { "--run", "-r" },
                description: "Index of the run to view (0-based)")
            {
                IsRequired = true
            };

            var command = new Command("view-run", "View details of a specific run")
            {
                fileOption,
                runIndexOption
            };

            command.SetHandler((FileInfo? file, int runIndex) => Execute(file, runIndex),
                fileOption, runIndexOption);
            return command;
        }

        private static void Execute(FileInfo? file, int runIndex)
        {
            try
            {
                Logger.Debug($"Loading run {runIndex} details...");

                var configManager = new ConfigurationManager();

                // Load run history using centralized service
                var runs = RunDataService.LoadRunHistory(file, configManager);

                if (runs.Count == 0)
                {
                    Console.WriteLine("No run history found. Make sure you have a corresponding Stats file (e.g., Stats_0.data).");
                    return;
                }

                // Initialize game data using centralized service
                GameDataService.InitializeGameData(configManager);

                // Load relic cache using centralized service
                var relicCache = GameDataService.LoadRelicCache();

                // Get specific run using centralized validation
                var run = RunDataService.GetRunByIndex(runs, runIndex);
                if (run == null)
                {
                    Console.WriteLine($"Invalid run index {runIndex}. Available runs: 0-{runs.Count - 1}");
                    return;
                }

                Console.WriteLine($"\n=== Run {runIndex} Details ===");
                Console.WriteLine($"Won: {run.Won}");
                Console.WriteLine($"Date: {run.Timestamp:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Seed: {run.Seed}");
                Console.WriteLine($"Score: {RunDisplayFormatter.FormatNumber(run.Score)}");
                Console.WriteLine($"Duration: {RunDisplayFormatter.FormatDuration(run.Duration, showSeconds: true)}");
                Console.WriteLine($"Coins Earned: {RunDisplayFormatter.FormatNumber(run.CoinsEarned)}");
                if (run.CoinsSpent > 0)
                {
                    Console.WriteLine($"Coins Spent: {RunDisplayFormatter.FormatNumber(run.CoinsSpent)}");
                }
                if (!string.IsNullOrEmpty(run.CharacterClass))
                {
                    Console.WriteLine($"Character Class: {run.CharacterClass}");
                }
                if (run.CruciballLevel > 0)
                {
                    Console.WriteLine($"Cruciball Level: {run.CruciballLevel}");
                }

                // Combat Statistics
                Console.WriteLine($"\nCombat Statistics:");
                Console.WriteLine($"  Total Damage Dealt: {run.DamageDealt:N0}");
                if (run.MostDamageDealtWithSingleAttack > 0)
                {
                    Console.WriteLine($"  Highest Single Attack: {run.MostDamageDealtWithSingleAttack:N0}");
                }
                if (run.TotalDamageNegated > 0)
                {
                    Console.WriteLine($"  Damage Blocked: {run.TotalDamageNegated:N0}");
                }
                if (run.ShotsTaken > 0)
                {
                    Console.WriteLine($"  Total Shots: {run.ShotsTaken:N0}");
                    if (run.CritShotsTaken > 0)
                    {
                        var critRate = (double)run.CritShotsTaken / run.ShotsTaken * 100;
                        Console.WriteLine($"  Critical Shots: {run.CritShotsTaken:N0} ({critRate:F1}%)");
                    }
                }
                if (run.BombsThrown > 0)
                {
                    Console.WriteLine($"  Bombs Thrown: {run.BombsThrown:N0}");
                    if (run.BombsThrownRigged > 0)
                    {
                        Console.WriteLine($"  Rigged Bombs: {run.BombsThrownRigged:N0}");
                    }
                }
                if (run.MostPegsHitInOneTurn > 0)
                {
                    Console.WriteLine($"  Most Pegs Hit (One Turn): {run.MostPegsHitInOneTurn}");
                }
                if (run.PegsHitRefresh > 0 || run.PegsHitCrit > 0 || run.PegsRefreshed > 0)
                {
                    Console.WriteLine($"  Peg Statistics:");
                    if (run.PegsHitRefresh > 0) Console.WriteLine($"    Refresh Pegs Hit: {run.PegsHitRefresh}");
                    if (run.PegsHitCrit > 0) Console.WriteLine($"    Crit Pegs Hit: {run.PegsHitCrit}");
                    if (run.PegsRefreshed > 0) Console.WriteLine($"    Pegs Refreshed: {run.PegsRefreshed}");
                }
                if (run.FinalHp > 0 || run.MaxHp > 0)
                {
                    Console.WriteLine($"  Health: {run.FinalHp}/{run.MaxHp}");
                }
                if (!string.IsNullOrEmpty(run.DefeatedBy))
                {
                    Console.WriteLine($"  Defeated By: {run.DefeatedBy}");
                }

                // Additional statistics from enhanced RunStats parsing
                if (run.BombsCreated > 0)
                {
                    Console.WriteLine($"  Bombs Created: {run.BombsCreated:N0}");
                    if (run.BombsCreatedRigged > 0)
                    {
                        Console.WriteLine($"  Rigged Bombs Created: {run.BombsCreatedRigged:N0}");
                    }
                }
                if (run.DefeatedOnRoom > 0)
                {
                    Console.WriteLine($"  Defeated on Room: {run.DefeatedOnRoom}");
                }
                if (run.VampireDealTaken)
                {
                    Console.WriteLine($"  Vampire Deal Taken: Yes");
                }
                if (run.PegUpgradeEvents.Any())
                {
                    Console.WriteLine($"  Peg Upgrades: {run.PegUpgradeEvents.Count} events");
                }

                Console.WriteLine($"\nBosses Defeated:");
                if (run.VisitedBosses.Any())
                {
                    foreach (var bossId in run.VisitedBosses.Distinct())
                    {
                        var bossName = GameDataMappings.GetBossName(bossId);
                        Console.WriteLine($"  • {bossName}");
                    }
                }
                else
                {
                    Console.WriteLine("  None");
                }

                Console.WriteLine($"\nRoom Statistics:");
                var roomStats = run.RoomTypeStatistics;
                if (roomStats.Any())
                {
                    foreach (var room in roomStats.OrderByDescending(x => x.Value))
                    {
                        Console.WriteLine($"  {room.Key}: {room.Value}");
                    }
                }
                else
                {
                    Console.WriteLine("  None");
                }

                Console.WriteLine($"\nRoom Timeline ({run.VisitedRooms.Length} rooms):");
                if (run.VisitedRooms.Any())
                {
                    // Symbol mapping for room types
                    var symbols = new Dictionary<string, string>
                    {
                        ["Battle"] = "⚔",      // Crossed swords for battle
                        ["Treasure"] = "💰",    // Money bag for treasure
                        ["Store"] = "🏪",      // Store/shop for store
                        ["Scenario"] = "📜",    // Scroll for scenario/event
                        ["Mini Boss"] = "👹",   // Ogre face for mini boss
                        ["Boss"] = "🐉",       // Dragon for boss
                        ["Peg Minigame"] = "🎯", // Target for peg minigame
                        ["Unknown"] = "❓",     // Question mark for unknown
                        ["None"] = "⭕"        // Circle with slash for none/empty
                    };

                    var timelineSymbols = new List<string>();
                    for (int i = 0; i < run.VisitedRooms.Length; i++)
                    {
                        var roomId = run.VisitedRooms[i];
                        var roomName = GameDataMappings.GetRoomName(roomId);
                        var symbol = symbols.GetValueOrDefault(roomName, $"#{roomId}");
                        timelineSymbols.Add(symbol);
                    }

                    // Split timeline by acts (separated by boss rooms)
                    var acts = new List<List<string>>();
                    var currentAct = new List<string>();

                    for (int i = 0; i < run.VisitedRooms.Length; i++)
                    {
                        var roomId = run.VisitedRooms[i];
                        var roomName = GameDataMappings.GetRoomName(roomId);
                        var symbol = symbols.GetValueOrDefault(roomName, $"#{roomId}");

                        currentAct.Add(symbol);

                        // If this is a boss room, end the current act
                        if (roomName == "Boss")
                        {
                            acts.Add(new List<string>(currentAct));
                            currentAct.Clear();
                        }
                    }

                    // Add any remaining rooms as the final act (if run didn't end with boss)
                    if (currentAct.Count > 0)
                    {
                        acts.Add(currentAct);
                    }

                    // Display each act separately
                    for (int actIndex = 0; actIndex < acts.Count; actIndex++)
                    {
                        var act = acts[actIndex];
                        if (act.Count == 0) continue;

                        // Determine act label
                        var actLabel = actIndex < acts.Count - 1 || act.Last() == "🐉"
                            ? $"Act {actIndex + 1}"
                            : "Final Rooms";

                        Console.WriteLine($"  {actLabel} ({act.Count} rooms):");

                        // Calculate max per line for this act
                        int maxPerLine;
                        try
                        {
                            var terminalWidth = Console.WindowWidth;
                            var availableWidth = Math.Max(20, terminalWidth - 15);
                            var charsPerSymbol = 4;
                            maxPerLine = Math.Max(1, availableWidth / charsPerSymbol);
                            maxPerLine = Math.Min(maxPerLine, 25);
                        }
                        catch
                        {
                            maxPerLine = 15;
                        }

                        // Display this act's timeline
                        for (int i = 0; i < act.Count; i += maxPerLine)
                        {
                            var line = string.Join(" → ", act.Skip(i).Take(maxPerLine));
                            Console.WriteLine($"    {line}");

                            // Add continuation arrow if there are more symbols in this act
                            if (i + maxPerLine < act.Count)
                            {
                                Console.WriteLine("      ↓");
                            }
                        }

                        // Add spacing between acts
                        if (actIndex < acts.Count - 1)
                        {
                            Console.WriteLine();
                        }
                    }

                    // Legend
                    Console.WriteLine("\n  Legend:");
                    Console.WriteLine("  ⚔ = Battle    💰 = Treasure   🏪 = Store     📜 = Scenario");
                    Console.WriteLine("  👹 = Mini Boss 🐉 = Boss      🎯 = Minigame  ❓ = Unknown");
                }
                else
                {
                    Console.WriteLine("  No rooms visited");
                }

                Console.WriteLine($"\nStatus Effects (Final):");
                var activeEffects = run.ActiveStatusEffects;
                if (activeEffects.Any())
                {
                    foreach (var effect in activeEffects)
                    {
                        Console.WriteLine($"  • {effect}");
                    }
                }
                else
                {
                    Console.WriteLine("  None");
                }

                // Load relic mappings once for efficiency using centralized service
                var configManager2 = new ConfigurationManager();
                var peglinPath = configManager2.GetEffectivePeglinPath();
                var cachedMappings = GameDataService.GetRelicMappings(peglinPath);

                Console.WriteLine($"\nRelics ({run.RelicIds.Length}):");
                if (run.RelicIds.Any())
                {
                    foreach (var relicId in run.RelicIds)
                    {
                        // Try to get relic name from cache first, fallback to GameDataMappings
                        if (cachedMappings != null)
                        {
                            var cachedName = cachedMappings.GetValueOrDefault(relicId);
                            if (!string.IsNullOrEmpty(cachedName))
                            {
                                Console.WriteLine($"  • {cachedName}");
                                continue;
                            }
                        }

                        // Fallback to assembly enum name
                        var enumName = GameDataMappings.GetRelicName(relicId);
                        Console.WriteLine($"  • {enumName} (ID: {relicId})");
                    }
                }
                else
                {
                    Console.WriteLine("  None");
                }

                // Orbs section - show detailed stats if available, fallback to simple list
                Logger.Debug($"OrbStats count: {run.OrbStats.Count}");
                if (run.OrbStats.Any())
                {
                    Console.WriteLine($"\nOrbs Used ({run.OrbStats.Count}):");
                    var sortedOrbs = run.OrbStats.OrderByDescending(kvp => kvp.Value.DamageDealt).ThenBy(kvp => kvp.Value.Name);
                    
                    foreach (var orbStat in sortedOrbs)
                    {
                        var orb = orbStat.Value;
                        Console.WriteLine($"  • {orb.Name}");
                        if (orb.DamageDealt > 0) Console.WriteLine($"    Damage Dealt: {orb.DamageDealt:N0}");
                        if (orb.TimesFired > 0) Console.WriteLine($"    Times Fired: {orb.TimesFired:N0}");
                        if (orb.AmountInDeck > 0) Console.WriteLine($"    Amount in Deck: {orb.AmountInDeck}");
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
                                Console.WriteLine($"    Level Distribution: {string.Join(", ", levelInfo)}");
                            }
                        }
                        if (orb.TimesDiscarded > 0) Console.WriteLine($"    Times Discarded: {orb.TimesDiscarded}");
                        if (orb.TimesRemoved > 0) Console.WriteLine($"    Times Removed: {orb.TimesRemoved}");
                        if (orb.Starting) Console.WriteLine($"    Starting Orb: Yes");
                        if (orb.HighestCruciballBeat > 0) Console.WriteLine($"    Highest Cruciball Beat: {orb.HighestCruciballBeat}");
                    }
                }
                else if (run.OrbsUsed.Any())
                {
                    Console.WriteLine($"\nOrbs Used ({run.OrbsUsed.Count}):");
                    // Group orbs by name and count usage
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
                            Console.WriteLine($"  • {orbName} (used {count} times)");
                        }
                        else
                        {
                            Console.WriteLine($"  • {orbName}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("\nOrbs Used (0):");
                    Console.WriteLine("  None");
                }

                // Enhanced Status Effects section with stacks
                Logger.Debug($"StacksPerStatusEffect count: {run.StacksPerStatusEffect.Count}");
                if (run.StacksPerStatusEffect.Any())
                {
                    Console.WriteLine($"\nDetailed Status Effects ({run.StacksPerStatusEffect.Count}):");
                    var sortedEffects = run.StacksPerStatusEffect.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key);
                    
                    foreach (var effect in sortedEffects)
                    {
                        Console.WriteLine($"  • {effect.Key}: {effect.Value} stacks");
                    }
                }

                // Enemy Combat Data section  
                Logger.Debug($"EnemyData count: {run.EnemyData.Count}");
                if (run.EnemyData.Any())
                {
                    Console.WriteLine($"\nEnemy Combat Data ({run.EnemyData.Count}):");
                    var sortedEnemies = run.EnemyData.OrderByDescending(kvp => kvp.Value.AmountFought).ThenBy(kvp => kvp.Value.Name);
                    
                    foreach (var enemyStat in sortedEnemies)
                    {
                        var enemy = enemyStat.Value;
                        Console.WriteLine($"  • {enemy.Name}");
                        if (enemy.AmountFought > 0) Console.WriteLine($"    Times Fought: {enemy.AmountFought}");
                        if (enemy.MeleeDamageReceived > 0) Console.WriteLine($"    Melee Damage Received: {enemy.MeleeDamageReceived:N0}");
                        if (enemy.RangedDamageReceived > 0) Console.WriteLine($"    Ranged Damage Received: {enemy.RangedDamageReceived:N0}");
                        if (enemy.DefeatedBy) Console.WriteLine($"    Defeated By This Enemy: Yes");
                    }
                }

                // Slime Peg Statistics
                Logger.Debug($"SlimePegsPerSlimeType count: {run.SlimePegsPerSlimeType.Count}");
                if (run.SlimePegsPerSlimeType.Any())
                {
                    Console.WriteLine($"\nSlime Peg Statistics ({run.SlimePegsPerSlimeType.Count} types):");
                    var sortedSlimes = run.SlimePegsPerSlimeType.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key);
                    
                    foreach (var slime in sortedSlimes)
                    {
                        Console.WriteLine($"  • {slime.Key}: {slime.Value} pegs");
                    }
                }

                // Show current mappings for debugging
                Logger.Debug($"\n=== Debug: Current Mappings ===");
                var mappingInfo = GameDataMappings.GetMappingInfo();
                foreach (var info in mappingInfo)
                {
                    Logger.Debug($"{info.Key}: {info.Value}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error viewing run: {ex.Message}");
                if (true) // Always show stack trace for this debug command
                {
                    Logger.Debug($"Stack trace: {ex.StackTrace}");
                }
            }
        }
    }
}

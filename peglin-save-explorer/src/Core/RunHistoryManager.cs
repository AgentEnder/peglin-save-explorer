using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OdinSerializer;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Services;
using peglin_save_explorer.Data;

namespace peglin_save_explorer.Core
{
    public class RunHistoryManager
    {
        private readonly ConfigurationManager configManager;
        private Assembly? peglinAssembly;

        public RunHistoryManager(ConfigurationManager? configManager = null)
        {
            this.configManager = configManager ?? new ConfigurationManager();
        }

        public List<RunRecord> ExtractRunHistory(JObject saveData)
        {
            var runs = new List<RunRecord>();

            try
            {
                // First check if this is a stats file (contains RunStatsHistory)
                var runStatsHistory = saveData["RunStatsHistory"]?["Value"]?["runsHistory"];
                // Also check if it's wrapped in a "data" key (from dumped format)
                if (runStatsHistory == null)
                {
                    runStatsHistory = saveData["data"]?["RunStatsHistory"]?["Value"]?["runsHistory"];
                }

                if (runStatsHistory is JArray statsArray)
                {
                    // This is a stats file with detailed run history
                    foreach (var runToken in statsArray)
                    {
                        var run = ParseStatsFileRun(runToken);
                        if (run != null) runs.Add(run);
                    }
                    // Continue to process with persistent database merge
                }
                else
                {
                    // Otherwise, check standard save file locations
                    var data = saveData["peglinData"] as JObject ?? saveData["data"] as JObject;
                    if (data != null)
                    {
                        // Look for run history in various possible locations
                        var runSources = new[]
                        {
                            data["runHistory"],
                            data["completedRuns"],
                            data["gameHistory"],
                            data["sessionHistory"],
                            data["runs"],
                            data["PermanentStats"]?["Value"]?["runHistory"],
                            data["PermanentStats"]?["Value"]?["completedRuns"],
                            data["gameData"]?["runHistory"]
                        };

                        foreach (var source in runSources)
                        {
                            if (source != null)
                            {
                                runs.AddRange(ParseRunData(source));
                            }
                        }
                    }
                }

                // Don't reconstruct fake data - only use real run history
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting run history: {ex.Message}");
            }

            var sortedRuns = runs.OrderByDescending(r => r.Timestamp).ToList();
            
            // Apply relic mappings to all runs
            ApplyRelicMappings(sortedRuns);
            
            // Merge with persistent database and save new runs
            var mergedRuns = MergeWithPersistentDatabase(sortedRuns);
            
            return mergedRuns;
        }

        /// <summary>
        /// Apply proper relic mappings to all runs using GameDataService
        /// </summary>
        private void ApplyRelicMappings(List<RunRecord> runs)
        {
            try
            {
                var peglinPath = configManager.GetEffectivePeglinPath();
                var relicMappings = GameDataService.GetRelicMappings(peglinPath);
                
                if (relicMappings != null && relicMappings.Count > 0)
                {
                    foreach (var run in runs)
                    {
                        run.RelicNames = run.RelicIds.Select(relicId =>
                        {
                            if (relicMappings.TryGetValue(relicId, out var cachedName))
                            {
                                return cachedName;
                            }
                            else
                            {
                                // Fallback to GameDataMappings
                                return GameDataMappings.GetRelicName(relicId);
                            }
                        }).ToList();
                    }
                }
                else
                {
                    Logger.Warning("No relic mappings available, using fallback mappings");
                    
                    // Fallback to GameDataMappings for all runs
                    foreach (var run in runs)
                    {
                        run.RelicNames = GameDataMappings.GetRelicNames(run.RelicIds);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error applying relic mappings: {ex.Message}");
                
                // Fallback to GameDataMappings for all runs
                foreach (var run in runs)
                {
                    run.RelicNames = GameDataMappings.GetRelicNames(run.RelicIds);
                }
            }
        }

        /// <summary>
        /// Merge current runs with persistent database, avoiding duplicates
        /// </summary>
        private List<RunRecord> MergeWithPersistentDatabase(List<RunRecord> currentRuns)
        {
            try
            {
                var persistentRuns = LoadPersistentRunHistory();
                var newRuns = new List<RunRecord>();
                
                foreach (var run in currentRuns)
                {
                    var hash = GenerateRunHash(run);
                    run.Id = hash; // Use hash as ID for consistency
                    
                    // Check if this run already exists in persistent storage
                    if (!persistentRuns.Any(r => r.Id == hash))
                    {
                        newRuns.Add(run);
                        persistentRuns.Add(run);
                        Logger.Debug($"Added new run to database: {run.Timestamp:yyyy-MM-dd HH:mm:ss} - {(run.Won ? "WIN" : "LOSS")}");
                    }
                }
                
                if (newRuns.Count > 0)
                {
                    SavePersistentRunHistory(persistentRuns);
                    Logger.Info($"Saved {newRuns.Count} new runs to persistent database. Total runs: {persistentRuns.Count}");
                }
                
                return persistentRuns.OrderByDescending(r => r.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error merging with persistent database: {ex.Message}");
                return currentRuns; // Fallback to current runs only
            }
        }

        /// <summary>
        /// Generate a unique hash for a run to detect duplicates
        /// </summary>
        private string GenerateRunHash(RunRecord run)
        {
            // Create a string with key run data that should be unique
            // NOTE: Excluding CharacterClass from hash due to inconsistent character class lookups causing duplicate detection to fail
            var hashData = $"{run.Timestamp:yyyy-MM-dd-HH-mm-ss}_{run.Won}_{run.DamageDealt}_{run.Duration.TotalMilliseconds}_{run.Seed}_{run.CruciballLevel}_{string.Join(",", run.RelicIds.OrderBy(x => x))}";
            
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashData));
            return Convert.ToBase64String(hashBytes)[..16]; // Use first 16 characters for readability
        }

        /// <summary>
        /// Get the path to the persistent run history database
        /// </summary>
        private string GetPersistentDatabasePath()
        {
            var configFilePath = configManager.GetConfigFilePath();
            var appDataPath = Path.GetDirectoryName(configFilePath) ?? Path.GetTempPath();
            return Path.Combine(appDataPath, "run_history.json");
        }

        /// <summary>
        /// Load run history from persistent database
        /// </summary>
        private List<RunRecord> LoadPersistentRunHistory()
        {
            var dbPath = GetPersistentDatabasePath();
            
            if (!File.Exists(dbPath))
            {
                Logger.Debug("No persistent run history database found, starting fresh");
                return new List<RunRecord>();
            }

            try
            {
                var json = File.ReadAllText(dbPath);
                var database = JsonConvert.DeserializeObject<PersistentRunDatabase>(json);
                
                if (database?.Runs != null)
                {
                    Logger.Debug($"Loaded {database.Runs.Count} runs from persistent database");
                    
                    // Clean up duplicates that may exist from previous hash generation issues
                    var cleanedRuns = CleanupDuplicateRuns(database.Runs);
                    
                    // If we removed duplicates, save the cleaned database
                    if (cleanedRuns.Count != database.Runs.Count)
                    {
                        Logger.Info($"Cleaned up {database.Runs.Count - cleanedRuns.Count} duplicate runs from persistent database");
                        SavePersistentRunHistory(cleanedRuns);
                    }
                    
                    return cleanedRuns;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error loading persistent run history: {ex.Message}, starting fresh");
            }
            
            return new List<RunRecord>();
        }

        /// <summary>
        /// Clean up duplicate runs by regenerating hashes and keeping the first occurrence
        /// </summary>
        private List<RunRecord> CleanupDuplicateRuns(List<RunRecord> runs)
        {
            var seenHashes = new HashSet<string>();
            var cleanedRuns = new List<RunRecord>();
            
            foreach (var run in runs.OrderByDescending(r => r.Timestamp))
            {
                var hash = GenerateRunHash(run);
                
                if (!seenHashes.Contains(hash))
                {
                    run.Id = hash; // Update ID to use new hash
                    seenHashes.Add(hash);
                    cleanedRuns.Add(run);
                }
                // Else: Skip duplicate run
            }
            
            return cleanedRuns;
        }

        /// <summary>
        /// Save run history to persistent database
        /// </summary>
        private void SavePersistentRunHistory(List<RunRecord> runs)
        {
            var dbPath = GetPersistentDatabasePath();
            
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var database = new PersistentRunDatabase
                {
                    Version = 1,
                    LastUpdated = DateTime.UtcNow,
                    TotalRuns = runs.Count,
                    Runs = runs.OrderByDescending(r => r.Timestamp).ToList()
                };

                var json = JsonConvert.SerializeObject(database, Formatting.Indented, new JsonSerializerSettings
                {
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    DateTimeZoneHandling = DateTimeZoneHandling.Utc
                });

                File.WriteAllText(dbPath, json);
                Logger.Debug($"Saved {runs.Count} runs to persistent database at {dbPath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving persistent run history: {ex.Message}");
            }
        }

        private RunRecord? ParseStatsFileRun(JToken runToken)
        {
            try
            {
                if (runToken is not JObject runObj) return null;

                // Debug: Show available fields in the run data
                Logger.Debug($"Available fields in run data: {string.Join(", ", runObj.Properties().Select(p => p.Name))}");

                // Get class index and look up the name using our assembly-extracted mappings
                var classIndex = runObj["selectedClass"]?.Value<int>() ?? 0;
                var className = GameDataMappings.GetCharacterClassName(classIndex);

                var run = new RunRecord
                {
                    Id = runObj["runId"]?.ToString() ?? Guid.NewGuid().ToString(),
                    Timestamp = ParseTimestamp(runObj["endDate"]),
                    Won = runObj["hasWon"]?.Value<bool>() ?? false,
                    Score = 0, // Score not stored in stats file
                    DamageDealt = runObj["totalDamageDealt"]?.Value<long>() ?? 0,
                    PegsHit = runObj["pegsHit"]?.Value<int>() ?? 0,
                    Duration = TimeSpan.FromMilliseconds(runObj["runTimerElapsedMilliseconds"]?.Value<double>() ?? 0),
                    CharacterClass = className,
                    Seed = runObj["seed"]?.ToString(),
                    FinalLevel = runObj["defeatedOnLevel"]?.Value<int>() ?? (runObj["hasWon"]?.Value<bool>() == true ? 7 : 0),
                    CoinsEarned = runObj["coinsEarned"]?.Value<long>() ?? 0,
                    IsCustomRun = runObj["isCustomRun"]?.Value<bool>() ?? false,
                    CruciballLevel = runObj["cruciballLevel"]?.Value<int>() ?? 0,
                    DefeatedBy = runObj["defeatedBy"]?.ToString(),
                    FinalHp = runObj["finalHp"]?.Value<int>() ?? 0,
                    MaxHp = runObj["maxHp"]?.Value<int>() ?? 0,
                    MostDamageDealtWithSingleAttack = runObj["mostDamageDealtWithSingleAttack"]?.Value<long>() ?? 0,
                    TotalDamageNegated = runObj["totalDamageNegated"]?.Value<long>() ?? 0,
                    PegsHitRefresh = runObj["pegsHitRefresh"]?.Value<int>() ?? 0,
                    PegsHitCrit = runObj["pegsHitCrit"]?.Value<int>() ?? 0,
                    PegsRefreshed = runObj["pegsRefreshed"]?.Value<int>() ?? 0,
                    BombsThrown = runObj["bombsThrown"]?.Value<int>() ?? 0,
                    BombsThrownRigged = runObj["bombsThrownRigged"]?.Value<int>() ?? 0,
                    MostPegsHitInOneTurn = runObj["mostPegsHitInOneTurn"]?.Value<int>() ?? 0,
                    CoinsSpent = runObj["coinsSpent"]?.Value<long>() ?? 0,
                    ShotsTaken = runObj["shotsTaken"]?.Value<int>() ?? 0,
                    CritShotsTaken = runObj["critShotsTaken"]?.Value<int>() ?? 0,
                    StartDate = ParseTimestamp(runObj["startDate"]),

                    // Additional fields from RunStats.cs structure
                    BombsCreated = runObj["bombsCreated"]?.Value<int>() ?? 0,
                    BombsCreatedRigged = runObj["bombsCreatedRigged"]?.Value<int>() ?? 0,
                    DefeatedOnRoom = runObj["defeatedOnRoom"]?.Value<int>() ?? 0,
                    VampireDealTaken = runObj["vampireDealTaken"]?.Value<bool>() ?? false
                };

                // Debug: Show values of new fields
                Logger.Debug($"Parsed new fields - BombsCreated: {run.BombsCreated}, BombsCreatedRigged: {run.BombsCreatedRigged}, DefeatedOnRoom: {run.DefeatedOnRoom}, VampireDealTaken: {run.VampireDealTaken}");

                // Parse orb play data
                var orbPlayData = runObj["orbPlayData"] as JArray;
                Logger.Debug($"orbPlayData found: {orbPlayData != null}, Count: {orbPlayData?.Count ?? 0}");
                if (orbPlayData != null && orbPlayData.Count > 0)
                {
                    run.OrbsUsed = orbPlayData.Select(o => o["name"]?.ToString() ?? o["id"]?.ToString() ?? "Unknown").ToList();
                    
                    // Also parse detailed orb statistics from orbPlayData if available
                    foreach (var orbToken in orbPlayData)
                    {
                        if (orbToken is JObject orbData)
                        {
                            var orbName = orbData["name"]?.ToString() ?? orbData["id"]?.ToString() ?? "Unknown";
                            var orbStats = new OrbPlayData
                            {
                                Id = orbData["id"]?.ToString() ?? orbName,
                                Name = orbName,
                                DamageDealt = orbData["damageDealt"]?.Value<int>() ?? 0,
                                TimesFired = orbData["timesFired"]?.Value<int>() ?? 0,
                                TimesDiscarded = orbData["timesDiscarded"]?.Value<int>() ?? 0,
                                TimesRemoved = orbData["timesRemoved"]?.Value<int>() ?? 0,
                                Starting = orbData["starting"]?.Value<bool>() ?? false,
                                AmountInDeck = orbData["amountInDeck"]?.Value<int>() ?? 0,
                                HighestCruciballBeat = orbData["highestCruciballBeat"]?.Value<int>() ?? 0
                            };

                            // Parse level instances array
                            var levelInstancesArray = orbData["levelInstances"] as JArray;
                            if (levelInstancesArray != null && levelInstancesArray.Count >= 3)
                            {
                                orbStats.LevelInstances = new int[3]
                                {
                                    levelInstancesArray[0]?.Value<int>() ?? 0,
                                    levelInstancesArray[1]?.Value<int>() ?? 0,
                                    levelInstancesArray[2]?.Value<int>() ?? 0
                                };
                            }

                            run.OrbStats[orbName] = orbStats;
                        }
                    }
                }
                else if (orbPlayData != null)
                {
                    Logger.Debug("orbPlayData exists but is empty array - this could indicate orb data loss during save file deserialization");
                }

                // Parse raw data arrays for enrichment
                var relicsArray = runObj["relics"] as JArray;
                if (relicsArray != null)
                {
                    run.RelicIds = relicsArray.Select(r => r.Value<int>()).ToArray();
                }

                var visitedRoomsArray = runObj["visitedRooms"] as JArray;
                if (visitedRoomsArray != null)
                {
                    run.VisitedRooms = visitedRoomsArray.Select(r => r.Value<int>()).ToArray();
                }

                var visitedBossesArray = runObj["visitedBosses"] as JArray;
                if (visitedBossesArray != null)
                {
                    run.VisitedBosses = visitedBossesArray.Select(b => b.Value<int>()).ToArray();
                }

                var statusEffectsArray = runObj["statusEffects"] as JArray;
                if (statusEffectsArray != null)
                {
                    run.StatusEffects = statusEffectsArray.Select(s => s.Value<int>()).ToArray();
                }

                var slimePegsArray = runObj["slimePegs"] as JArray;
                if (slimePegsArray != null)
                {
                    run.SlimePegs = slimePegsArray.Select(s => s.Value<int>()).ToArray();
                }

                // Parse additional complex data structures from RunStats.cs
                
                // Parse peg upgrade events
                var pegUpgradeEventsArray = runObj["pegUpgradeEvents"] as JArray;
                if (pegUpgradeEventsArray != null)
                {
                    run.PegUpgradeEvents = pegUpgradeEventsArray.Select(p => p.Value<int>()).ToList();
                }

                // Parse status effect stacks dictionary
                var stacksPerStatusEffect = runObj["stacksPerStatusEffect"] as JObject;
                if (stacksPerStatusEffect != null)
                {
                    foreach (var kvp in stacksPerStatusEffect)
                    {
                        if (int.TryParse(kvp.Key, out var statusId) && kvp.Value?.Value<int>() is int stacks)
                        {
                            var statusName = GameDataMappings.GetStatusEffectName(statusId);
                            run.StacksPerStatusEffect[statusName] = stacks;
                        }
                    }
                }

                // Parse slime pegs per type dictionary
                var slimePegsPerSlimeType = runObj["slimePegsPerSlimeType"] as JObject;
                if (slimePegsPerSlimeType != null)
                {
                    foreach (var kvp in slimePegsPerSlimeType)
                    {
                        if (int.TryParse(kvp.Key, out var slimeId) && kvp.Value?.Value<int>() is int count)
                        {
                            var slimeName = GameDataMappings.GetSlimePegName(slimeId);
                            run.SlimePegsPerSlimeType[slimeName] = count;
                        }
                    }
                }

                // Parse orb stats dictionary
                var orbStatsDict = runObj["orbStats"] as JObject;
                if (orbStatsDict != null)
                {
                    foreach (var kvp in orbStatsDict)
                    {
                        if (kvp.Value is JObject orbData)
                        {
                            var orbStats = new OrbPlayData
                            {
                                Id = orbData["id"]?.ToString() ?? kvp.Key,
                                Name = orbData["name"]?.ToString() ?? kvp.Key,
                                DamageDealt = orbData["damageDealt"]?.Value<int>() ?? 0,
                                TimesFired = orbData["timesFired"]?.Value<int>() ?? 0,
                                TimesDiscarded = orbData["timesDiscarded"]?.Value<int>() ?? 0,
                                TimesRemoved = orbData["timesRemoved"]?.Value<int>() ?? 0,
                                Starting = orbData["starting"]?.Value<bool>() ?? false,
                                AmountInDeck = orbData["amountInDeck"]?.Value<int>() ?? 0,
                                HighestCruciballBeat = orbData["highestCruciballBeat"]?.Value<int>() ?? 0
                            };

                            // Parse level instances array
                            var levelInstancesArray = orbData["levelInstances"] as JArray;
                            if (levelInstancesArray != null && levelInstancesArray.Count >= 3)
                            {
                                orbStats.LevelInstances = new int[3]
                                {
                                    levelInstancesArray[0]?.Value<int>() ?? 0,
                                    levelInstancesArray[1]?.Value<int>() ?? 0,
                                    levelInstancesArray[2]?.Value<int>() ?? 0
                                };
                            }

                            run.OrbStats[kvp.Key] = orbStats;
                        }
                    }
                }

                // Parse enemy data array (not dictionary)
                var enemyPlayData = runObj["enemyPlayData"] as JArray;
                if (enemyPlayData != null && enemyPlayData.Count > 0)
                {
                    foreach (var enemyToken in enemyPlayData)
                    {
                        if (enemyToken is JObject enemy)
                        {
                            var name = enemy["name"]?.ToString() ?? "Unknown";
                            var enemyData = new EnemyPlayData
                            {
                                Name = name,
                                AmountFought = enemy["amountFought"]?.Value<int>() ?? 0,
                                MeleeDamageReceived = enemy["meleeDamageReceived"]?.Value<int>() ?? 0,
                                RangedDamageReceived = enemy["rangedDamageReceived"]?.Value<int>() ?? 0,
                                DefeatedBy = enemy["defeatedBy"]?.Value<bool>() ?? false
                            };

                            run.EnemyData[name] = enemyData;
                        }
                    }
                }

                // Also check for legacy "enemyData" format for backwards compatibility
                var legacyEnemyData = runObj["enemyData"] as JObject;
                if (legacyEnemyData != null)
                {
                    foreach (var kvp in legacyEnemyData)
                    {
                        if (kvp.Value is JObject enemy)
                        {
                            var enemyData = new EnemyPlayData
                            {
                                Name = enemy["name"]?.ToString() ?? kvp.Key,
                                AmountFought = enemy["amountFought"]?.Value<int>() ?? 0,
                                MeleeDamageReceived = enemy["meleeDamageReceived"]?.Value<int>() ?? 0,
                                RangedDamageReceived = enemy["rangedDamageReceived"]?.Value<int>() ?? 0,
                                DefeatedBy = enemy["defeatedBy"]?.Value<bool>() ?? false
                            };

                            run.EnemyData[kvp.Key] = enemyData;
                        }
                    }
                }

                return run;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing stats file run record: {ex.Message}");
                return null;
            }
        }

        private List<RunRecord> ParseRunData(JToken runData)
        {
            var runs = new List<RunRecord>();

            if (runData is JArray runArray)
            {
                foreach (var runToken in runArray)
                {
                    var run = ParseSingleRun(runToken);
                    if (run != null) runs.Add(run);
                }
            }
            else if (runData is JObject runObject)
            {
                // Check if it's a collection of runs with keys
                foreach (var kvp in runObject)
                {
                    var run = ParseSingleRun(kvp.Value);
                    if (run != null) runs.Add(run);
                }
            }

            return runs;
        }

        private RunRecord? ParseSingleRun(JToken runToken)
        {
            try
            {
                if (runToken is not JObject runObj) return null;

                var run = new RunRecord
                {
                    Id = runObj["id"]?.ToString() ?? Guid.NewGuid().ToString(),
                    Timestamp = ParseTimestamp(runObj["timestamp"] ?? runObj["date"] ?? runObj["completedAt"]),
                    Won = runObj["won"]?.Value<bool>() ?? runObj["victory"]?.Value<bool>() ?? false,
                    Score = runObj["score"]?.Value<long>() ?? 0,
                    DamageDealt = runObj["damageDealt"]?.Value<long>() ?? runObj["totalDamage"]?.Value<long>() ?? 0,
                    PegsHit = runObj["pegsHit"]?.Value<int>() ?? 0,
                    Duration = ParseDuration(runObj["duration"] ?? runObj["time"]),
                    CharacterClass = runObj["characterClass"]?.ToString() ?? runObj["class"]?.ToString() ?? "Unknown",
                    Seed = runObj["seed"]?.ToString(),
                    FinalLevel = runObj["finalLevel"]?.Value<int>() ?? runObj["level"]?.Value<int>() ?? 0,
                    CoinsEarned = runObj["coinsEarned"]?.Value<long>() ?? runObj["coins"]?.Value<long>() ?? 0
                };

                // Parse orbs used in this run
                var orbsUsed = runObj["orbsUsed"] ?? runObj["orbs"];
                if (orbsUsed is JArray orbArray)
                {
                    run.OrbsUsed = orbArray.Select(o => o.ToString()).ToList();
                }

                // Parse relics used in this run
                var relicsUsed = runObj["relicsUsed"] ?? runObj["relics"];
                if (relicsUsed is JArray relicArray)
                {
                    run.RelicsUsed = relicArray.Select(r => r.ToString()).ToList();
                }

                return run;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing run record: {ex.Message}");
                return null;
            }
        }

        private List<RunRecord> ReconstructFromStats(JObject data)
        {
            var runs = new List<RunRecord>();

            try
            {
                var statsData = data["PermanentStats"]?["Value"] as JObject;
                if (statsData == null) return runs;

                var totalRuns = (int)(statsData["totalRuns"] ?? 0);
                var totalWins = (int)(statsData["totalWins"] ?? 0);

                // Create placeholder records based on stats
                // This is a fallback when detailed run history isn't available
                for (int i = 0; i < totalRuns; i++)
                {
                    runs.Add(new RunRecord
                    {
                        Id = $"reconstructed_{i}",
                        Timestamp = DateTime.Now.AddDays(-i), // Fake timestamps
                        Won = i < totalWins, // Assume recent runs were wins
                        Score = 0,
                        DamageDealt = 0,
                        PegsHit = 0,
                        Duration = TimeSpan.Zero,
                        CharacterClass = "Unknown",
                        Seed = null,
                        FinalLevel = 0,
                        CoinsEarned = 0,
                        OrbsUsed = new List<string>(),
                        RelicsUsed = new List<string>(),
                        IsReconstructed = true
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reconstructing from stats: {ex.Message}");
            }

            return runs;
        }

        private DateTime ParseTimestamp(JToken? timestampToken)
        {
            if (timestampToken == null) return DateTime.Now;

            try
            {
                if (timestampToken.Type == JTokenType.Date)
                {
                    return timestampToken.Value<DateTime>();
                }

                var timestampStr = timestampToken.ToString();
                if (DateTime.TryParse(timestampStr, out var dateTime))
                {
                    return dateTime;
                }

                // Try parsing as Unix timestamp
                if (long.TryParse(timestampStr, out var unixTime))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;
                }
            }
            catch
            {
                // Fall through to default
            }

            return DateTime.Now;
        }

        private TimeSpan ParseDuration(JToken? durationToken)
        {
            if (durationToken == null) return TimeSpan.Zero;

            try
            {
                var durationStr = durationToken.ToString();

                // Try parsing as seconds
                if (double.TryParse(durationStr, out var seconds))
                {
                    return TimeSpan.FromSeconds(seconds);
                }

                // Try parsing as TimeSpan string
                if (TimeSpan.TryParse(durationStr, out var timeSpan))
                {
                    return timeSpan;
                }
            }
            catch
            {
                // Fall through to default
            }

            return TimeSpan.Zero;
        }

        public void ExportRunHistory(List<RunRecord> runs, string filePath)
        {
            try
            {
                var exportData = new
                {
                    ExportedAt = DateTime.Now,
                    TotalRuns = runs.Count,
                    Runs = runs
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = System.Text.Json.JsonSerializer.Serialize(exportData, options);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export run history: {ex.Message}");
            }
        }

        public void DumpRawRunHistoryData(JObject saveData, string filePath)
        {
            try
            {
                var dump = new JObject();
                dump["dumpedAt"] = DateTime.Now;
                dump["description"] = "Raw run history and permanent stats data from stats file";

                var sources = new JObject();

                // First check if this is a stats file (contains RunStatsHistory) - the primary source
                var runStatsHistory = saveData["RunStatsHistory"]?["Value"]?["runsHistory"];
                // Also check if it's wrapped in a "data" key (from dumped format)
                if (runStatsHistory == null)
                {
                    runStatsHistory = saveData["data"]?["RunStatsHistory"]?["Value"]?["runsHistory"];
                }

                if (runStatsHistory is JArray statsArray)
                {
                    sources["RunStatsHistory.Value.runsHistory"] = new JObject
                    {
                        ["found"] = true,
                        ["type"] = "Array",
                        ["count"] = statsArray.Count,
                        ["description"] = "Primary run history data from stats file",
                        ["data"] = runStatsHistory
                    };
                }
                else
                {
                    sources["RunStatsHistory.Value.runsHistory"] = new JObject
                    {
                        ["found"] = false,
                        ["description"] = "Primary run history location (stats file)"
                    };
                }

                // Also check the full RunStatsHistory structure for context
                var fullRunStatsHistory = saveData["RunStatsHistory"] ?? saveData["data"]?["RunStatsHistory"];
                if (fullRunStatsHistory != null)
                {
                    sources["RunStatsHistory"] = new JObject
                    {
                        ["found"] = true,
                        ["type"] = fullRunStatsHistory.Type.ToString(),
                        ["description"] = "Full RunStatsHistory object",
                        ["data"] = fullRunStatsHistory
                    };
                }

                // Extract data from possible save file locations as fallback
                var data = saveData["peglinData"] as JObject ?? saveData["data"] as JObject;
                if (data != null)
                {
                    // Direct run history locations (fallback for save files)
                    var runHistoryPaths = new Dictionary<string, string[]>
                    {
                        ["runHistory"] = new[] { "runHistory" },
                        ["completedRuns"] = new[] { "completedRuns" },
                        ["gameHistory"] = new[] { "gameHistory" },
                        ["sessionHistory"] = new[] { "sessionHistory" },
                        ["runs"] = new[] { "runs" },
                        ["permanentStats.runHistory"] = new[] { "PermanentStats", "Value", "runHistory" },
                        ["permanentStats.completedRuns"] = new[] { "PermanentStats", "Value", "completedRuns" },
                        ["gameData.runHistory"] = new[] { "gameData", "runHistory" }
                    };

                    foreach (var kvp in runHistoryPaths)
                    {
                        JToken? current = data;
                        foreach (var key in kvp.Value)
                        {
                            current = current?[key];
                            if (current == null) break;
                        }

                        if (current != null)
                        {
                            sources[kvp.Key] = new JObject
                            {
                                ["found"] = true,
                                ["type"] = current.Type.ToString(),
                                ["count"] = current.Type == JTokenType.Array ? ((JArray)current).Count :
                                          current.Type == JTokenType.Object ? ((JObject)current).Count : 0,
                                ["data"] = current
                            };
                        }
                        else
                        {
                            sources[kvp.Key] = new JObject
                            {
                                ["found"] = false
                            };
                        }
                    }

                    // Also dump PermanentStats for additional context
                    var permanentStats = data["PermanentStats"]?["Value"];
                    if (permanentStats != null)
                    {
                        sources["permanentStatsOverview"] = new JObject
                        {
                            ["found"] = true,
                            ["totalRuns"] = permanentStats["totalRuns"],
                            ["totalWins"] = permanentStats["totalWins"],
                            ["totalDeaths"] = permanentStats["totalDeaths"],
                            ["totalDamageDealt"] = permanentStats["totalDamageDealt"],
                            ["totalPegsHit"] = permanentStats["totalPegsHit"],
                            ["totalCoinsEarned"] = permanentStats["totalCoinsEarned"],
                            ["totalRelicsCollected"] = permanentStats["totalRelicsCollected"],
                            ["allStats"] = permanentStats
                        };
                    }

                    // Focus only on permanent stats, not additional broad searching
                    // Comment out the broad search to avoid dumping entire save file
                    /*
                    // Look for any other potential run-related data
                    var searchTerms = new[] { "run", "Run", "session", "Session", "game", "Game", "history", "History" };
                    var additionalFindings = new JObject();
                    
                    SearchForRunRelatedData(data, searchTerms, "", additionalFindings);
                    
                    if (additionalFindings.Count > 0)
                    {
                        sources["additionalFindings"] = additionalFindings;
                    }
                    */
                }

                dump["sources"] = sources;

                // Try to parse and normalize any found run data using the same logic as ExtractRunHistory
                var normalizedRuns = new JArray();

                // Use ExtractRunHistory to get properly parsed run records
                var extractedRuns = ExtractRunHistory(saveData);
                if (extractedRuns.Count > 0)
                {
                    normalizedRuns.Add(new JObject
                    {
                        ["source"] = "ExtractRunHistory",
                        ["runCount"] = extractedRuns.Count,
                        ["runs"] = JArray.FromObject(extractedRuns)
                    });
                }

                // Also try parsing raw data from found sources
                foreach (var source in sources)
                {
                    if (source.Value is JObject sourceObj &&
                        sourceObj["found"]?.Value<bool>() == true &&
                        sourceObj["data"] != null)
                    {
                        var sourceRuns = ParseRunDataWithAllFields(sourceObj["data"]!);
                        if (sourceRuns.Count > 0)
                        {
                            normalizedRuns.Add(new JObject
                            {
                                ["source"] = source.Key,
                                ["runCount"] = sourceRuns.Count,
                                ["runs"] = sourceRuns
                            });
                        }
                    }
                }

                dump["normalizedRuns"] = normalizedRuns;

                // Write the dump
                var json = dump.ToString(Formatting.Indented);
                File.WriteAllText(filePath, json);

                // Also create a separate file with just the raw data for easier viewing
                var rawDataPath = Path.Combine(
                    Path.GetDirectoryName(filePath) ?? "",
                    Path.GetFileNameWithoutExtension(filePath) + "_raw.json"
                );

                var rawDump = new JObject();
                foreach (var source in sources)
                {
                    if (source.Value is JObject sourceObj &&
                        sourceObj["found"]?.Value<bool>() == true &&
                        sourceObj["data"] != null)
                    {
                        rawDump[source.Key] = sourceObj["data"];
                    }
                }

                File.WriteAllText(rawDataPath, rawDump.ToString(Formatting.Indented));
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to dump raw run history data: {ex.Message}");
            }
        }

        private void SearchForRunRelatedData(JToken token, string[] searchTerms, string path, JObject results)
        {
            if (token == null || path.Length > 100) return; // Limit depth

            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = (JObject)token;
                    foreach (var property in obj.Properties())
                    {
                        var propertyPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";

                        // Check if property name contains any search terms
                        if (searchTerms.Any(term => property.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
                        {
                            // Skip if it's one of our already known paths
                            if (!IsKnownPath(propertyPath))
                            {
                                results[propertyPath] = new JObject
                                {
                                    ["type"] = property.Value?.Type.ToString(),
                                    ["preview"] = GetPreview(property.Value)
                                };
                            }
                        }

                        // Recurse into the value
                        SearchForRunRelatedData(property.Value, searchTerms, propertyPath, results);
                    }
                    break;

                case JTokenType.Array:
                    // Don't recurse into arrays to avoid too much noise
                    break;
            }
        }

        private bool IsKnownPath(string path)
        {
            var knownPaths = new[]
            {
                "runHistory", "completedRuns", "gameHistory", "sessionHistory", "runs",
                "PermanentStats", "gameData.runHistory"
            };

            return knownPaths.Any(known => path.Contains(known, StringComparison.OrdinalIgnoreCase));
        }

        private JToken GetPreview(JToken? token)
        {
            if (token == null) return JValue.CreateNull();

            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = (JObject)token;
                    return $"Object with {obj.Count} properties";

                case JTokenType.Array:
                    var arr = (JArray)token;
                    return $"Array with {arr.Count} items";

                case JTokenType.String:
                    var str = token.ToString();
                    return str.Length > 50 ? str.Substring(0, 50) + "..." : str;

                default:
                    return token;
            }
        }

        private JArray ParseRunDataWithAllFields(JToken runData)
        {
            var runs = new JArray();

            if (runData is JArray runArray)
            {
                foreach (var runToken in runArray)
                {
                    var run = ParseSingleRunWithAllFields(runToken);
                    if (run != null) runs.Add(run);
                }
            }
            else if (runData is JObject runObject)
            {
                foreach (var kvp in runObject)
                {
                    var run = ParseSingleRunWithAllFields(kvp.Value);
                    if (run != null)
                    {
                        run["key"] = kvp.Key; // Preserve the key if it exists
                        runs.Add(run);
                    }
                }
            }

            return runs;
        }

        private JObject? ParseSingleRunWithAllFields(JToken runToken)
        {
            try
            {
                if (runToken is not JObject runObj) return null;

                // Create a new object that includes ALL fields from the original
                var result = new JObject();

                // Copy all original fields
                foreach (var property in runObj.Properties())
                {
                    result[property.Name] = property.Value;
                }

                // Add normalized fields for our known properties
                result["_normalized"] = new JObject
                {
                    ["id"] = runObj["id"]?.ToString() ?? runObj["runId"]?.ToString() ?? Guid.NewGuid().ToString(),
                    ["timestamp"] = ParseTimestamp(runObj["timestamp"] ?? runObj["date"] ?? runObj["completedAt"]),
                    ["won"] = runObj["won"] ?? runObj["victory"] ?? runObj["isWin"] ?? false,
                    ["score"] = runObj["score"] ?? runObj["finalScore"] ?? 0,
                    ["damageDealt"] = runObj["damageDealt"] ?? runObj["totalDamage"] ?? runObj["damage"] ?? 0,
                    ["pegsHit"] = runObj["pegsHit"] ?? runObj["totalPegsHit"] ?? runObj["pegs"] ?? 0,
                    ["duration"] = ParseDuration(runObj["duration"] ?? runObj["time"] ?? runObj["playTime"]),
                    ["characterClass"] = runObj["characterClass"] ?? runObj["class"] ?? runObj["character"] ?? "Unknown",
                    ["seed"] = runObj["seed"] ?? runObj["gameSeed"],
                    ["finalLevel"] = runObj["finalLevel"] ?? runObj["level"] ?? runObj["floor"] ?? 0,
                    ["coinsEarned"] = runObj["coinsEarned"] ?? runObj["coins"] ?? runObj["gold"] ?? 0
                };

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing run record: {ex.Message}");
                return null;
            }
        }

        public List<RunRecord> ImportRunHistory(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Import file not found: {filePath}");
                }

                var json = File.ReadAllText(filePath);
                var importData = System.Text.Json.JsonSerializer.Deserialize<RunHistoryExport>(json);

                return importData?.Runs ?? new List<RunRecord>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to import run history: {ex.Message}");
            }
        }

        public void UpdateSaveFileWithRuns(string saveFilePath, List<RunRecord> newRuns)
        {
            try
            {
                if (!File.Exists(saveFilePath))
                {
                    throw new FileNotFoundException($"Save file not found: {saveFilePath}");
                }

                // Load Peglin assembly for proper type context
                LoadPeglinAssembly();

                // Use OdinSerializer with proper type context to update the save file
                UpdateSaveFileWithOdin(saveFilePath, newRuns);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update save file: {ex.Message}");
            }
        }

        private void UpdateSaveFileDirectly(string saveFilePath, List<RunRecord> newRuns)
        {
            // Read and deserialize the stats file using SaveFileDumper
            var statsBytes = File.ReadAllBytes(saveFilePath);
            var dumper = new SaveFileDumper(configManager);
            var statsJson = dumper.DumpSaveFile(statsBytes);
            var statsData = JObject.Parse(statsJson);
            
            // Extract the runs history array
            var runsHistoryToken = statsData["data"]?["RunStatsHistory"]?["Value"]?["runsHistory"];
            if (runsHistoryToken is not JArray runsArray)
            {
                throw new Exception("Could not find runsHistory array in stats file");
            }

            // Convert imported RunRecord objects back to raw stats format
            var newRawRuns = ConvertRunRecordsToRawFormat(newRuns);
            
            // Replace the bottom entries (oldest runs) with new ones
            var maxRuns = Math.Min(runsArray.Count, 20); // Stats file typically holds 20 runs
            var runsToReplace = Math.Min(newRuns.Count, maxRuns);
            
            // Remove the oldest runs (from the end of the array)
            for (int i = 0; i < runsToReplace; i++)
            {
                if (runsArray.Count > 0)
                {
                    runsArray.RemoveAt(runsArray.Count - 1);
                }
            }
            
            // Add new runs at the beginning (most recent)
            for (int i = newRawRuns.Count - 1; i >= 0; i--)
            {
                runsArray.Insert(0, newRawRuns[i]);
            }
            
            // Ensure we don't exceed the typical 20-run limit
            while (runsArray.Count > 20)
            {
                runsArray.RemoveAt(runsArray.Count - 1);
            }

            // Update the modified data back into the stats structure
            statsData["data"]!["RunStatsHistory"]!["Value"]!["runsHistory"] = runsArray;
            
            // For now, we'll document this limitation - full binary rewrite would require
            // more complex OdinSerializer integration with the exact Unity serialization context
            Logger.Warning("Save file updating is currently limited to displaying the structure.");
            Logger.Warning("Full binary rewrite requires matching Unity's exact serialization context.");
            Logger.Info($"Would replace {runsToReplace} oldest runs with {newRuns.Count} imported runs.");
            Logger.Info("The updated structure has been prepared but not written to preserve save file integrity.");
        }

        private List<JObject> ConvertRunRecordsToRawFormat(List<RunRecord> runs)
        {
            var rawRuns = new List<JObject>();
            
            foreach (var run in runs)
            {
                // Convert RunRecord back to the raw stats format structure
                var rawRun = new JObject
                {
                    ["runId"] = run.Id ?? Guid.NewGuid().ToString(),
                    ["hasWon"] = run.Won,
                    ["isCustomRun"] = run.IsCustomRun,
                    ["selectedClass"] = GetClassIndexFromName(run.CharacterClass),
                    ["cruciballLevel"] = run.CruciballLevel,
                    ["defeatedBy"] = string.IsNullOrEmpty(run.DefeatedBy) ? null : run.DefeatedBy,
                    ["finalHp"] = run.FinalHp,
                    ["maxHp"] = run.MaxHp,
                    ["defeatedOnLevel"] = 0, // Not stored in RunRecord
                    ["defeatedOnRoom"] = run.DefeatedOnRoom,
                    ["mostDamageDealtWithSingleAttack"] = run.MostDamageDealtWithSingleAttack,
                    ["totalDamageNegated"] = run.TotalDamageNegated,
                    ["totalDamageDealt"] = run.DamageDealt,
                    ["pegsHit"] = run.PegsHit,
                    ["pegsHitRefresh"] = run.PegsHitRefresh,
                    ["pegsHitCrit"] = run.PegsHitCrit,
                    ["pegsRefreshed"] = run.PegsRefreshed,
                    ["bombsThrown"] = run.BombsThrown,
                    ["bombsThrownRigged"] = run.BombsThrownRigged,
                    ["bombsCreated"] = run.BombsCreated,
                    ["bombsCreatedRigged"] = run.BombsCreatedRigged,
                    ["mostPegsHitInOneTurn"] = run.MostPegsHitInOneTurn,
                    ["coinsEarned"] = run.CoinsEarned,
                    ["coinsSpent"] = run.CoinsSpent,
                    ["shotsTaken"] = run.ShotsTaken,
                    ["critShotsTaken"] = run.CritShotsTaken,
                    ["runTimerElapsedMilliseconds"] = (long)run.Duration.TotalMilliseconds,
                    ["startDate"] = run.StartDate.ToString("yyyy-MM-ddTHH:mm:ss.fffffffK"),
                    ["endDate"] = run.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffK"),
                    ["seed"] = run.Seed ?? "",
                };

                // Add orb play data if available
                if (run.OrbStats.Any())
                {
                    var orbPlayData = new JArray();
                    foreach (var orbStat in run.OrbStats.Values)
                    {
                        var orbData = new JObject
                        {
                            ["id"] = GetOrbIdFromName(orbStat.Name), // This would need mapping
                            ["name"] = orbStat.Name,
                            ["damageDealt"] = orbStat.DamageDealt,
                            ["timesFired"] = orbStat.TimesFired,
                            ["timesDiscarded"] = orbStat.TimesDiscarded,
                            ["timesRemoved"] = orbStat.TimesRemoved,
                            ["starting"] = orbStat.Starting,
                            ["amountInDeck"] = orbStat.AmountInDeck,
                            ["levelInstances"] = new JArray(orbStat.LevelInstances ?? new int[0]),
                            ["highestCruciballBeat"] = orbStat.HighestCruciballBeat
                        };
                        orbPlayData.Add(orbData);
                    }
                    rawRun["orbPlayData"] = orbPlayData;
                }
                else
                {
                    rawRun["orbPlayData"] = new JArray();
                }

                // Add visited rooms data
                if (run.VisitedRooms.Any())
                {
                    rawRun["visitedRooms"] = new JArray(run.VisitedRooms);
                }
                else
                {
                    rawRun["visitedRooms"] = new JArray();
                }

                // Add relics data
                if (run.RelicIds.Any())
                {
                    rawRun["relics"] = new JArray(run.RelicIds);
                }
                else
                {
                    rawRun["relics"] = new JArray();
                }

                rawRuns.Add(rawRun);
            }
            
            return rawRuns;
        }

        private int GetClassIndexFromName(string className)
        {
            // Map character class names back to indices
            return className.ToLower() switch
            {
                "peglin" => 0,
                "roundrel" => 1, 
                "balladin" => 2,
                "spinventor" => 3,
                _ => 0
            };
        }

        private string GetOrbIdFromName(string orbName)
        {
            // Basic mapping of orb names to IDs - would need to be expanded
            if (orbName.StartsWith("StoneOrb")) return "stone";
            if (orbName.StartsWith("Splatorb")) return "splatorb";
            if (orbName.StartsWith("Ballm")) return "ballm";
            if (orbName.StartsWith("BuffOrb")) return "buff";
            // Add more mappings as needed
            return orbName.ToLower().Replace("-", "").Replace("orb", "");
        }

        private void LoadPeglinAssembly()
        {
            if (peglinAssembly != null) return;

            // Set up assembly resolution handler for missing Unity dependencies
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            try
            {
                // Use the same approach as SaveFileDumper
                var assemblyPaths = new List<string>();

                // First check if we have a configured path
                var configuredPath = configManager.GetEffectivePeglinPath();
                if (!string.IsNullOrEmpty(configuredPath))
                {
                    var dllPath = PeglinPathHelper.GetAssemblyPath(configuredPath);
                    if (!string.IsNullOrEmpty(dllPath) && File.Exists(dllPath))
                    {
                        assemblyPaths.Add(dllPath);
                    }
                }

                // Try auto-detected Peglin installations
                var detectedInstallations = configManager.DetectPeglinInstallations();
                foreach (var installation in detectedInstallations)
                {
                    var dllPath = PeglinPathHelper.GetAssemblyPath(installation);
                    if (!string.IsNullOrEmpty(dllPath) && File.Exists(dllPath))
                    {
                        assemblyPaths.Add(dllPath);
                    }
                }

                // Try to load assembly from any of the found paths
                foreach (var path in assemblyPaths.Distinct())
                {
                    try
                    {
                        peglinAssembly = Assembly.LoadFrom(path);
                        Logger.Debug($"Successfully loaded Peglin assembly from: {path}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Failed to load assembly from {path}: {ex.Message}");
                    }
                }

                if (peglinAssembly == null)
                {
                    Logger.Warning("Could not load Peglin assembly - save file updates may not work correctly");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting up Peglin assembly loading: {ex.Message}");
            }
        }

        private Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            // Handle Unity assembly resolution for OdinSerializer
            var assemblyName = new AssemblyName(args.Name);
            
            // Common Unity assemblies that might be requested
            var unityAssemblies = new[] { "UnityEngine", "UnityEngine.CoreModule", "UnityEditor" };
            
            if (unityAssemblies.Any(name => assemblyName.Name?.StartsWith(name) == true))
            {
                // Try to find Unity assemblies in the odin-serializer Libraries folder
                var unityLibPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
                    "..", "..", "..", "odin-serializer", "Libraries", "Unity"
                );
                
                if (Directory.Exists(unityLibPath))
                {
                    var dllPath = Path.Combine(unityLibPath, $"{assemblyName.Name}.dll");
                    if (File.Exists(dllPath))
                    {
                        try
                        {
                            return Assembly.LoadFrom(dllPath);
                        }
                        catch
                        {
                            // Ignore load failures
                        }
                    }
                }
            }
            
            return null;
        }

        private void UpdateSaveFileWithOdin(string saveFilePath, List<RunRecord> newRuns)
        {
            try
            {
                // Read the original save file
                var saveData = File.ReadAllBytes(saveFilePath);
                
                // Deserialize using OdinSerializer with proper context
                object deserializedData;
                try
                {
                    deserializedData = SerializationUtility.DeserializeValue<object>(saveData, DataFormat.Binary);
                }
                catch
                {
                    // Try with Unity context
                    var context = new DeserializationContext();
                    context.Config.SerializationPolicy = SerializationPolicies.Everything;
                    deserializedData = SerializationUtility.DeserializeValue<object>(saveData, DataFormat.Binary, context);
                }

                if (deserializedData == null)
                {
                    throw new Exception("Could not deserialize save data");
                }

                // Update the run history data
                var updatedData = UpdateRunHistoryInObject(deserializedData, newRuns);

                // Serialize back with same context
                byte[] updatedSaveData;
                try
                {
                    updatedSaveData = SerializationUtility.SerializeValue(updatedData, DataFormat.Binary);
                }
                catch
                {
                    // Try with Unity context
                    var context = new SerializationContext();
                    context.Config.SerializationPolicy = SerializationPolicies.Everything;
                    updatedSaveData = SerializationUtility.SerializeValue(updatedData, DataFormat.Binary, context);
                }

                // Create backup before writing
                var backupPath = saveFilePath + ".backup";
                File.Copy(saveFilePath, backupPath, true);
                Logger.Info($"Created backup: {backupPath}");

                // Write the updated save data
                File.WriteAllBytes(saveFilePath, updatedSaveData);
                Logger.Info($"Successfully updated save file with {newRuns.Count} runs");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update save file with OdinSerializer: {ex.Message}");
            }
        }

        private object UpdateRunHistoryInObject(object saveData, List<RunRecord> newRuns)
        {
            // Convert to JSON for manipulation, then back - this preserves most type information
            var json = JsonConvert.SerializeObject(saveData, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Include
            });

            var jObject = JObject.Parse(json);
            
            // Find and update the runs history
            var runsHistoryToken = jObject.SelectToken("$..RunStatsHistory.Value.runsHistory");
            if (runsHistoryToken is JArray runsArray)
            {
                // Convert RunRecord objects to raw format and replace oldest entries
                var newRawRuns = ConvertRunRecordsToRawFormat(newRuns);
                var runsToReplace = Math.Min(newRuns.Count, runsArray.Count);
                
                // Remove oldest runs
                for (int i = 0; i < runsToReplace && runsArray.Count > 0; i++)
                {
                    runsArray.RemoveAt(runsArray.Count - 1);
                }
                
                // Add new runs at the beginning
                for (int i = newRawRuns.Count - 1; i >= 0; i--)
                {
                    runsArray.Insert(0, newRawRuns[i]);
                }
                
                // Ensure we don't exceed the limit
                while (runsArray.Count > 20)
                {
                    runsArray.RemoveAt(runsArray.Count - 1);
                }
                
                Logger.Info($"Replaced {runsToReplace} oldest runs with {newRuns.Count} imported runs");
            }
            else
            {
                throw new Exception("Could not find runsHistory array in save data");
            }

            // Convert back to the original object type
            var originalType = saveData.GetType();
            var updatedJson = jObject.ToString();
            var updatedObject = JsonConvert.DeserializeObject(updatedJson, originalType, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            });

            return updatedObject ?? throw new Exception("Failed to convert updated data back to original type");
        }

        private object? DeserializeSaveData(byte[] saveData)
        {
            try
            {
                // Try to deserialize using OdinSerializer with binary format
                var deserializedData = SerializationUtility.DeserializeValue<object>(saveData, DataFormat.Binary);

                if (deserializedData == null)
                {
                    // Try with Unity serialization policy
                    var context = new DeserializationContext();
                    context.Config.SerializationPolicy = SerializationPolicies.Everything;
                    deserializedData = SerializationUtility.DeserializeValue<object>(saveData, DataFormat.Binary, context);
                }

                return deserializedData;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to deserialize save data: {ex.Message}");
            }
        }

        private byte[] SerializeSaveData(object saveData)
        {
            try
            {
                // Serialize using OdinSerializer with binary format
                var serializedData = SerializationUtility.SerializeValue(saveData, DataFormat.Binary);
                return serializedData;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to serialize save data: {ex.Message}");
            }
        }

        private object MergeRunHistory(object? saveData, List<RunRecord> newRuns)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData), "Save data is null");
            }

            // Convert to JObject for easier manipulation
            var json = JsonConvert.SerializeObject(saveData, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Include
            });

            var jObject = JObject.Parse(json);

            // Find and update run history in the appropriate location
            UpdateRunHistoryInJObject(jObject, newRuns);

            // Convert back to original object type
            var originalType = saveData.GetType();
            var updatedJson = jObject.ToString();
            var updatedObject = JsonConvert.DeserializeObject(updatedJson, originalType);

            return updatedObject ?? throw new Exception("Failed to convert updated data back to original type");
        }

        private void UpdateRunHistoryInJObject(JObject saveData, List<RunRecord> newRuns)
        {
            // Look for the most appropriate location to update run history
            var runHistoryPaths = new[]
            {
                new[] { "peglinData", "runHistory" },
                new[] { "data", "runHistory" },
                new[] { "peglinData", "completedRuns" },
                new[] { "data", "completedRuns" },
                new[] { "peglinData", "gameHistory" },
                new[] { "data", "gameHistory" },
                new[] { "peglinData", "PermanentStats", "Value", "runHistory" },
                new[] { "data", "PermanentStats", "Value", "runHistory" }
            };

            bool updated = false;

            foreach (var path in runHistoryPaths)
            {
                JToken? current = saveData;
                JToken? parent = null;
                string? lastKey = null;

                // Navigate to the path
                foreach (var key in path)
                {
                    parent = current;
                    lastKey = key;
                    current = current?[key];
                    if (current == null) break;
                }

                // If we found a valid location
                if (current != null && parent != null && lastKey != null)
                {
                    // Convert new runs to JArray
                    var runArray = new JArray();

                    // Add existing runs if any
                    if (current is JArray existingArray)
                    {
                        runArray = existingArray;
                    }

                    // Add new runs
                    foreach (var run in newRuns)
                    {
                        var runJson = new JObject
                        {
                            ["id"] = run.Id,
                            ["timestamp"] = run.Timestamp,
                            ["won"] = run.Won,
                            ["score"] = run.Score,
                            ["damageDealt"] = run.DamageDealt,
                            ["pegsHit"] = run.PegsHit,
                            ["duration"] = run.Duration.TotalSeconds,
                            ["characterClass"] = run.CharacterClass,
                            ["seed"] = run.Seed,
                            ["finalLevel"] = run.FinalLevel,
                            ["coinsEarned"] = run.CoinsEarned,
                            ["orbsUsed"] = new JArray(run.OrbsUsed),
                            ["relicsUsed"] = new JArray(run.RelicsUsed)
                        };

                        runArray.Add(runJson);
                    }

                    // Update the parent with the new array
                    parent[lastKey] = runArray;
                    updated = true;

                    // Also update stats if applicable
                    UpdateStatsInJObject(saveData, runArray);

                    break;
                }
            }

            if (!updated)
            {
                // If no existing location found, create one
                var data = saveData["peglinData"] as JObject ?? saveData["data"] as JObject;
                if (data == null)
                {
                    data = new JObject();
                    saveData["data"] = data;
                }

                var runArray = new JArray();
                foreach (var run in newRuns)
                {
                    var runJson = new JObject
                    {
                        ["id"] = run.Id,
                        ["timestamp"] = run.Timestamp,
                        ["won"] = run.Won,
                        ["score"] = run.Score,
                        ["damageDealt"] = run.DamageDealt,
                        ["pegsHit"] = run.PegsHit,
                        ["duration"] = run.Duration.TotalSeconds,
                        ["characterClass"] = run.CharacterClass,
                        ["seed"] = run.Seed,
                        ["finalLevel"] = run.FinalLevel,
                        ["coinsEarned"] = run.CoinsEarned,
                        ["orbsUsed"] = new JArray(run.OrbsUsed),
                        ["relicsUsed"] = new JArray(run.RelicsUsed)
                    };

                    runArray.Add(runJson);
                }

                data["runHistory"] = runArray;
                UpdateStatsInJObject(saveData, runArray);
            }
        }

        private void UpdateStatsInJObject(JObject saveData, JArray runHistory)
        {
            // Update PermanentStats if it exists
            var statsPaths = new[]
            {
                new[] { "peglinData", "PermanentStats", "Value" },
                new[] { "data", "PermanentStats", "Value" }
            };

            foreach (var path in statsPaths)
            {
                JToken? stats = saveData;
                foreach (var key in path)
                {
                    stats = stats?[key];
                    if (stats == null) break;
                }

                if (stats is JObject statsObj)
                {
                    // Update total runs and wins
                    var totalRuns = runHistory.Count;
                    var totalWins = runHistory.Where(r => r["won"]?.Value<bool>() ?? false).Count();

                    statsObj["totalRuns"] = totalRuns;
                    statsObj["totalWins"] = totalWins;

                    break;
                }
            }
        }

        public Dictionary<string, ClassStatistics> GetClassStatistics(List<RunRecord> runs)
        {
            var classStats = new Dictionary<string, ClassStatistics>();

            foreach (var run in runs)
            {
                if (!classStats.ContainsKey(run.CharacterClass))
                {
                    classStats[run.CharacterClass] = new ClassStatistics { ClassName = run.CharacterClass };
                }

                var stats = classStats[run.CharacterClass];
                stats.TotalRuns++;

                if (run.Won)
                {
                    stats.Wins++;
                }

                stats.TotalDamage += run.DamageDealt;
                stats.TotalPegsHit += run.PegsHit;
                stats.TotalDuration += run.Duration;
                stats.TotalCoinsEarned += run.CoinsEarned;

                if (run.CruciballLevel > stats.HighestCruciball)
                {
                    stats.HighestCruciball = run.CruciballLevel;
                }

                if (run.DamageDealt > stats.BestDamageRun)
                {
                    stats.BestDamageRun = run.DamageDealt;
                }
            }

            return classStats;
        }

        public Dictionary<string, OrbStatistics> GetOrbStatistics(List<RunRecord> runs)
        {
            var orbStats = new Dictionary<string, OrbStatistics>();

            foreach (var run in runs.Where(r => r.OrbsUsed.Any()))
            {
                foreach (var orb in run.OrbsUsed)
                {
                    if (string.IsNullOrEmpty(orb)) continue;

                    if (!orbStats.ContainsKey(orb))
                    {
                        orbStats[orb] = new OrbStatistics { OrbName = orb };
                    }

                    var stats = orbStats[orb];
                    stats.TimesUsed++;

                    if (run.Won)
                    {
                        stats.WinsWithOrb++;
                    }

                    stats.TotalDamageWithOrb += run.DamageDealt;
                    stats.TotalRunsWithOrb++;
                }
            }

            return orbStats;
        }
    }

    public class ClassStatistics
    {
        public string ClassName { get; set; } = string.Empty;
        public int TotalRuns { get; set; }
        public int Wins { get; set; }
        public int Losses => TotalRuns - Wins;
        public double WinRate => TotalRuns > 0 ? (double)Wins / TotalRuns : 0;
        public long TotalDamage { get; set; }
        public int TotalPegsHit { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public long TotalCoinsEarned { get; set; }
        public int HighestCruciball { get; set; }
        public long BestDamageRun { get; set; }

        public double AverageDamage => TotalRuns > 0 ? (double)TotalDamage / TotalRuns : 0;
        public TimeSpan AverageDuration => TotalRuns > 0 ? TimeSpan.FromTicks(TotalDuration.Ticks / TotalRuns) : TimeSpan.Zero;
    }

    public class OrbStatistics
    {
        public string OrbName { get; set; } = string.Empty;
        public int TimesUsed { get; set; }
        public int WinsWithOrb { get; set; }
        public int TotalRunsWithOrb { get; set; }
        public long TotalDamageWithOrb { get; set; }

        public double WinRateWithOrb => TotalRunsWithOrb > 0 ? (double)WinsWithOrb / TotalRunsWithOrb : 0;
        public double AverageDamageWithOrb => TotalRunsWithOrb > 0 ? (double)TotalDamageWithOrb / TotalRunsWithOrb : 0;
    }

    public class RoomInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }

    public class RunRecord
    {
        public string Id { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool Won { get; set; }
        public long Score { get; set; }
        public long DamageDealt { get; set; }
        public int PegsHit { get; set; }
        public TimeSpan Duration { get; set; }
        public string CharacterClass { get; set; } = string.Empty;
        public string? Seed { get; set; }
        public int FinalLevel { get; set; }
        public long CoinsEarned { get; set; }
        public List<string> OrbsUsed { get; set; } = new();
        public List<string> RelicsUsed { get; set; } = new();
        public bool IsReconstructed { get; set; } = false;

        // Additional fields from stats file
        public bool IsCustomRun { get; set; } = false;
        public int CruciballLevel { get; set; } = 0;
        public string? DefeatedBy { get; set; }
        public int FinalHp { get; set; } = 0;
        public int MaxHp { get; set; } = 0;
        public long MostDamageDealtWithSingleAttack { get; set; } = 0;
        public long TotalDamageNegated { get; set; } = 0;
        public int PegsHitRefresh { get; set; } = 0;
        public int PegsHitCrit { get; set; } = 0;
        public int PegsRefreshed { get; set; } = 0;
        public int BombsThrown { get; set; } = 0;
        public int BombsThrownRigged { get; set; } = 0;
        public int MostPegsHitInOneTurn { get; set; } = 0;
        public long CoinsSpent { get; set; } = 0;
        public int ShotsTaken { get; set; } = 0;
        public int CritShotsTaken { get; set; } = 0;
        public DateTime StartDate { get; set; }

        // Additional fields from RunStats.cs structure
        public int BombsCreated { get; set; } = 0;
        public int BombsCreatedRigged { get; set; } = 0;
        public int DefeatedOnRoom { get; set; } = 0;
        public bool VampireDealTaken { get; set; } = false;
        public List<int> PegUpgradeEvents { get; set; } = new();
        public Dictionary<string, int> StacksPerStatusEffect { get; set; } = new();
        public Dictionary<string, int> SlimePegsPerSlimeType { get; set; } = new();
        public Dictionary<string, OrbPlayData> OrbStats { get; set; } = new();
        public Dictionary<string, EnemyPlayData> EnemyData { get; set; } = new();

        // Raw data arrays
        public int[] RelicIds { get; set; } = Array.Empty<int>();
        public int[] VisitedRooms { get; set; } = Array.Empty<int>();
        public int[] VisitedBosses { get; set; } = Array.Empty<int>();
        public int[] StatusEffects { get; set; } = Array.Empty<int>();
        public int[] SlimePegs { get; set; } = Array.Empty<int>();

        // Enriched data properties (populated by RunHistoryManager)
        public List<string> RelicNames { get; set; } = new();
        
        // Computed properties (fallback if RelicNames not populated)
        public List<string> BossNames => GameDataMappings.GetBossNames(VisitedBosses);
        public Dictionary<string, int> RoomTypeStatistics => GameDataMappings.GetRoomTypeStatistics(VisitedRooms);
        public List<RoomInfo> VisitedRoomsInfo => VisitedRooms.Select(roomId => new RoomInfo
        {
            Id = roomId,
            Name = GameDataMappings.GetRoomName(roomId),
            Symbol = GetRoomSymbol(roomId),
            Color = GetRoomColor(roomId)
        }).ToList();
        public List<string> ActiveStatusEffects => GameDataMappings.GetActiveStatusEffects(StatusEffects);
        public List<string> ActiveSlimePegs => GameDataMappings.GetActiveSlimePegs(SlimePegs);

        private static string GetRoomSymbol(int roomId) => roomId switch
        {
            0 => "⭕", // NONE - Circle with slash for none/empty
            1 => "⚔",  // BATTLE - Crossed swords for battle
            2 => "👹", // MINI_BOSS - Ogre face for mini boss
            3 => "💰", // TREASURE - Money bag for treasure
            4 => "🏪", // STORE - Store/shop for store
            5 => "📜", // SCENARIO - Scroll for scenario/event
            6 => "❓", // UNKNOWN - Question mark for unknown
            7 => "🐉", // BOSS - Dragon for boss
            8 => "🎯", // PEG_MINIGAME - Target for peg minigame
            _ => $"#{roomId}"
        };

        private static string GetRoomColor(int roomId) => roomId switch
        {
            0 => "default",   // NONE
            1 => "error",     // BATTLE - Red for combat
            2 => "warning",   // MINI_BOSS - Orange for mini boss
            3 => "success",   // TREASURE - Green for treasure
            4 => "info",      // STORE - Blue for store
            5 => "secondary", // SCENARIO - Purple for events
            6 => "default",   // UNKNOWN
            7 => "error",     // BOSS - Dark red for boss
            8 => "primary",   // PEG_MINIGAME - Primary color for minigames
            _ => "default"
        };
    }

    public class RunHistoryExport
    {
        public DateTime ExportedAt { get; set; }
        public int TotalRuns { get; set; }
        public List<RunRecord> Runs { get; set; } = new();
    }

    /// <summary>
    /// Orb play data structure matching the game's OrbPlayData class
    /// </summary>
    public class OrbPlayData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DamageDealt { get; set; } = 0;
        public int TimesFired { get; set; } = 0;
        public int TimesDiscarded { get; set; } = 0;
        public int TimesRemoved { get; set; } = 0;
        public bool Starting { get; set; } = false;
        public int AmountInDeck { get; set; } = 0;
        public int[] LevelInstances { get; set; } = new int[3]; // Level 1, 2, 3 instances
        public int HighestCruciballBeat { get; set; } = 0;
    }

    /// <summary>
    /// Enemy play data structure matching the game's EnemyPlayData class
    /// </summary>
    public class EnemyPlayData
    {
        public string Name { get; set; } = string.Empty;
        public int AmountFought { get; set; } = 0;
        public int MeleeDamageReceived { get; set; } = 0;
        public int RangedDamageReceived { get; set; } = 0;
        public bool DefeatedBy { get; set; } = false;
    }

    /// <summary>
    /// Persistent run history database structure
    /// </summary>
    public class PersistentRunDatabase
    {
        public int Version { get; set; } = 1;
        public DateTime LastUpdated { get; set; }
        public int TotalRuns { get; set; }
        public List<RunRecord> Runs { get; set; } = new();
    }
}
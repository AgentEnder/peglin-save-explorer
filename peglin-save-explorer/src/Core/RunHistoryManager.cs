using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OdinSerializer;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Core
{
    public class RunHistoryManager
    {
        private readonly ConfigurationManager configManager;

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
                    return runs.OrderByDescending(r => r.Timestamp).ToList();
                }

                // Otherwise, check standard save file locations
                var data = saveData["peglinData"] as JObject ?? saveData["data"] as JObject;
                if (data == null) return runs;

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

                // Don't reconstruct fake data - only use real run history
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting run history: {ex.Message}");
            }

            return runs.OrderByDescending(r => r.Timestamp).ToList();
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
                if (orbPlayData != null)
                {
                    run.OrbsUsed = orbPlayData.Select(o => o["name"]?.ToString() ?? o["id"]?.ToString() ?? "Unknown").ToList();
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

                // Parse enemy data dictionary
                var enemyData = runObj["enemyData"] as JObject;
                if (enemyData != null)
                {
                    foreach (var kvp in enemyData)
                    {
                        if (kvp.Value is JObject enemy)
                        {
                            var enemyPlayData = new EnemyPlayData
                            {
                                Name = enemy["name"]?.ToString() ?? kvp.Key,
                                AmountFought = enemy["amountFought"]?.Value<int>() ?? 0,
                                MeleeDamageReceived = enemy["meleeDamageReceived"]?.Value<int>() ?? 0,
                                RangedDamageReceived = enemy["rangedDamageReceived"]?.Value<int>() ?? 0,
                                DefeatedBy = enemy["defeatedBy"]?.Value<bool>() ?? false
                            };

                            run.EnemyData[kvp.Key] = enemyPlayData;
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

                // Read the original save file
                var saveData = File.ReadAllBytes(saveFilePath);

                // Deserialize the save data
                var deserializedData = DeserializeSaveData(saveData);

                // Update the run history
                var updatedData = MergeRunHistory(deserializedData, newRuns);

                // Serialize back to binary format
                var updatedSaveData = SerializeSaveData(updatedData);

                // Create backup before writing
                var backupPath = saveFilePath + ".backup";
                File.Copy(saveFilePath, backupPath, true);

                // Write the updated save data
                File.WriteAllBytes(saveFilePath, updatedSaveData);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update save file: {ex.Message}");
            }
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
                    context.Config.SerializationPolicy = SerializationPolicies.Unity;
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

        // Enriched data properties (computed from raw data)
        public List<string> RelicNames => GameDataMappings.GetRelicNames(RelicIds);
        public List<string> BossNames => GameDataMappings.GetBossNames(VisitedBosses);
        public Dictionary<string, int> RoomTypeStatistics => GameDataMappings.GetRoomTypeStatistics(VisitedRooms);
        public List<string> ActiveStatusEffects => GameDataMappings.GetActiveStatusEffects(StatusEffects);
        public List<string> ActiveSlimePegs => GameDataMappings.GetActiveSlimePegs(SlimePegs);
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
}
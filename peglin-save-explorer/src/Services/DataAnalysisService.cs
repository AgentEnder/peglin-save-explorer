using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using peglin_save_explorer.Core;
using peglin_save_explorer.Data;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Services
{
    /// <summary>
    /// Service for analyzing and processing Peglin save data
    /// Provides functionality used by both CLI commands and web API
    /// </summary>
    public class DataAnalysisService
    {
        private readonly ConfigurationManager _configManager;
        private readonly RunHistoryManager _runHistoryManager;

        public DataAnalysisService(ConfigurationManager configManager)
        {
            _configManager = configManager;
            _runHistoryManager = new RunHistoryManager(configManager);
        }

        /// <summary>
        /// Load complete run history data from a save file
        /// </summary>
        public RunHistoryData LoadCompleteRunData(FileInfo saveFile)
        {
            try
            {
                var saveData = SaveDataLoader.LoadSaveData(saveFile);
                if (saveData == null)
                {
                    return new RunHistoryData();
                }

                var runs = RunDataService.LoadRunHistory(saveFile, _configManager);
                var classStats = _runHistoryManager.GetClassStatistics(runs);
                var orbStats = _runHistoryManager.GetOrbStatistics(runs);
                var playerStats = ExtractPlayerStatistics(saveData);

                return new RunHistoryData
                {
                    Runs = runs,
                    ClassStatistics = classStats,
                    OrbStatistics = orbStats,
                    PlayerStatistics = playerStats,
                    TotalRuns = runs.Count,
                    TotalWins = runs.Count(r => r.Won),
                    WinRate = runs.Count > 0 ? (double)runs.Count(r => r.Won) / runs.Count : 0
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading save file data: {ex.Message}");
                return new RunHistoryData();
            }
        }

        /// <summary>
        /// Extract player statistics from save data (similar to StatsCommand)
        /// </summary>
        public PlayerStatistics ExtractPlayerStatistics(JObject saveData)
        {
            var data = saveData?["peglinData"] as JObject;
            if (data == null)
            {
                return new PlayerStatistics();
            }

            return new PlayerStatistics
            {
                GameplayStats = ExtractGameplayStats(data),
                CombatStats = ExtractCombatStats(data),
                PegStats = ExtractPegStats(data),
                EconomyStats = ExtractEconomyStats(data)
            };
        }

        /// <summary>
        /// Get summary information from save data (similar to SummaryCommand)
        /// </summary>
        public SaveSummary GetSaveSummary(JObject saveData, string fileName)
        {
            var summary = new SaveSummary
            {
                FileName = fileName,
                IsValid = saveData["peglinDeserializationSuccess"]?.Value<bool>() == true
            };

            if (!summary.IsValid)
            {
                return summary;
            }

            var data = saveData["peglinData"] as JObject;
            if (data != null)
            {
                summary.BasicStats = ExtractBasicSummaryStats(data);
                summary.ClassPerformance = ExtractClassPerformance(data);
                summary.SaveInfo = ExtractSaveInfo(data);
            }

            return summary;
        }

        /// <summary>
        /// Filter runs based on criteria (extracted from WebCommand)
        /// </summary>
        public List<RunRecord> FilterRuns(List<RunRecord> runs, RunFilter filter)
        {
            var filteredRuns = runs.AsEnumerable();

            if (!string.IsNullOrEmpty(filter.CharacterClass))
            {
                filteredRuns = filteredRuns.Where(r => r.CharacterClass == filter.CharacterClass);
            }

            if (filter.Won.HasValue)
            {
                filteredRuns = filteredRuns.Where(r => r.Won == filter.Won.Value);
            }

            if (filter.StartDate.HasValue)
            {
                filteredRuns = filteredRuns.Where(r => r.Timestamp >= filter.StartDate.Value);
            }

            if (filter.EndDate.HasValue)
            {
                filteredRuns = filteredRuns.Where(r => r.Timestamp <= filter.EndDate.Value);
            }

            if (filter.MinDamage.HasValue)
            {
                filteredRuns = filteredRuns.Where(r => r.DamageDealt >= filter.MinDamage.Value);
            }

            if (filter.MaxDamage.HasValue)
            {
                filteredRuns = filteredRuns.Where(r => r.DamageDealt <= filter.MaxDamage.Value);
            }

            if (filter.MinDuration.HasValue)
            {
                filteredRuns = filteredRuns.Where(r => r.Duration >= filter.MinDuration.Value);
            }

            if (filter.MaxDuration.HasValue)
            {
                filteredRuns = filteredRuns.Where(r => r.Duration <= filter.MaxDuration.Value);
            }

            return filteredRuns.ToList();
        }

        /// <summary>
        /// Export runs to CSV format
        /// </summary>
        public string ExportToCsv(List<RunRecord> runs)
        {
            var csv = new System.Text.StringBuilder();
            
            // Header
            csv.AppendLine("Id,Timestamp,Won,Score,DamageDealt,PegsHit,Duration,CharacterClass,Seed,FinalLevel,CoinsEarned,CruciballLevel");

            // Data
            foreach (var run in runs)
            {
                csv.AppendLine($"{run.Id},{run.Timestamp:yyyy-MM-dd HH:mm:ss},{run.Won},{run.Score},{run.DamageDealt},{run.PegsHit},{run.Duration.TotalSeconds},{run.CharacterClass},{run.Seed},{run.FinalLevel},{run.CoinsEarned},{run.CruciballLevel}");
            }

            return csv.ToString();
        }

        #region Private Helper Methods

        private Dictionary<string, object> ExtractGameplayStats(JObject data)
        {
            var stats = new Dictionary<string, object>();
            var gameplayStats = new[]
            {
                ("gamesPlayed", "Games Played"),
                ("hoursPlayed", "Hours Played"),
                ("levelsCompleted", "Levels Completed"),
                ("bossesDefeated", "Bosses Defeated")
            };

            foreach (var (key, label) in gameplayStats)
            {
                var value = GetNestedValue(data, key);
                if (value != null)
                {
                    stats[label] = value;
                }
            }

            return stats;
        }

        private Dictionary<string, object> ExtractCombatStats(JObject data)
        {
            var stats = new Dictionary<string, object>();
            var combatStats = new[]
            {
                ("totalDamageDealt", "Total Damage Dealt"),
                ("criticalHits", "Critical Hits"),
                ("enemiesDefeated", "Enemies Defeated"),
                ("damageBlocked", "Damage Blocked")
            };

            foreach (var (key, label) in combatStats)
            {
                var value = GetNestedValue(data, key);
                if (value != null)
                {
                    stats[label] = value;
                }
            }

            return stats;
        }

        private Dictionary<string, object> ExtractPegStats(JObject data)
        {
            var stats = new Dictionary<string, object>();
            var pegStats = new[]
            {
                ("totalPegsHit", "Total Pegs Hit"),
                ("refreshPegsHit", "Refresh Pegs Hit"),
                ("critPegsHit", "Crit Pegs Hit"),
                ("bombPegsHit", "Bomb Pegs Hit")
            };

            foreach (var (key, label) in pegStats)
            {
                var value = GetNestedValue(data, key);
                if (value != null)
                {
                    stats[label] = value;
                }
            }

            return stats;
        }

        private Dictionary<string, object> ExtractEconomyStats(JObject data)
        {
            var stats = new Dictionary<string, object>();
            var economyStats = new[]
            {
                ("coinsEarned", "Coins Earned"),
                ("coinsSpent", "Coins Spent"),
                ("orbsAcquired", "Orbs Acquired"),
                ("relicsAcquired", "Relics Acquired")
            };

            foreach (var (key, label) in economyStats)
            {
                var value = GetNestedValue(data, key);
                if (value != null)
                {
                    stats[label] = value;
                }
            }

            return stats;
        }

        private Dictionary<string, object> ExtractBasicSummaryStats(JObject data)
        {
            var stats = new Dictionary<string, object>();
            var basicStats = new[]
            {
                ("gamesPlayed", "Games Played"),
                ("hoursPlayed", "Hours Played"),
                ("totalDamageDealt", "Total Damage"),
                ("highestScore", "Highest Score")
            };

            foreach (var (key, label) in basicStats)
            {
                var value = GetNestedValue(data, key);
                if (value != null)
                {
                    stats[label] = value;
                }
            }

            return stats;
        }

        private Dictionary<string, object> ExtractClassPerformance(JObject data)
        {
            var performance = new Dictionary<string, object>();
            // This would extract class-specific performance data
            // Implementation depends on the save data structure
            return performance;
        }

        private Dictionary<string, object> ExtractSaveInfo(JObject data)
        {
            var info = new Dictionary<string, object>();
            var saveInfo = new[]
            {
                ("version", "Game Version"),
                ("saveDate", "Save Date"),
                ("playtime", "Total Playtime")
            };

            foreach (var (key, label) in saveInfo)
            {
                var value = GetNestedValue(data, key);
                if (value != null)
                {
                    info[label] = value;
                }
            }

            return info;
        }

        private object? GetNestedValue(JObject data, string path)
        {
            try
            {
                var token = data.SelectToken(path);
                return token?.ToObject<object>();
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }

    #region Data Classes

    public class RunHistoryData
    {
        public List<RunRecord> Runs { get; set; } = new();
        public Dictionary<string, ClassStatistics> ClassStatistics { get; set; } = new();
        public Dictionary<string, OrbStatistics> OrbStatistics { get; set; } = new();
        public PlayerStatistics? PlayerStatistics { get; set; }
        public int TotalRuns { get; set; }
        public int TotalWins { get; set; }
        public double WinRate { get; set; }
    }

    public class PlayerStatistics
    {
        public Dictionary<string, object> GameplayStats { get; set; } = new();
        public Dictionary<string, object> CombatStats { get; set; } = new();
        public Dictionary<string, object> PegStats { get; set; } = new();
        public Dictionary<string, object> EconomyStats { get; set; } = new();
    }

    public class SaveSummary
    {
        public string FileName { get; set; } = "";
        public bool IsValid { get; set; }
        public Dictionary<string, object> BasicStats { get; set; } = new();
        public Dictionary<string, object> ClassPerformance { get; set; } = new();
        public Dictionary<string, object> SaveInfo { get; set; } = new();
    }

    public class RunFilter
    {
        public string? CharacterClass { get; set; }
        public bool? Won { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public long? MinDamage { get; set; }
        public long? MaxDamage { get; set; }
        public TimeSpan? MinDuration { get; set; }
        public TimeSpan? MaxDuration { get; set; }
    }

    #endregion
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using peglin_save_explorer.Data;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer
{
    /// <summary>
    /// Provides mappings between game data IDs and human-readable names
    /// </summary>
    public static class GameDataMappings
    {
        private static bool _mappingsLoaded = false;
        private static string? _lastGameDataPath = null;

        private static Dictionary<int, string> _relicMappings = new();
        private static Dictionary<int, string> _roomMappings = new();
        private static Dictionary<int, string> _bossMappings = new();
        private static Dictionary<int, string> _statusEffectMappings = new();
        private static Dictionary<int, string> _slimePegMappings = new();

        private static System.Reflection.Assembly? _assembly = null;

        // Fallback mappings for common game data
        private static readonly Dictionary<int, string> FallbackRelicMappings = new()
        {
            { 1, "Unknown Relic 1" },
            { 2, "Unknown Relic 2" },
            { 3, "Unknown Relic 3" },
            { 89, "Unknown Relic 89" },
            { 170, "Unknown Relic 170" }
        };

        private static readonly Dictionary<int, string> FallbackRoomMappings = new()
        {
            { 0, "Unknown Room" },
            { 1, "Forest" },
            { 2, "Mines" },
            { 3, "Desert" },
            { 4, "Castle" }
        };

        private static readonly Dictionary<int, string> FallbackBossMappings = new()
        {
            { 0, "Unknown Boss" },
            { 1, "Ballista" },
            { 2, "Dragon" },
            { 3, "Minotaur" }
        };

        private static readonly Dictionary<int, string> FallbackStatusEffectMappings = new()
        {
            { 0, "None" },
            { 1, "Strength" },
            { 2, "Poison" },
            { 3, "Armor" }
        };

        private static readonly Dictionary<int, string> FallbackSlimePegMappings = new()
        {
            { 0, "None" },
            { 1, "Red Slime" },
            { 2, "Blue Slime" },
            { 3, "Green Slime" }
        };

        /// <summary>
        /// Gets the human-readable name for a relic ID
        /// </summary>
        public static string GetRelicName(int relicId)
        {
            EnsureMappingsLoaded();
            
            // First try the loaded mappings, but skip fallback "Unknown Relic" patterns
            if (_relicMappings.TryGetValue(relicId, out var name) && !name.StartsWith("Unknown Relic"))
            {
                return name;
            }
            
            // Try the RelicMappingCache for better resolution
            try
            {
                var relicCache = new RelicMappingCache();
                relicCache.LoadFromDisk();
                
                // Try to resolve using the cache
                var unknownRelicWithParens = $"Unknown Relic ({relicId})";
                var unknownRelicWithoutParens = $"Unknown Relic {relicId}";
                
                var resolvedNameWithParens = relicCache.ResolveRelicName(unknownRelicWithParens);
                if (!string.IsNullOrEmpty(resolvedNameWithParens) && resolvedNameWithParens != unknownRelicWithParens)
                {
                    return resolvedNameWithParens;
                }
                
                var resolvedNameWithoutParens = relicCache.ResolveRelicName(unknownRelicWithoutParens);
                if (!string.IsNullOrEmpty(resolvedNameWithoutParens) && resolvedNameWithoutParens != unknownRelicWithoutParens)
                {
                    return resolvedNameWithoutParens;
                }
            }
            catch (Exception ex)
            {
                // Silently continue to fallback if cache fails
                Logger.Warning($"RelicMappingCache failed: {ex.Message}");
            }
            
            // If we have a fallback mapping, use it (this preserves existing fallback behavior)
            if (_relicMappings.TryGetValue(relicId, out var fallbackName))
            {
                return fallbackName;
            }
            
            // Final fallback to the unknown format
            return $"Unknown Relic ({relicId})";
        }

        /// <summary>
        /// Gets the human-readable name for a room type ID
        /// </summary>
        public static string GetRoomName(int roomId)
        {
            EnsureMappingsLoaded();
            return _roomMappings.TryGetValue(roomId, out var name) ? name : $"Unknown Room ({roomId})";
        }

        /// <summary>
        /// Gets the human-readable name for a boss ID
        /// </summary>
        public static string GetBossName(int bossId)
        {
            EnsureMappingsLoaded();
            return _bossMappings.TryGetValue(bossId, out var name) ? name : $"Unknown Boss ({bossId})";
        }

        /// <summary>
        /// Gets the human-readable name for a status effect ID
        /// </summary>
        public static string GetStatusEffectName(int statusEffectId)
        {
            EnsureMappingsLoaded();
            return _statusEffectMappings.TryGetValue(statusEffectId, out var name) ? name : $"Unknown Status Effect ({statusEffectId})";
        }

        /// <summary>
        /// Gets the human-readable name for a slime peg ID
        /// </summary>
        public static string GetSlimePegName(int slimeId)
        {
            EnsureMappingsLoaded();
            return _slimePegMappings.TryGetValue(slimeId, out var name) ? name : $"Unknown Slime ({slimeId})";
        }

        // Collection methods for getting multiple names at once
        public static List<string> GetRelicNames(int[] relicIds)
        {
            if (relicIds == null) return new List<string>();
            return relicIds.Select(GetRelicName).ToList();
        }

        /// <summary>
        /// Converts RelicsUsed string array (Effect IDs as strings) to human-readable relic names
        /// </summary>
        public static List<string> GetRelicNamesFromStrings(List<string> relicStrings)
        {
            if (relicStrings == null || relicStrings.Count == 0) return new List<string>();

            var relicNames = new List<string>();
            foreach (var relicString in relicStrings)
            {
                if (int.TryParse(relicString, out int relicId))
                {
                    relicNames.Add(GetRelicName(relicId));
                }
                else
                {
                    relicNames.Add($"Invalid Relic ID ({relicString})");
                }
            }
            return relicNames;
        }

        public static List<string> GetBossNames(int[] bossIds)
        {
            if (bossIds == null) return new List<string>();
            return bossIds.Select(GetBossName).ToList();
        }

        public static Dictionary<string, int> GetRoomTypeStatistics(int[] roomIds)
        {
            if (roomIds == null) return new Dictionary<string, int>();

            var roomNames = roomIds.Select(GetRoomName);
            return roomNames.GroupBy(name => name)
                           .ToDictionary(g => g.Key, g => g.Count());
        }

        public static List<string> GetActiveStatusEffects(int[] statusEffectIds)
        {
            if (statusEffectIds == null) return new List<string>();
            return statusEffectIds.Select(GetStatusEffectName).ToList();
        }

        public static List<string> GetActiveSlimePegs(int[] slimeIds)
        {
            if (slimeIds == null) return new List<string>();
            return slimeIds.Select(GetSlimePegName).ToList();
        }

        /// <summary>
        /// Forces a reload of game data mappings even if already loaded
        /// </summary>
        public static void ReloadGameDataMappings(string? peglinPath)
        {
            _mappingsLoaded = false;
            _lastGameDataPath = null;
            LoadGameDataMappings(peglinPath);
        }

        /// <summary>
        /// Gets information about the currently loaded mappings
        /// </summary>
        public static Dictionary<string, object> GetMappingInfo()
        {
            EnsureMappingsLoaded();

            return new Dictionary<string, object>
            {
                ["relics_loaded"] = _relicMappings?.Count ?? 0,
                ["rooms_loaded"] = _roomMappings?.Count ?? 0,
                ["bosses_loaded"] = _bossMappings?.Count ?? 0,
                ["status_effects_loaded"] = _statusEffectMappings?.Count ?? 0,
                ["slime_pegs_loaded"] = _slimePegMappings?.Count ?? 0,
                ["data_source"] = _lastGameDataPath ?? "fallback",
                ["using_fallback"] = string.IsNullOrEmpty(_lastGameDataPath)
            };
        }

        /// <summary>
        /// Loads game data mappings from the Peglin installation directory
        /// </summary>
        public static void LoadGameDataMappings(string? peglinPath)
        {
            if (_mappingsLoaded && _lastGameDataPath == peglinPath)
            {
                return; // Already loaded for this path
            }

            Logger.Debug($"GameDataMappings: Loading mappings for path: {peglinPath ?? "none"}");

            // Initialize with fallback data
            _relicMappings = new Dictionary<int, string>(FallbackRelicMappings);
            _roomMappings = new Dictionary<int, string>(FallbackRoomMappings);
            _bossMappings = new Dictionary<int, string>(FallbackBossMappings);
            _statusEffectMappings = new Dictionary<int, string>(FallbackStatusEffectMappings);
            _slimePegMappings = new Dictionary<int, string>(FallbackSlimePegMappings);

            if (!string.IsNullOrEmpty(peglinPath))
            {
                try
                {
                    // Use the shared AssemblyAnalyzer to extract data
                    var analysisResult = AssemblyAnalyzer.AnalyzePeglinAssembly(peglinPath);

                    if (analysisResult.Success && analysisResult.LoadedAssembly != null)
                    {
                        _assembly = analysisResult.LoadedAssembly;
                        _lastGameDataPath = peglinPath;

                        // Use extracted mappings if available, otherwise keep fallback data
                        if (analysisResult.RelicMappings.Any())
                        {
                            _relicMappings = analysisResult.RelicMappings;
                        }

                        if (analysisResult.RoomMappings.Any())
                        {
                            _roomMappings = analysisResult.RoomMappings;
                        }

                        if (analysisResult.BossMappings.Any())
                        {
                            _bossMappings = analysisResult.BossMappings;
                        }

                        if (analysisResult.StatusEffectMappings.Any())
                        {
                            _statusEffectMappings = analysisResult.StatusEffectMappings;
                        }

                        if (analysisResult.SlimePegMappings.Any())
                        {
                            _slimePegMappings = analysisResult.SlimePegMappings;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"GameDataMappings: Error analyzing assembly: {ex.Message}");
                }
            }

            _mappingsLoaded = true;

            Logger.Debug($"GameDataMappings: Loaded mappings - Relics: {_relicMappings.Count}, Rooms: {_roomMappings.Count}, Bosses: {_bossMappings.Count}, Status Effects: {_statusEffectMappings.Count}, Slimes: {_slimePegMappings.Count}");
        }

        private static void EnsureMappingsLoaded()
        {
            if (!_mappingsLoaded)
            {
                LoadGameDataMappings(null);
            }
        }
    }
}
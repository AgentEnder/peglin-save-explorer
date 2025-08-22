using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using peglin_save_explorer.Core;
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
        private static Dictionary<int, string> _characterClassMappings = new();

        private static System.Reflection.Assembly? _assembly = null;

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
            var result = _roomMappings.TryGetValue(roomId, out var name) ? name : $"Unknown Room ({roomId})";
            Logger.Debug($"GetRoomName({roomId}) -> {result} (from {_roomMappings.Count} total mappings)");
            return result;
        }

        /// <summary>
        /// Gets the human-readable name for a boss ID
        /// </summary>
        public static string GetBossName(int bossId)
        {
            EnsureMappingsLoaded();
            var result = _bossMappings.TryGetValue(bossId, out var name) ? name : $"Unknown Boss ({bossId})";
            Logger.Debug($"GetBossName({bossId}) -> {result} (from {_bossMappings.Count} total mappings)");
            return result;
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

        /// <summary>
        /// Gets the character class index for a character class name
        /// </summary>
        public static int GetCharacterClassIndex(string className)
        {
            if (string.IsNullOrEmpty(className))
                return -1;

            // Map of class names to their indices based on the actual Peglin character classes
            var classNameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "Peglin", 0 },
                { "Balladin", 1 }, 
                { "Roundrel", 2 },
                { "Spinventor", 3 }
            };

            return classNameToIndex.TryGetValue(className, out var index) ? index : -1;
        }

        /// <summary>
        /// Gets the human-readable name for a character class ID
        /// </summary>
        public static string GetCharacterClassName(int classId)
        {
            EnsureMappingsLoaded();
            return _characterClassMappings.TryGetValue(classId, out var name) ? name : $"Unknown Class ({classId})";
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

        public static List<string> GetActiveStatusEffects(int[] statusEffectCounts)
        {
            if (statusEffectCounts == null) return new List<string>();

            var activeEffects = new List<string>();

            // Array indices represent status effect IDs, values represent application counts
            for (int effectId = 0; effectId < statusEffectCounts.Length; effectId++)
            {
                var count = statusEffectCounts[effectId];

                // Skip if count is 0 or negative (not applied)
                if (count <= 0)
                    continue;

                var effectName = GetStatusEffectName(effectId);

                // Skip "None" effects
                if (effectName == "None")
                    continue;

                // Include count in the display if > 1
                if (count > 1)
                    activeEffects.Add($"{effectName} ({count})");
                else
                    activeEffects.Add(effectName);
            }

            return activeEffects;
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
            // If no path provided, try to get the default path
            if (string.IsNullOrEmpty(peglinPath))
            {
                var configManager = new ConfigurationManager();
                peglinPath = configManager.GetEffectivePeglinPath(promptIfNotFound: false);
                
                if (string.IsNullOrEmpty(peglinPath))
                {
                    Logger.Warning("No Peglin path available for loading game data mappings");
                    _mappingsLoaded = true; // Mark as loaded to prevent repeated attempts
                    return;
                }
            }

            if (_mappingsLoaded && _lastGameDataPath == peglinPath)
            {
                return; // Already loaded for this path
            }

            Logger.Debug($"GameDataMappings: Loading mappings for path: {peglinPath}");

            // Initialize empty - we'll only use reflected data
            _relicMappings = new Dictionary<int, string>();
            _roomMappings = new Dictionary<int, string>();
            _bossMappings = new Dictionary<int, string>();
            _statusEffectMappings = new Dictionary<int, string>();
            _slimePegMappings = new Dictionary<int, string>();
            _characterClassMappings = new Dictionary<int, string>();

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
                            Logger.Debug($"Loaded {_relicMappings.Count} relic mappings from assembly");
                        }

                        if (analysisResult.RoomMappings.Any())
                        {
                            _roomMappings = analysisResult.RoomMappings;
                            Logger.Debug($"Loaded {_roomMappings.Count} room mappings from assembly: {string.Join(", ", _roomMappings.Select(x => $"{x.Key}={x.Value}"))}");
                        }

                        if (analysisResult.BossMappings.Any())
                        {
                            _bossMappings = analysisResult.BossMappings;
                            Logger.Debug($"Loaded {_bossMappings.Count} boss mappings from assembly: {string.Join(", ", _bossMappings.Select(x => $"{x.Key}={x.Value}"))}");
                        }

                        if (analysisResult.StatusEffectMappings.Any())
                        {
                            _statusEffectMappings = analysisResult.StatusEffectMappings;
                            Logger.Debug($"Loaded {_statusEffectMappings.Count} status effect mappings from assembly");
                        }

                        if (analysisResult.SlimePegMappings.Any())
                        {
                            _slimePegMappings = analysisResult.SlimePegMappings;
                            Logger.Debug($"Loaded {_slimePegMappings.Count} slime peg mappings from assembly");
                        }

                        if (analysisResult.CharacterClassMappings.Any())
                        {
                            _characterClassMappings = analysisResult.CharacterClassMappings;
                            Logger.Debug($"Loaded {_characterClassMappings.Count} character class mappings from assembly");
                        }
                    }
            }
            catch (Exception ex)
            {
                Logger.Warning($"GameDataMappings: Error analyzing assembly: {ex.Message}");
            }

            _mappingsLoaded = true;

            Logger.Debug($"GameDataMappings: Loaded mappings - Relics: {_relicMappings.Count}, Rooms: {_roomMappings.Count}, Bosses: {_bossMappings.Count}, Status Effects: {_statusEffectMappings.Count}, Slimes: {_slimePegMappings.Count}, Classes: {_characterClassMappings.Count}");
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
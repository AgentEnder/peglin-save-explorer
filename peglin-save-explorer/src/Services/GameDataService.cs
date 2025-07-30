using System;
using peglin_save_explorer.Core;
using peglin_save_explorer.Data;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Services
{
    /// <summary>
    /// Centralized service for initializing and managing game data mappings
    /// </summary>
    public static class GameDataService
    {
        /// <summary>
        /// Initialize all game data mappings and caches
        /// </summary>
        public static void InitializeGameData(ConfigurationManager configManager)
        {
            var peglinPath = configManager.GetEffectivePeglinPath();
            InitializeGameData(peglinPath);
        }

        /// <summary>
        /// Initialize all game data mappings and caches with specific Peglin path
        /// </summary>
        public static void InitializeGameData(string? peglinPath)
        {
            // Load game data mappings
            if (!string.IsNullOrEmpty(peglinPath))
            {
                Logger.Debug($"Loading game data from: {peglinPath}");
                GameDataMappings.LoadGameDataMappings(peglinPath);
            }
            else
            {
                Logger.Warning("No Peglin path configured, using fallback mappings");
                GameDataMappings.LoadGameDataMappings(null);
            }

            // Ensure relic cache is up to date
            EnsureRelicCache(peglinPath);
        }

        /// <summary>
        /// Ensure relic cache is loaded and up to date
        /// </summary>
        public static void EnsureRelicCache(string? peglinPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(peglinPath))
                {
                    RelicMappingCache.EnsureCacheFromAssetRipper(peglinPath);
                    Logger.Debug("Relic cache updated for name resolution.");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not update relic cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Load relic cache from disk with error handling and return mappings
        /// </summary>
        public static Dictionary<int, string>? GetRelicMappings(string? peglinPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(peglinPath))
                {
                    var mappings = RelicMappingCache.GetRelicMappings(peglinPath);
                    Logger.Debug($"Loaded {mappings.Count} relic mappings from cache");
                    return mappings;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not load relic mappings: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Load relic cache from disk with error handling
        /// </summary>
        public static RelicMappingCache LoadRelicCache()
        {
            var relicCache = new RelicMappingCache();
            try
            {
                relicCache.LoadFromDisk();
                Logger.Debug("Relic cache loaded from disk.");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not load relic cache: {ex.Message}");
            }
            return relicCache;
        }

        /// <summary>
        /// Initialize game data with comprehensive error handling and validation
        /// </summary>
        public static bool TryInitializeGameData(ConfigurationManager configManager, out string? errorMessage)
        {
            errorMessage = null;
            try
            {
                InitializeGameData(configManager);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to initialize game data: {ex.Message}";
                Logger.Error(errorMessage);
                return false;
            }
        }
    }
}

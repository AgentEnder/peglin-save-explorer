using System;
using System.IO;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Data
{
    /// <summary>
    /// Centralized cache management for all cache systems
    /// </summary>
    public static class CacheManager
    {
        /// <summary>
        /// Clears all caches (entities and sprites)
        /// </summary>
        public static void ClearAllCaches()
        {
            Logger.Info("Clearing all caches...");
            
            try
            {
                EntityCacheManager.ClearCache();
                SpriteCacheManager.ClearCache();
                
                Logger.Info("All caches cleared successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error clearing caches: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Shows status of all cache systems
        /// </summary>
        public static void ShowAllCacheStatus()
        {
            Console.WriteLine("=== CACHE STATUS ===");
            
            // Show entity cache status
            EntityCacheManager.ShowCacheStatus();
            Console.WriteLine();
            
            // Show sprite cache status  
            SpriteCacheManager.ShowCacheStatus();
        }
    }
}
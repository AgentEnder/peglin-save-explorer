using System;
using System.IO;

namespace peglin_save_explorer.Utils
{
    /// <summary>
    /// Provides centralized cache directory management for the application
    /// </summary>
    public static class CacheDirectoryHelper
    {
        /// <summary>
        /// Gets the base cache directory for PeglinSaveExplorer
        /// </summary>
        public static string GetCacheDirectory()
        {
            // Use the standard Application Support directory on macOS and appropriate paths on other platforms
            string baseDir;
            if (OperatingSystem.IsMacOS())
            {
                baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library",
                    "Application Support",
                    "PeglinSaveExplorer"
                );
            }
            else if (OperatingSystem.IsWindows())
            {
                baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PeglinSaveExplorer"
                );
            }
            else // Linux and others
            {
                baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config",
                    "PeglinSaveExplorer"
                );
            }

            // Ensure the directory exists
            Directory.CreateDirectory(baseDir);
            return baseDir;
        }

        /// <summary>
        /// Gets a specific subdirectory within the cache directory
        /// </summary>
        /// <param name="subdirectory">The subdirectory name</param>
        /// <returns>Full path to the subdirectory</returns>
        public static string GetCacheSubdirectory(string subdirectory)
        {
            var path = Path.Combine(GetCacheDirectory(), subdirectory);
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// Gets the extracted data directory
        /// </summary>
        public static string GetExtractedDataDirectory()
        {
            return GetCacheSubdirectory("extracted-data");
        }

        /// <summary>
        /// Gets the sprites cache directory
        /// </summary>
        public static string GetSpritesDirectory()
        {
            return GetCacheSubdirectory("extracted-data/sprites");
        }

        /// <summary>
        /// Gets the entities cache directory
        /// </summary>
        public static string GetEntitiesDirectory()
        {
            return GetCacheSubdirectory("extracted-data/entities");
        }
    }
}
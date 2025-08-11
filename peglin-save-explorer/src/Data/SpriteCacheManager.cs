using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Data
{
    /// <summary>
    /// Manages caching and extraction of sprite assets from Unity bundles
    /// Provides hash-based validation to only re-extract when Peglin installation changes
    /// </summary>
    public class SpriteCacheManager
    {
        private static readonly string CacheDirectory = GetCacheDirectory();
        private static readonly string SpritesDirectory = Path.Combine(CacheDirectory, "extracted-data", "sprites");
        private static readonly string RelicSpritesDirectory = Path.Combine(SpritesDirectory, "relics");
        private static readonly string EnemySpritesDirectory = Path.Combine(SpritesDirectory, "enemies");
        private static readonly string MetadataFilePath = Path.Combine(SpritesDirectory, "sprite_cache_metadata.json");

        private static string GetCacheDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PeglinSaveExplorer"
                );
            }
            else if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "PeglinSaveExplorer"
                );
            }
            else
            {
                // Linux - use XDG_CONFIG_HOME or fallback to ~/.config
                var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (string.IsNullOrEmpty(xdgConfigHome))
                {
                    xdgConfigHome = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".config"
                    );
                }
                return Path.Combine(xdgConfigHome, "PeglinSaveExplorer");
            }
        }

        public enum SpriteType
        {
            Relic,
            Enemy,
            Orb
        }

        public class SpriteFrame
        {
            public string Name { get; set; } = "";
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public float PivotX { get; set; }
            public float PivotY { get; set; }
            public long SpritePathID { get; set; }
        }

        public class SpriteMetadata
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public SpriteType Type { get; set; }
            public string FilePath { get; set; } = "";
            public int Width { get; set; }
            public int Height { get; set; }
            public string SourceBundle { get; set; } = "";
            public DateTime ExtractedAt { get; set; }

            public int FrameX { get; set; }
            public int FrameY { get; set; }
            public int FrameWidth { get; set; }
            public int FrameHeight { get; set; }
            public int FrameCount { get; set; } = 1;

            // Atlas-specific properties
            public bool IsAtlas { get; set; } = false;
            public List<SpriteFrame> AtlasFrames { get; set; } = new List<SpriteFrame>();
        }

        public class SpriteCacheMetadata
        {
            public DateTime LastUpdated { get; set; }
            public string PeglinPath { get; set; } = "";
            public string PeglinInstallHash { get; set; } = "";
            public string Version { get; set; } = "1.0";
            public int RelicSpriteCount { get; set; }
            public int EnemySpriteCount { get; set; }
            public List<string> SourceBundles { get; set; } = new();
            public Dictionary<string, SpriteMetadata> Sprites { get; set; } = new();
        }

        private static List<SpriteMetadata> _cachedSprites = new();

        /// <summary>
        /// Checks if the sprite cache is valid for the given Peglin installation
        /// </summary>
        public static bool IsCacheValid(string peglinPath, bool forceRefresh = false)
        {
            if (forceRefresh)
                return false;

            try
            {
                if (!File.Exists(MetadataFilePath))
                {
                    Logger.Debug("Sprite cache metadata not found");
                    return false;
                }

                var metadata = JsonConvert.DeserializeObject<SpriteCacheMetadata>(File.ReadAllText(MetadataFilePath));
                if (metadata == null)
                {
                    Logger.Warning("Invalid sprite cache metadata");
                    return false;
                }

                // Check if Peglin path matches
                if (!string.Equals(metadata.PeglinPath, peglinPath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warning("Sprite cache invalid: Peglin path changed");
                    return false;
                }

                // Check if Peglin installation hash matches (detects game updates)
                var currentHash = CalculatePeglinInstallHash(peglinPath);
                if (currentHash != metadata.PeglinInstallHash)
                {
                    Logger.Warning("Sprite cache invalid: Peglin installation changed (hash mismatch)");
                    return false;
                }

                // Check if cache is too old (older than 30 days)
                if ((DateTime.Now - metadata.LastUpdated).TotalDays > 30)
                {
                    Logger.Warning("Sprite cache invalid: older than 30 days");
                    return false;
                }

                // Verify that sprite directories exist
                if (!Directory.Exists(RelicSpritesDirectory) || !Directory.Exists(EnemySpritesDirectory))
                {
                    Logger.Warning("Sprite cache invalid: sprite directories missing");
                    return false;
                }

                // Verify that referenced sprite files exist
                var missingFiles = metadata.Sprites.Values.Where(sprite => !File.Exists(sprite.FilePath)).ToList();
                if (missingFiles.Any())
                {
                    Logger.Warning($"Sprite cache invalid: {missingFiles.Count} sprite files missing");
                    return false;
                }

                Logger.Debug("Sprite cache is valid");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error validating sprite cache: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Calculates a hash of the Peglin installation to detect changes
        /// </summary>
        private static string CalculatePeglinInstallHash(string peglinPath)
        {
            try
            {
                var bundleDirectory = PeglinPathHelper.GetStreamingAssetsBundlePath(peglinPath);
                if (string.IsNullOrEmpty(bundleDirectory) || !Directory.Exists(bundleDirectory))
                {
                    return "";
                }

                var hashBuilder = new StringBuilder();

                // Get all bundle files and their modification times
                var bundleFiles = Directory.GetFiles(bundleDirectory, "*.bundle", SearchOption.AllDirectories)
                    .OrderBy(f => f)
                    .ToList();

                foreach (var bundleFile in bundleFiles)
                {
                    var fileInfo = new FileInfo(bundleFile);
                    hashBuilder.Append($"{Path.GetFileName(bundleFile)}:{fileInfo.Length}:{fileInfo.LastWriteTime.Ticks};");
                }

                // Hash the combined string
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashBuilder.ToString()));
                return Convert.ToHexString(hashBytes)[..16]; // Use first 16 chars for brevity
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error calculating Peglin install hash: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Gets the path to a cached sprite file
        /// </summary>
        public static string GetSpritePath(SpriteType type, string spriteId)
        {
            var directory = type == SpriteType.Relic ? RelicSpritesDirectory : EnemySpritesDirectory;
            return Path.Combine(directory, $"{spriteId}.png");
        }

        /// <summary>
        /// Gets sprite metadata from cache
        /// </summary>
        public static SpriteMetadata GetSpriteMetadata(SpriteType type, string spriteId)
        {
            try
            {
                if (!File.Exists(MetadataFilePath))
                    return null;

                var metadata = JsonConvert.DeserializeObject<SpriteCacheMetadata>(File.ReadAllText(MetadataFilePath));
                if (metadata?.Sprites == null)
                    return null;

                var key = $"{type}:{spriteId}";
                return metadata.Sprites.TryGetValue(key, out var sprite) ? sprite : null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting sprite metadata: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all cached sprites of a specific type
        /// </summary>
        public static List<SpriteMetadata> GetCachedSprites(SpriteType? type = null)
        {
            try
            {
                if (!File.Exists(MetadataFilePath))
                    return new List<SpriteMetadata>();

                var metadata = JsonConvert.DeserializeObject<SpriteCacheMetadata>(File.ReadAllText(MetadataFilePath));
                if (metadata?.Sprites == null)
                    return new List<SpriteMetadata>();

                var sprites = metadata.Sprites.Values.ToList();

                if (type.HasValue)
                {
                    sprites = sprites.Where(s => s.Type == type.Value).ToList();
                }

                return sprites;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting cached sprites: {ex.Message}");
                return new List<SpriteMetadata>();
            }
        }

        /// <summary>
        /// Saves extracted sprites and metadata to cache
        /// </summary>
        public static void SaveToCache(Dictionary<string, SpriteMetadata> sprites, string peglinPath, List<string> sourceBundles)
        {
            try
            {
                // Create directories
                Directory.CreateDirectory(RelicSpritesDirectory);
                Directory.CreateDirectory(EnemySpritesDirectory);

                // Calculate install hash
                var installHash = CalculatePeglinInstallHash(peglinPath);

                // Create cache metadata
                var metadata = new SpriteCacheMetadata
                {
                    LastUpdated = DateTime.Now,
                    PeglinPath = peglinPath,
                    PeglinInstallHash = installHash,
                    SourceBundles = sourceBundles,
                    Sprites = sprites,
                    RelicSpriteCount = sprites.Values.Count(s => s.Type == SpriteType.Relic),
                    EnemySpriteCount = sprites.Values.Count(s => s.Type == SpriteType.Enemy)
                };

                // Save metadata
                var metadataJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                File.WriteAllText(MetadataFilePath, metadataJson);

                Logger.Info($"Saved sprite cache with {metadata.RelicSpriteCount} relic sprites and {metadata.EnemySpriteCount} enemy sprites");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving sprite cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the sprite cache
        /// </summary>
        public static void ClearCache()
        {
            try
            {
                if (Directory.Exists(SpritesDirectory))
                {
                    Directory.Delete(SpritesDirectory, true);
                    Logger.Info("Sprite cache cleared");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error clearing sprite cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows sprite cache status information
        /// </summary>
        public static void ShowCacheStatus()
        {
            try
            {
                if (!File.Exists(MetadataFilePath))
                {
                    Console.WriteLine("No sprite cache found");
                    return;
                }

                var metadata = JsonConvert.DeserializeObject<SpriteCacheMetadata>(File.ReadAllText(MetadataFilePath));
                if (metadata != null)
                {
                    Console.WriteLine("=== SPRITE CACHE STATUS ===");
                    Console.WriteLine($"Last Updated: {metadata.LastUpdated}");
                    Console.WriteLine($"Peglin Path: {metadata.PeglinPath}");
                    Console.WriteLine($"Install Hash: {metadata.PeglinInstallHash}");
                    Console.WriteLine($"Relic Sprites: {metadata.RelicSpriteCount}");
                    Console.WriteLine($"Enemy Sprites: {metadata.EnemySpriteCount}");
                    Console.WriteLine($"Total Sprites: {metadata.Sprites.Count}");
                    Console.WriteLine($"Source Bundles: {metadata.SourceBundles.Count}");
                    Console.WriteLine($"Cache Location: {SpritesDirectory}");

                    var age = DateTime.Now - metadata.LastUpdated;
                    Console.WriteLine($"Cache Age: {age.TotalDays:F1} days");

                    if (age.TotalDays > 30)
                    {
                        Console.WriteLine("⚠️ Cache is older than 30 days and may be outdated");
                    }
                    else
                    {
                        Console.WriteLine("✅ Cache is current");
                    }

                    // Check if directories exist
                    var relicDirExists = Directory.Exists(RelicSpritesDirectory);
                    var enemyDirExists = Directory.Exists(EnemySpritesDirectory);

                    Console.WriteLine($"Relic Directory: {(relicDirExists ? "✅" : "❌")} {RelicSpritesDirectory}");
                    Console.WriteLine($"Enemy Directory: {(enemyDirExists ? "✅" : "❌")} {EnemySpritesDirectory}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing sprite cache status: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the cache directory path
        /// </summary>
        public static string GetSpriteCacheDirectory()
        {
            return SpritesDirectory;
        }

        public static void SetCachedSprites(List<SpriteMetadata> sprites)
        {
            _cachedSprites = sprites;
        }
    }
}
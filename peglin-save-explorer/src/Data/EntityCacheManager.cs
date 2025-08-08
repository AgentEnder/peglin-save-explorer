using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Extractors;

namespace peglin_save_explorer.Data
{
    /// <summary>
    /// Manages caching and extraction of entity data (relics, enemies, orbs) from Unity bundles
    /// Provides hash-based validation to only re-extract when Peglin installation changes
    /// </summary>
    public class EntityCacheManager
    {
        private static readonly string CacheDirectory = GetCacheDirectory();
        private static readonly string EntityDataDirectory = Path.Combine(CacheDirectory, "extracted-data", "entities");
        private static readonly string RelicsFilePath = Path.Combine(EntityDataDirectory, "relics.json");
        private static readonly string EnemiesFilePath = Path.Combine(EntityDataDirectory, "enemies.json");
        private static readonly string OrbsFilePath = Path.Combine(EntityDataDirectory, "orbs.json");
        private static readonly string OrbsGroupedFilePath = Path.Combine(EntityDataDirectory, "orbs_grouped.json");
        private static readonly string OrbFamiliesFilePath = Path.Combine(EntityDataDirectory, "orb_families.json");
        private static readonly string MetadataFilePath = Path.Combine(EntityDataDirectory, "entity_cache_metadata.json");

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

        public class EntityCacheMetadata
        {
            public string PeglinInstallHash { get; set; } = "";
            public DateTime CachedAt { get; set; }
            public List<string> SourceBundles { get; set; } = new List<string>();
            public Dictionary<string, int> EntityCounts { get; set; } = new Dictionary<string, int>();
        }

        /// <summary>
        /// Saves extracted entity data to cache (with orb families)
        /// </summary>
        public static void SaveToCache(
            Dictionary<string, RelicData>? relics,
            Dictionary<string, EnemyData>? enemies,
            Dictionary<string, OrbData>? orbs,
            // Dictionary<string, OrbFamily>? orbFamilies,
            string peglinPath,
            List<string> sourceBundles)
        {
            // Call the main implementation with orb families
            SaveToCacheInternal(relics, enemies, orbs, peglinPath, sourceBundles);
        }
        
        private static void SaveToCacheInternal(
            Dictionary<string, RelicData>? relics,
            Dictionary<string, EnemyData>? enemies,
            Dictionary<string, OrbData>? orbs,
            // Dictionary<string, OrbFamily>? orbFamilies,
            string peglinPath,
            List<string> sourceBundles)
        {
            try
            {
                // Create directory
                Directory.CreateDirectory(EntityDataDirectory);

                // Calculate install hash
                var installHash = CalculatePeglinInstallHash(peglinPath);

                // Save entity data files
                if (relics != null && relics.Count > 0)
                {
                    var relicJson = JsonConvert.SerializeObject(relics, Formatting.Indented);
                    File.WriteAllText(RelicsFilePath, relicJson);
                    _cachedRelics = relics; // keep memory cache in sync
                    Logger.Debug($"Saved {relics.Count} relics to cache: {RelicsFilePath}");
                }

                if (enemies != null && enemies.Count > 0)
                {
                    var enemyJson = JsonConvert.SerializeObject(enemies, Formatting.Indented);
                    File.WriteAllText(EnemiesFilePath, enemyJson);
                    _cachedEnemies = enemies; // keep memory cache in sync
                    Logger.Debug($"Saved {enemies.Count} enemies to cache: {EnemiesFilePath}");
                }

                if (orbs != null && orbs.Count > 0)
                {
                    var orbJson = JsonConvert.SerializeObject(orbs, Formatting.Indented);
                    File.WriteAllText(OrbsFilePath, orbJson);
                    _cachedOrbs = orbs; // keep memory cache in sync
                    Logger.Debug($"Saved {orbs.Count} orbs to cache: {OrbsFilePath}");
                }
                
                // // Save orb families
                // if (orbFamilies != null && orbFamilies.Count > 0)
                // {
                //     var orbFamiliesJson = JsonConvert.SerializeObject(orbFamilies, Formatting.Indented);
                //     File.WriteAllText(OrbFamiliesFilePath, orbFamiliesJson);
                //     _cachedOrbFamilies = orbFamilies; // keep memory cache in sync
                //     Logger.Debug($"Saved {orbFamilies.Count} orb families to cache: {OrbFamiliesFilePath}");
                // }


                // Save metadata
                var metadata = new EntityCacheMetadata
                {
                    PeglinInstallHash = installHash,
                    CachedAt = DateTime.Now,
                    SourceBundles = sourceBundles,
                    EntityCounts = new Dictionary<string, int>
                    {
                        ["Relics"] = relics?.Count ?? 0,
                        ["Enemies"] = enemies?.Count ?? 0,
                        ["Orbs"] = orbs?.Count ?? 0,
                        // ["OrbFamilies"] = orbFamilies?.Count ?? 0
                    }
                };

                var metadataJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                File.WriteAllText(MetadataFilePath, metadataJson);

                Logger.Info($"Entity cache saved to: {EntityDataDirectory}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving entity cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if entity cache is valid for the given Peglin installation
        /// </summary>
        public static bool IsCacheValid(string peglinPath)
        {
            try
            {
                if (!File.Exists(MetadataFilePath))
                    return false;

                var metadataJson = File.ReadAllText(MetadataFilePath);
                var metadata = JsonConvert.DeserializeObject<EntityCacheMetadata>(metadataJson);

                if (metadata == null)
                    return false;

                var currentHash = CalculatePeglinInstallHash(peglinPath);
                return metadata.PeglinInstallHash == currentHash;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking entity cache validity: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets cached relics data
        /// </summary>
        public static Dictionary<string, RelicData> GetCachedRelics()
        {
            try
            {
                if (_cachedRelics.Count > 0)
                    return _cachedRelics;

                if (!File.Exists(RelicsFilePath))
                    return new Dictionary<string, RelicData>();

                var json = File.ReadAllText(RelicsFilePath);
                _cachedRelics = JsonConvert.DeserializeObject<Dictionary<string, RelicData>>(json)
                                ?? new Dictionary<string, RelicData>();
                return _cachedRelics;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading cached relics: {ex.Message}");
                return new Dictionary<string, RelicData>();
            }
        }

        /// <summary>
        /// Gets cached enemies data
        /// </summary>
        public static Dictionary<string, EnemyData> GetCachedEnemies()
        {
            try
            {
                if (_cachedEnemies.Count > 0)
                    return _cachedEnemies;

                if (!File.Exists(EnemiesFilePath))
                    return new Dictionary<string, EnemyData>();

                var json = File.ReadAllText(EnemiesFilePath);
                _cachedEnemies = JsonConvert.DeserializeObject<Dictionary<string, EnemyData>>(json)
                                 ?? new Dictionary<string, EnemyData>();
                return _cachedEnemies;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading cached enemies: {ex.Message}");
                return new Dictionary<string, EnemyData>();
            }
        }

        /// <summary>
        /// Gets cached orbs data
        /// </summary>
        public static Dictionary<string, OrbData> GetCachedOrbs()
        {
            try
            {
                if (_cachedOrbs.Count > 0)
                {
                    Logger.Debug($"Returning {_cachedOrbs.Count} orbs from memory cache");
                    return _cachedOrbs;
                }

                Logger.Debug($"Looking for orbs file at: {OrbsFilePath}");
                if (!File.Exists(OrbsFilePath))
                {
                    Logger.Warning($"Orbs file does not exist at: {OrbsFilePath}");
                    return new Dictionary<string, OrbData>();
                }

                var json = File.ReadAllText(OrbsFilePath);
                Logger.Debug($"Loaded orbs JSON, length: {json.Length}");
                _cachedOrbs = JsonConvert.DeserializeObject<Dictionary<string, OrbData>>(json)
                              ?? new Dictionary<string, OrbData>();
                Logger.Info($"Successfully loaded {_cachedOrbs.Count} orbs from cache");
                return _cachedOrbs;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading cached orbs from {OrbsFilePath}: {ex.Message}");
                Logger.Error($"Exception details: {ex}");
                return new Dictionary<string, OrbData>();
            }
        }

        /// <summary>
        /// Gets cached grouped orbs data
        /// </summary>
        public static Dictionary<string, OrbGroupedData> GetCachedOrbsGrouped()
        {
            try
            {
                if (_cachedOrbsGrouped.Count > 0)
                    return _cachedOrbsGrouped;

                if (!File.Exists(OrbsGroupedFilePath))
                    return new Dictionary<string, OrbGroupedData>();

                var json = File.ReadAllText(OrbsGroupedFilePath);
                _cachedOrbsGrouped = JsonConvert.DeserializeObject<Dictionary<string, OrbGroupedData>>(json)
                                      ?? new Dictionary<string, OrbGroupedData>();
                return _cachedOrbsGrouped;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading grouped orbs: {ex.Message}");
                return new Dictionary<string, OrbGroupedData>();
            }
        }

        private static Dictionary<string, RelicData> _cachedRelics = new();
        private static Dictionary<string, EnemyData> _cachedEnemies = new();
        private static Dictionary<string, OrbData> _cachedOrbs = new();
        private static Dictionary<string, OrbGroupedData> _cachedOrbsGrouped = new();
        // private static Dictionary<string, OrbFamily> _cachedOrbFamilies = new();

        public static void SetCachedRelics(Dictionary<string, RelicData> relics)
        {
            _cachedRelics = relics;
        }

        public static void SetCachedEnemies(Dictionary<string, EnemyData> enemies)
        {
            _cachedEnemies = enemies;
        }

        public static void SetCachedOrbs(Dictionary<string, OrbData> orbs)
        {
            _cachedOrbs = orbs;
        }

        public static void SaveGroupedOrbs(Dictionary<string, OrbGroupedData> grouped)
        {
            try
            {
                Directory.CreateDirectory(EntityDataDirectory);
                var json = JsonConvert.SerializeObject(grouped, Formatting.Indented);
                File.WriteAllText(OrbsGroupedFilePath, json);
                _cachedOrbsGrouped = grouped;
                Logger.Info($"Saved grouped orbs to: {OrbsGroupedFilePath} ({grouped.Count})");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving grouped orbs: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets cached orb families
        // /// </summary>
        // public static Dictionary<string, OrbFamily> GetCachedOrbFamilies()
        // {
        //     try
        //     {
        //         if (_cachedOrbFamilies.Count > 0)
        //         {
        //             Logger.Debug($"Returning {_cachedOrbFamilies.Count} orb families from memory cache");
        //             return _cachedOrbFamilies;
        //         }
                
        //         if (!File.Exists(OrbFamiliesFilePath))
        //         {
        //             Logger.Debug("No orb families cache file found");
        //             return new Dictionary<string, OrbFamily>();
        //         }
                
        //         var json = File.ReadAllText(OrbFamiliesFilePath);
        //         Logger.Debug($"Loaded orb families JSON, length: {json.Length}");
        //         // _cachedOrbFamilies = JsonConvert.DeserializeObject<Dictionary<string, OrbFamily>>(json)
        //         //                     ?? new Dictionary<string, OrbFamily>();
        //         // Logger.Info($"Successfully loaded {_cachedOrbFamilies.Count} orb families from cache");
        //         return null;
        //     }
        //     catch (Exception ex)
        //     {
        //         Logger.Error($"Error loading orb families from cache: {ex.Message}");
        //         return new Dictionary<string, OrbFamily>();
        //     }
        // }
        
        /// <summary>
        /// Saves orb families to cache file only
        /// </summary>
        // public static void SaveOrbFamiliesToCache(Dictionary<string, OrbFamily> families)
        // {
        //     try
        //     {
        //         var json = JsonConvert.SerializeObject(families, Formatting.Indented);
        //         File.WriteAllText(OrbFamiliesFilePath, json);
        //         _cachedOrbFamilies = families;
        //         Logger.Info($"Saved orb families to: {OrbFamiliesFilePath} ({families.Count})");
        //     }
        //     catch (Exception ex)
        //     {
        //         Logger.Error($"Error saving orb families: {ex.Message}");
        //     }
        // }

        /// <summary>
        /// Calculates hash of Peglin installation to detect changes
        /// </summary>
        private static string CalculatePeglinInstallHash(string peglinPath)
        {
            try
            {
                var bundlePath = Path.Combine(peglinPath, "Data", "StreamingAssets", "aa");
                
                if (!Directory.Exists(bundlePath))
                    return "no-bundles-found";

                var bundleFiles = Directory.GetFiles(bundlePath, "*.bundle", SearchOption.AllDirectories)
                    .OrderBy(f => f)
                    .ToList();

                if (!bundleFiles.Any())
                    return "no-bundles-found";

                using var sha256 = SHA256.Create();
                var combinedHash = new StringBuilder();

                foreach (var bundleFile in bundleFiles)
                {
                    var fileInfo = new FileInfo(bundleFile);
                    var fileData = $"{fileInfo.Name}:{fileInfo.Length}:{fileInfo.LastWriteTime:yyyy-MM-dd-HH-mm-ss}";
                    var fileHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(fileData));
                    combinedHash.Append(Convert.ToHexString(fileHash));
                }

                var finalHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedHash.ToString()));
                return Convert.ToHexString(finalHash);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating Peglin install hash: {ex.Message}");
                return "hash-error";
            }
        }

        /// <summary>
        /// Clears the entity cache
        /// </summary>
        public static void ClearCache()
        {
            try
            {
                if (Directory.Exists(EntityDataDirectory))
                {
                    Directory.Delete(EntityDataDirectory, true);
                    Logger.Info("Entity cache cleared");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error clearing entity cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows entity cache status information
        /// </summary>
        public static void ShowCacheStatus()
        {
            try
            {
                if (!File.Exists(MetadataFilePath))
                {
                    Console.WriteLine("No entity cache found");
                    return;
                }

                var metadataJson = File.ReadAllText(MetadataFilePath);
                var metadata = JsonConvert.DeserializeObject<EntityCacheMetadata>(metadataJson);

                if (metadata != null)
                {
                    Console.WriteLine("=== ENTITY CACHE STATUS ===");
                    Console.WriteLine($"Cached At: {metadata.CachedAt}");
                    Console.WriteLine($"Install Hash: {metadata.PeglinInstallHash}");
                    Console.WriteLine($"Relics: {metadata.EntityCounts.GetValueOrDefault("Relics", 0)}");
                    Console.WriteLine($"Enemies: {metadata.EntityCounts.GetValueOrDefault("Enemies", 0)}");
                    Console.WriteLine($"Orbs: {metadata.EntityCounts.GetValueOrDefault("Orbs", 0)}");
                    Console.WriteLine($"Source Bundles: {metadata.SourceBundles.Count}");
                    Console.WriteLine($"Cache Location: {EntityDataDirectory}");

                    var age = DateTime.Now - metadata.CachedAt;
                    Console.WriteLine($"Cache Age: {age.TotalDays:F1} days");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing entity cache status: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the path to cached entity files
        /// </summary>
        public static string GetCachedRelicsPath() => RelicsFilePath;
        public static string GetCachedEnemiesPath() => EnemiesFilePath;
        public static string GetCachedOrbsPath() => OrbsFilePath;
    }
}
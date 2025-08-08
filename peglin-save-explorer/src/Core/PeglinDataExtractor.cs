using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using peglin_save_explorer.Data;
using peglin_save_explorer.Extractors;
using peglin_save_explorer.UI;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Services;

namespace peglin_save_explorer.Core
{
    /// <summary>
    /// Unified extractor for all Peglin game data (relics, sprites, classes, etc.)
    /// Provides hash-based caching and progress reporting for a better user experience
    /// </summary>
    public class PeglinDataExtractor
    {
        private static readonly string CacheDirectory = GetCacheDirectory();
        private static readonly string ExtractionMetadataPath = Path.Combine(CacheDirectory, "extraction_metadata.json");

        public enum ExtractionType
        {
            Relics,
            Sprites,
            Classes,
            GameObjects,
            All
        }

        public class ExtractionMetadata
        {
            public DateTime LastExtraction { get; set; }
            public string PeglinPath { get; set; } = "";
            public string PeglinInstallHash { get; set; } = "";
            public string Version { get; set; } = "1.0";
            public Dictionary<string, DateTime> ExtractionTimes { get; set; } = new();
            public Dictionary<string, int> ExtractedCounts { get; set; } = new();
            public List<string> SourceBundles { get; set; } = new();
        }

        public class ExtractionResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public TimeSpan Duration { get; set; }
            public Dictionary<string, int> ExtractedCounts { get; set; } = new();
            public bool UsedCache { get; set; }
            public string PeglinPath { get; set; } = "";
        }

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

        /// <summary>
        /// Checks if extraction is needed based on Peglin installation hash
        /// </summary>
        public static bool IsExtractionNeeded(string peglinPath, ExtractionType extractionType = ExtractionType.All, bool force = false)
        {
            if (force)
                return true;

            try
            {
                if (!File.Exists(ExtractionMetadataPath))
                {
                    Logger.Debug("Extraction metadata not found, extraction needed");
                    return true;
                }

                var metadata = JsonConvert.DeserializeObject<ExtractionMetadata>(File.ReadAllText(ExtractionMetadataPath));
                if (metadata == null)
                {
                    Logger.Warning("Invalid extraction metadata, extraction needed");
                    return true;
                }

                // Check if Peglin path matches
                if (!string.Equals(metadata.PeglinPath, peglinPath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info("Peglin path changed, extraction needed");
                    return true;
                }

                // Check if Peglin installation hash matches (detects game updates)
                var currentHash = CalculatePeglinInstallHash(peglinPath);
                if (currentHash != metadata.PeglinInstallHash)
                {
                    Logger.Info("Peglin installation changed (hash mismatch), extraction needed");
                    return true;
                }

                // Check if extraction is too old (older than 7 days)
                if ((DateTime.Now - metadata.LastExtraction).TotalDays > 7)
                {
                    Logger.Info("Extraction is older than 7 days, re-extraction needed");
                    return true;
                }

                // Check specific extraction types
                if (extractionType != ExtractionType.All)
                {
                    var typeName = extractionType.ToString();
                    if (!metadata.ExtractionTimes.ContainsKey(typeName) || 
                        !metadata.ExtractedCounts.ContainsKey(typeName))
                    {
                        Logger.Info($"No previous {typeName} extraction found, extraction needed");
                        return true;
                    }

                    // Check if specific extraction is older than overall extraction
                    if (metadata.ExtractionTimes[typeName] < metadata.LastExtraction.AddHours(-1))
                    {
                        Logger.Info($"{typeName} extraction is outdated, extraction needed");
                        return true;
                    }
                }

                Logger.Debug("All extractions are up to date");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error checking extraction status, assuming extraction needed: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Performs unified extraction of Peglin data with progress reporting
        /// </summary>
        public static async Task<ExtractionResult> ExtractPeglinDataAsync(
            string peglinPath, 
            ExtractionType extractionType = ExtractionType.All, 
            bool force = false,
            IProgress<string>? progress = null)
        {
            var startTime = DateTime.Now;
            var result = new ExtractionResult
            {
                PeglinPath = peglinPath
            };

            try
            {
                // Validate Peglin installation
                var bundleDirectory = PeglinPathHelper.GetStreamingAssetsBundlePath(peglinPath);
                if (string.IsNullOrEmpty(bundleDirectory) || !Directory.Exists(bundleDirectory))
                {
                    result.ErrorMessage = $"Invalid Peglin installation: {peglinPath}";
                    return result;
                }

                // Check if extraction is needed
                if (!IsExtractionNeeded(peglinPath, extractionType, force))
                {
                    progress?.Report("✅ Peglin data is up to date, skipping extraction");
                    result.Success = true;
                    result.UsedCache = true;
                    result.Duration = DateTime.Now - startTime;
                    
                    // Load existing counts
                    if (File.Exists(ExtractionMetadataPath))
                    {
                        var existingMetadata = JsonConvert.DeserializeObject<ExtractionMetadata>(File.ReadAllText(ExtractionMetadataPath));
                        if (existingMetadata?.ExtractedCounts != null)
                        {
                            result.ExtractedCounts = existingMetadata.ExtractedCounts;
                        }
                    }
                    
                    return result;
                }

                progress?.Report($"🎮 Extracting Peglin data from: {peglinPath}");
                
                var bundleFiles = Directory.GetFiles(bundleDirectory, "*.bundle", SearchOption.AllDirectories);
                var assetFiles = Directory.GetFiles(bundleDirectory, "*.assets", SearchOption.AllDirectories);
                var allFiles = bundleFiles.Concat(assetFiles).ToArray();
                progress?.Report($"📦 Found {bundleFiles.Length} bundle files and {assetFiles.Length} assets files to process ({allFiles.Length} total)");

                var extractedCounts = new Dictionary<string, int>();
                var extractionTimes = new Dictionary<string, DateTime>();

                    // Use the new unified extractor for single-pass extraction
                    progress?.Report("🚀 Starting unified extraction of all data types...");
                    
                    var unifiedExtractor = new UnifiedAssetExtractor(null);
                    var extractionResult = await Task.Run(() => unifiedExtractor.ExtractAllAssetsFromPeglinInstall(peglinPath, progress));
                    
                    // Save all extracted data
                    progress?.Report("💾 Saving extracted data...");
                    
                    // // Group orbs and save grouped cache
                    // var groupedOrbs = AssetRipperOrbExtractor.GroupOrbs(extractionResult.Orbs);
                    // Data.EntityCacheManager.SaveGroupedOrbs(groupedOrbs);
                    
                    // Save entities using unified cache manager
                    var sourceBundles = bundleFiles.Select(Path.GetFileName).Where(name => name != null).Cast<string>().ToList();
                    EntityCacheManager.SaveToCache(extractionResult.Relics, extractionResult.Enemies, extractionResult.Orbs, peglinPath, sourceBundles);
                    
                    // Populate caches with extracted data
                    if (extractionResult.Relics.Count > 0)
                    {
                        Data.EntityCacheManager.SetCachedRelics(extractionResult.Relics);
                        progress?.Report($"✅ Cached {extractionResult.Relics.Count} relics");
                    }

                    if (extractionResult.Enemies.Count > 0)
                    {
                        Data.EntityCacheManager.SetCachedEnemies(extractionResult.Enemies);
                        progress?.Report($"✅ Cached {extractionResult.Enemies.Count} enemies");
                    }

                    if (extractionResult.Orbs.Count > 0)
                    {
                        Data.EntityCacheManager.SetCachedOrbs(extractionResult.Orbs);
                        progress?.Report($"✅ Cached {extractionResult.Orbs.Count} orbs");
                    }

                    if (extractionResult.Sprites.Count > 0)
                    {
                        Data.SpriteCacheManager.SetCachedSprites(extractionResult.Sprites.Values.ToList());
                        progress?.Report($"✅ Cached {extractionResult.Sprites.Count} sprites");
                    }
                    
                    extractedCounts["Relics"] = extractionResult.Relics.Count;
                    extractedCounts["Enemies"] = extractionResult.Enemies.Count;
                    extractedCounts["Orbs"] = extractionResult.Orbs.Count;
                    
                    // Also create the structured orbs.json file
                    var orbsOutputPath = Path.Combine(CacheDirectory, "orbs.json");
                    CreateStructuredOrbsJsonFromExtractedData(extractionResult.Orbs, orbsOutputPath);
                    
                    // Save sprites
                    SpriteCacheManager.SaveToCache(extractionResult.Sprites, peglinPath, sourceBundles);
                    
                    // Extract and save localization strings
                    progress?.Report("🌐 Extracting localization strings...");
                    await ExtractAndSaveLocalizationStrings(progress);
                    
                    var relicSprites = extractionResult.Sprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Relic);
                    var enemySprites = extractionResult.Sprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Enemy);
                    var orbSprites = extractionResult.Sprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Orb);
                    
                    extractedCounts["Sprites"] = extractionResult.Sprites.Count;
                    extractedCounts["RelicSprites"] = relicSprites;
                    extractedCounts["EnemySprites"] = enemySprites;
                    extractedCounts["OrbSprites"] = orbSprites;
                    
                    // Add localization strings count
                    var localizationService = LocalizationService.Instance;
                    if (localizationService.IsLoaded)
                    {
                        extractedCounts["LocalizationStrings"] = localizationService.GetTermCount();
                        extractedCounts["LocalizationLanguages"] = localizationService.GetAvailableLanguages().Count;
                    }
                    
                    // Extract character classes using existing extractor
                    progress?.Report("👤 Extracting character classes...");
                    var classExtractor = new AssetRipperClassExtractor();
                    var classes = await Task.Run(() => classExtractor.ExtractClassInfoFromBundles(bundleDirectory));
                    extractedCounts["Classes"] = classes.Count;
                    progress?.Report($"✅ Extracted {classes.Count} character classes");
                    
                    // Update extraction times for all types
                    extractionTimes["Relics"] = DateTime.Now;
                    extractionTimes["Orbs"] = DateTime.Now;
                    extractionTimes["Sprites"] = DateTime.Now;
                    extractionTimes["Classes"] = DateTime.Now;
                    
                    Logger.Info($"🎯 Unified extraction complete: {extractionResult.Relics.Count} relics, {extractionResult.Orbs.Count} orbs, {extractionResult.Sprites.Count} sprites, {classes.Count} classes");
                    progress?.Report("✅ Unified extraction complete!");
               

                // Save extraction metadata
                var metadata = new ExtractionMetadata
                {
                    LastExtraction = DateTime.Now,
                    PeglinPath = peglinPath,
                    PeglinInstallHash = CalculatePeglinInstallHash(peglinPath),
                    ExtractionTimes = extractionTimes,
                    ExtractedCounts = extractedCounts,
                    SourceBundles = bundleFiles.Select(Path.GetFileName).Where(name => name != null).ToList()!
                };

                Directory.CreateDirectory(CacheDirectory);
                var metadataJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                File.WriteAllText(ExtractionMetadataPath, metadataJson);

                result.Success = true;
                result.ExtractedCounts = extractedCounts;
                result.Duration = DateTime.Now - startTime;

                var totalItems = extractedCounts.Sum(kvp => kvp.Value);
                progress?.Report($"🎉 Extraction completed! {totalItems} items extracted in {result.Duration.TotalSeconds:F1}s");

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"Extraction failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                result.Duration = DateTime.Now - startTime;
                progress?.Report($"❌ Extraction failed: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Synchronous version of ExtractPeglinDataAsync with console spinner
        /// </summary>
        public static ExtractionResult ExtractPeglinData(
            string peglinPath, 
            ExtractionType extractionType = ExtractionType.All, 
            bool force = false,
            bool showSpinner = true)
        {
            if (!showSpinner)
            {
                return ExtractPeglinDataAsync(peglinPath, extractionType, force).Result;
            }

            ConsoleSpinner? spinner = null;
            ExtractionResult? result = null;
            Exception? exception = null;

            var task = Task.Run(async () =>
            {
                try
                {
                    result = await ExtractPeglinDataAsync(peglinPath, extractionType, force, new Progress<string>(message =>
                    {
                        spinner?.Update(message);
                    }));
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });

            // Start spinner
            spinner = new ConsoleSpinner();
            spinner.Start("🎮 Initializing Peglin data extraction...");

            try
            {
                task.Wait();
            }
            finally
            {
                spinner.Stop();
            }

            if (exception != null)
            {
                throw exception;
            }

            return result ?? new ExtractionResult { ErrorMessage = "Unknown error occurred" };
        }

        /// <summary>
        /// Gets the current extraction status
        /// </summary>
        public static ExtractionMetadata? GetExtractionStatus()
        {
            try
            {
                if (!File.Exists(ExtractionMetadataPath))
                    return null;

                return JsonConvert.DeserializeObject<ExtractionMetadata>(File.ReadAllText(ExtractionMetadataPath));
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting extraction status: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Shows extraction status information
        /// </summary>
        public static void ShowExtractionStatus()
        {
            var metadata = GetExtractionStatus();
            if (metadata == null)
            {
                Console.WriteLine("No extraction data found.");
                Console.WriteLine("Run 'peglin-save-explorer extract' to extract Peglin data.");
                return;
            }

            Console.WriteLine("=== PEGLIN DATA EXTRACTION STATUS ===");
            Console.WriteLine($"Last Extraction: {metadata.LastExtraction}");
            Console.WriteLine($"Peglin Path: {metadata.PeglinPath}");
            Console.WriteLine($"Install Hash: {metadata.PeglinInstallHash}");
            Console.WriteLine($"Source Bundles: {metadata.SourceBundles.Count}");
            Console.WriteLine();
            Console.WriteLine("📁 Extracted Data Locations:");
            Console.WriteLine($"   Base Directory: {CacheDirectory}");
            Console.WriteLine($"   ├── extraction_metadata.json (extraction status)");
            if (metadata.ExtractedCounts.ContainsKey("Sprites") && metadata.ExtractedCounts["Sprites"] > 0)
            {
                Console.WriteLine($"   ├── extracted-data/sprites/relics/ ({metadata.ExtractedCounts.GetValueOrDefault("RelicSprites", 0)} relic sprites)");
                Console.WriteLine($"   ├── extracted-data/sprites/enemies/ ({metadata.ExtractedCounts.GetValueOrDefault("EnemySprites", 0)} enemy sprites)");
                Console.WriteLine($"   └── extracted-data/sprites/sprite_cache_metadata.json (sprite metadata)");
            }
            if (metadata.ExtractedCounts.ContainsKey("Relics") && metadata.ExtractedCounts["Relics"] > 0)
            {
                Console.WriteLine($"   └── relic_mapping_cache.json ({metadata.ExtractedCounts["Relics"]} relics)");
            }

            var age = DateTime.Now - metadata.LastExtraction;
            Console.WriteLine($"Cache Age: {age.TotalDays:F1} days");

            if (age.TotalDays > 7)
            {
                Console.WriteLine("⚠️ Cache is older than 7 days and may be outdated");
            }
            else
            {
                Console.WriteLine("✅ Cache is current");
            }

            Console.WriteLine();
            Console.WriteLine("Extracted Data:");
            foreach (var kvp in metadata.ExtractedCounts.OrderBy(k => k.Key))
            {
                var icon = kvp.Key switch
                {
                    "Relics" => "🔮",
                    "Sprites" => "🎨",
                    "RelicSprites" => "🖼️",
                    "EnemySprites" => "👹",
                    "Classes" => "👤",
                    "EntityCorrelations" => "🔗",
                    "RelicCorrelations" => "🔮🔗",
                    "EnemyCorrelations" => "👹🔗",
                    "UncorrelatedSprites" => "📦",
                    _ => "📊"
                };
                Console.WriteLine($"  {icon} {kvp.Key}: {kvp.Value}");
            }

            Console.WriteLine();
            Console.WriteLine("Extraction Times:");
            foreach (var kvp in metadata.ExtractionTimes.OrderBy(k => k.Key))
            {
                Console.WriteLine($"  • {kvp.Key}: {kvp.Value}");
            }
        }

        /// <summary>
        /// Clears all extraction caches
        /// </summary>
        public static void ClearAllCaches()
        {
            try
            {
                // Clear sprite cache
                SpriteCacheManager.ClearCache();
                
                // Clear relic cache
                RelicMappingCache.ClearCache();
                
                // Clear extraction metadata
                if (File.Exists(ExtractionMetadataPath))
                {
                    File.Delete(ExtractionMetadataPath);
                }

                // Clear any other cache files
                if (Directory.Exists(CacheDirectory))
                {
                    var cacheFiles = Directory.GetFiles(CacheDirectory, "*cache*.json");
                    foreach (var file in cacheFiles)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Could not delete cache file {file}: {ex.Message}");
                        }
                    }
                }

                Console.WriteLine("✅ All extraction caches cleared");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error clearing caches: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the cache directory path where extracted data is stored
        /// </summary>
        public static string GetExtractionCacheDirectory()
        {
            return CacheDirectory;
        }

        /// <summary>
        /// Creates a structured orbs.json file from extracted orb data
        /// </summary>
        private static void CreateStructuredOrbsJsonFromExtractedData(Dictionary<string, OrbData> orbs, string outputPath)
        {
            return;
            //     try
            //     {
            //         var structuredOrbs = new Dictionary<string, object>();
            //         var grouped = AssetRipperOrbExtractor.GroupOrbs(orbs);

            //         foreach (var kvp in grouped)
            //         {
            //             var g = kvp.Value;
            //             var levels = g.Levels.Select(l => new Dictionary<string, object>
            //             {
            //                 ["level"] = l.Level,
            //                 ["damagePerPeg"] = l.DamagePerPeg ?? 0f,
            //                 ["critDamagePerPeg"] = l.CritDamagePerPeg ?? 0f,
            //                 ["leafId"] = l.LeafId ?? string.Empty
            //             }).ToList();

            //             var orbData = new Dictionary<string, object>
            //             {
            //                 ["name"] = g.Name,
            //                 ["id"] = g.Id,
            //                 ["locKey"] = g.LocKey ?? string.Empty,
            //                 ["description"] = g.Description,
            //                 ["rarity"] = g.Rarity ?? string.Empty,
            //                 ["rarityValue"] = g.RarityValue ?? 0,
            //                 ["orbType"] = g.OrbType ?? string.Empty,
            //                 ["correlated"] = !string.IsNullOrEmpty(g.CorrelatedSpriteId),
            //                 ["spriteId"] = g.CorrelatedSpriteId ?? string.Empty,
            //                 ["spriteFilePath"] = g.SpriteFilePath ?? string.Empty,
            //                 ["correlationMethod"] = g.CorrelationMethod ?? string.Empty,
            //                 ["correlationConfidence"] = g.CorrelationConfidence,
            //                 ["levels"] = levels
            //             };
            //             structuredOrbs[g.Id] = orbData;
            //         }

            //         var json = JsonConvert.SerializeObject(structuredOrbs, Formatting.Indented);
            //         Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "");
            //         File.WriteAllText(outputPath, json);

            //         Logger.Info($"📝 Created structured orbs.json with {structuredOrbs.Count} orbs at: {outputPath}");
            //     }
            //     catch (Exception ex)
            //     {
            //         Logger.Error($"Error creating structured orbs.json: {ex.Message}");
            //     }
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
        /// Extracts and saves localization strings to strings.json
        /// </summary>
        private static async Task ExtractAndSaveLocalizationStrings(IProgress<string>? progress = null)
        {
            try
            {
                await Task.Run(() =>
                {
                    var localizationService = LocalizationService.Instance;
                    
                    // Ensure localization is loaded
                    if (!localizationService.EnsureLoaded())
                    {
                        Logger.Warning("[PeglinDataExtractor] Failed to load localization data for strings.json export");
                        progress?.Report("⚠️  Failed to load localization data");
                        return;
                    }
                    
                    // Get all localization data
                    var allStrings = localizationService.GetAllLocalizationData();
                    
                    if (allStrings == null || allStrings.Count == 0)
                    {
                        Logger.Warning("[PeglinDataExtractor] No localization strings found");
                        progress?.Report("⚠️  No localization strings found");
                        return;
                    }
                    
                    // Save to strings.json
                    var stringsPath = Path.Combine(CacheDirectory, "strings.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(stringsPath) ?? CacheDirectory);
                    
                    var json = JsonConvert.SerializeObject(allStrings, Formatting.Indented);
                    File.WriteAllText(stringsPath, json);
                    
                    var totalStrings = allStrings.Values.SelectMany(lang => lang.Keys).Distinct().Count();
                    var languages = allStrings.Keys.Count;
                    
                    Logger.Info($"🌐 Exported {totalStrings} localization strings in {languages} languages to strings.json");
                    progress?.Report($"✅ Saved {totalStrings} localization strings");
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"[PeglinDataExtractor] Error extracting localization strings: {ex.Message}");
                progress?.Report("❌ Failed to extract localization strings");
            }
        }

    }
}
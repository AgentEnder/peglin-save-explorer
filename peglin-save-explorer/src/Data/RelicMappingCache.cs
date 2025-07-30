using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using peglin_save_explorer.Extractors;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Data
{
    /// <summary>
    /// Manages caching and mapping of relic data extracted from Unity bundles
    /// Provides Effect # -> Display Name mappings for save file analysis
    /// </summary>
    public class RelicMappingCache
    {
        private List<RelicMapping> _cachedMappings = new();
        private Dictionary<int, string> _idToNameMap = new();
        private Dictionary<string, string> _unknownRelicPatternMap = new();
        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PeglinSaveExplorer"
        );

        private static readonly string CacheFilePath = Path.Combine(CacheDirectory, "relic_mappings.json");
        private static readonly string MetadataFilePath = Path.Combine(CacheDirectory, "cache_metadata.json");

        public class RelicMapping
        {
            public int EffectId { get; set; }
            public string DisplayName { get; set; } = "";
            public string InternalId { get; set; } = "";
            public string Description { get; set; } = "";
            public string SourceBundle { get; set; } = "";
            public DateTime ExtractedAt { get; set; }
        }

        public class CacheMetadata
        {
            public DateTime LastUpdated { get; set; }
            public string PeglinPath { get; set; } = "";
            public string Version { get; set; } = "1.0";
            public int RelicCount { get; set; }
            public List<string> SourceBundles { get; set; } = new();
        }

        /// <summary>
        /// Gets cached relic mappings or extracts them if cache is invalid/missing
        /// </summary>
        public static Dictionary<int, string> GetRelicMappings(string peglinPath, bool forceRefresh = false)
        {
            try
            {
                // Check if cache is valid
                if (!forceRefresh && IsCacheValid(peglinPath))
                {
                    Console.WriteLine("Loading relic mappings from cache...");
                    var cachedMappings = LoadFromCache();
                    if (cachedMappings.Any())
                    {
                        Console.WriteLine($"Loaded {cachedMappings.Count} relic mappings from cache");
                        return cachedMappings.ToDictionary(r => r.EffectId, r => r.DisplayName);
                    }
                }

                Console.WriteLine("Cache invalid or missing, extracting fresh relic data...");
                return ExtractAndCacheRelicMappings(peglinPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting relic mappings: {ex.Message}");
                return new Dictionary<int, string>();
            }
        }

        /// <summary>
        /// Forces a refresh of the relic cache
        /// </summary>
        public static Dictionary<int, string> RefreshCache(string peglinPath)
        {
            return GetRelicMappings(peglinPath, forceRefresh: true);
        }

        /// <summary>
        /// Gets detailed relic information including descriptions
        /// </summary>
        public static List<RelicMapping> GetDetailedRelicMappings(string peglinPath, bool forceRefresh = false)
        {
            try
            {
                if (!forceRefresh && IsCacheValid(peglinPath))
                {
                    var cachedMappings = LoadFromCache();
                    if (cachedMappings.Any())
                    {
                        return cachedMappings;
                    }
                }

                ExtractAndCacheRelicMappings(peglinPath);
                return LoadFromCache();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting detailed relic mappings: {ex.Message}");
                return new List<RelicMapping>();
            }
        }

        private static bool IsCacheValid(string peglinPath)
        {
            try
            {
                if (!File.Exists(CacheFilePath) || !File.Exists(MetadataFilePath))
                {
                    return false;
                }

                var metadata = JsonConvert.DeserializeObject<CacheMetadata>(File.ReadAllText(MetadataFilePath));
                if (metadata == null)
                {
                    return false;
                }

                // Check if Peglin path matches
                if (!string.Equals(metadata.PeglinPath, peglinPath, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Cache invalid: Peglin path changed");
                    return false;
                }

                // Check if cache is too old (older than 7 days)
                if ((DateTime.Now - metadata.LastUpdated).TotalDays > 7)
                {
                    Console.WriteLine("Cache invalid: older than 7 days");
                    return false;
                }

                // Check if source bundles still exist and haven't been modified
                var bundleDirectory = Path.Combine(peglinPath, "Peglin_Data", "StreamingAssets", "aa", "StandaloneWindows64");
                if (!Directory.Exists(bundleDirectory))
                {
                    Console.WriteLine("Cache invalid: bundle directory not found");
                    return false;
                }

                foreach (var sourceBundle in metadata.SourceBundles)
                {
                    var bundlePath = Path.Combine(bundleDirectory, sourceBundle);
                    if (!File.Exists(bundlePath))
                    {
                        Console.WriteLine($"Cache invalid: source bundle not found: {sourceBundle}");
                        return false;
                    }

                    // Check if bundle was modified after cache creation
                    if (File.GetLastWriteTime(bundlePath) > metadata.LastUpdated)
                    {
                        Console.WriteLine($"Cache invalid: source bundle modified: {sourceBundle}");
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Dictionary<int, string> ExtractAndCacheRelicMappings(string peglinPath)
        {
            try
            {
                // Create cache directory if it doesn't exist
                if (!Directory.Exists(CacheDirectory))
                {
                    Directory.CreateDirectory(CacheDirectory);
                }

                // Extract relic data using the improved extractor
                var tempOutputDir = Path.Combine(Path.GetTempPath(), "peglin_relic_extraction_" + Guid.NewGuid().ToString("N")[..8]);
                Directory.CreateDirectory(tempOutputDir);

                try
                {
                    var bundleDirectory = Path.Combine(peglinPath, "Peglin_Data", "StreamingAssets", "aa", "StandaloneWindows64");
                    
                    // Use AssetRipper extractor instead of ImprovedRelicExtractor
                    var extractor = new AssetRipperRelicExtractor(null);
                    var allRelics = new Dictionary<string, AssetRipperRelicExtractor.RelicData>();
                    
                    var bundleFiles = Directory.GetFiles(bundleDirectory, "*.bundle", SearchOption.AllDirectories);
                    foreach (var bundleFile in bundleFiles)
                    {
                        var relics = extractor.ExtractRelics(bundleFile);
                        foreach (var kvp in relics)
                        {
                            allRelics[kvp.Key] = kvp.Value;
                        }
                    }
                    
                    // Save to temporary JSON file for processing
                    var tempJsonFile = Path.Combine(tempOutputDir, "assetripper_relics.json");
                    var json = JsonConvert.SerializeObject(allRelics, Formatting.Indented);
                    File.WriteAllText(tempJsonFile, json);

                    // Process extracted data and convert to mappings
                    var relicMappings = ProcessAssetRipperRelicData(allRelics, peglinPath);

                    // Save to cache
                    SaveToCache(relicMappings, peglinPath, GetSourceBundles(bundleDirectory));

                    Console.WriteLine($"Successfully cached {relicMappings.Count} relic mappings");
                    return relicMappings.ToDictionary(r => r.EffectId, r => r.DisplayName);
                }
                finally
                {
                    // Clean up temp directory
                    if (Directory.Exists(tempOutputDir))
                    {
                        Directory.Delete(tempOutputDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting and caching relic mappings: {ex.Message}");
                return new Dictionary<int, string>();
            }
        }

        private static List<RelicMapping> ProcessAssetRipperRelicData(Dictionary<string, AssetRipperRelicExtractor.RelicData> allRelics, string peglinPath)
        {
            var relicMappings = new List<RelicMapping>();

            try
            {
                foreach (var kvp in allRelics)
                {
                    var relic = kvp.Value;
                    
                    // Try to extract effect ID from the effect field
                    if (int.TryParse(relic.Effect, out int effectId) && effectId != 0)
                    {
                        var displayName = GetBestDisplayNameFromAssetRipper(relic);
                        
                        if (!string.IsNullOrEmpty(displayName))
                        {
                            relicMappings.Add(new RelicMapping
                            {
                                EffectId = effectId,
                                DisplayName = displayName,
                                InternalId = relic.Id,
                                Description = relic.Description ?? ""
                            });
                        }
                    }
                }

                Console.WriteLine($"Processed {relicMappings.Count} relic mappings from AssetRipper data");
                return relicMappings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing AssetRipper relic data: {ex.Message}");
                return relicMappings;
            }
        }

        private static List<RelicMapping> ProcessExtractedRelicData(string extractionDirectory, string peglinPath)
        {
            var relicMappings = new List<RelicMapping>();

            try
            {
                // Look for the combined results file
                var combinedResultsPath = Path.Combine(extractionDirectory, "all_relics_combined.json");
                if (File.Exists(combinedResultsPath))
                {
                    var extractedData = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(
                        File.ReadAllText(combinedResultsPath));

                    if (extractedData != null)
                    {
                        // Group by normalized ID to avoid duplicates
                        var uniqueRelics = extractedData.Values
                            .Where(r => !string.IsNullOrWhiteSpace(r.NormalizedId))
                            .GroupBy(r => r.NormalizedId)
                            .Select(g => g.First()) // Take first instance of each unique relic
                            .OrderBy(r => r.NormalizedId)
                            .ToList();

                        int fallbackEffectId = 1000; // Start fallback IDs at 1000 to avoid conflicts

                        // First pass: collect all relics with valid Effect IDs
                        var relicsWithEffectIds = new Dictionary<int, RelicMapping>();
                        var relicsWithoutEffectIds = new List<dynamic>();

                        foreach (var relic in uniqueRelics)
                        {
                            if (relic.EffectId.HasValue && relic.EffectId.Value > 0)
                            {
                                // Use actual Effect ID from game data
                                int effectId = relic.EffectId.Value;

                                // Handle potential duplicates by using the first occurrence
                                if (!relicsWithEffectIds.ContainsKey(effectId))
                                {
                                    var mapping = new RelicMapping
                                    {
                                        EffectId = effectId,
                                        DisplayName = GetBestDisplayName(relic),
                                        InternalId = relic.NormalizedId,
                                        Description = relic.Description ?? "",
                                        SourceBundle = relic.SourceFile,
                                        ExtractedAt = DateTime.Now
                                    };

                                    relicsWithEffectIds[effectId] = mapping;
                                    Console.WriteLine($"Using real Effect ID {effectId} for {mapping.DisplayName}");
                                }
                                else
                                {
                                    Console.WriteLine($"Skipping duplicate Effect ID {effectId} for {GetBestDisplayName(relic)} (already have {relicsWithEffectIds[effectId].DisplayName})");
                                }
                            }
                            else
                            {
                                relicsWithoutEffectIds.Add(relic);
                            }
                        }

                        // Add relics with real Effect IDs
                        relicMappings.AddRange(relicsWithEffectIds.Values);

                        // Second pass: assign fallback IDs to relics without Effect IDs
                        foreach (var relic in relicsWithoutEffectIds)
                        {
                            // Find next available fallback ID
                            while (relicsWithEffectIds.ContainsKey(fallbackEffectId))
                            {
                                fallbackEffectId++;
                            }

                            var mapping = new RelicMapping
                            {
                                EffectId = fallbackEffectId,
                                DisplayName = GetBestDisplayName(relic),
                                InternalId = relic.NormalizedId,
                                Description = relic.Description ?? "",
                                SourceBundle = relic.SourceFile,
                                ExtractedAt = DateTime.Now
                            };

                            relicMappings.Add(mapping);
                            relicsWithEffectIds[fallbackEffectId] = mapping; // Track for future duplicates
                            Console.WriteLine($"Using fallback Effect ID {fallbackEffectId} for {mapping.DisplayName} (no Effect ID found in data)");
                            fallbackEffectId++;
                        }

                        Console.WriteLine($"Processed {uniqueRelics.Count} unique relics into effect mappings");
                    }
                }
                else
                {
                    Console.WriteLine("No combined results file found in extraction directory");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing extracted relic data: {ex.Message}");
            }

            return relicMappings;
        }

        private static string GetBestDisplayNameFromAssetRipper(AssetRipperRelicExtractor.RelicData relic)
        {
            // Prioritize Name, then fallback to cleaned ID
            if (!string.IsNullOrWhiteSpace(relic.Name))
                return CleanDisplayName(relic.Name);

            if (!string.IsNullOrWhiteSpace(relic.Id))
                return CleanDisplayName(relic.Id.Replace("_", " "));

            return "Unknown Relic";
        }

        private static string GetBestDisplayName(dynamic relic)
        {
            // Prioritize display name, then name, then normalized ID
            if (!string.IsNullOrWhiteSpace(relic.DisplayName))
                return CleanDisplayName(relic.DisplayName);

            if (!string.IsNullOrWhiteSpace(relic.Name))
                return CleanDisplayName(relic.Name);

            if (!string.IsNullOrWhiteSpace(relic.NormalizedId))
                return CleanDisplayName(relic.NormalizedId.Replace("_", " "));

            return "Unknown Relic";
        }

        private static string CleanDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Unknown";

            // Clean up the display name
            return name.Trim()
                .Replace("_", " ")
                .Split(' ')
                .Select(word => word.Length > 0 ? char.ToUpper(word[0]) + word.Substring(1).ToLower() : word)
                .Aggregate((a, b) => a + " " + b)
                .Trim();
        }

        private static List<string> GetSourceBundles(string bundleDirectory)
        {
            try
            {
                return Directory.GetFiles(bundleDirectory, "*", SearchOption.AllDirectories)
                    .Where(f => f.Contains("scriptableobjects", StringComparison.OrdinalIgnoreCase) ||
                               f.Contains("scriptable", StringComparison.OrdinalIgnoreCase) ||
                               f.Contains("so_", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList()!;
            }
            catch
            {
                return new List<string>();
            }
        }

        private static void SaveToCache(List<RelicMapping> relicMappings, string peglinPath, List<string> sourceBundles)
        {
            try
            {
                // Save relic mappings
                var mappingsJson = JsonConvert.SerializeObject(relicMappings, Formatting.Indented);
                File.WriteAllText(CacheFilePath, mappingsJson);

                // Save metadata
                var metadata = new CacheMetadata
                {
                    LastUpdated = DateTime.Now,
                    PeglinPath = peglinPath,
                    RelicCount = relicMappings.Count,
                    SourceBundles = sourceBundles
                };

                var metadataJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                File.WriteAllText(MetadataFilePath, metadataJson);

                Console.WriteLine($"Saved {relicMappings.Count} relic mappings to cache at: {CacheFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving to cache: {ex.Message}");
            }
        }

        private static List<RelicMapping> LoadFromCache()
        {
            try
            {
                if (File.Exists(CacheFilePath))
                {
                    var json = File.ReadAllText(CacheFilePath);
                    return JsonConvert.DeserializeObject<List<RelicMapping>>(json) ?? new List<RelicMapping>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading from cache: {ex.Message}");
            }

            return new List<RelicMapping>();
        }

        /// <summary>
        /// Clears the relic mapping cache
        /// </summary>
        public static void ClearCache()
        {
            try
            {
                if (File.Exists(CacheFilePath))
                    File.Delete(CacheFilePath);

                if (File.Exists(MetadataFilePath))
                    File.Delete(MetadataFilePath);

                Console.WriteLine("Relic mapping cache cleared");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets cache status information
        /// </summary>
        public static void ShowCacheStatus()
        {
            try
            {
                if (!File.Exists(MetadataFilePath))
                {
                    Console.WriteLine("No relic mapping cache found");
                    return;
                }

                var metadata = JsonConvert.DeserializeObject<CacheMetadata>(File.ReadAllText(MetadataFilePath));
                if (metadata != null)
                {
                    Console.WriteLine("=== RELIC MAPPING CACHE STATUS ===");
                    Console.WriteLine($"Last Updated: {metadata.LastUpdated}");
                    Console.WriteLine($"Peglin Path: {metadata.PeglinPath}");
                    Console.WriteLine($"Relic Count: {metadata.RelicCount}");
                    Console.WriteLine($"Source Bundles: {metadata.SourceBundles.Count}");
                    Console.WriteLine($"Cache Location: {CacheFilePath}");

                    var age = DateTime.Now - metadata.LastUpdated;
                    Console.WriteLine($"Cache Age: {age.TotalDays:F1} days");

                    if (age.TotalDays > 7)
                    {
                        Console.WriteLine("⚠️ Cache is older than 7 days and may be outdated");
                    }
                    else
                    {
                        Console.WriteLine("✅ Cache is current");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing cache status: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads cached relic mappings from disk into this instance
        /// </summary>
        public void LoadFromDisk()
        {
            try
            {
                _cachedMappings = LoadFromCache();
                _idToNameMap = _cachedMappings.ToDictionary(r => r.EffectId, r => r.DisplayName);
                
                // Create patterns for resolving "Unknown Relic (ID)" patterns
                BuildUnknownRelicPatternMap();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading relic cache from disk: {ex.Message}");
                _cachedMappings = new List<RelicMapping>();
                _idToNameMap = new Dictionary<int, string>();
                _unknownRelicPatternMap = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Ensures relic cache is up to date by directly extracting from Peglin installation
        /// </summary>
        public static void EnsureCacheFromAssetRipper(string peglinPath)
        {
            try
            {
                if (string.IsNullOrEmpty(peglinPath) || !Directory.Exists(peglinPath))
                {
                    Console.WriteLine($"Peglin installation not found: {peglinPath}");
                    return;
                }

                // Check if we need to update the cache
                var shouldUpdate = false;
                
                if (!File.Exists(CacheFilePath) || !File.Exists(MetadataFilePath))
                {
                    shouldUpdate = true;
                    Console.WriteLine("Relic cache not found, extracting from Peglin installation...");
                }
                else
                {
                    // Check if cache is older than 7 days or if bundle files are newer
                    var metadata = JsonConvert.DeserializeObject<CacheMetadata>(File.ReadAllText(MetadataFilePath));
                    if (metadata != null)
                    {
                        var cacheAge = DateTime.Now - metadata.LastUpdated;
                        if (cacheAge.TotalDays > 7)
                        {
                            shouldUpdate = true;
                            Console.WriteLine("Relic cache is older than 7 days, updating...");
                        }
                        else if (!string.Equals(metadata.PeglinPath, peglinPath, StringComparison.OrdinalIgnoreCase))
                        {
                            shouldUpdate = true;
                            Console.WriteLine("Peglin path changed, updating cache...");
                        }
                    }
                }

                if (shouldUpdate)
                {
                    Console.WriteLine("Extracting relic data from Peglin installation...");
                    var extractor = new AssetRipperRelicExtractor(null);
                    var assetRipperData = extractor.ExtractAllRelicsFromPeglinInstall(peglinPath);
                    
                    if (assetRipperData != null && assetRipperData.Count > 0)
                    {
                        var relicMappings = ProcessAssetRipperDirectData(assetRipperData);
                        SaveToCache(relicMappings, peglinPath, new List<string>());
                        Console.WriteLine($"Updated relic cache with {relicMappings.Count} mappings from direct AssetRipper extraction");
                    }
                    else
                    {
                        Console.WriteLine("No relic data extracted from Peglin installation");
                    }
                }
                else
                {
                    Logger.Debug("Relic cache is up to date");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ensuring cache from AssetRipper: {ex.Message}");
            }
        }

        private static List<RelicMapping> ProcessAssetRipperDirectData(Dictionary<string, AssetRipperRelicExtractor.RelicData> assetRipperData)
        {
            var relicMappings = new List<RelicMapping>();

            try
            {
                foreach (var kvp in assetRipperData)
                {
                    var relic = kvp.Value;
                    
                    // Try to extract effect ID from the effect field
                    if (int.TryParse(relic.Effect, out int effectId) && effectId > 0)
                    {
                        var displayName = !string.IsNullOrWhiteSpace(relic.Name) ? relic.Name : CleanDisplayName(relic.Id);
                        
                        if (!string.IsNullOrEmpty(displayName))
                        {
                            relicMappings.Add(new RelicMapping
                            {
                                EffectId = effectId,
                                DisplayName = displayName,
                                InternalId = relic.Id,
                                Description = relic.Description ?? "",
                                ExtractedAt = DateTime.Now
                            });
                        }
                    }
                }

                Console.WriteLine($"Processed {relicMappings.Count} relic mappings from direct AssetRipper data");
                return relicMappings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing direct AssetRipper data: {ex.Message}");
                return relicMappings;
            }
        }

        private static List<RelicMapping> ProcessAssetRipperData(Dictionary<string, AssetRipperRelicData> assetRipperData)
        {
            var relicMappings = new List<RelicMapping>();

            try
            {
                foreach (var kvp in assetRipperData)
                {
                    var relic = kvp.Value;
                    
                    // Try to extract effect ID from the effect field
                    if (int.TryParse(relic.Effect, out int effectId) && effectId > 0)
                    {
                        var displayName = !string.IsNullOrWhiteSpace(relic.Name) ? relic.Name : CleanDisplayName(relic.Id);
                        
                        if (!string.IsNullOrEmpty(displayName))
                        {
                            relicMappings.Add(new RelicMapping
                            {
                                EffectId = effectId,
                                DisplayName = displayName,
                                InternalId = relic.Id,
                                Description = relic.Description ?? "",
                                ExtractedAt = DateTime.Now
                            });
                        }
                    }
                }

                Console.WriteLine($"Processed {relicMappings.Count} relic mappings from AssetRipper data");
                return relicMappings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing AssetRipper data: {ex.Message}");
                return relicMappings;
            }
        }

        public class AssetRipperRelicData
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string Effect { get; set; } = "";
            public int Rarity { get; set; }
        }

        /// <summary>
        /// Resolves a relic name from "Unknown Relic (ID)" or "Unknown Relic ID" format to actual relic name if available
        /// </summary>
        /// <param name="relicName">The relic name to resolve (e.g., "Unknown Relic (135)" or "Unknown Relic 170")</param>
        /// <returns>The resolved relic name or the original name if no mapping found</returns>
        public string ResolveRelicName(string relicName)
        {
            if (string.IsNullOrEmpty(relicName))
                return relicName;

            // Check if we have a direct pattern match
            if (_unknownRelicPatternMap.ContainsKey(relicName))
            {
                return _unknownRelicPatternMap[relicName];
            }

            // Try to extract ID from "Unknown Relic (ID)" pattern
            if (relicName.StartsWith("Unknown Relic (") && relicName.EndsWith(")"))
            {
                var idStr = relicName.Substring(15, relicName.Length - 16); // Extract ID from parentheses
                if (int.TryParse(idStr, out int relicId))
                {
                    if (_idToNameMap.ContainsKey(relicId))
                    {
                        var resolvedName = _idToNameMap[relicId];
                        // Cache this pattern for future lookups
                        _unknownRelicPatternMap[relicName] = resolvedName;
                        return resolvedName;
                    }
                }
            }

            // Try to extract ID from "Unknown Relic ID" pattern (without parentheses)
            if (relicName.StartsWith("Unknown Relic ") && !relicName.Contains("("))
            {
                var idStr = relicName.Substring(14); // Extract ID after "Unknown Relic "
                if (int.TryParse(idStr, out int relicId))
                {
                    if (_idToNameMap.ContainsKey(relicId))
                    {
                        var resolvedName = _idToNameMap[relicId];
                        // Cache this pattern for future lookups
                        _unknownRelicPatternMap[relicName] = resolvedName;
                        return resolvedName;
                    }
                }
            }

            return relicName; // Return original if no mapping found
        }

        /// <summary>
        /// Gets the count of loaded relic mappings
        /// </summary>
        public int Count => _cachedMappings.Count;

        /// <summary>
        /// Gets all loaded relic mappings
        /// </summary>
        public IReadOnlyList<RelicMapping> Mappings => _cachedMappings.AsReadOnly();

        private void BuildUnknownRelicPatternMap()
        {
            _unknownRelicPatternMap.Clear();
            
            // Pre-populate common patterns that we might encounter
            foreach (var mapping in _cachedMappings)
            {
                var unknownPatternWithParens = $"Unknown Relic ({mapping.EffectId})";
                var unknownPatternWithoutParens = $"Unknown Relic {mapping.EffectId}";
                
                _unknownRelicPatternMap[unknownPatternWithParens] = mapping.DisplayName;
                _unknownRelicPatternMap[unknownPatternWithoutParens] = mapping.DisplayName;
            }
        }
    }
}

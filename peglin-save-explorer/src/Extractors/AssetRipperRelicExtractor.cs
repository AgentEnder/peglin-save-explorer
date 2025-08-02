using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.IO;
using AssetRipper.Assets.Metadata;
using AssetRipper.Import.AssetCreation;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.SerializedFiles.Parser;
using AssetRipper.SourceGenerated;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using Newtonsoft.Json;
using peglin_save_explorer.UI;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Extractors
{
    public class AssetRipperRelicExtractor
    {
        private readonly Dictionary<string, RelicData> _relicCache = new();
        private readonly ConsoleSession _session;

        public AssetRipperRelicExtractor(ConsoleSession session)
        {
            _session = session;
        }

        public class RelicData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Effect { get; set; }
            public int Rarity { get; set; }
            public Dictionary<string, object> RawData { get; set; }
        }

        public async Task<Dictionary<string, RelicData>> ExtractRelicsAsync(string bundlePath)
        {
            return await Task.Run(() => ExtractRelics(bundlePath));
        }

        /// <summary>
        /// Extracts all relics from a Peglin installation directory
        /// </summary>
        /// <param name="peglinPath">Path to Peglin installation directory</param>
        /// <returns>Dictionary of all extracted relic data</returns>
        public Dictionary<string, RelicData> ExtractAllRelicsFromPeglinInstall(string peglinPath)
        {
            var allRelics = new Dictionary<string, RelicData>();
            
            try
            {
                var bundleDirectory = PeglinPathHelper.GetStreamingAssetsBundlePath(peglinPath);
                if (string.IsNullOrEmpty(bundleDirectory) || !Directory.Exists(bundleDirectory))
                {
                    Console.WriteLine($"[AssetRipper] Bundle directory not found for: {peglinPath}");
                    Console.WriteLine("[AssetRipper] Checked for platform-specific streaming assets directories");
                    return allRelics;
                }

                Console.WriteLine($"[AssetRipper] Extracting relics from Peglin installation: {peglinPath}");
                
                var bundleFiles = Directory.GetFiles(bundleDirectory, "*.bundle", SearchOption.AllDirectories);
                Console.WriteLine($"[AssetRipper] Found {bundleFiles.Length} bundle files");
                
                foreach (var bundleFile in bundleFiles)
                {
                    try
                    {
                        // Clear the cache for each bundle to avoid conflicts
                        _relicCache.Clear();
                        
                        var relics = ExtractRelics(bundleFile);
                        foreach (var kvp in relics)
                        {
                            // Use the relic ID as key to avoid duplicates
                            allRelics[kvp.Key] = kvp.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AssetRipper] Failed to process bundle {Path.GetFileName(bundleFile)}: {ex.Message}");
                    }
                }

                Console.WriteLine($"[AssetRipper] Total relics extracted: {allRelics.Count}");
                return allRelics;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetRipper] Error extracting relics from Peglin install: {ex.Message}");
                return allRelics;
            }
        }

        public Dictionary<string, RelicData> ExtractRelics(string bundlePath)
        {
            try
            {
                Console.WriteLine($"[AssetRipper] Loading bundle from: {bundlePath}");
                
                // Create a simple assembly manager (we won't need full script compilation)
                var assemblyManager = new BaseManager(s => { });
                var assetFactory = new GameAssetFactory(assemblyManager);
                
                // Load the bundle
                var gameBundle = GameBundle.FromPaths(new[] { bundlePath }, assetFactory);
                
                Console.WriteLine($"[AssetRipper] Loaded {gameBundle.FetchAssetCollections().Count()} collections");

                // Find and extract MonoBehaviours
                foreach (var collection in gameBundle.FetchAssetCollections())
                {
                    foreach (var asset in collection.Assets)
                    {
                        if (asset.Value is IMonoBehaviour monoBehaviour)
                        {
                            ProcessMonoBehaviour(monoBehaviour);
                        }
                    }
                }

                Console.WriteLine($"[AssetRipper] Extracted {_relicCache.Count} relics");
                return _relicCache;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetRipper] Error extracting relics: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return _relicCache;
            }
        }

        private void ProcessMonoBehaviour(IMonoBehaviour monoBehaviour)
        {
            try
            {
                // Get the structure data
                var structure = monoBehaviour.LoadStructure();
                if (structure == null) return;

                // Convert to dictionary for easier processing
                var data = ConvertStructureToDict(structure);
                
                // Check if this looks like a relic
                if (IsRelicData(data))
                {
                    var relic = ExtractRelicFromData(monoBehaviour.Name, data);
                    if (relic != null && !string.IsNullOrEmpty(relic.Id))
                    {
                        _relicCache[relic.Id] = relic;
                        Console.WriteLine($"[AssetRipper] Found relic: {relic.Id} - {relic.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetRipper] Error processing MonoBehaviour: {ex.Message}");
            }
        }

        private Dictionary<string, object> ConvertStructureToDict(SerializableStructure structure)
        {
            var result = new Dictionary<string, object>();

            foreach (var field in structure.Type.Fields)
            {
                try
                {
                    if (structure.TryGetField(field.Name, out var value))
                    {
                        result[field.Name] = ConvertSerializableValue(value, field);
                    }
                }
                catch
                {
                    // Skip fields that fail to convert
                }
            }

            return result;
        }

        private object ConvertSerializableValue(SerializableValue value, dynamic field)
        {
            try
            {
                // Try to get string value first (most common for relics)
                if (!string.IsNullOrEmpty(value.AsString))
                {
                    return value.AsString;
                }
                
                // Try numeric values
                if (value.PValue != 0)
                {
                    // Could be int, float, or bool
                    return value.AsInt32;
                }
                
                // Handle complex types
                if (value.CValue != null)
                {
                    if (value.CValue is SerializableStructure subStructure)
                    {
                        return ConvertStructureToDict(subStructure);
                    }
                    else if (value.CValue is IUnityAssetBase asset)
                    {
                        return asset.ToString();
                    }
                    else
                    {
                        return ConvertValue(value.CValue);
                    }
                }
            }
            catch
            {
                // If conversion fails, return a placeholder
            }
            
            // Default to the field name if we can't extract the value
            return field?.Name ?? "unknown";
        }

        private object ConvertValue(object value)
        {
            if (value == null) return null;

            // Handle basic types
            if (value is string || value is bool || value is int || value is float || value is double)
            {
                return value;
            }

            // Handle Unity types
            if (value is IUnityAssetBase asset)
            {
                // Extract relevant data from Unity asset types
                return asset.ToString();
            }

            // Handle collections
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(ConvertValue(item));
                }
                return list;
            }

            // Default to string representation
            return value.ToString();
        }

        private bool IsRelicData(Dictionary<string, object> data)
        {
            // Check for relic-like fields
            var relicFields = new[] { "locKey", "englishDisplayName", "effect", "globalRarity", "sprite" };
            var matchCount = relicFields.Count(field => data.ContainsKey(field));
            
            // Also check for specific patterns in field names or values
            if (data.ContainsKey("englishDescription") && data["englishDescription"] is string desc)
            {
                if (desc.Contains("relic", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("attack", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("damage", StringComparison.OrdinalIgnoreCase))
                {
                    matchCount++;
                }
            }

            return matchCount >= 3;
        }

        private RelicData ExtractRelicFromData(string assetName, Dictionary<string, object> data)
        {
            try
            {
                var relic = new RelicData
                {
                    RawData = data,
                    Id = assetName ?? "unknown"
                };

                // Extract known fields
                if (data.TryGetValue("englishDisplayName", out var name))
                {
                    relic.Name = name?.ToString() ?? assetName;
                }
                else if (data.TryGetValue("locKey", out var locKey))
                {
                    relic.Name = locKey?.ToString() ?? assetName;
                }

                if (data.TryGetValue("englishDescription", out var desc))
                {
                    relic.Description = desc?.ToString() ?? "";
                }

                if (data.TryGetValue("effect", out var effect))
                {
                    relic.Effect = effect?.ToString() ?? "";
                }

                if (data.TryGetValue("globalRarity", out var rarity) && int.TryParse(rarity?.ToString(), out var rarityInt))
                {
                    relic.Rarity = rarityInt;
                }

                // Clean up the ID
                relic.Id = CleanRelicId(relic.Id);

                return relic;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetRipper] Error extracting relic data: {ex.Message}");
                return null;
            }
        }

        private string CleanRelicId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "unknown";
            
            // Remove common prefixes/suffixes
            id = id.Replace("Relic_", "").Replace("_Relic", "").Replace(".asset", "");
            
            // Convert to consistent format
            id = id.ToLowerInvariant().Replace(" ", "_");
            
            return id;
        }

        public void SaveRelicCache(string filePath)
        {
            try
            {
                var json = JsonConvert.SerializeObject(_relicCache, Formatting.Indented);
                File.WriteAllText(filePath, json);
                Console.WriteLine($"[AssetRipper] Saved {_relicCache.Count} relics to cache");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetRipper] Error saving relic cache: {ex.Message}");
            }
        }

        public void LoadRelicCache(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var loaded = JsonConvert.DeserializeObject<Dictionary<string, RelicData>>(json);
                    if (loaded != null)
                    {
                        _relicCache.Clear();
                        foreach (var kvp in loaded)
                        {
                            _relicCache[kvp.Key] = kvp.Value;
                        }
                        Console.WriteLine($"[AssetRipper] Loaded {_relicCache.Count} relics from cache");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetRipper] Error loading relic cache: {ex.Message}");
            }
        }
    }

}
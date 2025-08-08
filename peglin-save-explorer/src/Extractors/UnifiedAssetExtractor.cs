using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.IO;
using AssetRipper.Assets.Metadata;
using AssetRipper.Import.AssetCreation;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.SerializedFiles.Parser;
using AssetRipper.SourceGenerated;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.Export.Modules.Textures;
using peglin_save_explorer.Data;
using peglin_save_explorer.UI;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Core;

namespace peglin_save_explorer.Extractors
{
    /// <summary>
    /// Unified extractor that processes all assets in a single pass while maintaining AssetRipper context
    /// This ensures proper PPtr resolution for accurate entity-sprite correlation
    /// </summary>
    public class UnifiedAssetExtractor
    {
        public class GameObjectData
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public long PathID { get; set; }
            public List<ComponentData> Components { get; set; } = new();
            public Dictionary<string, object> RawData { get; set; } = new();
        }

        public class ComponentData
        {
            public string Type { get; set; } = "";
            public string Name { get; set; } = "";
            public long PathID { get; set; }
            public Dictionary<string, object> Properties { get; set; } = new();
        }

        public class UnifiedExtractionResult
        {
            public Dictionary<string, RelicData> Relics { get; set; } = new();
            public Dictionary<string, EnemyData> Enemies { get; set; } = new();
            public Dictionary<string, OrbData> Orbs { get; set; } = new();
            public Dictionary<string, OrbGroupedData> OrbFamilies { get; set; } = new();
            public Dictionary<string, SpriteCacheManager.SpriteMetadata> Sprites { get; set; } = new();
            public Dictionary<string, string> RelicSpriteCorrelations { get; set; } = new();
            public Dictionary<string, string> EnemySpriteCorrelations { get; set; } = new();
            public Dictionary<string, string> OrbSpriteCorrelations { get; set; } = new();
        }

        private readonly ConsoleSession? _session;
        private readonly EnumExtractor _enumExtractor = new();
        private string? _currentPeglinPath;

        public UnifiedAssetExtractor(ConsoleSession? session = null)
        {
            _session = session;
        }

        /// <summary>
        /// Extracts all assets from a Peglin installation in a single pass with full cross-bundle reference resolution
        /// </summary>
        public UnifiedExtractionResult ExtractAllAssetsFromPeglinInstall(string peglinPath, IProgress<string>? progress = null)
        {
            _currentPeglinPath = peglinPath;
            var result = new UnifiedExtractionResult();

            try
            {
                // Load the assembly for enum reflection
                _enumExtractor.LoadAssembly(peglinPath);
                var bundleDirectory = PeglinPathHelper.GetStreamingAssetsBundlePath(peglinPath);
                if (string.IsNullOrEmpty(bundleDirectory) || !Directory.Exists(bundleDirectory))
                {
                    Logger.Error($"Bundle directory not found for: {peglinPath}");
                    return result;
                }

                progress?.Report($"🔍 Scanning bundle directory: {bundleDirectory}");
                var bundleFiles = Directory.GetFiles(bundleDirectory, "*.bundle", SearchOption.AllDirectories);
                var assetFiles = Directory.GetFiles(bundleDirectory, "*.assets", SearchOption.AllDirectories);
                var allFiles = bundleFiles.Concat(assetFiles).ToArray();
                progress?.Report($"📦 Found {bundleFiles.Length} bundle files and {assetFiles.Length} assets files to process ({allFiles.Length} total)");

                // Load ALL bundles at once to enable cross-bundle reference resolution
                progress?.Report($"🚀 Loading all bundles with dependency resolution...");
                var gameBundle = ProcessAllBundlesWithDependencies(allFiles, result, progress);

                // Consolidate orbs into families after extraction
                ConsolidateOrbsIntoFamilies(result, progress);

                var relicSprites = result.Sprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Relic);
                var enemySprites = result.Sprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Enemy);
                var orbSprites = result.Sprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Orb);
                
                progress?.Report($"✅ Extracted {result.Relics.Count} relics, {result.Enemies.Count} enemies, {result.Orbs.Count} orbs ({result.OrbFamilies.Count} families), {result.Sprites.Count} sprites");
                progress?.Report($"   📊 Sprite breakdown: {relicSprites} relic sprites, {enemySprites} enemy sprites, {orbSprites} orb sprites");
                progress?.Report($"🔗 Correlated {result.RelicSpriteCorrelations.Count} relics, {result.EnemySpriteCorrelations.Count} enemies, {result.OrbSpriteCorrelations.Count} orbs with sprites");

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during unified extraction: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Processes ALL bundle files at once to enable cross-bundle reference resolution
        /// </summary>
        private GameBundle ProcessAllBundlesWithDependencies(string[] bundlePaths, UnifiedExtractionResult result, IProgress<string>? progress = null)
        {
            try
            {
                // Create assembly manager for asset creation
                var assemblyManager = new BaseManager(s => { });
                var assetFactory = new GameAssetFactory(assemblyManager);

                // Load ALL bundles at once - this is crucial for cross-bundle PPtr resolution
                // GameBundle.FromPaths automatically initializes dependency lists for cross-bundle references
                progress?.Report($"   🔄 Loading {bundlePaths.Length} bundles with full dependency resolution...");
                var gameBundle = GameBundle.FromPaths(bundlePaths, assetFactory);

                progress?.Report($"   📊 Processing {gameBundle.FetchAssetCollections().Count()} asset collections...");

                // Now process all collections with full cross-bundle context available
                var collections = gameBundle.FetchAssetCollections().ToList();
                int processedCollections = 0;

                foreach (var collection in collections)
                {
                    processedCollections++;
                    progress?.Report($"   📁 Processing collection {processedCollections}/{collections.Count}: {collection.Name}");
                    
                    try
                    {
                        ProcessCollectionWithCrossReferences(collection, gameBundle, result);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Error processing collection {collection.Name}: {ex.Message}");
                    }
                }

                return gameBundle;
            }
            catch (ArgumentException ex) when (ex.Message.Contains("No SerializedFile found"))
            {
                Logger.Warning("Some bundles were not valid Unity bundles and were skipped");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing bundles with dependencies: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Processes an asset collection with full cross-bundle reference resolution
        /// </summary>
        private void ProcessCollectionWithCrossReferences(AssetCollection collection, GameBundle gameBundle, UnifiedExtractionResult result)
        {
            try
            {
                // Step 1: Build sprite information lookup tables from SpriteInformationObject assets
                var spriteInfoLookup = BuildSpriteInformationLookup(collection, gameBundle);
                Logger.Debug($"🗺️ Built sprite information lookup with {spriteInfoLookup.Count} mappings");
                
                // Step 2: Extract entity data (relics, enemies, orbs) and collect sprite references
                var relicSpriteRefs = new Dictionary<string, IUnityAssetBase>(); // relic ID -> sprite asset reference
                var enemySpriteRefs = new Dictionary<string, IUnityAssetBase>(); // enemy ID -> sprite asset reference
                var orbSpriteRefs = new Dictionary<string, IUnityAssetBase>(); // orb ID -> sprite asset reference

                // Create a map of component PathID to component for GameObject processing
                var componentMap = new Dictionary<long, IMonoBehaviour>();
                
                // First pass: collect all MonoBehaviour components and process them
                foreach (var asset in collection.Assets)
                {
                    if (asset.Value is IMonoBehaviour monoBehaviour)
                    {
                        componentMap[monoBehaviour.PathID] = monoBehaviour;
                        ProcessMonoBehaviour(monoBehaviour, collection, gameBundle, result, relicSpriteRefs, enemySpriteRefs, orbSpriteRefs);
                    }
                }

                // Second pass: process GameObjects to find orb entities
                var gameObjectCount = 0;
                var processedGameObjects = 0;
                foreach (var asset in collection.Assets)
                {
                    if (asset.Value is IGameObject gameObject)
                    {
                        gameObjectCount++;
                        Logger.Debug($"🎮 Processing GameObject: {gameObject.Name} (PathID: {gameObject.PathID})");
                        ProcessGameObjectForOrbs(gameObject, componentMap, result, orbSpriteRefs, collection);
                        processedGameObjects++;
                    }
                }
                Logger.Info($"🎮 Processed {processedGameObjects} GameObjects out of {gameObjectCount} found in collection");

                // Step 2: Extract sprites and textures
                var processedSprites = new HashSet<long>(); // Track processed PathIDs to avoid duplicates

                // Process Sprite assets
                foreach (var sprite in collection.OfType<ISprite>())
                {
                    if (processedSprites.Contains(sprite.PathID))
                        continue;

                    var spriteMetadata = ExtractSpriteWithImprovedProcessing(sprite);
                    if (spriteMetadata != null)
                    {
                        result.Sprites[spriteMetadata.Id] = spriteMetadata;
                        processedSprites.Add(sprite.PathID);
                        Logger.Verbose($"✅ Processed ISprite: {sprite.Name} -> {spriteMetadata.Id}");
                    }
                }

                // Process ALL other assets to find sprite-related types we might be missing
                Logger.Verbose($"🔍 Scanning {collection.Assets.Count} total assets for sprite-related types...");
                var assetTypeCounts = new Dictionary<string, int>();
                
                foreach (var asset in collection.Assets)
                {
                    var assetType = asset.Value?.GetType()?.Name ?? "Unknown";
                    if (!assetTypeCounts.ContainsKey(assetType))
                        assetTypeCounts[assetType] = 0;
                    assetTypeCounts[assetType]++;
                    
                    // Look for potential sprite-related assets we're not processing
                    if (asset.Value != null && !processedSprites.Contains(asset.Key))
                    {
                        var assetName = asset.Value.ToString() ?? "";
                        
                        // Check for sprite-like asset types or names
                        if (assetType.Contains("Sprite", StringComparison.OrdinalIgnoreCase) ||
                            assetName.Contains("sprite", StringComparison.OrdinalIgnoreCase) ||
                            assetName.Contains("relic", StringComparison.OrdinalIgnoreCase) ||
                            assetName.Contains("orb", StringComparison.OrdinalIgnoreCase) ||
                            (assetType == "Texture2D" && (assetName.Contains("relic", StringComparison.OrdinalIgnoreCase) ||
                                                          assetName.Contains("orb", StringComparison.OrdinalIgnoreCase))))
                        {
                            Logger.Debug($"🎯 Found potential sprite asset: {assetType} - {assetName} (PathID: {asset.Key})");
                            
                            // Try to process as sprite-related asset
                            var collectionName = collection.Name ?? "unknown";
                            var potentialSpriteMetadata = ProcessPotentialSpriteAsset(asset.Value, collectionName);
                            if (potentialSpriteMetadata != null)
                            {
                                if (!result.Sprites.ContainsKey(potentialSpriteMetadata.Id)) // Avoid duplicates
                                {
                                    result.Sprites[potentialSpriteMetadata.Id] = potentialSpriteMetadata;
                                    processedSprites.Add(asset.Key);
                                    Logger.Debug($"✅ Successfully processed potential sprite: {potentialSpriteMetadata.Name}");
                                }
                            }
                        }
                    }
                }
                
                // Log asset type summary for debugging
                var topAssetTypes = assetTypeCounts.OrderByDescending(kvp => kvp.Value).Take(10);
                Logger.Debug($"📊 Top asset types in collection: {string.Join(", ", topAssetTypes.Select(kvp => $"{kvp.Key}({kvp.Value})"))}");
                Logger.Debug($"🎨 Found {collection.OfType<ISprite>().Count()} ISprite assets in collection");

                // Process Texture2D assets (standalone textures not referenced by sprites)
                foreach (var texture in collection.OfType<ITexture2D>())
                {
                    if (processedSprites.Contains(texture.PathID))
                        continue;

                    var spriteMetadata = ExtractTexture(texture);
                    if (spriteMetadata != null)
                    {
                        result.Sprites[spriteMetadata.Id] = spriteMetadata;
                        processedSprites.Add(texture.PathID);
                    }
                }

                // Step 3: Resolve sprite references and build correlations
                // This is the key improvement - we resolve references while AssetRipper context is still active
                foreach (var kvp in relicSpriteRefs)
                {
                    var relicId = kvp.Key;
                    var spriteRef = kvp.Value;

                    // Try to resolve the sprite reference with cross-bundle support and sprite info lookup
                    var resolvedSprite = ResolveSpriteWithLookup(spriteRef, gameBundle, spriteInfoLookup, SpriteCacheManager.SpriteType.Relic);
                    if (resolvedSprite != null)
                    {
                        
                        // Always update/add the sprite with the correct type (overwrite if it exists with wrong type)
                        result.Sprites[resolvedSprite.Id] = resolvedSprite;

                        // Create correlation
                        result.RelicSpriteCorrelations[relicId] = resolvedSprite.Id;

                        // Update the relic data with correlation info
                        if (result.Relics.TryGetValue(relicId, out var relic))
                        {
                            relic.CorrelatedSpriteId = resolvedSprite.Id;
                            relic.SpriteFilePath = resolvedSprite.FilePath;
                            relic.CorrelationConfidence = 1.0f; // Direct resolution = 100% confidence
                            relic.CorrelationMethod = "Direct PPtr Resolution";
                        }

                        Logger.Verbose($"✅ Successfully correlated relic {relicId} with sprite {resolvedSprite.Id} -> {resolvedSprite.FilePath}");
                    }
                    else
                    {
                        // Sprite resolution failed - dump the asset to understand its structure
                        var relicName = result.Relics.TryGetValue(relicId, out var relicData) ? relicData.Name : "Unknown";
                        Logger.Warning($"❌ Failed to resolve sprite for relic {relicId} ({relicName}) - sprite reference type: {spriteRef.GetType().Name}");
                        if (spriteRef is IUnityObjectBase objBase)
                        {
                            DumpAssetPropertiesForAnalysis(objBase, $"FailedRelic_{relicId}_{spriteRef.GetType().Name}");
                        }
                        else
                        {
                            Logger.Warning($"❌ Cannot dump asset properties - spriteRef is {spriteRef.GetType().Name}, not IUnityObjectBase");
                        }
                    }
                }

                foreach (var kvp in enemySpriteRefs)
                {
                    var enemyId = kvp.Key;
                    var spriteRef = kvp.Value;

                    var resolvedSprite = ResolveSpriteWithLookup(spriteRef, gameBundle, spriteInfoLookup, SpriteCacheManager.SpriteType.Enemy);
                    if (resolvedSprite != null)
                    {
                        
                        // Always update/add the sprite with the correct type (overwrite if it exists with wrong type)
                        result.Sprites[resolvedSprite.Id] = resolvedSprite;

                        result.EnemySpriteCorrelations[enemyId] = resolvedSprite.Id;

                        if (result.Enemies.TryGetValue(enemyId, out var enemy))
                        {
                            enemy.CorrelatedSpriteId = resolvedSprite.Id;
                            enemy.SpriteFilePath = resolvedSprite.FilePath;
                            enemy.CorrelationConfidence = 1.0f;
                            enemy.CorrelationMethod = "Direct PPtr Resolution";
                        }

                        Logger.Debug($"Correlated enemy {enemyId} with sprite {resolvedSprite.Id}");
                    }
                }

                foreach (var kvp in orbSpriteRefs)
                {
                    var orbId = kvp.Key;
                    var spriteRef = kvp.Value;

                    var resolvedSprite = ResolveSpriteWithLookup(spriteRef, gameBundle, spriteInfoLookup, SpriteCacheManager.SpriteType.Orb);
                    if (resolvedSprite != null)
                    {
                        
                        // Always update/add the sprite with the correct type (overwrite if it exists with wrong type)  
                        result.Sprites[resolvedSprite.Id] = resolvedSprite;

                        result.OrbSpriteCorrelations[orbId] = resolvedSprite.Id;

                        if (result.Orbs.TryGetValue(orbId, out var orb))
                        {
                            orb.CorrelatedSpriteId = resolvedSprite.Id;
                            orb.SpriteFilePath = resolvedSprite.FilePath;
                            orb.CorrelationConfidence = 1.0f;
                            orb.CorrelationMethod = "Direct PPtr Resolution";
                        }

                        Logger.Debug($"Correlated orb {orbId} with sprite {resolvedSprite.Id}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing collection: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a MonoBehaviour to extract entity data and sprite references
        /// </summary>
        private void ProcessMonoBehaviour(
            IMonoBehaviour monoBehaviour,
            AssetCollection collection,
            GameBundle gameBundle,
            UnifiedExtractionResult result,
            Dictionary<string, IUnityAssetBase> relicSpriteRefs,
            Dictionary<string, IUnityAssetBase> enemySpriteRefs,
            Dictionary<string, IUnityAssetBase> orbSpriteRefs)
        {
            try
            {
                var structure = monoBehaviour.LoadStructure();
                if (structure == null) return;

                var data = ConvertStructureToDict(structure, collection, out var spriteReference);

                // Determine entity type and extract accordingly
                if (IsRelicData(data))
                {
                    var relic = ExtractRelic(monoBehaviour.Name, data);
                    if (relic != null && !string.IsNullOrEmpty(relic.Id))
                    {
                        result.Relics[relic.Id] = relic;

                        // Store sprite reference if found
                        if (spriteReference != null)
                        {
                            relicSpriteRefs[relic.Id] = spriteReference;
                            Logger.Debug($"🔮 Found sprite reference for relic {relic.Id}: {spriteReference.GetType().Name}");
                        }
                        else
                        {
                            Logger.Debug($"🔮 No sprite reference found for relic {relic.Id}");
                        }

                        Logger.Debug($"Found relic: {relic.Id} - {relic.Name}");
                    }
                }
                else if (IsEnemyData(data))
                {
                    var enemy = ExtractEnemy(monoBehaviour.Name, data);
                    if (enemy != null && !string.IsNullOrEmpty(enemy.Id))
                    {
                        result.Enemies[enemy.Id] = enemy;

                        if (spriteReference != null)
                        {
                            enemySpriteRefs[enemy.Id] = spriteReference;
                            Logger.Debug($"👹 Found sprite reference for enemy {enemy.Id}: {spriteReference.GetType().Name}");
                        }
                        else
                        {
                            Logger.Debug($"👹 No sprite reference found for enemy {enemy.Id}");
                        }

                        Logger.Debug($"Found enemy: {enemy.Id} - {enemy.Name}");
                    }
                }
                // Skip direct MonoBehaviour orb extraction - only extract orbs from GameObjects with OrbComponent data
                // This ensures we only get orbs with proper component structure and prefab sprite references
                else if (IsOrbData(data))
                {
                    Logger.Debug($"🔍 Skipping MonoBehaviour-only orb extraction for {monoBehaviour.Name} - will extract from GameObject if present");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error processing MonoBehaviour: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts a SerializableStructure to a dictionary and extracts sprite references
        /// </summary>
        private Dictionary<string, object> ConvertStructureToDict(
            SerializableStructure structure,
            AssetCollection collection,
            out IUnityAssetBase? spriteReference)
        {
            var result = new Dictionary<string, object>();
            spriteReference = null;

            foreach (var field in structure.Type.Fields)
            {
                try
                {
                    if (structure.TryGetField(field.Name, out var value))
                    {
                        var fieldName = field.Name.ToLowerInvariant();
                        var convertedValue = ConvertSerializableValue(value, field, collection);

                        result[field.Name] = convertedValue;

                        // Debug: Log all field names with asset references to understand data structure
                        if (value.CValue is IUnityAssetBase debugAsset)
                        {
                            Logger.Debug($"🔍 Field '{field.Name}' has asset reference: {debugAsset.GetType().Name}");
                            
                            // For orbs, prioritize the main "sprite" field over status effect icons
                            if (spriteReference == null)
                            {
                                if (field.Name.Equals("sprite", StringComparison.OrdinalIgnoreCase))
                                {
                                    spriteReference = debugAsset;
                                    Logger.Debug($"🎯 Found main sprite field '{field.Name}' with asset type: {debugAsset.GetType().Name}");
                                }
                                else if (IsSpriteField(fieldName))
                                {
                                    spriteReference = debugAsset;
                                    Logger.Debug($"🎯 Found sprite field '{field.Name}' with asset type: {debugAsset.GetType().Name}");
                                }
                                else if (CouldBeSpriteField(field.Name))
                                {
                                    Logger.Debug($"🤔 Field '{field.Name}' might be sprite-related, investigating...");
                                    spriteReference = debugAsset;
                                }
                            }
                            // If we already found a sprite reference but this is the main "sprite" field, override it
                            else if (field.Name.Equals("sprite", StringComparison.OrdinalIgnoreCase))
                            {
                                Logger.Debug($"🔄 Overriding with main sprite field '{field.Name}' (was using other sprite field)");
                                spriteReference = debugAsset;
                            }
                        }
                        // Also check for GUID-based asset references (Unity Addressables)
                        else if (spriteReference == null && CouldBeSpriteField(field.Name) && IsGuidAssetReference(convertedValue))
                        {
                            Logger.Debug($"🎯 Found GUID-based sprite reference in field '{field.Name}': {convertedValue}");
                            // For GUID references, we can't resolve them immediately but we can note that this entity has a sprite reference
                            // We'll handle GUID resolution differently since AssetRipper might not be able to resolve these cross-bundle
                        }
                    }
                }
                catch
                {
                    // Skip fields that fail to convert
                }
            }

            return result;
        }

        /// <summary>
        /// Converts a SerializableValue to a usable object
        /// </summary>
        private object ConvertSerializableValue(SerializableValue value, dynamic field, AssetCollection? collection = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(value.AsString))
                {
                    return value.AsString;
                }

                if (value.PValue != 0)
                {
                    return value.AsInt32;
                }

                if (value.CValue != null)
                {
                    if (value.CValue is SerializableStructure subStructure)
                    {
                        return ConvertStructureToDict(subStructure, collection, out _);
                    }
                    else if (value.CValue is IUnityAssetBase asset)
                    {
                        // For asset references, store basic info
                        return new Dictionary<string, object>
                        {
                            ["type"] = asset.GetType().Name,
                            ["pathId"] = 0, // PathID not directly accessible from IUnityAssetBase
                            ["name"] = asset.ToString() ?? "unknown"
                        };
                    }
                    else if (value.CValue is IList<SerializableValue> list)
                    {
                        var convertedList = new List<object>();
                        foreach (var item in list)
                        {
                            convertedList.Add(ConvertSerializableValue(item, field, collection));
                        }
                        return convertedList;
                    }
                }

                return value.ToString();
            }
            catch
            {
                return value.ToString();
            }
        }

        /// <summary>
        /// Resolves a sprite reference to actual sprite metadata using cross-bundle support
        /// </summary>
        private SpriteCacheManager.SpriteMetadata? ResolveSprite(IUnityAssetBase spriteRef, GameBundle gameBundle)
        {
            try
            {
                // Check for directly resolved sprite/texture references
                if (spriteRef is ISprite sprite)
                {
                    Logger.Debug($"✅ Direct sprite reference resolved: {sprite.Name}");
                    return ExtractSpriteWithImprovedProcessing(sprite);
                }

                if (spriteRef is ITexture2D texture)
                {
                    Logger.Debug($"✅ Direct texture reference resolved: {texture.Name}");
                    return ExtractTextureWithImprovedProcessing(texture);
                }

                // Handle AssetRipper-generated PPtr types (like PPtr_Object_5)
                // These types have a TryGetAsset method that resolves the actual referenced object
                var ptrType = spriteRef.GetType();
                var tryGetAssetMethod = ptrType.GetMethod("TryGetAsset");
                
                if (tryGetAssetMethod != null)
                {
                    try
                    {
                        // Call TryGetAsset to resolve the PPtr reference  
                        // Method signature: bool TryGetAsset(AssetCollection collection, out T? asset)
                        // We need to find the right collection - try all collections in the gameBundle
                        foreach (var candidateCollection in gameBundle.FetchAssetCollections())
                        {
                            var parameters = new object?[] { candidateCollection, null };
                            var collectionResult = tryGetAssetMethod.Invoke(spriteRef, parameters);
                            
                            if (collectionResult is bool collectionSuccess && collectionSuccess && parameters[1] is IUnityObjectBase resolvedAsset)
                            {
                                if (resolvedAsset is ISprite resolvedSprite)
                                {
                                    Logger.Debug($"✅ PPtr resolved to sprite: {resolvedSprite.Name}");
                                    return ExtractSpriteWithImprovedProcessing(resolvedSprite);
                                }
                                else if (resolvedAsset is ITexture2D resolvedTexture)
                                {
                                    Logger.Debug($"✅ PPtr resolved to texture: {resolvedTexture.Name}");
                                    return ExtractTextureWithImprovedProcessing(resolvedTexture);
                                }
                                else
                                {
                                    Logger.Debug($"⚠️ PPtr resolved to unexpected type: {resolvedAsset.GetType().Name}");
                                    // Dump full properties of this asset to understand its structure
                                    DumpAssetPropertiesForAnalysis(resolvedAsset, $"PPtr_Resolved_{resolvedAsset.GetType().Name}");
                                }
                                
                                // Successfully resolved, break out of collection loop
                                break;
                            }
                        }
                        
                        Logger.Debug($"❌ PPtr resolution failed for {ptrType.Name} across all collections");
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"❌ Error resolving PPtr {ptrType.Name}: {ex.Message}");
                    }
                }
                else
                {
                    Logger.Debug($"❌ Unrecognized sprite reference type: {ptrType.Name}");
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error resolving sprite reference: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resolves sprite references with forced sprite type (overrides automatic type detection)
        /// </summary>
        private SpriteCacheManager.SpriteMetadata? ResolveSprite(IUnityAssetBase spriteRef, GameBundle gameBundle, SpriteCacheManager.SpriteType forcedSpriteType)
        {
            try
            {
                // Check for directly resolved sprite/texture references
                if (spriteRef is ISprite sprite)
                {
                    Logger.Debug($"✅ Direct sprite reference resolved: {sprite.Name} (forced type: {forcedSpriteType})");
                    return ExtractSpriteWithImprovedProcessing(sprite, forcedSpriteType);
                }

                if (spriteRef is ITexture2D texture)
                {
                    Logger.Debug($"✅ Direct texture reference resolved: {texture.Name} (forced type: {forcedSpriteType})");
                    return ExtractTextureWithImprovedProcessing(texture, forcedSpriteType);
                }

                // Handle AssetRipper-generated PPtr types (like PPtr_Object_5)
                // These types have a TryGetAsset method that resolves the actual referenced object
                var ptrType = spriteRef.GetType();
                var tryGetAssetMethod = ptrType.GetMethod("TryGetAsset");
                
                if (tryGetAssetMethod != null)
                {
                    try
                    {
                        // Call TryGetAsset to resolve the PPtr reference  
                        // Method signature: bool TryGetAsset(AssetCollection collection, out T? asset)
                        // We need to find the right collection - try all collections in the gameBundle
                        foreach (var candidateCollection in gameBundle.FetchAssetCollections())
                        {
                            var parameters = new object?[] { candidateCollection, null };
                            var collectionResult = tryGetAssetMethod.Invoke(spriteRef, parameters);
                            
                            if (collectionResult is bool collectionSuccess && collectionSuccess && parameters[1] is IUnityObjectBase resolvedAsset)
                            {
                                if (resolvedAsset is ISprite resolvedSprite)
                                {
                                    Logger.Debug($"✅ PPtr resolved to sprite: {resolvedSprite.Name} (forced type: {forcedSpriteType})");
                                    return ExtractSpriteWithImprovedProcessing(resolvedSprite, forcedSpriteType);
                                }
                                else if (resolvedAsset is ITexture2D resolvedTexture)
                                {
                                    Logger.Debug($"✅ PPtr resolved to texture: {resolvedTexture.Name} (forced type: {forcedSpriteType})");
                                    return ExtractTextureWithImprovedProcessing(resolvedTexture, forcedSpriteType);
                                }
                                else
                                {
                                    Logger.Debug($"⚠️ PPtr resolved to unexpected type: {resolvedAsset.GetType().Name} (forced type: {forcedSpriteType})");
                                    // Dump full properties of this asset to understand its structure
                                    DumpAssetPropertiesForAnalysis(resolvedAsset, $"PPtr_Resolved_{resolvedAsset.GetType().Name}");
                                }
                                
                                // Successfully resolved, break out of collection loop
                                break;
                            }
                        }
                        
                        Logger.Debug($"❌ PPtr resolution failed for {ptrType.Name} across all collections (forced type: {forcedSpriteType})");
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"❌ Error resolving PPtr {ptrType.Name}: {ex.Message} (forced type: {forcedSpriteType})");
                    }
                }
                else
                {
                    Logger.Debug($"❌ Unrecognized sprite reference type: {ptrType.Name} (forced type: {forcedSpriteType})");
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error resolving sprite reference: {ex.Message} (forced type: {forcedSpriteType})");
                return null;
            }
        }

        /// <summary>
        /// Extracts sprite metadata from a Sprite asset
        /// </summary>
        private SpriteCacheManager.SpriteMetadata? ExtractSprite(ISprite sprite)
        {
            try
            {
                var texture = sprite.TryGetTexture();
                if (texture == null) return null;

                var spriteId = GenerateSpriteId(sprite.Name ?? texture.Name);
                var spriteType = DetermineSpriteType(sprite.Name);

                var metadata = new SpriteCacheManager.SpriteMetadata
                {
                    Id = spriteId,
                    Name = sprite.Name ?? texture.Name,
                    Type = spriteType,
                    Width = (int)sprite.RD.TextureRect.Width,
                    Height = (int)sprite.RD.TextureRect.Height,
                    FilePath = GetSpriteFilePath(spriteId, spriteType),
                    SourceBundle = ""
                };

                // Extract and save sprite image
                SaveSpriteImage(sprite, metadata.FilePath);

                return metadata;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error extracting sprite {sprite.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts sprite metadata from a Texture2D asset
        /// </summary>
        private SpriteCacheManager.SpriteMetadata? ExtractTexture(ITexture2D texture)
        {
            try
            {
                var spriteId = GenerateSpriteId(texture.Name);
                var spriteType = DetermineSpriteType(texture.Name);

                var metadata = new SpriteCacheManager.SpriteMetadata
                {
                    Id = spriteId,
                    Name = texture.Name,
                    Type = spriteType,
                    Width = texture.Width_C28,
                    Height = texture.Height_C28,
                    FilePath = GetSpriteFilePath(spriteId, spriteType),
                    SourceBundle = ""
                };

                // Extract and save texture image
                SaveTextureImage(texture, metadata.FilePath);

                return metadata;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error extracting texture {texture.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves sprite image to disk
        /// </summary>
        private void SaveSpriteImage(ISprite sprite, string filePath)
        {
            try
            {
                var cacheDir = PeglinDataExtractor.GetExtractionCacheDirectory();
                var fullPath = Path.Combine(cacheDir, filePath);
                var directory = Path.GetDirectoryName(fullPath);

                Logger.Debug($"💾 Attempting to save sprite '{sprite.Name}' to: {fullPath}");

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var texture = sprite.TryGetTexture();
                if (texture != null)
                {
                    Logger.Debug($"✅ Got texture for sprite '{sprite.Name}': {texture.Name} ({texture.Width_C28}x{texture.Height_C28})");
                    var success = ConvertTextureToPng(texture, fullPath);
                    if (success)
                    {
                        Logger.Debug($"🎉 Successfully saved sprite '{sprite.Name}'");
                    }
                    else
                    {
                        Logger.Warning($"❌ Failed to convert sprite '{sprite.Name}' to PNG");
                    }
                }
                else
                {
                    Logger.Warning($"❌ Failed to get texture for sprite '{sprite.Name}'");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"❌ Error saving sprite image '{sprite.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Saves texture image to disk
        /// </summary>
        private void SaveTextureImage(ITexture2D texture, string filePath)
        {
            try
            {
                var cacheDir = PeglinDataExtractor.GetExtractionCacheDirectory();
                var fullPath = Path.Combine(cacheDir, filePath);
                var directory = Path.GetDirectoryName(fullPath);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                ConvertTextureToPng(texture, fullPath);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error saving texture image: {ex.Message}");
            }
        }

        // Entity detection methods
        private bool IsRelicData(Dictionary<string, object> data)
        {
            var relicFields = new[] { "locKey", "englishDisplayName", "effect", "globalRarity", "sprite" };
            var matchCount = relicFields.Count(field => data.ContainsKey(field));
            return matchCount >= 3;
        }

        private bool IsEnemyData(Dictionary<string, object> data)
        {
            // Use the same field detection logic as AssetRipperEnemyExtractor
            var enemyFields = new[] 
            { 
                // From Enemy MonoBehaviour
                "CurrentHealth", "StartingHealth", "DamagePerMeleeAttack", "AttackRange", "enemyTypes",
                // From EnemyData ScriptableObject  
                "MaxHealth", "MaxHealthCruciball", "MeleeAttackDamage", "RangedAttackDamage", "location", "Type"
            };
            
            var matchCount = enemyFields.Count(field => data.ContainsKey(field));
            
            // Also check for specific patterns in values that indicate enemy data
            if (data.ContainsKey("LocKey") && data["LocKey"] is string locKey)
            {
                var enemyPatterns = new[] { "enemy", "boss", "slime", "ballista", "dragon", "demon", "sapper", "knight", "archer" };
                if (enemyPatterns.Any(pattern => locKey.ToLowerInvariant().Contains(pattern)))
                {
                    matchCount += 2; // Boost confidence for pattern match
                }
            }
            
            return matchCount >= 2;
        }

        private bool IsOrbData(Dictionary<string, object> data)
        {
            // Based on actual analysis: orb MonoBehaviour data is flattened, not nested in m_Structure
            // Look directly for orb-specific fields in the data
            
            // Debug logging to see what keys we have
            var keys = string.Join(", ", data.Keys.Take(20)); // Show first 20 keys
            Logger.Debug($"🔍 IsOrbData checking data with keys: {keys}");
            
            // Check for orb-specific fields directly in the data (not in m_Structure)
            var requiredOrbFields = new[] { "locNameString", "locName", "DamagePerPeg", "CritDamagePerPeg", "Level" };
            var requiredFieldCount = requiredOrbFields.Count(field => data.ContainsKey(field));
            
            Logger.Debug($"🔍 Required orb fields found: {requiredFieldCount}/5 - {string.Join(", ", requiredOrbFields.Where(field => data.ContainsKey(field)))}");
            
            // Must have at least 3 of the 5 required orb fields
            if (requiredFieldCount < 3)
            {
                Logger.Debug($"🔍 Not enough required orb fields ({requiredFieldCount} < 3)");
                return false;
            }
            
            // Check for attack-type specific fields that distinguish orb types
            var attackTypeFields = new[] { "shotPrefab", "_shotPrefab", "_thunderPrefab", "_criticalShotPrefab", "_criticalThunderPrefab", "targetColumn", "verticalAttack" };
            var hasAttackTypeFields = attackTypeFields.Any(field => data.ContainsKey(field));
            
            // Check for MonoBehaviour script references that identify orb component types
            var hasScriptRef = data.ContainsKey("m_Script");
            
            Logger.Debug($"🔍 Attack type fields: {hasAttackTypeFields}, Script ref: {hasScriptRef}");
            
            var isOrb = requiredFieldCount >= 3 && (hasAttackTypeFields || hasScriptRef);
            Logger.Debug($"🔍 IsOrb result: {isOrb} (required fields: {requiredFieldCount >= 3}, type indicators: {hasAttackTypeFields || hasScriptRef})");
            
            // Must have both required fields and either attack-type fields or script reference
            return isOrb;
        }

        // Entity extraction methods
        private RelicData? ExtractRelic(string assetName, Dictionary<string, object> data)
        {
            try
            {
                var relic = new RelicData
                {
                    RawData = data,
                    Id = CleanEntityId(assetName ?? "unknown")
                };

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
                    relic.RarityValue = rarityInt;
                    relic.Rarity = _enumExtractor.GetEnumName("RelicRarity", rarityInt);
                }

                return relic;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error extracting relic data: {ex.Message}");
                return null;
            }
        }

        private EnemyData? ExtractEnemy(string assetName, Dictionary<string, object> data)
        {
            try
            {
                var enemy = new EnemyData
                {
                    RawData = data,
                    Id = CleanEntityId(assetName ?? "unknown")
                };

                // Try different name fields
                if (data.TryGetValue("EnglishDisplayName", out var displayName))
                {
                    enemy.Name = displayName?.ToString() ?? assetName;
                }
                else if (data.TryGetValue("LocKey", out var locKey))
                {
                    enemy.Name = locKey?.ToString() ?? assetName;
                }
                else if (data.TryGetValue("enemyName", out var enemyName))
                {
                    enemy.Name = enemyName?.ToString() ?? assetName;
                }

                // Try different health fields
                if (data.TryGetValue("MaxHealth", out var maxHealth) && float.TryParse(maxHealth?.ToString(), out var maxHealthFloat))
                {
                    enemy.MaxHealth = maxHealthFloat;
                }
                else if (data.TryGetValue("StartingHealth", out var startingHealth) && float.TryParse(startingHealth?.ToString(), out var startingHealthFloat))
                {
                    enemy.MaxHealth = startingHealthFloat;
                }

                // Try different attack damage fields
                if (data.TryGetValue("MeleeAttackDamage", out var meleeAttack) && float.TryParse(meleeAttack?.ToString(), out var meleeAttackFloat))
                {
                    enemy.MeleeAttackDamage = meleeAttackFloat;
                }
                else if (data.TryGetValue("DamagePerMeleeAttack", out var damagePerMelee) && float.TryParse(damagePerMelee?.ToString(), out var damagePerMeleeFloat))
                {
                    enemy.MeleeAttackDamage = damagePerMeleeFloat;
                }

                return enemy;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error extracting enemy data: {ex.Message}");
                return null;
            }
        }

        public OrbData? ExtractOrb(string assetName, Dictionary<string, object> data)
        {
            try
            {
                // Extract orb data directly from the data dictionary (not from m_Structure)
                var orb = new OrbData
                {
                    Id = CleanEntityId(assetName ?? "unknown"),
                    RawData = data
                };

                // Extract localization key from locNameString
                if (data.TryGetValue("locNameString", out var locNameString))
                {
                    orb.LocKey = locNameString?.ToString();
                }
                
                // Extract display name from locName  
                if (data.TryGetValue("locName", out var locName))
                {
                    orb.Name = locName?.ToString();
                }
                
                // Fallback to formatted asset name if no name found
                if (string.IsNullOrWhiteSpace(orb.Name))
                {
                    orb.Name = assetName ?? "Unknown Orb";
                }
                
                // Extract description from locDescription (try direct path first, then nested)
                if (data.TryGetValue("locDescription", out var locDescription))
                {
                    orb.Description = locDescription?.ToString() ?? "";
                }
                else if (data.TryGetValue("ComponentData", out var componentDataObj) &&
                    componentDataObj is Dictionary<string, object> componentData &&
                    componentData.TryGetValue("OrbComponent", out var orbComponentObj) &&
                    orbComponentObj is Dictionary<string, object> orbComponent &&
                    orbComponent.TryGetValue("locDescription", out var nestedLocDescription))
                {
                    orb.Description = nestedLocDescription?.ToString() ?? "";
                }
                else if (data.TryGetValue("RawData", out var rawDataObj) &&
                    rawDataObj is Dictionary<string, object> rawData &&
                    rawData.TryGetValue("ComponentData", out var rawComponentDataObj) &&
                    rawComponentDataObj is Dictionary<string, object> rawComponentData &&
                    rawComponentData.TryGetValue("OrbComponent", out var rawOrbComponentObj) &&
                    rawOrbComponentObj is Dictionary<string, object> rawOrbComponent &&
                    rawOrbComponent.TryGetValue("locDescription", out var rawLocDescription))
                {
                    orb.Description = rawLocDescription?.ToString() ?? "";
                }

                // Extract damage values directly from data
                if (data.TryGetValue("DamagePerPeg", out var damagePerPegValue) && 
                    float.TryParse(damagePerPegValue?.ToString(), out var damageFloat))
                {
                    orb.DamagePerPeg = damageFloat;
                }

                if (data.TryGetValue("CritDamagePerPeg", out var critDamagePerPegValue) && 
                    float.TryParse(critDamagePerPegValue?.ToString(), out var critDamageFloat))
                {
                    orb.CritDamagePerPeg = critDamageFloat;
                }

                // Extract level from data (store in RawData for now since OrbData doesn't have Level property)
                if (data.TryGetValue("Level", out var levelValue) && 
                    int.TryParse(levelValue?.ToString(), out var level))
                {
                    // Store level info in RawData for future processing
                    if (orb.RawData == null) orb.RawData = new Dictionary<string, object>();
                    orb.RawData["Level"] = level;
                }

                // Determine orb type based on available fields  
                orb.OrbType = DetermineOrbTypeFromData(assetName, data);

                // Set default rarity (orbs typically don't have explicit rarity like relics)
                orb.RarityValue = 1;
                orb.Rarity = "COMMON";
                
                // Try to resolve sprite references from prefab fields
                ResolvePrefabSprites(orb, data);

                return orb;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error extracting orb data: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Tries to resolve sprites from orb prefab references like _shotPrefab, _criticalShotPrefab, etc.
        /// </summary>
        private void ResolvePrefabSprites(OrbData orb, Dictionary<string, object> data)
        {
            try
            {
                // List of prefab fields that might contain sprites
                var prefabFields = new[] { "_shotPrefab", "_criticalShotPrefab", "_thunderPrefab", "_criticalThunderPrefab" };
                
                foreach (var fieldName in prefabFields)
                {
                    if (data.TryGetValue(fieldName, out var prefabRef) && 
                        prefabRef is Dictionary<string, object> prefabDict)
                    {
                        Logger.Debug($"🔍 Found prefab reference {fieldName} in orb {orb.Name}");
                        
                        // Try to resolve this prefab reference and extract sprites from it
                        // Store the prefab reference info for now - actual resolution would need 
                        // access to the AssetCollection to call TryGetAsset
                        if (orb.RawData == null) orb.RawData = new Dictionary<string, object>();
                        
                        // Extract PathID if available for later correlation
                        if (prefabDict.TryGetValue("pathId", out var pathIdObj))
                        {
                            orb.RawData[$"{fieldName}_PathID"] = pathIdObj;
                            Logger.Debug($"📋 Stored PathID for {fieldName}: {pathIdObj}");
                        }
                    }
                }
                
                // Also check for direct sprite field references that were found in debug logs
                var spriteFields = new[] { "pierceSprite", "originalSprite", "dullSprite" };
                
                foreach (var fieldName in spriteFields)
                {
                    if (data.TryGetValue(fieldName, out var spriteRef) && 
                        spriteRef is Dictionary<string, object> spriteDict)
                    {
                        Logger.Debug($"🎨 Found direct sprite reference {fieldName} in orb {orb.Name}");
                        
                        if (spriteDict.TryGetValue("pathId", out var pathIdObj))
                        {
                            if (orb.RawData == null) orb.RawData = new Dictionary<string, object>();
                            orb.RawData[$"{fieldName}_PathID"] = pathIdObj;
                            Logger.Debug($"🎯 Stored sprite PathID for {fieldName}: {pathIdObj}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"⚠️ Error resolving prefab sprites for orb {orb.Name}: {ex.Message}");
            }
        }

        private string DetermineOrbTypeFromData(string assetName, Dictionary<string, object> data)
        {
            var lowerName = assetName.ToLowerInvariant();
            
            // Check for specific orb type patterns in name
            if (lowerName.Contains("heal") || lowerName.Contains("support"))
                return "UTILITY";
            if (lowerName.Contains("special") || lowerName.Contains("unique"))
                return "SPECIAL";
            if (lowerName.Contains("attack") || lowerName.Contains("damage") || data.ContainsKey("DamagePerPeg"))
                return "ATTACK";
                
            return "ATTACK"; // Default to attack type
        }

        private OrbData? ExtractOrbFromStructure(string assetName, Dictionary<string, object> componentData, Dictionary<string, object> structure)
        {
            try
            {
                var orb = new OrbData
                {
                    RawData = componentData,
                    Id = CleanEntityId(assetName ?? "unknown")
                };

                // Extract localization key from locNameString
                if (structure.TryGetValue("locNameString", out var locNameString))
                {
                    orb.LocKey = locNameString?.ToString();
                }
                
                // Extract display name from locName
                if (structure.TryGetValue("locName", out var locName))
                {
                    orb.Name = locName?.ToString();
                }
                
                // Fallback to asset name if no localized name found
                if (string.IsNullOrWhiteSpace(orb.Name))
                {
                    orb.Name = assetName ?? "Unknown Orb";
                }
                
                // Extract description from locDescription (try structure first, then componentData fallback)
                if (structure.TryGetValue("locDescription", out var locDescription))
                {
                    orb.Description = locDescription?.ToString() ?? "";
                }
                else if (componentData.TryGetValue("locDescription", out var componentLocDescription))
                {
                    orb.Description = componentLocDescription?.ToString() ?? "";
                }
                else if (componentData.TryGetValue("ComponentData", out var nestedComponentDataObj) &&
                    nestedComponentDataObj is Dictionary<string, object> nestedComponentData &&
                    nestedComponentData.TryGetValue("OrbComponent", out var orbComponentObj) &&
                    orbComponentObj is Dictionary<string, object> orbComponent &&
                    orbComponent.TryGetValue("locDescription", out var nestedLocDescription))
                {
                    orb.Description = nestedLocDescription?.ToString() ?? "";
                }

                // Extract damage values directly from structure
                if (structure.TryGetValue("DamagePerPeg", out var damagePerPeg) && 
                    float.TryParse(damagePerPeg?.ToString(), out var damage))
                {
                    orb.DamagePerPeg = damage;
                }

                if (structure.TryGetValue("CritDamagePerPeg", out var critDamagePerPeg) && 
                    float.TryParse(critDamagePerPeg?.ToString(), out var critDamage))
                {
                    orb.CritDamagePerPeg = critDamage;
                }

                // Extract level from structure
                if (structure.TryGetValue("Level", out var levelObj) && 
                    int.TryParse(levelObj?.ToString(), out var level))
                {
                    // orb.Level = level; // TODO: Enable when Level property is available
                }

                // Derive base ID from locNameString (clean base name without level)  
                // TODO: Enable when BaseId property is available
                // if (!string.IsNullOrWhiteSpace(orb.LocKey))
                // {
                //     orb.BaseId = orb.LocKey.ToLowerInvariant().Replace(" ", "_");
                // }
                // else
                // {
                //     orb.BaseId = CleanEntityId(assetName ?? "unknown");
                // }

                // Simple orb type detection based on damage values
                if (orb.DamagePerPeg.HasValue && orb.DamagePerPeg.Value > 0)
                {
                    orb.OrbType = "ATTACK";
                }
                else
                {
                    orb.OrbType = "UTILITY";
                }

                // Extract rarity (orbs typically don't have rarity tiers)
                orb.RarityValue = 1; // Default to COMMON
                orb.Rarity = "COMMON";

                return orb;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error extracting orb from structure: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Processes a GameObject to check if it represents an orb and extract orb data
        /// </summary>
        private void ProcessGameObjectForOrbs(IGameObject gameObject, Dictionary<long, IMonoBehaviour> componentMap, 
            UnifiedExtractionResult result, Dictionary<string, IUnityAssetBase> orbSpriteRefs, AssetCollection collection)
        {
            try
            {
                var gameObjectData = new GameObjectData
                {
                    Id = CleanEntityId(gameObject.Name),
                    Name = gameObject.Name,
                    PathID = gameObject.PathID
                };

                // Extract components from the GameObject
                ExtractGameObjectComponents(gameObject, componentMap, gameObjectData, collection);

                // Check if this looks like an orb based on its components
                Logger.Debug($"🔍 Checking if GameObject {gameObjectData.Name} is an orb. Components: {string.Join(", ", gameObjectData.Components.Select(c => c.Type))}");
                if (IsOrbGameObject(gameObjectData))
                {
                    Logger.Debug($"✅ GameObject {gameObjectData.Name} identified as orb");
                    // Convert GameObject data to OrbData
                    var orbData = ConvertGameObjectToOrbData(gameObjectData);
                    if (orbData != null && !string.IsNullOrEmpty(orbData.Id))
                    {
                        result.Orbs[orbData.Id] = orbData;

                        // Try to find sprite reference (look for sprite components or references)
                        var spriteReference = FindGameObjectSpriteReference(gameObject, componentMap, collection);
                        if (spriteReference != null)
                        {
                            orbSpriteRefs[orbData.Id] = spriteReference;
                            Logger.Debug($"⚪ Found sprite reference for orb GameObject {orbData.Id}: {spriteReference.GetType().Name}");
                        }

                        Logger.Debug($"Found orb GameObject: {orbData.Id} - {orbData.Name}");
                    }
                }
                else
                {
                    Logger.Debug($"❌ GameObject {gameObjectData.Name} rejected as orb");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error processing GameObject for orbs: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts components from a GameObject
        /// </summary>
        private void ExtractGameObjectComponents(IGameObject gameObject, Dictionary<long, IMonoBehaviour> componentMap, 
            GameObjectData gameObjectData, AssetCollection collection)
        {
            try
            {
                // Extract actual component data from the GameObject's component references
                var gameObjectInfo = new Dictionary<string, object>
                {
                    ["Name"] = gameObject.Name,
                    ["PathID"] = gameObject.PathID
                };
                
                // Fetch all components from the GameObject
                var components = gameObject.FetchComponents();
                var extractedComponentData = new Dictionary<string, object>();
                
                foreach (var componentPtr in components)
                {
                    try
                    {
                        // Try to resolve the component reference
                        var component = componentPtr.TryGetAsset(collection);
                        if (component != null)
                        {
                            // Handle MonoBehaviour components (orb data, etc.)
                            if (component is IMonoBehaviour monoBehaviour)
                            {
                                // Load the structure data from the MonoBehaviour
                                var structure = monoBehaviour.LoadStructure();
                                if (structure != null)
                                {
                                    // Convert to dictionary for processing
                                    IUnityAssetBase? spriteRef = null;
                                    var componentData = ConvertStructureToDict(structure, collection, out spriteRef);
                                    
                                    // Check if this is orb data
                                    if (IsOrbData(componentData))
                                    {
                                        Logger.Debug($"✅ Found orb MonoBehaviour in GameObject {gameObject.Name}");
                                        
                                        // Extract the orb data from this component
                                        var orbData = ExtractOrb(gameObject.Name, componentData);
                                        if (orbData != null)
                                        {
                                            // Merge the extracted orb data into the GameObject data
                                            gameObjectData.Name = orbData.Name ?? gameObject.Name;
                                            gameObjectData.RawData = orbData.RawData;
                                            
                                            // Store the extracted component data for later use
                                            extractedComponentData["OrbComponent"] = componentData;
                                            extractedComponentData["HasOrbData"] = true;
                                            extractedComponentData["LocKey"] = orbData.LocKey;
                                            extractedComponentData["DamagePerPeg"] = orbData.DamagePerPeg;
                                            extractedComponentData["CritDamagePerPeg"] = orbData.CritDamagePerPeg;
                                        }
                                    }
                                    
                                    // Store component info even if not orb data
                                    var componentType = monoBehaviour.GetType().Name;
                                    gameObjectData.Components.Add(new ComponentData 
                                    { 
                                        Type = componentType,
                                        Properties = componentData
                                    });
                                }
                            }
                            // Handle Sprite components
                            else if (component is ISprite sprite)
                            {
                                Logger.Debug($"🎨 Found Sprite component in GameObject {gameObject.Name}: {sprite.Name}");
                                
                                var spriteData = new Dictionary<string, object>
                                {
                                    ["Name"] = sprite.Name,
                                    ["PathID"] = sprite.PathID,
                                    ["Type"] = "Sprite"
                                };
                                
                                // Try to get the texture associated with this sprite
                                try
                                {
                                    var texture = sprite.TryGetTexture();
                                    if (texture != null)
                                    {
                                        spriteData["TextureName"] = texture.Name;
                                        spriteData["TexturePathID"] = texture.PathID;
                                        Logger.Debug($"🖼️ Sprite {sprite.Name} references texture: {texture.Name} (PathID: {texture.PathID})");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Debug($"Could not resolve texture for sprite {sprite.Name}: {ex.Message}");
                                }
                                
                                // Store sprite data for correlation
                                extractedComponentData["SpriteComponent"] = spriteData;
                                
                                gameObjectData.Components.Add(new ComponentData
                                {
                                    Type = "Sprite",
                                    Name = sprite.Name,
                                    PathID = sprite.PathID,
                                    Properties = spriteData
                                });
                            }
                            // Handle Texture2D components
                            else if (component is ITexture2D texture2D)
                            {
                                Logger.Debug($"🖼️ Found Texture2D component in GameObject {gameObject.Name}: {texture2D.Name}");
                                
                                var textureData = new Dictionary<string, object>
                                {
                                    ["Name"] = texture2D.Name,
                                    ["PathID"] = texture2D.PathID,
                                    ["Type"] = "Texture2D"
                                };
                                
                                // Store texture data for correlation
                                extractedComponentData["TextureComponent"] = textureData;
                                
                                gameObjectData.Components.Add(new ComponentData
                                {
                                    Type = "Texture2D",
                                    Name = texture2D.Name,
                                    PathID = texture2D.PathID,
                                    Properties = textureData
                                });
                            }
                            else
                            {
                                // Handle other component types
                                var componentType = component.GetType().Name;
                                Logger.Debug($"🔧 Found component type {componentType} in GameObject {gameObject.Name}");
                                
                                gameObjectData.Components.Add(new ComponentData
                                {
                                    Type = componentType,
                                    Name = component.ToString() ?? componentType,
                                    PathID = component.PathID,
                                    Properties = new Dictionary<string, object> { ["ComponentType"] = componentType }
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Could not resolve component for GameObject {gameObject.Name}: {ex.Message}");
                    }
                }
                
                // Store the extracted component data in RawData
                if (extractedComponentData.Count > 0)
                {
                    gameObjectInfo["ComponentData"] = extractedComponentData;
                }
                
                gameObjectData.RawData = gameObjectInfo;
                
                // For now, create a placeholder component based on the GameObject name
                var placeholderComponent = new ComponentData
                {
                    Type = DetermineComponentTypeFromName(gameObject.Name),
                    Name = gameObject.Name,
                    PathID = gameObject.PathID,
                    Properties = gameObjectInfo
                };
                
                gameObjectData.Components.Add(placeholderComponent);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error extracting components for {gameObject.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines component type from GameObject name
        /// </summary>
        private string DetermineComponentTypeFromName(string name)
        {
            var lowerName = name.ToLowerInvariant();
            
            // Guess component types based on naming patterns
            if (lowerName.Contains("attack") || lowerName.Contains("damage"))
                return "Attack";
            if (lowerName.Contains("ball") || lowerName.Contains("pachinko"))
                return "PachinkoBall";
            if (lowerName.Contains("orb"))
                return "OrbData";
            if (lowerName.Contains("upgrade"))
                return "UnlimitedUpgrades";
                
            return "MonoBehaviour";
        }

        /// <summary>
        /// Checks if a GameObject represents an orb
        /// </summary>
        private static bool IsOrbGameObject(GameObjectData gameObjectData)
        {
            // Check if the GameObject has orb-related components
            var componentTypes = gameObjectData.Components.Select(c => c.Type.ToLowerInvariant()).ToList();
            
            // Look for orb-specific component patterns
            var orbComponentPatterns = new[] 
            { 
                "attack", "pachinkoball", "orbdata", "ball", "projectile", 
                "damage", "critdamage", "unlimitedupgrades"
            };
            
            var hasOrbComponents = orbComponentPatterns.Any(pattern => 
                componentTypes.Any(type => type.Contains(pattern)));
            
            if (!hasOrbComponents)
            {
                return false;
            }
            
            // Additional filtering based on name patterns
            var name = gameObjectData.Name.ToLowerInvariant();
            var id = gameObjectData.Id.ToLowerInvariant();
            
            // Include if name/id suggests it's an orb
            var orbNamePatterns = new[] { "orb", "ball", "attack", "projectile" };
            var hasOrbName = orbNamePatterns.Any(pattern => 
                name.Contains(pattern) || id.Contains(pattern));
            
            // Exclude non-orb entities
            var excludePatterns = new[] 
            { 
                "ui", "menu", "button", "text", "canvas", "camera", "light", "manager", 
                "controller", "system", "effect", "particle", "enemy", "relic", "statue"
            };
            
            var isExcluded = excludePatterns.Any(pattern => 
                name.Contains(pattern) || id.Contains(pattern));
            
            return hasOrbComponents && (hasOrbName || componentTypes.Count > 0) && !isExcluded;
        }

        /// <summary>
        /// Converts GameObject data to OrbData
        /// </summary>
        private OrbData? ConvertGameObjectToOrbData(GameObjectData gameObjectData)
        {
            try
            {
                var orb = new OrbData
                {
                    Id = gameObjectData.Id,
                    Name = gameObjectData.Name,
                    RawData = gameObjectData.RawData
                };

                // Check if we have extracted component data with orb information
                if (gameObjectData.RawData != null && 
                    gameObjectData.RawData.TryGetValue("ComponentData", out var componentDataObj) &&
                    componentDataObj is Dictionary<string, object> componentData)
                {
                    // Use the extracted orb data from the MonoBehaviour component
                    if (componentData.TryGetValue("HasOrbData", out var hasOrbData) && 
                        hasOrbData is bool && (bool)hasOrbData)
                    {
                        // Extract all the orb fields we found
                        if (componentData.TryGetValue("LocKey", out var locKey))
                            orb.LocKey = locKey?.ToString();
                            
                        if (componentData.TryGetValue("DamagePerPeg", out var damage))
                            orb.DamagePerPeg = damage as float?;
                            
                        if (componentData.TryGetValue("CritDamagePerPeg", out var critDamage))
                            orb.CritDamagePerPeg = critDamage as float?;
                            
                        // Extract description from componentData.OrbComponent.locDescription path
                        if (componentData.TryGetValue("OrbComponent", out var orbComponentObj) &&
                            orbComponentObj is Dictionary<string, object> orbComponent &&
                            orbComponent.TryGetValue("locDescription", out var locDescription))
                        {
                            orb.Description = locDescription?.ToString() ?? "";
                        }
                        
                        // Set default values for other fields
                        orb.OrbType = "ATTACK";
                        orb.RarityValue = 1;
                        orb.Rarity = "COMMON";
                        
                        Logger.Debug($"✅ Successfully extracted orb data from GameObject {gameObjectData.Name}: LocKey={orb.LocKey}, Damage={orb.DamagePerPeg}");
                    }
                }
                else
                {
                    // Fallback: Try to extract damage values from component properties
                    foreach (var component in gameObjectData.Components)
                    {
                        if (component.Type.ToLowerInvariant().Contains("attack"))
                        {
                            // Try to find damage values in component properties
                            if (component.Properties.TryGetValue("DamagePerPeg", out var damage) && 
                                float.TryParse(damage?.ToString(), out var damageFloat))
                            {
                                orb.DamagePerPeg = damageFloat;
                            }
                            if (component.Properties.TryGetValue("CritDamagePerPeg", out var critDamage) && 
                                float.TryParse(critDamage?.ToString(), out var critDamageFloat))
                            {
                                orb.CritDamagePerPeg = critDamageFloat;
                            }
                        }
                    }
                }

                return orb;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error converting GameObject to OrbData: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds sprite reference for a GameObject
        /// </summary>
        private IUnityAssetBase? FindGameObjectSpriteReference(IGameObject gameObject, Dictionary<long, IMonoBehaviour> componentMap, 
            AssetCollection collection)
        {
            try
            {
                // This would need proper implementation to traverse GameObject's component array
                // For now, return null - sprite correlation will be handled by the sprite extractor
                return null;
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error finding sprite reference for GameObject {gameObject.Name}: {ex.Message}");
                return null;
            }
        }

        // Helper methods
        private static bool IsSpriteField(string? fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return false;
            var spriteFieldNames = new[] { "sprite", "icon", "image", "texture", "picture", "graphic", "avatar" };
            return spriteFieldNames.Any(name => fieldName.Contains(name));
        }

        /// <summary>
        /// More aggressive check for potential sprite fields based on common Unity patterns
        /// </summary>
        private static bool CouldBeSpriteField(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return false;
            
            var fieldLower = fieldName.ToLowerInvariant();
            
            // Common patterns in Unity assets
            var potentialSpritePatterns = new[]
            {
                "icon", "sprite", "image", "texture", "graphic", "picture", "avatar",
                "prefabAssetReference", // Unity Addressable references
                "asset", // Generic asset references
                "visual", "portrait", "thumbnail"
            };
            
            return potentialSpritePatterns.Any(pattern => fieldLower.Contains(pattern));
        }

        /// <summary>
        /// Checks if a field contains a GUID-based asset reference (Unity Addressables)
        /// </summary>
        private static bool IsGuidAssetReference(object? value)
        {
            if (value is Dictionary<string, object> dict)
            {
                return dict.ContainsKey("m_AssetGUID") && 
                       dict.TryGetValue("m_AssetGUID", out var guid) && 
                       !string.IsNullOrEmpty(guid?.ToString());
            }
            return false;
        }

        private static string CleanEntityId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "unknown";
            id = id.Replace("Relic_", "").Replace("_Relic", "").Replace(".asset", "");
            id = id.Replace("Enemy_", "").Replace("_Enemy", "");
            id = id.Replace("Orb_", "").Replace("_Orb", "");
            id = id.ToLowerInvariant().Replace(" ", "_");
            return id;
        }

        private static string GenerateSpriteId(string name)
        {
            if (string.IsNullOrEmpty(name)) return $"sprite_{Guid.NewGuid():N}";
            return name.Replace(" ", "_").Replace(".", "_").ToLowerInvariant();
        }

        private static SpriteCacheManager.SpriteType DetermineSpriteType(string name)
        {
            var nameLower = name?.ToLowerInvariant() ?? "";

            if (nameLower.Contains("relic") || nameLower.Contains("artifact") || nameLower.Contains("item"))
                return SpriteCacheManager.SpriteType.Relic;

            if (nameLower.Contains("enemy") || nameLower.Contains("monster") || nameLower.Contains("boss"))
                return SpriteCacheManager.SpriteType.Enemy;

            if (nameLower.Contains("orb") || nameLower.Contains("ball") || nameLower.Contains("projectile"))
                return SpriteCacheManager.SpriteType.Orb;

            return SpriteCacheManager.SpriteType.Orb; // Default to Orb for unknowns
        }

        /// <summary>
        /// Converts a Texture2D to PNG format and saves it to disk
        /// </summary>
        private bool ConvertTextureToPng(ITexture2D texture, string outputPath)
        {
            try
            {
                Logger.Debug($"🔄 Converting texture '{texture.Name}' to PNG...");
                
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Use AssetRipper's TextureConverter to convert to DirectBitmap
                if (TextureConverter.TryConvertToBitmap(texture, out DirectBitmap bitmap))
                {
                    Logger.Debug($"✅ Successfully converted texture '{texture.Name}' to bitmap");
                    
                    // Save as PNG using AssetRipper's built-in PNG export
                    using var fileStream = File.Create(outputPath);
                    bitmap.SaveAsPng(fileStream);
                    // DirectBitmap doesn't have Dispose, it's managed by GC
                    
                    Logger.Debug($"🎉 Successfully saved PNG to: {outputPath}");
                    return true;
                }
                else
                {
                    Logger.Warning($"❌ Failed to convert texture '{texture.Name}' to bitmap - format may not be supported");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"❌ Error converting texture '{texture.Name}' to PNG: {ex.Message}");
                return false;
            }
        }

        private static string GetSpriteFilePath(string spriteId, SpriteCacheManager.SpriteType type)
        {
            var folder = type switch
            {
                SpriteCacheManager.SpriteType.Relic => "extracted-data/sprites/relics",
                SpriteCacheManager.SpriteType.Enemy => "extracted-data/sprites/enemies",
                SpriteCacheManager.SpriteType.Orb => "extracted-data/sprites/orbs",
                _ => "extracted-data/sprites/unknown"
            };

            return $"{folder}/{spriteId}.png";
        }

        /// <summary>
        /// Extracts sprite metadata with improved processing (incorporating AssetRipperSpriteExtractor techniques)
        /// </summary>
        private SpriteCacheManager.SpriteMetadata? ExtractSpriteWithImprovedProcessing(ISprite sprite)
        {
            try
            {
                var spriteName = CleanSpriteName(sprite.Name);
                var spriteType = DetermineSpriteType(spriteName);
                var spriteId = GenerateSpriteId(spriteName);
                
                Logger.Debug($"🎯 Processing sprite '{spriteName}' as {spriteType} with ID: {spriteId}");

                var metadata = new SpriteCacheManager.SpriteMetadata
                {
                    Id = spriteId,
                    Name = spriteName,
                    Type = spriteType,
                    FilePath = GetSpriteFilePath(spriteId, spriteType),
                    SourceBundle = "", // Will be set by caller
                    ExtractedAt = DateTime.Now,
                    IsAtlas = false
                };

                // Get the texture from the sprite and save it
                var texture = sprite.TryGetTexture();
                if (texture != null)
                {
                    metadata.Width = texture.Width_C28;
                    metadata.Height = texture.Height_C28;
                    
                    // Use improved conversion method
                    if (ConvertTextureToPngImproved(texture, metadata.FilePath, spriteName))
                    {
                        Logger.Debug($"✅ Successfully processed sprite: {spriteName} -> {spriteId}");
                        return metadata;
                    }
                    else
                    {
                        Logger.Warning($"❌ Failed to convert sprite texture: {spriteName}");
                        return null;
                    }
                }
                else
                {
                    Logger.Verbose($"❌ Could not get texture from sprite: {spriteName}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Verbose($"Error processing sprite {sprite.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts texture metadata with improved processing (incorporating AssetRipperSpriteExtractor techniques)
        /// </summary>
        private SpriteCacheManager.SpriteMetadata? ExtractTextureWithImprovedProcessing(ITexture2D texture)
        {
            try
            {
                var textureName = CleanSpriteName(texture.Name);
                var spriteType = DetermineSpriteType(textureName);
                var spriteId = GenerateSpriteId(textureName);
                
                Logger.Debug($"🎯 Processing texture '{textureName}' as {spriteType} with ID: {spriteId}");

                var metadata = new SpriteCacheManager.SpriteMetadata
                {
                    Id = spriteId,
                    Name = textureName,
                    Type = spriteType,
                    Width = texture.Width_C28,
                    Height = texture.Height_C28,
                    FilePath = GetSpriteFilePath(spriteId, spriteType),
                    SourceBundle = "", // Will be set by caller
                    ExtractedAt = DateTime.Now,
                    IsAtlas = DetectIfSpriteSheet(textureName, texture.Width_C28, texture.Height_C28)
                };

                // Convert and save the texture
                if (ConvertTextureToPngImproved(texture, metadata.FilePath, textureName))
                {
                    Logger.Debug($"✅ Successfully processed texture: {textureName} -> {spriteId}");
                    return metadata;
                }
                else
                {
                    Logger.Warning($"❌ Failed to convert texture: {textureName}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error processing texture {texture.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts sprite metadata with forced sprite type (overrides automatic type detection)
        /// </summary>
        private SpriteCacheManager.SpriteMetadata? ExtractSpriteWithImprovedProcessing(ISprite sprite, SpriteCacheManager.SpriteType forcedType)
        {
            try
            {
                var spriteName = CleanSpriteName(sprite.Name);
                var spriteId = GenerateSpriteId(spriteName);
                
                Logger.Debug($"🎯 Processing sprite '{spriteName}' with FORCED type {forcedType} and ID: {spriteId}");

                var metadata = new SpriteCacheManager.SpriteMetadata
                {
                    Id = spriteId,
                    Name = spriteName,
                    Type = forcedType,
                    Width = (int)sprite.RD.TextureRect.Width,
                    Height = (int)sprite.RD.TextureRect.Height,
                    FilePath = GetSpriteFilePath(spriteId, forcedType),
                    SourceBundle = ""
                };

                if (sprite.RD.Texture.TryGetAsset(sprite.Collection, out ITexture2D? texture) && texture != null)
                {
                    var saved = ConvertTextureToPngImproved(texture, metadata.FilePath, spriteName);
                    if (saved)
                    {
                        Logger.Verbose($"✅ Extracted sprite: {spriteName} -> {metadata.FilePath} (forced type: {forcedType})");
                        return metadata;
                    }
                }

                Logger.Warning($"❌ Failed to extract sprite: {spriteName}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error processing sprite {sprite.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts texture metadata with forced sprite type (overrides automatic type detection)  
        /// </summary>
        private SpriteCacheManager.SpriteMetadata? ExtractTextureWithImprovedProcessing(ITexture2D texture, SpriteCacheManager.SpriteType forcedType)
        {
            try
            {
                var textureName = CleanSpriteName(texture.Name);
                var spriteId = GenerateSpriteId(textureName);
                
                Logger.Debug($"🎯 Processing texture '{textureName}' with FORCED type {forcedType} and ID: {spriteId}");
                
                var metadata = new SpriteCacheManager.SpriteMetadata
                {
                    Id = spriteId,
                    Name = textureName,
                    Type = forcedType,
                    Width = texture.Width_C28,
                    Height = texture.Height_C28,
                    FilePath = GetSpriteFilePath(spriteId, forcedType),
                    SourceBundle = ""
                };

                var saved = ConvertTextureToPngImproved(texture, metadata.FilePath, textureName);
                if (saved)
                {
                    Logger.Verbose($"✅ Extracted texture: {textureName} -> {metadata.FilePath} (forced type: {forcedType})");
                    return metadata;
                }
                else
                {
                    Logger.Warning($"❌ Failed to convert texture: {textureName}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error processing texture {texture.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Improved PNG conversion with better error handling and logging
        /// </summary>
        private bool ConvertTextureToPngImproved(ITexture2D texture, string relativePath, string displayName)
        {
            try
            {
                var cacheDir = PeglinDataExtractor.GetExtractionCacheDirectory();
                var fullPath = Path.Combine(cacheDir, relativePath);
                var directory = Path.GetDirectoryName(fullPath);

                Logger.Debug($"💾 Saving sprite '{displayName}' to: {fullPath}");

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Use AssetRipper's TextureConverter to convert to DirectBitmap
                if (TextureConverter.TryConvertToBitmap(texture, out DirectBitmap bitmap))
                {
                    Logger.Debug($"✅ Converted texture '{displayName}' to bitmap ({texture.Width_C28}x{texture.Height_C28})");
                    
                    // Save as PNG using AssetRipper's built-in PNG export
                    using var fileStream = File.Create(fullPath);
                    bitmap.SaveAsPng(fileStream);
                    
                    Logger.Debug($"🎉 Successfully saved PNG: {Path.GetFileName(fullPath)} (format: {texture.Format_C28E})");
                    return true;
                }
                else
                {
                    Logger.Warning($"❌ Failed to convert texture '{displayName}' to bitmap - format '{texture.Format_C28E}' may not be supported");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"❌ Error saving sprite '{displayName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Detects if a texture is likely a sprite sheet based on naming and dimensions
        /// (Borrowed from AssetRipperSpriteExtractor)
        /// </summary>
        private bool DetectIfSpriteSheet(string name, int width, int height)
        {
            var lowerName = name.ToLowerInvariant();
            
            // Check for sprite sheet naming patterns
            var spriteSheetPatterns = new[]
            {
                "spritesheet", "sheet", "anim", "animated", "frames", "idle", "walk", "run", "attack", "death", "hurt"
            };
            
            var hasSpriteSheetName = spriteSheetPatterns.Any(pattern => lowerName.Contains(pattern));
            
            // Check for dimensions that suggest a sprite sheet
            var possibleFrameSizes = new[] { 16, 24, 32, 48, 64, 80, 96, 128, 160 };
            
            // Check if width is a multiple of height (horizontal strip)
            var isHorizontalStrip = height <= 160 && width > height && width % height == 0 && (width / height) >= 2;
            
            // Check if height is a multiple of width (vertical strip) 
            var isVerticalStrip = width <= 160 && height > width && height % width == 0 && (height / width) >= 2;
            
            // Check if both dimensions are multiples of common frame sizes (grid layout)
            var isGrid = possibleFrameSizes.Any(frameSize => 
                width >= frameSize * 2 && height >= frameSize * 2 &&
                width % frameSize == 0 && height % frameSize == 0);
            
            var result = hasSpriteSheetName || isHorizontalStrip || isVerticalStrip || isGrid;
            
            if (result)
            {
                Logger.Debug($"🎞️ Detected sprite sheet: {name} ({width}x{height})");
            }
            
            return result;
        }

        /// <summary>
        /// Cleans sprite names by removing common suffixes and prefixes
        /// </summary>
        private string CleanSpriteName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";
            
            // Remove common Unity asset suffixes
            var cleaned = name;
            var suffixesToRemove = new[] { "(Clone)", "_1", "_2", "_3", "_Instance" };
            foreach (var suffix in suffixesToRemove)
            {
                if (cleaned.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    cleaned = cleaned[..^suffix.Length];
                }
            }
            
            return cleaned.Trim();
        }

        /// <summary>
        /// Builds a lookup table from sprite PathID to texture PathID using SpriteInformationObject assets
        /// </summary>
        private Dictionary<long, long> BuildSpriteInformationLookup(AssetCollection collection, GameBundle gameBundle)
        {
            var spriteToTextureLookup = new Dictionary<long, long>();
            
            try
            {
                // Look for SpriteInformationObject assets in all collections
                foreach (var assetCollection in gameBundle.FetchAssetCollections())
                {
                    foreach (var asset in assetCollection.Assets)
                    {
                        if (asset.Value == null) continue;
                        
                        var assetType = asset.Value.GetType().Name;
                        
                        // Look for any asset that contains sprite information mapping
                        // This could be named differently but should have Texture and Sprites properties
                        if (assetType.Contains("SpriteInformation", StringComparison.OrdinalIgnoreCase) ||
                            assetType.Contains("SpriteAtlas", StringComparison.OrdinalIgnoreCase) ||
                            HasSpriteInformationProperties(asset.Value))
                        {
                            Logger.Debug($"🗺️ Found potential sprite information object: {assetType}");
                            ProcessSpriteInformationObject(asset.Value, spriteToTextureLookup);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error building sprite information lookup: {ex.Message}");
            }
            
            return spriteToTextureLookup;
        }
        
        /// <summary>
        /// Checks if an asset has properties that suggest it contains sprite information
        /// </summary>
        private bool HasSpriteInformationProperties(IUnityObjectBase asset)
        {
            try
            {
                var type = asset.GetType();
                var hasTexture = type.GetProperty("Texture") != null;
                var hasSprites = type.GetProperty("Sprites") != null;
                
                return hasTexture && hasSprites;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Processes a sprite information object to extract sprite PathID -> texture PathID mappings
        /// </summary>
        private void ProcessSpriteInformationObject(IUnityObjectBase spriteInfoObject, Dictionary<long, long> spriteToTextureLookup)
        {
            try
            {
                Logger.Debug($"🔍 Processing sprite information object: {spriteInfoObject.GetType().Name}");
                
                // Get the Texture property (should point to the actual texture)
                var textureProperty = spriteInfoObject.GetType().GetProperty("Texture");
                if (textureProperty == null)
                {
                    Logger.Debug($"❌ No Texture property found on {spriteInfoObject.GetType().Name}");
                    return;
                }
                
                var textureRef = textureProperty.GetValue(spriteInfoObject);
                var texturePathId = ExtractPathIdFromReference(textureRef);
                
                if (texturePathId == 0)
                {
                    Logger.Debug($"❌ Could not extract texture PathID from reference");
                    return;
                }
                
                Logger.Debug($"🎯 Found texture PathID: {texturePathId}");
                
                // Get the Sprites property (should be a collection of sprite PathID mappings)
                var spritesProperty = spriteInfoObject.GetType().GetProperty("Sprites");
                if (spritesProperty == null)
                {
                    Logger.Debug($"❌ No Sprites property found on {spriteInfoObject.GetType().Name}");
                    return;
                }
                
                var spritesValue = spritesProperty.GetValue(spriteInfoObject);
                if (spritesValue == null)
                {
                    Logger.Debug($"❌ Sprites property is null");
                    return;
                }
                
                // Process the sprites collection (likely a Dictionary or Array of Key-Value pairs)
                ProcessSpritesCollection(spritesValue, texturePathId, spriteToTextureLookup);
                
            }
            catch (Exception ex)
            {
                Logger.Debug($"❌ Error processing sprite information object: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Processes a collection of sprite mappings (sprite PathID -> texture PathID)
        /// </summary>
        private void ProcessSpritesCollection(object spritesCollection, long texturePathId, Dictionary<long, long> spriteToTextureLookup)
        {
            try
            {
                // Handle different collection types
                if (spritesCollection is System.Collections.IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item == null) continue;
                        
                        // Look for Key property (sprite PathID)
                        var keyProperty = item.GetType().GetProperty("Key");
                        if (keyProperty != null)
                        {
                            var keyValue = keyProperty.GetValue(item);
                            var spritePathId = ExtractPathIdFromReference(keyValue);
                            
                            if (spritePathId != 0)
                            {
                                spriteToTextureLookup[spritePathId] = texturePathId;
                                Logger.Debug($"🗺️ Mapped sprite PathID {spritePathId} -> texture PathID {texturePathId}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"❌ Error processing sprites collection: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Extracts PathID from a Unity asset reference (handles PPtr structures)
        /// </summary>
        private long ExtractPathIdFromReference(object? reference)
        {
            if (reference == null) return 0;
            
            try
            {
                // Look for m_PathID property (standard Unity reference structure)
                var pathIdProperty = reference.GetType().GetProperty("m_PathID");
                if (pathIdProperty != null)
                {
                    var pathIdValue = pathIdProperty.GetValue(reference);
                    if (pathIdValue is long pathId)
                    {
                        return pathId;
                    }
                    if (pathIdValue is int intPathId)
                    {
                        return intPathId;
                    }
                }
                
                // Also try PathID (without m_ prefix)
                var pathIdProperty2 = reference.GetType().GetProperty("PathID");
                if (pathIdProperty2 != null)
                {
                    var pathIdValue = pathIdProperty2.GetValue(reference);
                    if (pathIdValue is long pathId)
                    {
                        return pathId;
                    }
                    if (pathIdValue is int intPathId)
                    {
                        return intPathId;
                    }
                }
            }
            catch
            {
                // Ignore extraction errors
            }
            
            return 0;
        }
        
        /// <summary>
        /// Resolves a sprite reference using the sprite information lookup table
        /// </summary>
        private SpriteCacheManager.SpriteMetadata? ResolveSpriteWithLookup(IUnityAssetBase spriteRef, GameBundle gameBundle, Dictionary<long, long> spriteInfoLookup, SpriteCacheManager.SpriteType? forcedSpriteType = null)
        {
            try
            {
                // First, try to extract PathID from the sprite reference
                var spritePathId = ExtractPathIdFromAsset(spriteRef);
                if (spritePathId != 0)
                {
                    Logger.Debug($"🔍 Looking up sprite PathID {spritePathId} in sprite info lookup table");
                    
                    // Check if this sprite PathID maps to a texture PathID
                    if (spriteInfoLookup.TryGetValue(spritePathId, out var texturePathId))
                    {
                        Logger.Debug($"🗺️ Found mapping: sprite PathID {spritePathId} -> texture PathID {texturePathId}");
                        
                        // Now find the texture with this PathID across all collections
                        var texture = FindTextureByPathId(texturePathId, gameBundle);
                        if (texture != null)
                        {
                            Logger.Debug($"✅ Found texture for PathID {texturePathId}: {texture.Name}");
                            return forcedSpriteType.HasValue 
                                ? ExtractTextureWithImprovedProcessing(texture, forcedSpriteType.Value)
                                : ExtractTextureWithImprovedProcessing(texture);
                        }
                        else
                        {
                            Logger.Debug($"❌ Could not find texture with PathID {texturePathId}");
                        }
                    }
                    else
                    {
                        Logger.Debug($"❌ No mapping found for sprite PathID {spritePathId}");
                    }
                }
                
                // Fall back to original resolution method
                Logger.Debug($"🔄 Falling back to original sprite resolution method");
                return forcedSpriteType.HasValue 
                    ? ResolveSprite(spriteRef, gameBundle, forcedSpriteType.Value)
                    : ResolveSprite(spriteRef, gameBundle);
            }
            catch (Exception ex)
            {
                Logger.Debug($"❌ Error in sprite lookup resolution: {ex.Message}");
                // Fall back to original resolution method
                return forcedSpriteType.HasValue 
                    ? ResolveSprite(spriteRef, gameBundle, forcedSpriteType.Value)
                    : ResolveSprite(spriteRef, gameBundle);
            }
        }
        
        /// <summary>
        /// Extracts PathID from a Unity asset
        /// </summary>
        private long ExtractPathIdFromAsset(IUnityAssetBase asset)
        {
            try
            {
                // Try PathID property
                var pathIdProperty = asset.GetType().GetProperty("PathID");
                if (pathIdProperty != null)
                {
                    var pathIdValue = pathIdProperty.GetValue(asset);
                    if (pathIdValue is long pathId)
                    {
                        return pathId;
                    }
                    if (pathIdValue is int intPathId)
                    {
                        return intPathId;
                    }
                }
                
                // For PPtr types, look for the m_PathID field in the referenced object
                if (asset.GetType().Name.StartsWith("PPtr_"))
                {
                    // Try to get the PathID from PPtr structure
                    var fields = asset.GetType().GetFields();
                    foreach (var field in fields)
                    {
                        if (field.Name.Contains("PathID") || field.Name.Contains("m_PathID"))
                        {
                            var value = field.GetValue(asset);
                            if (value is long lValue) return lValue;
                            if (value is int iValue) return iValue;
                        }
                    }
                }
            }
            catch
            {
                // Ignore extraction errors
            }
            
            return 0;
        }
        
        /// <summary>
        /// Finds a texture asset by PathID across all collections in the game bundle
        /// </summary>
        private ITexture2D? FindTextureByPathId(long pathId, GameBundle gameBundle)
        {
            try
            {
                foreach (var collection in gameBundle.FetchAssetCollections())
                {
                    if (collection.Assets.TryGetValue(pathId, out var asset) && asset is ITexture2D texture)
                    {
                        return texture;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"❌ Error finding texture by PathID {pathId}: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// Attempts to process any asset that might be sprite-related (including SpriteInformationObject)
        /// </summary>
        private SpriteCacheManager.SpriteMetadata? ProcessPotentialSpriteAsset(IUnityObjectBase asset, string bundleName)
        {
            try
            {
                var assetType = asset.GetType().Name;
                var assetName = asset.ToString() ?? "unknown";
                
                Logger.Debug($"🔍 Processing potential sprite asset: {assetType} - {assetName}");
                
                // Handle ISprite (should already be processed, but just in case)
                if (asset is ISprite sprite)
                {
                    return ExtractSpriteWithImprovedProcessing(sprite);
                }
                
                // Handle ITexture2D directly
                if (asset is ITexture2D texture)
                {
                    return ExtractTextureWithImprovedProcessing(texture);
                }
                
                // Try to handle other potential sprite types through reflection
                // This is where we'd handle SpriteInformationObject and similar types
                
                // Look for texture-related properties using reflection
                var textureProperty = asset.GetType().GetProperty("Texture");
                var textureField = asset.GetType().GetField("texture");
                
                if (textureProperty != null)
                {
                    Logger.Debug($"🔍 Found Texture property on {assetType}");
                    var textureValue = textureProperty.GetValue(asset);
                    if (textureValue is ITexture2D reflectionTexture)
                    {
                        Logger.Debug($"✅ Successfully got texture from {assetType} via Texture property");
                        var metadata = ExtractTextureWithImprovedProcessing(reflectionTexture);
                        if (metadata != null)
                        {
                            metadata.Name = CleanSpriteName(assetName); // Use the sprite asset name, not texture name
                            metadata.SourceBundle = bundleName;
                        }
                        return metadata;
                    }
                }
                
                if (textureField != null)
                {
                    Logger.Debug($"🔍 Found texture field on {assetType}");
                    var textureValue = textureField.GetValue(asset);
                    if (textureValue is ITexture2D reflectionTexture)
                    {
                        Logger.Debug($"✅ Successfully got texture from {assetType} via texture field");
                        var metadata = ExtractTextureWithImprovedProcessing(reflectionTexture);
                        if (metadata != null)
                        {
                            metadata.Name = CleanSpriteName(assetName); // Use the sprite asset name, not texture name
                            metadata.SourceBundle = bundleName;
                        }
                        return metadata;
                    }
                }
                
                // Look for sprite-related properties
                var spriteProperties = asset.GetType().GetProperties()
                    .Where(p => p.Name.Contains("Sprite", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                    
                foreach (var spriteProp in spriteProperties)
                {
                    Logger.Debug($"🔍 Found sprite property: {spriteProp.Name} on {assetType}");
                    try
                    {
                        var spriteValue = spriteProp.GetValue(asset);
                        if (spriteValue is ISprite reflectionSprite)
                        {
                            Logger.Debug($"✅ Successfully got sprite from {assetType} via {spriteProp.Name}");
                            var metadata = ExtractSpriteWithImprovedProcessing(reflectionSprite);
                            if (metadata != null)
                            {
                                metadata.SourceBundle = bundleName;
                            }
                            return metadata;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"⚠️ Error accessing sprite property {spriteProp.Name}: {ex.Message}");
                    }
                }
                
                Logger.Debug($"❌ Could not extract sprite/texture from {assetType}: {assetName}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Debug($"❌ Error processing potential sprite asset: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Dumps comprehensive asset properties for analysis to understand sprite reference structures
        /// </summary>
        private void DumpAssetPropertiesForAnalysis(IUnityObjectBase asset, string contextName)
        {
            try
            {
                var assetType = asset.GetType();
                var assetName = asset.ToString() ?? "Unknown";
                
                Logger.Info($"🔍 === ASSET PROPERTY DUMP: {contextName} ===");
                Logger.Info($"   Type: {assetType.Name}");
                Logger.Info($"   Name: {assetName}");
                Logger.Info($"   Full Type: {assetType.FullName}");
                
                // Dump all properties
                var properties = assetType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Where(p => p.CanRead)
                    .OrderBy(p => p.Name)
                    .ToList();
                
                Logger.Info($"   Properties ({properties.Count}):");
                foreach (var prop in properties)
                {
                    try
                    {
                        var value = prop.GetValue(asset);
                        var valueStr = FormatPropertyValue(value, prop.PropertyType);
                        Logger.Info($"     {prop.Name} ({prop.PropertyType.Name}): {valueStr}");
                        
                        // If this property contains texture/sprite references, log additional details
                        if (value is IUnityObjectBase refAsset)
                        {
                            Logger.Info($"       -> References: {refAsset.GetType().Name} '{refAsset}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Info($"     {prop.Name} ({prop.PropertyType.Name}): [Error: {ex.Message}]");
                    }
                }
                
                // Dump all fields
                var fields = assetType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .OrderBy(f => f.Name)
                    .ToList();
                
                if (fields.Any())
                {
                    Logger.Info($"   Fields ({fields.Count}):");
                    foreach (var field in fields)
                    {
                        try
                        {
                            var value = field.GetValue(asset);
                            var valueStr = FormatPropertyValue(value, field.FieldType);
                            Logger.Info($"     {field.Name} ({field.FieldType.Name}): {valueStr}");
                            
                            // If this field contains texture/sprite references, log additional details
                            if (value is IUnityObjectBase refAsset)
                            {
                                Logger.Info($"       -> References: {refAsset.GetType().Name} '{refAsset}'");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Info($"     {field.Name} ({field.FieldType.Name}): [Error: {ex.Message}]");
                        }
                    }
                }
                
                // Look specifically for any members that might contain "texture", "sprite", or "image" in their names
                var spriteRelatedMembers = properties.Cast<System.Reflection.MemberInfo>()
                    .Concat(fields.Cast<System.Reflection.MemberInfo>())
                    .Where(m => m.Name.ToLowerInvariant().Contains("texture") ||
                               m.Name.ToLowerInvariant().Contains("sprite") ||
                               m.Name.ToLowerInvariant().Contains("image") ||
                               m.Name.ToLowerInvariant().Contains("icon"))
                    .ToList();
                
                if (spriteRelatedMembers.Any())
                {
                    Logger.Info($"   🎯 Sprite/Texture-related members ({spriteRelatedMembers.Count}):");
                    foreach (var member in spriteRelatedMembers)
                    {
                        Logger.Info($"     🎨 {member.Name} ({member.MemberType})");
                    }
                }
                
                Logger.Info($"🔍 === END ASSET DUMP: {contextName} ===");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error dumping asset properties for {contextName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Formats a property value for readable logging
        /// </summary>
        private string FormatPropertyValue(object? value, Type valueType)
        {
            if (value == null) return "null";
            
            if (value is string str) return $"\"{str}\"";
            if (value is bool b) return b.ToString().ToLower();
            if (valueType.IsPrimitive) return value.ToString() ?? "null";
            
            if (value is IUnityObjectBase unityObj)
            {
                return $"{unityObj.GetType().Name}('{unityObj}')";
            }
            
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var items = enumerable.Cast<object>().Take(5).ToList();
                var preview = string.Join(", ", items.Select(i => i?.ToString() ?? "null"));
                return $"[{preview}{(items.Count == 5 ? "..." : "")}]";
            }
            
            return value.ToString() ?? "null";
        }

        /// <summary>
        /// Consolidates individual orb instances into families with levels
        /// </summary>
        private void ConsolidateOrbsIntoFamilies(UnifiedExtractionResult result, IProgress<string>? progress = null)
        {
            // Temporarily disabled due to compilation issues with BaseId and OrbFamily properties
            progress?.Report($"⚠️ Orb family consolidation temporarily disabled");
            return;
            
            /*try
            {
                progress?.Report($"🔄 Consolidating {result.Orbs.Count} orbs into families...");
                
                // Group orbs by base ID
                var orbGroups = new Dictionary<string, List<AssetRipperOrbExtractor.OrbData>>();
                
                foreach (var orb in result.Orbs.Values)
                {
                    // Use BaseId if available, otherwise derive it
                    string baseId;
                    // TODO: Re-enable when BaseId property is available in OrbData
                    // if (!string.IsNullOrEmpty(orb.BaseId))
                    // {
                    //     baseId = orb.BaseId;
                    // }
                    // else
                    // {
                    // else
                    // {
                        // Fallback to deriving from ID/LocKey
                        var locKey = orb.LocKey ?? orb.Id;
                        baseId = StripLevelMarkers(locKey);
                    }
                    
                    if (!orbGroups.ContainsKey(baseId))
                    {
                        orbGroups[baseId] = new List<AssetRipperOrbExtractor.OrbData>();
                    }
                    orbGroups[baseId].Add(orb);
                }
                
                // Ensure localization is loaded
                var locService = Services.LocalizationService.Instance;
                locService.EnsureLoaded();
                
                // Create families from groups
                foreach (var group in orbGroups)
                {
                    var baseId = group.Key;
                    var orbs = group.Value.OrderBy(o => o.Level ?? 1).ToList();
                    var firstOrb = orbs.First();
                    
                    var family = new AssetRipperOrbExtractor.OrbFamily
                    {
                        Id = baseId,
                        LocKey = firstOrb.LocKey,
                        Name = firstOrb.Name ?? baseId,
                        Description = firstOrb.Description ?? "",
                        RarityValue = orbs.Select(o => o.RarityValue).Where(v => v.HasValue).DefaultIfEmpty().Max(),
                        Rarity = orbs.Select(o => o.Rarity).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)) ?? firstOrb.Rarity,
                        OrbType = orbs.Select(o => o.OrbType).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? firstOrb.OrbType,
                        Levels = new List<AssetRipperOrbExtractor.OrbLevelData>()
                    };
                    
                    // Choose sprite from orbs - prefer the most common sprite across levels, or Level 1's sprite
                    var spriteIds = orbs.Select(o => o.CorrelatedSpriteId).Where(s => !string.IsNullOrEmpty(s)).ToList();
                    if (spriteIds.Any())
                    {
                        // Find most common sprite
                        var mostCommonSprite = spriteIds
                            .GroupBy(s => s)
                            .OrderByDescending(g => g.Count())
                            .First()
                            .Key;
                        
                        family.CorrelatedSpriteId = mostCommonSprite;
                        
                        // Find the orb with this sprite to copy correlation details
                        var orbWithSprite = orbs.FirstOrDefault(o => o.CorrelatedSpriteId == mostCommonSprite);
                        if (orbWithSprite != null)
                        {
                            family.SpriteFilePath = orbWithSprite.SpriteFilePath;
                            family.CorrelationMethod = orbWithSprite.CorrelationMethod;
                            family.CorrelationConfidence = orbWithSprite.CorrelationConfidence;
                        }
                        
                        // Collect alternate sprites
                        family.AlternateSpriteIds = spriteIds.Distinct().Where(s => s != mostCommonSprite).ToList();
                    }
                    
                    // Add level data
                    foreach (var orb in orbs)
                    {
                        family.Levels.Add(new AssetRipperOrbExtractor.OrbLevelData
                        {
                            Level = orb.Level ?? 1,
                            DamagePerPeg = orb.DamagePerPeg,
                            CritDamagePerPeg = orb.CritDamagePerPeg,
                            RawData = orb.RawData ?? new Dictionary<string, object>(),
                            LeafId = orb.Id
                        });
                    }
                    
                    // Apply localization if possible
                    if (!string.IsNullOrWhiteSpace(family.LocKey))
                    {
                        // Try to get display name
                        var displayName = locService.GetTranslation(family.LocKey);
                        if (!string.IsNullOrWhiteSpace(displayName))
                        {
                            family.Name = displayName;
                        }
                        
                        // Try common description key patterns
                        var descriptionKeys = new[]
                        {
                            family.LocKey + "_DESC",
                            family.LocKey + "_Description",
                            family.LocKey + ".Description"
                        };
                        
                        foreach (var descKey in descriptionKeys)
                        {
                            var desc = locService.GetTranslation(descKey);
                            if (!string.IsNullOrWhiteSpace(desc))
                            {
                                family.Description = desc;
                                break;
                            }
                        }
                    }
                    
                    // Fallback to englishDescription from any level if still empty
                    if (string.IsNullOrWhiteSpace(family.Description))
                    {
                        family.Description = orbs
                            .Select(o => o.Description)
                            .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)) ?? "";
                    }
                    
                    result.OrbFamilies[baseId] = family;
                }
                
                progress?.Report($"✅ Consolidated into {result.OrbFamilies.Count} orb families");
                
                // Log statistics
                var avgLevelsPerFamily = result.OrbFamilies.Values.Average(f => f.Levels.Count);
                var familiesWithSprites = result.OrbFamilies.Values.Count(f => !string.IsNullOrEmpty(f.CorrelatedSpriteId));
                Logger.Info($"Orb family statistics: {avgLevelsPerFamily:F1} average levels per family, {familiesWithSprites} families with sprites");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error consolidating orbs into families: {ex.Message}");
                // Don't fail the entire extraction if consolidation fails
                result.OrbFamilies = new Dictionary<string, AssetRipperOrbExtractor.OrbFamily>();
            }
        }
        
        /// <summary>
        /// Strips level markers from an orb ID or locKey
        /// </summary>
        */ // Close the commented ConsolidateOrbsIntoFamilies method
        }
        
        private static string StripLevelMarkers(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            
            var res = s;
            // Strip suffixes like _L1, _L2, _Level1, _Level2, etc.
            res = System.Text.RegularExpressions.Regex.Replace(res, @"(_|\.)?_(L|Lvl|Level)\s*0*(\d+)$", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // Strip trailing digits that might be level indicators
            res = System.Text.RegularExpressions.Regex.Replace(res, @"(\d+)$", string.Empty);
            
            return string.IsNullOrWhiteSpace(res) ? s : res;
        }
        
    }
}

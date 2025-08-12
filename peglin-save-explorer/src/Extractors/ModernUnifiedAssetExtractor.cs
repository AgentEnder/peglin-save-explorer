using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.AssetCreation;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Classes.ClassID_212;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Extensions;
using peglin_save_explorer.Data;
using peglin_save_explorer.UI;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Core;
using peglin_save_explorer.Services;
using peglin_save_explorer.Extractors.Models;
using peglin_save_explorer.Extractors.Services;

namespace peglin_save_explorer.Extractors
{
    /// <summary>
    /// Modernized unified extractor that orchestrates asset extraction using focused service classes
    /// </summary>
    public class ModernUnifiedAssetExtractor
    {
        private readonly ConsoleSession? _session;
        private readonly EnumExtractor _enumExtractor = new();
        private readonly EntityDetectionService _entityDetectionService = new();
        private readonly EntityExtractionService _entityExtractionService = new();
        private readonly SpriteProcessingService _spriteProcessingService = new();
        private readonly AssetProcessingService _assetProcessingService = new();
        private readonly LocalizationProcessingService _localizationProcessingService = new();

        private readonly LocalizationService _localizationService = LocalizationService.Instance;
        private Dictionary<string, string>? _globalRelicParameters;
        private readonly Dictionary<string, IUnityAssetBase> _orbSpriteRefs = new(); // gameObject_PathID -> sprite asset reference

        public ModernUnifiedAssetExtractor(ConsoleSession? session = null)
        {
            _session = session;
        }

        /// <summary>
        /// Extracts all assets from a Peglin installation in a single pass with full cross-bundle reference resolution
        /// </summary>
        public UnifiedExtractionResult ExtractAllAssetsFromPeglinInstall(string peglinPath, IProgress<string>? progress = null)
        {
            var result = new UnifiedExtractionResult();

            try
            {
                _enumExtractor.LoadAssembly(peglinPath);
                _localizationService.EnsureLoaded();

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
                var gameBundle = LoadGameBundle(allFiles, progress);

                // Extract global relic parameters first
                ExtractGlobalRelicParameters(gameBundle, progress);

                // Process all collections
                ProcessAllCollections(gameBundle, result, progress);

                // Consolidate orbs into families
                _localizationProcessingService.ConsolidateOrbsIntoFamilies(result.Orbs, result.OrbFamilies);

                ReportResults(result, progress);
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during unified extraction: {ex.Message}");
                return result;
            }
        }

        private GameBundle LoadGameBundle(string[] bundlePaths, IProgress<string>? progress)
        {
            try
            {
                var assemblyManager = new BaseManager(s => { });
                var assetFactory = new GameAssetFactory(assemblyManager);

                progress?.Report($"   🔄 Loading {bundlePaths.Length} bundles with full dependency resolution...");
                return GameBundle.FromPaths(bundlePaths, assetFactory);
            }
            catch (ArgumentException ex) when (ex.Message.Contains("No SerializedFile found"))
            {
                Logger.Warning("Some bundles were not valid Unity bundles and were skipped");
                throw;
            }
        }

        private void ExtractGlobalRelicParameters(GameBundle gameBundle, IProgress<string>? progress)
        {
            progress?.Report("🔤 Extracting global relic parameters...");

            var collections = gameBundle.FetchAssetCollections().ToList();
            var totalGameObjects = 0;
            var totalMonoBehaviours = 0;
            var totalLocalizationParamsManagerChecks = 0;

            foreach (var collection in collections)
            {
                try
                {
                    foreach (var gameObject in collection.OfType<IGameObject>())
                    {
                        totalGameObjects++;
                        var components = gameObject.FetchComponents();
                        if (components == null) continue;

                        foreach (var componentPtr in components)
                        {
                            try
                            {
                                var component = componentPtr.TryGetAsset(collection);
                                if (component is IMonoBehaviour monoBehaviour)
                                {
                                    totalMonoBehaviours++;
                                    var structure = monoBehaviour.LoadStructure();
                                    if (structure == null) continue;

                                    var data = _assetProcessingService.ConvertStructureToDict(structure, collection, out _);

                                    // Debug: Log what keys we're seeing
                                    if (data.ContainsKey("_Params") || data.ContainsKey("_IsGlobalManager"))
                                    {
                                        // var keys = string.Join(", ", data.Keys.Take(10));
                                        // Logger.Debug($"🔍 Potential LocalizationParamsManager found with keys: {keys}");
                                        totalLocalizationParamsManagerChecks++;
                                    }

                                    if (_entityDetectionService.IsLocalizationParamsManager(data))
                                    {
                                        // Logger.Debug($"✅ LocalizationParamsManager detected! Extracting parameters...");
                                        var parameters = _localizationProcessingService.ExtractLocalizationParams(data);
                                        if (parameters != null && parameters.Count > 0)
                                        {
                                            // Accumulate parameters from all LocalizationParamsManager objects
                                            if (_globalRelicParameters == null)
                                            {
                                                _globalRelicParameters = new Dictionary<string, string>(parameters);
                                            }
                                            else
                                            {
                                                foreach (var kvp in parameters)
                                                {
                                                    _globalRelicParameters[kvp.Key] = kvp.Value;
                                                }
                                            }
                                            // Logger.Debug($"🔤 New parameters: {string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"))}");
                                        }
                                        else
                                        {
                                            // Logger.Debug($"⚠️ LocalizationParamsManager detected but no parameters extracted");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Debug($"⚠️ Error processing component for global parameters: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Error extracting global parameters from collection {collection.Name}: {ex.Message}");
                }
            }

            Logger.Warning($"🔤 Global relic parameter extraction completed. Found {_globalRelicParameters?.Count ?? 0} total parameters from {totalLocalizationParamsManagerChecks} LocalizationParamsManager objects. Checked {totalGameObjects} GameObjects, {totalMonoBehaviours} MonoBehaviours.");

            if (_globalRelicParameters?.Count > 0)
            {
                Logger.Info($"🔤 Final parameter set: {string.Join(", ", _globalRelicParameters.Take(10).Select(p => $"{p.Key}={p.Value}"))}");
            }
        }

        private void ProcessAllCollections(GameBundle gameBundle, UnifiedExtractionResult result, IProgress<string>? progress)
        {
            var collections = gameBundle.FetchAssetCollections().ToList();
            var processedCollections = 0;

            // Phase 1: Collect sprite references for each entity type
            var relicSpriteRefs = new Dictionary<string, IUnityAssetBase>();
            var enemySpriteRefs = new Dictionary<string, IUnityAssetBase>();
            var orbSpriteRefs = new Dictionary<string, IUnityAssetBase>();

            progress?.Report($"📊 Processing {collections.Count} asset collections...");

            foreach (var collection in collections)
            {
                processedCollections++;
                progress?.Report($"   📁 Processing collection {processedCollections}/{collections.Count}: {collection.Name}");

                try
                {
                    ProcessCollection(collection, gameBundle, result, relicSpriteRefs, enemySpriteRefs, orbSpriteRefs);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Error processing collection {collection.Name}: {ex.Message}");
                }
            }

            // Phase 2: Resolve collected sprite references and correlate with entities
            progress?.Report("🔗 Correlating entities with sprites...");
            CorrelateEntitiesWithSprites(gameBundle, result, relicSpriteRefs, enemySpriteRefs, orbSpriteRefs, progress);
        }

        private void ProcessCollection(AssetCollection collection, GameBundle gameBundle, UnifiedExtractionResult result,
            Dictionary<string, IUnityAssetBase> relicSpriteRefs, Dictionary<string, IUnityAssetBase> enemySpriteRefs, Dictionary<string, IUnityAssetBase> orbSpriteRefs)
        {
            var componentMap = new Dictionary<long, IMonoBehaviour>();

            // Process MonoBehaviours for entity data
            foreach (var monoBehaviour in collection.OfType<IMonoBehaviour>())
            {
                try
                {
                    // Add to component map for GameObject processing
                    componentMap[monoBehaviour.PathID] = monoBehaviour;

                    // Debug: Log what's being added to componentMap
                    var mbName = monoBehaviour.GetBestName()?.ToLowerInvariant() ?? "";
                    // Component mapping for orb-related MonoBehaviours

                    var structure = monoBehaviour.LoadStructure();
                    if (structure == null) continue;

                    var data = _assetProcessingService.ConvertStructureToDict(structure, collection, out var spriteReference);
                    var assetName = monoBehaviour.GetBestName();

                    ProcessMonoBehaviourData(assetName, data, spriteReference, result, relicSpriteRefs, enemySpriteRefs, orbSpriteRefs, monoBehaviour, collection);
                }
                catch (Exception ex)
                {
                    Logger.Debug($"⚠️ Error processing MonoBehaviour {monoBehaviour.GetBestName()}: {ex.Message}");
                }
            }

            // Process GameObjects for orb extraction
            var gameObjects = collection.OfType<IGameObject>().ToList();
            Logger.Info($"Processing collection {collection.Name}: {gameObjects.Count} GameObjects, {componentMap.Count} MonoBehaviours");

            foreach (var gameObject in gameObjects)
            {
                try
                {
                    ProcessGameObjectForOrbs(gameObject, componentMap, result, orbSpriteRefs, collection);
                }
                catch (Exception ex)
                {
                    Logger.Debug($"⚠️ Error processing GameObject {gameObject.GetBestName()}: {ex.Message}");
                }
            }

            // Process standalone sprites only for fallback correlation
            // This should be much more selective now
            ProcessStandaloneSprites(collection, result);
        }

        private void ProcessMonoBehaviourData(string assetName, Dictionary<string, object> data, IUnityAssetBase? spriteReference,
            UnifiedExtractionResult result, Dictionary<string, IUnityAssetBase> relicSpriteRefs, Dictionary<string, IUnityAssetBase> enemySpriteRefs, Dictionary<string, IUnityAssetBase> orbSpriteRefs, IMonoBehaviour? monoBehaviour = null, AssetCollection? collection = null)
        {
            // Skip LocalizationParamsManager objects - they're handled separately in ExtractGlobalRelicParameters
            if (_entityDetectionService.IsLocalizationParamsManager(data))
            {
                Logger.Debug($"⏭️ Skipping LocalizationParamsManager in main processing: {assetName}");
                return;
            }

            // Extract entities based on data type
            if (_entityDetectionService.IsRelicData(data))
            {
                var relic = _entityExtractionService.ExtractRelic(assetName, data, _globalRelicParameters);
                if (relic != null && !string.IsNullOrEmpty(relic.Id))
                {
                    result.Relics[relic.Id] = relic;

                    // Store sprite reference for later correlation
                    if (spriteReference != null)
                    {
                        relicSpriteRefs[relic.Id] = spriteReference;
                        Logger.Debug($"🔮 Found sprite reference for relic {relic.Id}: {spriteReference.GetType().Name}");
                    }
                    else
                    {
                        Logger.Debug($"🔮 No direct sprite reference found for relic {relic.Id}");
                    }
                }
            }
            else if (_entityDetectionService.IsEnemyData(data))
            {
                var enemy = _entityExtractionService.ExtractEnemy(assetName, data);
                if (enemy != null && !string.IsNullOrEmpty(enemy.Id))
                {
                    result.Enemies[enemy.Id] = enemy;

                    // Store sprite reference for later correlation
                    if (spriteReference != null)
                    {
                        enemySpriteRefs[enemy.Id] = spriteReference;
                        Logger.Debug($"👹 Found sprite reference for enemy {enemy.Id}: {spriteReference.GetType().Name}");
                    }
                    else
                    {
                        Logger.Debug($"👹 No direct sprite reference found for enemy {enemy.Id}");
                    }
                }
            }
            else if (_entityDetectionService.IsOrbData(data))
            {
                // Don't create orb entities from MonoBehaviours directly, but DO allow them to be 
                // found by GameObjects for aggregation
                Logger.Debug($"📝 Found orb MonoBehaviour {assetName} - will be aggregated through GameObject path");
                // Note: The MonoBehaviour was already added to componentMap earlier
            }
            else if (_entityDetectionService.IsPachinkoBallData(data))
            {
                Logger.Debug($"Found PachinkoBall MonoBehaviour: {assetName}");

                // Get GameObject PathID from the MonoBehaviour's GameObject property
                string? gameObjectKey = null;
                if (monoBehaviour?.GameObject != null && !monoBehaviour.GameObject.IsNull())
                {
                    gameObjectKey = $"gameObject_{monoBehaviour.GameObject.PathID}";
                    Logger.Debug($"PachinkoBall belongs to GameObject with PathID: {monoBehaviour.GameObject.PathID}");
                }
                else
                {
                    Logger.Warning("PachinkoBall MonoBehaviour GameObject property is null or empty");
                }

                // Handle PachinkoBall sprite references for orbs
                if (data.ContainsKey("_renderer") && gameObjectKey != null && collection != null)
                {
                    // Process PachinkoBall renderer for sprite correlation
                    var rendererField = data["_renderer"];

                    // Handle both dictionary format (backup approach) and direct PPtr object (current approach)
                    if (rendererField is Dictionary<string, object> rendererDict && rendererDict.ContainsKey("pathId") && rendererDict["pathId"] != null)
                    {
                        // Dictionary format (backup approach)
                        var pathIdStr = rendererDict["pathId"].ToString();
                        if (long.TryParse(pathIdStr, out var pathId))
                        {
                            Logger.Debug($"🎯 Following _renderer PPtr dict to pathId: {pathId}");
                            var spriteRenderer = collection.TryGetAsset(pathId);
                            if (spriteRenderer != null)
                            {
                                Logger.Debug($"✅ Resolved SpriteRenderer from dict: {spriteRenderer.GetType().Name}");
                                orbSpriteRefs[gameObjectKey] = spriteRenderer;
                                // Sprite renderer stored for correlation
                            }
                            else
                            {
                                Logger.Debug($"❌ Failed to resolve SpriteRenderer with PathID: {pathId}");
                            }
                        }
                    }
                    else if (rendererField is IUnityAssetBase ptrObject)
                    {
                        // Direct PPtr object format (current approach) - use reflection to get PathID
                        try
                        {
                            var pathIdProp = ptrObject.GetType().GetProperty("PathID");
                            if (pathIdProp != null)
                            {
                                var pathIdValue = pathIdProp.GetValue(ptrObject);
                                if (pathIdValue is long pathId)
                                {
                                    Logger.Debug($"🎯 _renderer is direct PPtr object: {ptrObject.GetType().Name}, PathID: {pathId}");

                                    if (pathId != 0) // PathID of 0 usually means null reference
                                    {
                                        // Try to resolve the SpriteRenderer using the PathID
                                        var spriteRenderer = collection.TryGetAsset(pathId);
                                        if (spriteRenderer != null)
                                        {
                                            Logger.Debug($"✅ Resolved SpriteRenderer from PPtr: {spriteRenderer.GetType().Name}");
                                            orbSpriteRefs[gameObjectKey] = spriteRenderer;
                                            // Sprite renderer stored for correlation
                                        }
                                        else
                                        {
                                            Logger.Debug($"❌ Failed to resolve SpriteRenderer with PathID: {pathId}");
                                        }
                                    }
                                    else
                                    {
                                        Logger.Debug($"❌ PPtr has PathID 0 (null reference)");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug($"❌ Error accessing PathID: {ex.Message}");
                        }
                    }
                    else
                    {
                        Logger.Debug($"🎱 Unexpected _renderer field format - Type: {rendererField?.GetType().Name}");
                        Logger.Debug($"🎱 Is IUnityObjectBase: {rendererField is IUnityObjectBase}");
                        Logger.Debug($"🎱 Is IUnityAssetBase: {rendererField is IUnityAssetBase}");

                        // Try to get PathID through reflection if it has one
                        try
                        {
                            var pathIdProp = rendererField?.GetType().GetProperty("PathID");
                            if (pathIdProp != null)
                            {
                                var pathIdValue = pathIdProp.GetValue(rendererField);
                                Logger.Debug($"🎱 PathID via reflection: {pathIdValue}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug($"🎱 Reflection error: {ex.Message}");
                        }
                    }
                }
            }
        }

        private void ProcessGameObjectForOrbs(IGameObject gameObject, Dictionary<long, IMonoBehaviour> componentMap,
            UnifiedExtractionResult result, Dictionary<string, IUnityAssetBase> orbSpriteRefs, AssetCollection collection)
        {
            var gameObjectData = new GameObjectData
            {
                Id = gameObject.PathID.ToString(),
                Name = gameObject.GetBestName(),
                PathID = gameObject.PathID
            };

            // Debug: Log all GameObjects to understand what we're processing
            var goName = gameObject.GetBestName()?.ToLowerInvariant() ?? "";
            if (goName.Contains("orb") || goName.Contains("lvl") || goName.Contains("debuff"))
            {
                Logger.Debug($"Processing GameObject: {gameObject.GetBestName()} (PathID: {gameObject.PathID})");

                // Check what components this GameObject has
                var components = gameObject.FetchComponents();
                if (components != null)
                {
                    // Process GameObject components
                    int compCount = 0;
                    foreach (var comp in components)
                    {
                        if (compCount++ >= 5) break; // Only show first 5 components
                        var resolvedComp = comp.TryGetAsset(collection);
                        if (resolvedComp != null)
                        {
                            // Process component data
                            if (componentMap.TryGetValue(resolvedComp.PathID, out var monoBehaviour))
                            {
                                // MonoBehaviour data available
                            }
                        }
                    }
                }
            }

            _assetProcessingService.ExtractGameObjectComponents(gameObject, componentMap, gameObjectData, collection);

            // Debug: Check what was extracted
            if (goName.Contains("orb") || goName.Contains("lvl") || goName.Contains("debuff"))
            {
                if (gameObjectData.RawData is Dictionary<string, object> rawData)
                {
                    // RawData extracted for processing
                    if (rawData.TryGetValue("ComponentData", out var compData) && compData is Dictionary<string, object> cd)
                    {
                        // ComponentData available
                    }
                }
            }

            if (_entityDetectionService.IsOrbGameObject(gameObjectData))
            {
                Logger.Debug($"Found orb GameObject: {gameObject.GetBestName()}");
                var orbData = ConvertGameObjectToOrbData(gameObjectData);
                if (orbData != null && !string.IsNullOrEmpty(orbData.Id))
                {
                    // Process orb sprite correlation
                    result.Orbs[orbData.Id] = orbData;

                    // Try to correlate with PachinkoBall sprite using GameObject PathID
                    // Try multiple possible keys since the correlation might use different PathIDs
                    var possibleKeys = new[] {
                        $"gameObject_{gameObject.PathID}",
                        $"monoBehaviour_{gameObject.PathID}"
                    };

                    string? matchedKey = null;
                    foreach (var key in possibleKeys)
                    {
                        if (orbSpriteRefs.ContainsKey(key))
                        {
                            matchedKey = key;
                            break;
                        }
                    }

                    // Attempting sprite correlation with available keys

                    if (matchedKey != null)
                    {
                        Logger.Debug($"Found PachinkoBall sprite for orb {orbData.Id}");
                        var spriteRenderer = orbSpriteRefs[matchedKey];

                        // Extract the actual sprite from the SpriteRenderer (following backup approach)
                        IUnityAssetBase? actualSprite = null;
                        if (spriteRenderer.GetType().Name.Contains("SpriteRenderer"))
                        {
                            // Extract sprite from SpriteRenderer

                            // Try to access the sprite via the ISpriteRenderer interface if available
                            if (spriteRenderer is ISpriteRenderer renderer)
                            {
                                // Use reflection to find the m_Sprite property since we don't know the exact property name
                                var spriteProperty = renderer.GetType().GetProperty("M_Sprite")
                                    ?? renderer.GetType().GetProperty("Sprite_C212P")
                                    ?? renderer.GetType().GetProperty("Sprite");

                                if (spriteProperty != null)
                                {
                                    var spritePPtr = spriteProperty.GetValue(renderer);
                                    if (spritePPtr != null)
                                    {
                                        // The sprite property returns a PPtr, we need to resolve it
                                        actualSprite = ResolvePPtr(spritePPtr, collection);
                                        if (actualSprite != null)
                                        {
                                            // Successfully extracted sprite from SpriteRenderer
                                        }
                                        else
                                        {
                                            Logger.Debug("Could not resolve sprite PPtr from SpriteRenderer");
                                        }
                                    }
                                    else
                                    {
                                        Logger.Debug("Sprite property was null on SpriteRenderer");
                                    }
                                }
                                else
                                {
                                    Logger.Debug($"No m_Sprite property found on SpriteRenderer type {renderer.GetType().Name}");
                                }
                            }
                            else
                            {
                                Logger.Debug($"SpriteRenderer is not ISpriteRenderer: {spriteRenderer.GetType().Name}");
                            }
                        }
                        else
                        {
                            // If it's already a direct sprite, use it as-is
                            actualSprite = spriteRenderer;
                            Logger.Debug($"🎯 Using direct sprite reference: {actualSprite.GetType().Name}");
                        }

                        if (actualSprite != null)
                        {
                            // Resolve sprite metadata

                            var spriteMetadata = ResolveSpriteWithCollection(actualSprite, collection, SpriteCacheManager.SpriteType.Orb);
                            if (spriteMetadata != null)
                            {
                                result.Sprites[spriteMetadata.Id] = spriteMetadata;
                                result.OrbSpriteCorrelations[orbData.Id] = spriteMetadata.Id;

                                orbData.CorrelatedSpriteId = spriteMetadata.Id;
                                orbData.SpriteFilePath = spriteMetadata.FilePath;
                                orbData.CorrelationMethod = "PachinkoBall GameObject PathID";
                                orbData.CorrelationConfidence = 1.0f;

                                Logger.Info($"Correlated orb {orbData.Id} with sprite {spriteMetadata.Id}");
                            }
                            else
                            {
                                Logger.Warning($"Failed to resolve sprite metadata for orb {orbData.Id}");
                            }
                        }
                        else
                        {
                            Logger.Warning($"Failed to extract sprite from SpriteRenderer for orb {orbData.Id}");
                        }
                    }
                    else
                    {
                        Logger.Debug($"No PachinkoBall sprite found for orb: {orbData.Id}");

                        // Fallback: Try to find sprite reference directly
                        var spriteReference = FindGameObjectSpriteReference(gameObject, componentMap, collection);
                        if (spriteReference != null)
                        {
                            // Fallback sprite resolution

                            var spriteMetadata = ResolveSpriteWithCollection(spriteReference, collection, SpriteCacheManager.SpriteType.Orb);
                            if (spriteMetadata != null)
                            {
                                result.Sprites[spriteMetadata.Id] = spriteMetadata;
                                result.OrbSpriteCorrelations[orbData.Id] = spriteMetadata.Id;

                                orbData.CorrelatedSpriteId = spriteMetadata.Id;
                                orbData.SpriteFilePath = spriteMetadata.FilePath;
                                orbData.CorrelationMethod = "Direct GameObject Reference";
                                orbData.CorrelationConfidence = 0.8f;

                                Logger.Debug($"🔍 Fallback correlation for orb {orbData.Id} with direct sprite {spriteMetadata.Id}");
                            }
                        }
                    }
                }
            }
        }

        private void ProcessStandaloneSprites(AssetCollection collection, UnifiedExtractionResult result)
        {
            foreach (var sprite in collection.OfType<ISprite>())
            {
                try
                {
                    // Generate PathID-based sprite ID to check if this sprite was already processed by correlation
                    var pathId = sprite.PathID;
                    var pathIdBasedSpriteId = $"sprite_{pathId}";
                    
                    // Always extract the sprite to get PNG file created and metadata
                    var proposedSpriteMetadata = _spriteProcessingService.ExtractSpriteWithImprovedProcessing(sprite, null);
                    if (proposedSpriteMetadata == null)
                    {
                        Logger.Debug($"⚠️ Failed to extract sprite {sprite.GetBestName()}");
                        continue;
                    }

                    // Skip metadata creation if this sprite was already processed by the correlation system
                    if (result.Sprites.ContainsKey(pathIdBasedSpriteId))
                    {
                        Logger.Debug($"⏭️ Skipping sprite metadata {sprite.GetBestName()} - already processed by correlation system (PNG file created)");
                        continue;
                    }
                    
                    // Check if any existing sprite already points to this file path
                    var existingSprite = result.Sprites.Values.FirstOrDefault(s => s.FilePath == proposedSpriteMetadata.FilePath);
                    if (existingSprite != null)
                    {
                        Logger.Debug($"⏭️ Skipping sprite metadata {sprite.GetBestName()} - file path {proposedSpriteMetadata.FilePath} already exists (existing: {existingSprite.Id}, PNG file created)");
                        continue;
                    }
                    
                    Logger.Debug($"📎 Processing standalone sprite: {sprite.GetBestName()} -> {proposedSpriteMetadata.Id}");
                    result.Sprites[proposedSpriteMetadata.Id] = proposedSpriteMetadata;
                }
                catch (Exception ex)
                {
                    Logger.Debug($"⚠️ Error processing standalone sprite {sprite.GetBestName()}: {ex.Message}");
                }
            }
        }

        private OrbData? ConvertGameObjectToOrbData(GameObjectData gameObjectData)
        {
            try
            {
                // Create a clean ID from the GameObject name (e.g., "DebuffOrb-Lvl1" -> "debufforb_lvl1")
                var cleanId = gameObjectData.Name?.ToLowerInvariant()
                    .Replace("-", "_")
                    .Replace(" ", "_") ?? gameObjectData.Id;

                var orb = new OrbData
                {
                    Id = cleanId,
                    Name = gameObjectData.Name,
                    RawData = gameObjectData.RawData
                };

                // Check if we have extracted component data with orb information
                if (gameObjectData.RawData is Dictionary<string, object> rawData &&
                    rawData.TryGetValue("ComponentData", out var componentDataObj) &&
                    componentDataObj is Dictionary<string, object> componentData)
                {
                    // Use the extracted orb data from the MonoBehaviour component
                    if (componentData.TryGetValue("HasOrbData", out var hasOrbData) &&
                        hasOrbData is bool && (bool)hasOrbData)
                    {
                        // Extract all the orb fields we found
                        if (componentData.TryGetValue("LocKey", out var locKey))
                            orb.LocKey = locKey?.ToString();

                        // Get localized name using LocKey
                        if (!string.IsNullOrEmpty(orb.LocKey))
                        {
                            var nameKey = $"{orb.LocKey}_name";
                            var localizedName = _localizationService.GetTranslation($"Orbs/{nameKey}");
                            Logger.Debug($"Looking up orb name: Orbs/{nameKey} -> '{localizedName}'");
                            if (!string.IsNullOrWhiteSpace(localizedName))
                            {
                                orb.Name = localizedName;
                                Logger.Debug($"Updated orb name from '{gameObjectData.Name}' to '{localizedName}' using LocKey '{orb.LocKey}'");
                            }
                            else
                            {
                                Logger.Debug($"No localization found for Orbs/{nameKey}, keeping original name '{gameObjectData.Name}'");
                            }
                        }
                        if (componentData.TryGetValue("DamagePerPeg", out var damage))
                            orb.DamagePerPeg = damage as float?;
                        if (componentData.TryGetValue("CritDamagePerPeg", out var critDamage))
                            orb.CritDamagePerPeg = critDamage as float?;

                        // Extract and translate locDescStrings from componentData.OrbComponent.locDescStrings
                        if (componentData.TryGetValue("OrbComponent", out var orbComponentObjForStrings) &&
                            orbComponentObjForStrings is Dictionary<string, object> orbComponentForStrings &&
                            orbComponentForStrings.TryGetValue("locDescStrings", out var locDescStringsObj) &&
                            locDescStringsObj is IEnumerable<object> locDescStringsArray)
                        {
                            var locDescStrings = locDescStringsArray.Select(x => x?.ToString()).Where(x => !string.IsNullOrEmpty(x)).Cast<string>().ToList();
                            if (locDescStrings.Count > 0)
                            {
                                Logger.Debug($"🔍 Found {locDescStrings.Count} locDescStrings for orb {orb.Name}: [{string.Join(", ", locDescStrings)}]");
                                try
                                {
                                    var translatedStrings = new List<string>();
                                    foreach (var descKey in locDescStrings)
                                    {
                                        var translation = _localizationService.GetTranslation($"Orbs/{descKey}");
                                        Logger.Debug($"🔤 Translation for '{descKey}': '{translation}'");
                                        if (!string.IsNullOrWhiteSpace(translation))
                                        {
                                            translatedStrings.Add(translation);
                                        }
                                    }

                                    // Apply token resolution if we have LocalizationParams
                                    if (componentData.TryGetValue("LocalizationParams", out var locParamsObj) &&
                                        locParamsObj is Dictionary<string, string> locParams)
                                    {
                                        Logger.Debug($"🔤 Applying token resolution to {translatedStrings.Count} translated strings using {locParams.Count} parameters");
                                        var resolvedStrings = _localizationProcessingService.ResolveTokens(translatedStrings, locParams);

                                        // Log any resolved strings
                                        for (int i = 0; i < translatedStrings.Count; i++)
                                        {
                                            if (i < resolvedStrings.Count && resolvedStrings[i] != translatedStrings[i])
                                            {
                                                Logger.Debug($"🔤 Resolved translation[{i}]: '{translatedStrings[i]}' -> '{resolvedStrings[i]}'");
                                            }
                                        }

                                        orb.DescriptionStrings = resolvedStrings;
                                    }
                                    else
                                    {
                                        Logger.Debug($"⚠️ No localization parameters found for orb {orb.Name}");
                                        orb.DescriptionStrings = translatedStrings;
                                    }
                                    Logger.Debug($"✅ Translated {translatedStrings.Count} description strings for orb {orb.Name}");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Debug($"⚠️ Failed to get localized descriptions from locDescStrings for orb {orb.Name}: {ex.Message}");
                                }
                            }
                            else
                            {
                                Logger.Debug($"⏭️ No valid locDescStrings found for orb {orb.Name}");
                            }
                        }

                        // Set default values for other fields
                        orb.OrbType = "ATTACK";
                        orb.RarityValue = 1;
                        orb.Rarity = "COMMON";

                        Logger.Debug($"✅ Successfully extracted orb data from GameObject {gameObjectData.Name}: LocKey={orb.LocKey}, Damage={orb.DamagePerPeg}");
                        Logger.Debug($"🔤 Final DescriptionStrings for {orb.Name}: [{string.Join(", ", orb.DescriptionStrings ?? new List<string>())}]");
                        return orb;
                    }
                }

                // Fallback: Try to extract from component properties directly
                Logger.Debug($"⚠️ No HasOrbData found in ComponentData for {gameObjectData.Name}, trying fallback extraction");
                var orbComponent = gameObjectData.Components
                    .FirstOrDefault(c => c.Type.Contains("Orb") || c.Properties.ContainsKey("DamagePerPeg"));

                if (orbComponent != null)
                {
                    // Check if we have localization parameters stored in the GameObject's RawData
                    Dictionary<string, string>? localizationParams = null;
                    if (gameObjectData.RawData is Dictionary<string, object> rawDataFallback &&
                        rawDataFallback.TryGetValue("ComponentData", out var componentDataObjFallback) &&
                        componentDataObjFallback is Dictionary<string, object> componentDataFallback &&
                        componentDataFallback.TryGetValue("LocalizationParams", out var locParamsObjFallback) &&
                        locParamsObjFallback is Dictionary<string, string> locParamsFallback)
                    {
                        localizationParams = locParamsFallback;
                        Logger.Debug($"🔤 Using {locParamsFallback.Count} GameObject-level localization parameters for orb {gameObjectData.Name} (fallback)");
                    }
                    else
                    {
                        Logger.Debug($"⚠️ No localization parameters found in fallback path for orb {gameObjectData.Name}");
                    }

                    Logger.Debug($"⚠️ Using fallback EntityExtractionService.ExtractOrb for {gameObjectData.Name}");
                    return _entityExtractionService.ExtractOrb(gameObjectData.Name, orbComponent.Properties, localizationParams);
                }

                Logger.Debug($"⚠️ No orb components found for GameObject {gameObjectData.Name}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Debug($"⚠️ Error converting GameObject to OrbData: {ex.Message}");
                return null;
            }
        }

        private IUnityAssetBase? FindGameObjectSpriteReference(IGameObject gameObject, Dictionary<long, IMonoBehaviour> componentMap, AssetCollection collection)
        {
            // This is a simplified version - the full implementation would trace through PachinkoBall components
            var components = gameObject.FetchComponents();
            if (components == null) return null;

            foreach (var componentPtr in components)
            {
                var component = componentPtr.TryGetAsset(collection);
                if (component is IMonoBehaviour monoBehaviour)
                {
                    var structure = monoBehaviour.LoadStructure();
                    if (structure == null) continue;

                    var data = _assetProcessingService.ConvertStructureToDict(structure, collection, out var spriteRef);
                    if (spriteRef != null)
                    {
                        return spriteRef;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Phase 2: Correlate collected sprite references with entities
        /// </summary>
        private void CorrelateEntitiesWithSprites(GameBundle gameBundle, UnifiedExtractionResult result,
            Dictionary<string, IUnityAssetBase> relicSpriteRefs, Dictionary<string, IUnityAssetBase> enemySpriteRefs,
            Dictionary<string, IUnityAssetBase> orbSpriteRefs, IProgress<string>? progress)
        {
            // Correlate each entity type with their sprites
            CorrelateEntityTypeWithSprites(relicSpriteRefs, result.Relics, result.RelicSpriteCorrelations,
                result.Sprites, gameBundle, SpriteCacheManager.SpriteType.Relic, "relic");

            CorrelateEntityTypeWithSprites(enemySpriteRefs, result.Enemies, result.EnemySpriteCorrelations,
                result.Sprites, gameBundle, SpriteCacheManager.SpriteType.Enemy, "enemy");

            CorrelateEntityTypeWithSprites(orbSpriteRefs, result.Orbs, result.OrbSpriteCorrelations,
                result.Sprites, gameBundle, SpriteCacheManager.SpriteType.Orb, "orb");

            var totalCorrelations = result.RelicSpriteCorrelations.Count + result.EnemySpriteCorrelations.Count + result.OrbSpriteCorrelations.Count;
            progress?.Report($"🔗 Successfully correlated {totalCorrelations} entities with sprites");
        }

        /// <summary>
        /// Generic method to correlate a specific entity type with its sprites
        /// </summary>
        private void CorrelateEntityTypeWithSprites<T>(
            Dictionary<string, IUnityAssetBase> spriteRefs,
            Dictionary<string, T> entities,
            Dictionary<string, string> correlations,
            Dictionary<string, SpriteCacheManager.SpriteMetadata> sprites,
            GameBundle gameBundle,
            SpriteCacheManager.SpriteType spriteType,
            string entityTypeName) where T : class
        {
            foreach (var kvp in spriteRefs)
            {
                var entityId = kvp.Key;
                var spriteRef = kvp.Value;

                try
                {
                    var spriteMetadata = _spriteProcessingService.ResolveSprite(spriteRef, gameBundle, spriteType);
                    if (spriteMetadata != null)
                    {
                        sprites[spriteMetadata.Id] = spriteMetadata;
                        correlations[entityId] = spriteMetadata.Id;

                        // Update entity with correlated sprite ID and file path using reflection
                        if (entities.TryGetValue(entityId, out var entity))
                        {
                            var correlatedSpriteIdProperty = entity.GetType().GetProperty("CorrelatedSpriteId");
                            var spriteFilePathProperty = entity.GetType().GetProperty("SpriteFilePath");
                            var correlationMethodProperty = entity.GetType().GetProperty("CorrelationMethod");
                            var correlationConfidenceProperty = entity.GetType().GetProperty("CorrelationConfidence");

                            correlatedSpriteIdProperty?.SetValue(entity, spriteMetadata.Id);
                            spriteFilePathProperty?.SetValue(entity, spriteMetadata.FilePath);
                            correlationMethodProperty?.SetValue(entity, "Unity Asset Reference");
                            correlationConfidenceProperty?.SetValue(entity, 1.0f);
                        }

                        Logger.Debug($"🔗 Correlated {entityTypeName} {entityId} with sprite {spriteMetadata.Id}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"⚠️ Error correlating {entityTypeName} {entityId} with sprite: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Resolves a PPtr reference to an actual asset
        /// </summary>
        private IUnityAssetBase? ResolvePPtr(object pptr, AssetCollection collection)
        {
            try
            {
                // Check if it's already an IUnityAssetBase
                if (pptr is IUnityAssetBase asset)
                {
                    return asset;
                }

                // Check if it has a TryGetAsset method (for PPtr types)
                var tryGetMethod = pptr.GetType().GetMethod("TryGetAsset");
                if (tryGetMethod != null)
                {
                    var result = tryGetMethod.Invoke(pptr, new object[] { collection });
                    return result as IUnityAssetBase;
                }

                // Try to get PathID through reflection and resolve manually
                var pathIdProp = pptr.GetType().GetProperty("PathID");
                if (pathIdProp != null)
                {
                    var pathIdValue = pathIdProp.GetValue(pptr);
                    if (pathIdValue is long pathId && pathId != 0)
                    {
                        return collection.TryGetAsset(pathId);
                    }
                }

                Logger.Debug($"❌ Could not resolve PPtr of type {pptr.GetType().Name}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Debug($"❌ Error resolving PPtr: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resolves sprite metadata from a sprite reference using a specific AssetCollection
        /// This version properly checks the existing sprite cache and fixes file paths
        /// </summary>
        private SpriteCacheManager.SpriteMetadata? ResolveSpriteWithCollection(IUnityAssetBase spriteRef, AssetCollection collection, SpriteCacheManager.SpriteType? forcedSpriteType = null)
        {
            try
            {
                Logger.Debug($"ResolveSpriteWithCollection: sprite type {spriteRef.GetType().Name}");

                // Get PathID from sprite reference
                long pathId = 0;
                try
                {
                    var pathIdProp = spriteRef.GetType().GetProperty("PathID");
                    if (pathIdProp != null)
                    {
                        pathId = (long)(pathIdProp.GetValue(spriteRef) ?? 0);
                    }
                }
                catch { /* ignore */ }

                var spriteId = $"sprite_{pathId}";
                Logger.Debug($"Looking for sprite with ID: {spriteId}");

                // Check if sprite already exists in cache
                // Load cache manually since GetSpriteMetadata uses wrong key format
                var existingSprite = LoadSpriteFromCache(spriteId);
                if (existingSprite != null)
                {
                    Logger.Debug($"Found existing sprite: {existingSprite.Name}");

                    // Fix the file path to use consistent path generation logic
                    var spriteId_forCorrection = SpriteUtilities.GenerateSpriteId(existingSprite.Name);
                    var correctedFilePath = SpriteUtilities.GetSpriteFilePath(spriteId_forCorrection, forcedSpriteType ?? SpriteCacheManager.SpriteType.Orb);

                    // Check if the corrected file exists
                    var fullPath = Path.Combine("/Users/agentender/Library/Application Support/PeglinSaveExplorer", correctedFilePath);
                    if (File.Exists(fullPath))
                    {
                        Logger.Debug($"Found corrected sprite file: {correctedFilePath}");
                        existingSprite.FilePath = correctedFilePath;
                    }
                    else
                    {
                        Logger.Debug($"Corrected sprite file not found: {correctedFilePath}, keeping original");
                    }

                    return existingSprite;
                }

                // Handle AssetRipper-generated PPtr types (like PPtr_Sprite_5) for new sprites
                var ptrType = spriteRef.GetType();
                var tryGetAssetMethod = ptrType.GetMethod("TryGetAsset");

                if (tryGetAssetMethod != null)
                {
                    Logger.Debug($"Attempting to resolve new PPtr type: {ptrType.Name}");

                    try
                    {
                        // Call TryGetAsset with our specific collection
                        var parameters = new object?[] { collection, null };
                        var collectionResult = tryGetAssetMethod.Invoke(spriteRef, parameters);

                        if (collectionResult is bool collectionSuccess && collectionSuccess && parameters[1] != null)
                        {
                            var resolvedAsset = parameters[1];
                            Logger.Debug($"PPtr resolved to: {resolvedAsset.GetType().Name}");

                            // Create sprite metadata with proper file path structure and dimensions
                            var spriteName = resolvedAsset?.ToString() ?? "unknown";
                            var spriteId_forPath = SpriteUtilities.GenerateSpriteId(spriteName);
                            var filePath = SpriteUtilities.GetSpriteFilePath(spriteId_forPath, forcedSpriteType ?? SpriteCacheManager.SpriteType.Orb);

                            // Extract texture dimensions and create PNG file
                            int width = 0, height = 0;
                            ITexture2D? textureToConvert = null;
                            
                            if (resolvedAsset is ISprite sprite)
                            {
                                if (sprite.RD.Texture.TryGetAsset(sprite.Collection, out ITexture2D? texture))
                                {
                                    width = texture.Width_C28;
                                    height = texture.Height_C28;
                                    textureToConvert = texture;
                                    Logger.Debug($"Extracted sprite dimensions: {width}x{height}");
                                }
                            }
                            else if (resolvedAsset is ITexture2D directTexture)
                            {
                                width = directTexture.Width_C28;
                                height = directTexture.Height_C28;
                                textureToConvert = directTexture;
                                Logger.Debug($"Extracted texture dimensions: {width}x{height}");
                            }

                            // Create the PNG file if we have a texture
                            if (textureToConvert != null)
                            {
                                var success = SpriteUtilities.ConvertTextureToPngImproved(textureToConvert, filePath, spriteName);
                                if (!success)
                                {
                                    Logger.Debug($"⚠️ Failed to convert sprite {spriteName} to PNG");
                                }
                                else
                                {
                                    Logger.Debug($"✅ Created PNG file: {filePath}");
                                }
                            }

                            var spriteMetadata = new SpriteCacheManager.SpriteMetadata
                            {
                                Id = spriteId,
                                Name = resolvedAsset?.ToString() ?? "Unknown",
                                Width = width,
                                Height = height,
                                FilePath = filePath,
                                Type = forcedSpriteType ?? SpriteCacheManager.SpriteType.Orb
                            };

                            Logger.Debug($"Created new sprite metadata: {spriteMetadata.Id} ({width}x{height})");
                            return spriteMetadata;
                        }
                        else
                        {
                            Logger.Debug($"PPtr resolution failed for {ptrType.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Error resolving PPtr {ptrType.Name}: {ex.Message}");
                    }
                }
                else
                {
                    Logger.Debug($"No TryGetAsset method found on {ptrType.Name}");
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error in ResolveSpriteWithCollection: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads sprite metadata directly from cache file
        /// </summary>
        private SpriteCacheManager.SpriteMetadata? LoadSpriteFromCache(string spriteId)
        {
            try
            {
                var metadataPath = "/Users/agentender/Library/Application Support/PeglinSaveExplorer/extracted-data/sprites/sprite_cache_metadata.json";
                if (!File.Exists(metadataPath))
                    return null;

                var json = File.ReadAllText(metadataPath);
                var metadata = JsonConvert.DeserializeObject<dynamic>(json);
                if (metadata?.Sprites == null)
                    return null;

                var spriteData = metadata.Sprites[spriteId];
                if (spriteData == null)
                    return null;

                return new SpriteCacheManager.SpriteMetadata
                {
                    Id = spriteData.Id,
                    Name = spriteData.Name,
                    Width = (int?)spriteData.Width ?? 0,
                    Height = (int?)spriteData.Height ?? 0,
                    FilePath = spriteData.FilePath,
                    Type = (SpriteCacheManager.SpriteType)(int)spriteData.Type,
                    FrameX = (int?)spriteData.FrameX ?? 0,
                    FrameY = (int?)spriteData.FrameY ?? 0,
                    FrameWidth = (int?)spriteData.FrameWidth ?? 0,
                    FrameHeight = (int?)spriteData.FrameHeight ?? 0,
                    FrameCount = (int?)spriteData.FrameCount ?? 1,
                    IsAtlas = (bool?)spriteData.IsAtlas ?? false,
                    SourceBundle = (string?)spriteData.SourceBundle ?? ""
                };
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error loading sprite from cache: {ex.Message}");
                return null;
            }
        }

        private void ReportResults(UnifiedExtractionResult result, IProgress<string>? progress)
        {
            var relicSprites = result.Sprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Relic);
            var enemySprites = result.Sprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Enemy);
            var orbSprites = result.Sprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Orb);

            progress?.Report($"✅ Extracted {result.Relics.Count} relics, {result.Enemies.Count} enemies, {result.Orbs.Count} orbs ({result.OrbFamilies.Count} families), {result.Sprites.Count} sprites");
            progress?.Report($"   📊 Sprite breakdown: {relicSprites} relic sprites, {enemySprites} enemy sprites, {orbSprites} orb sprites");
            progress?.Report($"🔗 Correlated {result.RelicSpriteCorrelations.Count} relics, {result.EnemySpriteCorrelations.Count} enemies, {result.OrbSpriteCorrelations.Count} orbs with sprites");
        }
    }
}

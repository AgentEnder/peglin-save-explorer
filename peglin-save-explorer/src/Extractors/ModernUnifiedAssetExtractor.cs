using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                                        var keys = string.Join(", ", data.Keys.Take(10));
                                        Logger.Debug($"🔍 Potential LocalizationParamsManager found with keys: {keys}");
                                        totalLocalizationParamsManagerChecks++;
                                    }

                                    if (_entityDetectionService.IsLocalizationParamsManager(data))
                                    {
                                        Logger.Debug($"✅ LocalizationParamsManager detected! Extracting parameters...");
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
                                            Logger.Debug($"🔤 New parameters: {string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"))}");
                                        }
                                        else
                                        {
                                            Logger.Debug($"⚠️ LocalizationParamsManager detected but no parameters extracted");
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
                    var structure = monoBehaviour.LoadStructure();
                    if (structure == null) continue;

                    var data = _assetProcessingService.ConvertStructureToDict(structure, collection, out var spriteReference);
                    var assetName = monoBehaviour.GetBestName();

                    ProcessMonoBehaviourData(assetName, data, spriteReference, result, relicSpriteRefs, enemySpriteRefs, orbSpriteRefs);
                }
                catch (Exception ex)
                {
                    Logger.Debug($"⚠️ Error processing MonoBehaviour {monoBehaviour.GetBestName()}: {ex.Message}");
                }
            }

            // Process GameObjects for orb extraction
            foreach (var gameObject in collection.OfType<IGameObject>())
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
            UnifiedExtractionResult result, Dictionary<string, IUnityAssetBase> relicSpriteRefs, Dictionary<string, IUnityAssetBase> enemySpriteRefs, Dictionary<string, IUnityAssetBase> orbSpriteRefs)
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
                Logger.Debug($"Found orb via MonoBehaviour path: {assetName}");
                var orb = _entityExtractionService.ExtractOrb(assetName, data);
                if (orb != null && !string.IsNullOrEmpty(orb.Id))
                {
                    result.Orbs[orb.Id] = orb;

                    // Store sprite reference for later correlation
                    if (spriteReference != null)
                    {
                        orbSpriteRefs[orb.Id] = spriteReference;
                        Logger.Debug($"⚡ Found sprite reference for orb {orb.Id}: {spriteReference.GetType().Name}");
                    }
                    else
                    {
                        Logger.Debug($"⚡ No direct sprite reference found for orb {orb.Id}");
                    }
                }
            }
            else if (_entityDetectionService.IsPachinkoBallData(data))
            {
                // Handle PachinkoBall sprite references for orbs
                if (data.ContainsKey("_renderer"))
                {
                    var rendererRef = data["_renderer"];
                    if (rendererRef is IUnityAssetBase spriteAsset)
                    {
                        var pathId = _assetProcessingService.ExtractPathIdFromReference(spriteAsset);
                        var key = $"pachinkoball_{pathId}";
                        orbSpriteRefs[key] = spriteAsset;
                        Logger.Debug($"🎱 Found PachinkoBall sprite reference: {key}");
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

            _assetProcessingService.ExtractGameObjectComponents(gameObject, componentMap, gameObjectData, collection);

            if (_entityDetectionService.IsOrbGameObject(gameObjectData))
            {
                Logger.Debug($"Found orb via GameObject path: {gameObject.GetBestName()}");
                var orbData = ConvertGameObjectToOrbData(gameObjectData);
                if (orbData != null && !string.IsNullOrEmpty(orbData.Id))
                {
                    result.Orbs[orbData.Id] = orbData;

                    // Try to find sprite reference for this orb
                    var spriteReference = FindGameObjectSpriteReference(gameObject, componentMap, collection);
                    if (spriteReference != null)
                    {
                        var spriteMetadata = _spriteProcessingService.ResolveSprite(spriteReference, null!, SpriteCacheManager.SpriteType.Orb);
                        if (spriteMetadata != null)
                        {
                            result.Sprites[spriteMetadata.Id] = spriteMetadata;
                            result.OrbSpriteCorrelations[orbData.Id] = spriteMetadata.Id;
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
                    var spriteMetadata = _spriteProcessingService.ExtractSpriteWithImprovedProcessing(sprite);
                    if (spriteMetadata != null)
                    {
                        result.Sprites[spriteMetadata.Id] = spriteMetadata;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"⚠️ Error processing standalone sprite {sprite.GetBestName()}: {ex.Message}");
                }
            }
        }

        private OrbData? ConvertGameObjectToOrbData(GameObjectData gameObjectData)
        {
            // Find the best component data that represents an orb
            var orbComponent = gameObjectData.Components
                .FirstOrDefault(c => c.Type.Contains("Orb") || c.Properties.ContainsKey("DamagePerPeg"));

            if (orbComponent != null)
            {
                return _entityExtractionService.ExtractOrb(gameObjectData.Name, orbComponent.Properties);
            }

            return null;
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
            // Correlate relic sprites
            foreach (var kvp in relicSpriteRefs)
            {
                var relicId = kvp.Key;
                var spriteRef = kvp.Value;

                try
                {
                    var spriteMetadata = _spriteProcessingService.ResolveSprite(spriteRef, gameBundle, SpriteCacheManager.SpriteType.Relic);
                    if (spriteMetadata != null)
                    {
                        result.Sprites[spriteMetadata.Id] = spriteMetadata;
                        result.RelicSpriteCorrelations[relicId] = spriteMetadata.Id;

                        // Update relic with correlated sprite ID
                        if (result.Relics.TryGetValue(relicId, out var relic))
                        {
                            relic.CorrelatedSpriteId = spriteMetadata.Id;
                        }

                        Logger.Debug($"🔗 Correlated relic {relicId} with sprite {spriteMetadata.Id}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"⚠️ Error correlating relic {relicId} with sprite: {ex.Message}");
                }
            }

            // Correlate enemy sprites
            foreach (var kvp in enemySpriteRefs)
            {
                var enemyId = kvp.Key;
                var spriteRef = kvp.Value;

                try
                {
                    var spriteMetadata = _spriteProcessingService.ResolveSprite(spriteRef, gameBundle, SpriteCacheManager.SpriteType.Enemy);
                    if (spriteMetadata != null)
                    {
                        result.Sprites[spriteMetadata.Id] = spriteMetadata;
                        result.EnemySpriteCorrelations[enemyId] = spriteMetadata.Id;

                        // Update enemy with correlated sprite ID
                        if (result.Enemies.TryGetValue(enemyId, out var enemy))
                        {
                            enemy.CorrelatedSpriteId = spriteMetadata.Id;
                        }

                        Logger.Debug($"🔗 Correlated enemy {enemyId} with sprite {spriteMetadata.Id}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"⚠️ Error correlating enemy {enemyId} with sprite: {ex.Message}");
                }
            }

            // Correlate orb sprites
            foreach (var kvp in orbSpriteRefs)
            {
                var orbId = kvp.Key;
                var spriteRef = kvp.Value;

                try
                {
                    var spriteMetadata = _spriteProcessingService.ResolveSprite(spriteRef, gameBundle, SpriteCacheManager.SpriteType.Orb);
                    if (spriteMetadata != null)
                    {
                        result.Sprites[spriteMetadata.Id] = spriteMetadata;
                        result.OrbSpriteCorrelations[orbId] = spriteMetadata.Id;

                        // Update orb with correlated sprite ID
                        if (result.Orbs.TryGetValue(orbId, out var orb))
                        {
                            orb.CorrelatedSpriteId = spriteMetadata.Id;
                        }

                        Logger.Debug($"🔗 Correlated orb {orbId} with sprite {spriteMetadata.Id}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"⚠️ Error correlating orb {orbId} with sprite: {ex.Message}");
                }
            }

            var totalCorrelations = result.RelicSpriteCorrelations.Count + result.EnemySpriteCorrelations.Count + result.OrbSpriteCorrelations.Count;
            progress?.Report($"🔗 Successfully correlated {totalCorrelations} entities with sprites");
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

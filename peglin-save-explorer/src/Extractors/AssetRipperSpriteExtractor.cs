using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.IO;
using AssetRipper.Assets.Metadata;
using AssetRipper.Import.AssetCreation;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.SerializedFiles.Parser;
using AssetRipper.SourceGenerated;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.Export.Modules.Textures;
using peglin_save_explorer.Data;
using peglin_save_explorer.UI;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Services;

namespace peglin_save_explorer.Extractors
{
    /// <summary>
    /// Extracts sprite assets from Unity bundles using AssetRipper
    /// Focuses on extracting relic and enemy sprites for web frontend display
    /// </summary>
    public class AssetRipperSpriteExtractor
    {
        private readonly ConsoleSession? _session;
        private readonly Dictionary<string, SpriteCacheManager.SpriteMetadata> _extractedSprites = new();
        private readonly EntitySpriteCorrelationService? _correlationService;
        private readonly PathIDCorrelationService? _pathIdCorrelationService;

        // Patterns to identify relic and enemy sprites
        private static readonly Regex RelicSpritePattern = new(@"(relic|orb|artifact|item).*\.(png|jpg|tga)", RegexOptions.IgnoreCase);
        private static readonly Regex EnemySpritePattern = new(@"(enemy|monster|boss|creature|mob).*\.(png|jpg|tga)", RegexOptions.IgnoreCase);
        
        // Common relic/enemy name patterns in Unity asset names
        private static readonly string[] RelicNamePatterns = {
            "relic", "artifact", "item", "pickup", "collectible", "powerup"
        };
        
        private static readonly string[] EnemyNamePatterns = {
            "enemy", "monster", "boss", "creature", "mob", "slime", "guard", "wizard", "archer"
        };
        
        private static readonly string[] OrbNamePatterns = {
            "orb", "ball", "projectile", "attack", "spell", "shot", "missile"
        };

        public AssetRipperSpriteExtractor(ConsoleSession? session, EntitySpriteCorrelationService? correlationService = null, PathIDCorrelationService? pathIdCorrelationService = null)
        {
            _session = session;
            _correlationService = correlationService;
            _pathIdCorrelationService = pathIdCorrelationService;
        }

        /// <summary>
        /// Extracts all sprites from a Peglin installation directory with entity metadata for real-time correlation
        /// </summary>
        public Dictionary<string, SpriteCacheManager.SpriteMetadata> ExtractAllSpritesFromPeglinInstall(
            string peglinPath,
            Dictionary<string, RelicData>? relics = null,
            Dictionary<string, EnemyData>? enemies = null,
            Dictionary<string, OrbData>? orbs = null)
        {
            var allSprites = new Dictionary<string, SpriteCacheManager.SpriteMetadata>();
            
            try
            {
                var bundleDirectory = PeglinPathHelper.GetStreamingAssetsBundlePath(peglinPath);
                if (string.IsNullOrEmpty(bundleDirectory) || !Directory.Exists(bundleDirectory))
                {
                    Logger.Error($"Bundle directory not found for: {peglinPath}");
                    return allSprites;
                }

                Logger.Info($"Extracting sprites from Peglin installation: {peglinPath}");
                
                var bundleFiles = Directory.GetFiles(bundleDirectory, "*.bundle", SearchOption.AllDirectories);
                Logger.Info($"Found {bundleFiles.Length} bundle files");
                
                var processedBundles = 0;
                foreach (var bundleFile in bundleFiles)
                {
                    try
                    {
                        Logger.Debug($"Processing bundle {Path.GetFileName(bundleFile)} ({++processedBundles}/{bundleFiles.Length})");
                        
                        var sprites = ExtractSpritesFromBundle(bundleFile, relics, enemies, orbs);
                        foreach (var kvp in sprites)
                        {
                            allSprites[kvp.Key] = kvp.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to process bundle {Path.GetFileName(bundleFile)}: {ex.Message}");
                    }
                }

                Logger.Info($"Total sprites extracted: {allSprites.Count} (Relics: {allSprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Relic)}, Enemies: {allSprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Enemy)}, Orbs: {allSprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Orb)})");
                return allSprites;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting sprites from Peglin install: {ex.Message}");
                return allSprites;
            }
        }

        /// <summary>
        /// Legacy method - extracts all sprites from a Peglin installation directory without entity metadata
        /// </summary>
        public Dictionary<string, SpriteCacheManager.SpriteMetadata> ExtractAllSpritesFromPeglinInstall(string peglinPath)
        {
            var allSprites = new Dictionary<string, SpriteCacheManager.SpriteMetadata>();
            
            try
            {
                var bundleDirectory = PeglinPathHelper.GetStreamingAssetsBundlePath(peglinPath);
                if (string.IsNullOrEmpty(bundleDirectory) || !Directory.Exists(bundleDirectory))
                {
                    Logger.Error($"Bundle directory not found for: {peglinPath}");
                    return allSprites;
                }

                Logger.Info($"Extracting sprites from Peglin installation: {peglinPath}");
                
                var bundleFiles = Directory.GetFiles(bundleDirectory, "*.bundle", SearchOption.AllDirectories);
                Logger.Info($"Found {bundleFiles.Length} bundle files");
                
                var processedBundles = 0;
                foreach (var bundleFile in bundleFiles)
                {
                    try
                    {
                        Logger.Debug($"Processing bundle {Path.GetFileName(bundleFile)} ({++processedBundles}/{bundleFiles.Length})");
                        
                        var sprites = ExtractSpritesFromBundle(bundleFile);
                        foreach (var kvp in sprites)
                        {
                            allSprites[kvp.Key] = kvp.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to process bundle {Path.GetFileName(bundleFile)}: {ex.Message}");
                    }
                }

                Logger.Info($"Total sprites extracted: {allSprites.Count} (Relics: {allSprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Relic)}, Enemies: {allSprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Enemy)}, Orbs: {allSprites.Values.Count(s => s.Type == SpriteCacheManager.SpriteType.Orb)})");
                return allSprites;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting sprites from Peglin install: {ex.Message}");
                return allSprites;
            }
        }

        /// <summary>
        /// Extracts sprites from a single bundle file with entity metadata for real-time correlation
        /// </summary>
        private Dictionary<string, SpriteCacheManager.SpriteMetadata> ExtractSpritesFromBundle(
            string bundlePath,
            Dictionary<string, RelicData>? relics = null,
            Dictionary<string, EnemyData>? enemies = null,
            Dictionary<string, OrbData>? orbs = null)
        {
            var sprites = new Dictionary<string, SpriteCacheManager.SpriteMetadata>();
            
            try
            {
                // Create assembly manager for asset creation
                var assemblyManager = new AssetRipper.Import.Structure.Assembly.Managers.BaseManager(s => { });
                var assetFactory = new GameAssetFactory(assemblyManager);
                
                var file = SchemeReader.LoadFile(bundlePath);
                var bundle = new GameBundle();
                
                if (file is SerializedFile serializedFile)
                {
                    var collection = bundle.AddCollectionFromSerializedFile(serializedFile, assetFactory);
                    bundle.InitializeAllDependencyLists();
                    ProcessCollection(collection, sprites, bundlePath, relics, enemies, orbs);
                }
                else if (file is FileContainer container)
                {
                    file.ReadContents();
                    var serializedBundle = SerializedBundle.FromFileContainer(container, assetFactory);
                    foreach (var collection in serializedBundle.FetchAssetCollections())
                    {
                        ProcessCollection(collection, sprites, bundlePath, relics, enemies, orbs);
                    }
                }
                
                return sprites;
            }
            catch (ArgumentException ex) when (ex.Message.Contains("No SerializedFile found"))
            {
                Logger.Debug($"Skipping bundle {Path.GetFileName(bundlePath)}: Not a valid Unity bundle");
                return sprites;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error processing bundle {bundlePath}: {ex.Message}");
                return sprites;
            }
        }

        /// <summary>
        /// Legacy method - extracts sprites from a single bundle file without entity metadata
        /// </summary>
        private Dictionary<string, SpriteCacheManager.SpriteMetadata> ExtractSpritesFromBundle(string bundlePath)
        {
            var sprites = new Dictionary<string, SpriteCacheManager.SpriteMetadata>();
            
            try
            {
                // Create assembly manager for asset creation
                var assemblyManager = new AssetRipper.Import.Structure.Assembly.Managers.BaseManager(s => { });
                var assetFactory = new GameAssetFactory(assemblyManager);
                
                var file = SchemeReader.LoadFile(bundlePath);
                var bundle = new GameBundle();
                
                if (file is SerializedFile serializedFile)
                {
                    var collection = bundle.AddCollectionFromSerializedFile(serializedFile, assetFactory);
                    bundle.InitializeAllDependencyLists();
                    ProcessCollection(collection, sprites, bundlePath);
                }
                else if (file is FileContainer container)
                {
                    file.ReadContents();
                    var serializedBundle = SerializedBundle.FromFileContainer(container, assetFactory);
                    foreach (var collection in serializedBundle.FetchAssetCollections())
                    {
                        ProcessCollection(collection, sprites, bundlePath);
                    }
                }
                
                return sprites;
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error processing bundle {Path.GetFileName(bundlePath)}: {ex.Message}");
                return sprites;
            }
        }

        /// <summary>
        /// Processes an asset collection to extract sprites with entity metadata for real-time correlation
        /// </summary>
        private void ProcessCollection(
            AssetCollection collection, 
            Dictionary<string, SpriteCacheManager.SpriteMetadata> sprites, 
            string bundlePath,
            Dictionary<string, RelicData>? relics = null,
            Dictionary<string, EnemyData>? enemies = null,
            Dictionary<string, OrbData>? orbs = null)
        {
            try
            {
                // First pass: Group sprites by their source texture to identify atlases
                var textureToSprites = new Dictionary<long, List<ISprite>>();
                var relevantTextures = new Dictionary<long, ITexture2D>();
                
                // Collect all relevant sprite assets
                foreach (var sprite in collection.OfType<ISprite>())
                {
                    var spriteType = DetermineSpriteTypeWithMetadata(sprite.Name, relics, enemies, orbs);
                    if (!spriteType.HasValue)
                        continue;
                        
                    var texture = sprite.TryGetTexture();
                    if (texture == null)
                        continue;
                    
                    var textureId = texture.PathID;
                    if (!textureToSprites.ContainsKey(textureId))
                    {
                        textureToSprites[textureId] = new List<ISprite>();
                        relevantTextures[textureId] = texture;
                    }
                    textureToSprites[textureId].Add(sprite);
                }
                
                // Collect standalone Texture2D assets (not referenced by sprites)
                var standaloneTextures = new List<ITexture2D>();
                foreach (var texture in collection.OfType<ITexture2D>())
                {
                    var spriteType = DetermineSpriteTypeWithMetadata(texture.Name, relics, enemies, orbs);
                    if (!spriteType.HasValue)
                        continue;
                        
                    // If this texture is not referenced by any sprites, it's standalone
                    if (!relevantTextures.ContainsKey(texture.PathID))
                    {
                        standaloneTextures.Add(texture);
                    }
                }
                
                // Second pass: Process grouped sprites as atlases or individual sprites
                foreach (var kvp in textureToSprites)
                {
                    var textureId = kvp.Key;
                    var spritesFromTexture = kvp.Value;
                    var texture = relevantTextures[textureId];
                    
                    if (spritesFromTexture.Count > 1)
                    {
                        // Multiple sprites from same texture = Atlas
                        ProcessTextureAsAtlas(texture, spritesFromTexture, sprites, bundlePath);
                    }
                    else
                    {
                        // Single sprite from texture = Individual sprite
                        ProcessSpriteAsIndividual(spritesFromTexture[0], sprites, bundlePath);
                    }
                }
                
                // Process standalone textures (without sprites)
                foreach (var texture in standaloneTextures)
                {
                    ProcessTexture2D(texture, sprites, bundlePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing collection: {ex.Message}");
            }
        }

        /// <summary>
        /// Legacy method - processes an asset collection to extract sprites without entity metadata
        /// </summary>
        private void ProcessCollection(AssetCollection collection, Dictionary<string, SpriteCacheManager.SpriteMetadata> sprites, string bundlePath)
        {
            try
            {
                // First pass: Group sprites by their source texture to identify atlases
                var textureToSprites = new Dictionary<long, List<ISprite>>();
                var relevantTextures = new Dictionary<long, ITexture2D>();
                
                // Collect all relevant sprite assets
                foreach (var sprite in collection.OfType<ISprite>())
                {
                    var spriteType = DetermineSpriteType(sprite.Name);
                    if (!spriteType.HasValue)
                        continue;
                        
                    var texture = sprite.TryGetTexture();
                    if (texture == null)
                        continue;
                    
                    var textureId = texture.PathID;
                    if (!textureToSprites.ContainsKey(textureId))
                    {
                        textureToSprites[textureId] = new List<ISprite>();
                        relevantTextures[textureId] = texture;
                    }
                    textureToSprites[textureId].Add(sprite);
                }
                
                // Collect standalone Texture2D assets (not referenced by sprites)
                var standaloneTextures = new List<ITexture2D>();
                foreach (var texture in collection.OfType<ITexture2D>())
                {
                    var spriteType = DetermineSpriteType(texture.Name);
                    if (!spriteType.HasValue)
                        continue;
                        
                    // If this texture is not referenced by any sprites, it's standalone
                    if (!relevantTextures.ContainsKey(texture.PathID))
                    {
                        standaloneTextures.Add(texture);
                    }
                }
                
                // Second pass: Process grouped sprites as atlases or individual sprites
                foreach (var kvp in textureToSprites)
                {
                    var textureId = kvp.Key;
                    var spritesFromTexture = kvp.Value;
                    var texture = relevantTextures[textureId];
                    
                    if (spritesFromTexture.Count > 1)
                    {
                        // Multiple sprites from same texture = Atlas
                        ProcessTextureAsAtlas(texture, spritesFromTexture, sprites, bundlePath);
                    }
                    else
                    {
                        // Single sprite from texture = Individual sprite
                        ProcessSpriteAsIndividual(spritesFromTexture[0], sprites, bundlePath);
                    }
                }
                
                // Third pass: Process standalone textures as individual sprites
                foreach (var texture in standaloneTextures)
                {
                    ProcessTexture2D(texture, sprites, bundlePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error processing collection {collection.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a texture with multiple sprites as an atlas
        /// </summary>
        private void ProcessTextureAsAtlas(ITexture2D texture, List<ISprite> spritesFromTexture, Dictionary<string, SpriteCacheManager.SpriteMetadata> sprites, string bundlePath)
        {
            try
            {
                // Use the first sprite to determine the type
                var firstSprite = spritesFromTexture[0];
                var spriteType = DetermineSpriteType(firstSprite.Name);
                if (!spriteType.HasValue)
                    return;

                // Generate atlas key based on texture
                var textureKey = $"{texture.PathID}_{texture.Width_C28}x{texture.Height_C28}";
                var atlasId = GenerateAtlasId(textureKey);
                
                // Save the complete texture as atlas
                var atlasFilePath = SpriteCacheManager.GetSpritePath(spriteType.Value, atlasId);
                
                if (ConvertTextureToPng(texture, atlasFilePath))
                {
                    var atlasMetadata = new SpriteCacheManager.SpriteMetadata
                    {
                        Id = atlasId,
                        Name = $"{GetTextureBaseName(firstSprite.Name)}_atlas",
                        Type = spriteType.Value,
                        FilePath = atlasFilePath,
                        Width = texture.Width_C28,
                        Height = texture.Height_C28,
                        SourceBundle = Path.GetFileName(bundlePath),
                        ExtractedAt = DateTime.Now,
                        IsAtlas = true,
                        AtlasFrames = new List<SpriteCacheManager.SpriteFrame>()
                    };

                    // Add frame information for each sprite
                    foreach (var sprite in spritesFromTexture)
                    {
                        var frameInfo = ExtractSpriteFrameInfo(sprite);
                        if (frameInfo != null)
                        {
                            atlasMetadata.AtlasFrames.Add(frameInfo);
                        }
                    }
                    atlasMetadata.FrameCount = atlasMetadata.AtlasFrames.Count;

                    var atlasKey = $"{spriteType.Value}:{atlasId}";
                    sprites[atlasKey] = atlasMetadata;
                    
                    // Register sprite PathIDs for correlation if service is available
                    // Register all sprites in the atlas with the same atlas ID for correlation
                    if (_pathIdCorrelationService != null)
                    {
                        foreach (var sprite in spritesFromTexture)
                        {
                            _pathIdCorrelationService.RegisterSpritePathId(sprite.PathID, atlasId);
                        }
                    }
                    
                    Logger.Debug($"Extracted {spriteType.Value} atlas: {atlasMetadata.Name} -> {atlasId} ({texture.Width_C28}x{texture.Height_C28}) with {spritesFromTexture.Count} frames");
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error processing texture as atlas {texture.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a single sprite, checking if it's a sprite sheet or individual sprite
        /// </summary>
        private void ProcessSpriteAsIndividual(ISprite sprite, Dictionary<string, SpriteCacheManager.SpriteMetadata> sprites, string bundlePath)
        {
            try
            {
                var spriteName = sprite.Name;
                var spriteType = DetermineSpriteType(spriteName);
                if (!spriteType.HasValue)
                    return;

                // Get the texture to save the full texture
                var texture = sprite.TryGetTexture();
                if (texture == null)
                    return;

                    // Check if this texture is already processed as a multi-sprite atlas
                var textureId = texture.PathID;
                var atlasKey = sprites.Keys.FirstOrDefault(k => sprites[k].Id.Contains($"_{textureId}_"));
                
                if (atlasKey != null)
                {
                    // This texture was already processed as an atlas, skip synthetic sprite sheet processing
                    Logger.Debug($"Skipping sprite sheet detection for {spriteName} - already processed as atlas: {sprites[atlasKey].Id}");
                    return;
                }

                // Check if this is actually a sprite sheet (atlas)
                var isSpriteSheet = DetectSpriteSheet(spriteName, texture.Width_C28, texture.Height_C28);
                
                if (isSpriteSheet)
                {
                    // Process as sprite sheet atlas
                    ProcessSpriteSheet(sprite, texture, sprites, bundlePath);
                }
                else
                {
                    // Process as individual sprite
                    var spriteId = GenerateSpriteId(spriteName, texture.PathID);
                    var spriteFilePath = SpriteCacheManager.GetSpritePath(spriteType.Value, spriteId);

                    if (ConvertTextureToPng(texture, spriteFilePath))
                    {
                        // Extract sprite frame information from Unity sprite data
                        var rect = sprite.Rect;
                        var frameWidth = (int)rect.Width;
                        var frameHeight = (int)rect.Height;
                        var frameX = (int)rect.X;
                        var frameY = (int)rect.Y;
                        
                        // Calculate potential frame count if this is actually a sprite sheet
                        var textureWidth = texture.Width_C28;
                        var textureHeight = texture.Height_C28;
                        var frameCount = 1;
                        
                        // Debug logging for sprite frame detection
                        Logger.Debug($"Sprite {spriteName}: texture={textureWidth}x{textureHeight}, frame={frameWidth}x{frameHeight}, pos=({frameX},{frameY})");
                        
                        // Check if the texture is larger than the frame, indicating multiple frames
                        if (frameWidth > 0 && frameHeight > 0 && 
                            (textureWidth > frameWidth || textureHeight > frameHeight))
                        {
                            var framesX = textureWidth / frameWidth;
                            var framesY = textureHeight / frameHeight;
                            frameCount = Math.Max(1, framesX * framesY);
                            Logger.Debug($"Detected sprite sheet {spriteName}: {frameCount} frames ({framesX}x{framesY})");
                        }
                        
                        var metadata = new SpriteCacheManager.SpriteMetadata
                        {
                            Id = spriteId,
                            Name = spriteName,
                            Type = spriteType.Value,
                            FilePath = spriteFilePath,
                            Width = textureWidth,
                            Height = textureHeight,
                            FrameWidth = frameWidth,
                            FrameHeight = frameHeight,
                            FrameX = frameX,
                            FrameY = frameY,
                            FrameCount = frameCount,
                            SourceBundle = Path.GetFileName(bundlePath),
                            ExtractedAt = DateTime.Now,
                            IsAtlas = frameCount > 1  // Mark as atlas if multiple frames detected
                        };

                        var key = $"{spriteType.Value}:{spriteId}";
                        sprites[key] = metadata;
                        
                        // Register sprite PathID for correlation if service is available
                        _pathIdCorrelationService?.RegisterSpritePathId(sprite.PathID, spriteId);
                        
                        Logger.Debug($"Extracted {spriteType.Value} individual sprite: {spriteName} -> {spriteId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error processing individual sprite {sprite.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Detects if a sprite is actually a sprite sheet based on naming patterns and dimensions
        /// </summary>
        private bool DetectSpriteSheet(string spriteName, int width, int height)
        {
            var lowerName = spriteName.ToLowerInvariant();
            
            // Check for sprite sheet naming patterns
            var spriteSheetPatterns = new[]
            {
                "spritesheet", "sheet", "anim", "animated", "frames", "idle", "walk", "run", "attack", "death", "hurt"
            };
            
            var hasSpriteSheetName = spriteSheetPatterns.Any(pattern => lowerName.Contains(pattern));
            
            // Check for dimensions that suggest a sprite sheet
            // Common sprite sheet patterns: width > height (horizontal strips), or both dimensions are multiples of common frame sizes
            var possibleFrameSizes = new[] { 16, 24, 32, 48, 64, 80, 96, 128, 160 };
            
            // Check if width is a multiple of height (horizontal strip)
            var isHorizontalStrip = height <= 160 && width > height && width % height == 0 && (width / height) >= 2;
            
            // Check if height is a multiple of width (vertical strip) 
            var isVerticalStrip = width <= 160 && height > width && height % width == 0 && (height / width) >= 2;
            
            // Check if both dimensions are multiples of common frame sizes (grid layout)
            var isGrid = possibleFrameSizes.Any(frameSize => 
                width >= frameSize * 2 && height >= frameSize * 2 &&
                width % frameSize == 0 && height % frameSize == 0);
            
            // Check for irregular dimensions that don't fit standard frame sizes but still suggest sprite sheets
            var hasIrregularDimensions = (width > 100 && height > 100) && 
                                        (width != height) && 
                                        !possibleFrameSizes.Contains(width) && 
                                        !possibleFrameSizes.Contains(height);
            
            var result = hasSpriteSheetName || isHorizontalStrip || isVerticalStrip || isGrid || hasIrregularDimensions;
            
            if (result)
            {
                Logger.Debug($"Detected sprite sheet: {spriteName} ({width}x{height}) - Name: {hasSpriteSheetName}, HStrip: {isHorizontalStrip}, VStrip: {isVerticalStrip}, Grid: {isGrid}, Irregular: {hasIrregularDimensions}");
            }
            
            return result;
        }

        /// <summary>
        /// Processes a sprite sheet as an atlas with synthetic frame data
        /// </summary>
        private void ProcessSpriteSheet(ISprite sprite, ITexture2D texture, Dictionary<string, SpriteCacheManager.SpriteMetadata> sprites, string bundlePath)
        {
            try
            {
                var spriteName = sprite.Name;
                var spriteType = DetermineSpriteType(spriteName);
                if (!spriteType.HasValue)
                    return;

                // Generate atlas ID for the sprite sheet
                var textureKey = $"{texture.PathID}_{texture.Width_C28}x{texture.Height_C28}";
                var atlasId = GenerateAtlasId(textureKey);
                
                // Save the complete texture as atlas
                var atlasFilePath = SpriteCacheManager.GetSpritePath(spriteType.Value, atlasId);
                
                if (ConvertTextureToPng(texture, atlasFilePath))
                {
                    var atlasMetadata = new SpriteCacheManager.SpriteMetadata
                    {
                        Id = atlasId,
                        Name = $"{GetTextureBaseName(spriteName)}_atlas",
                        Type = spriteType.Value,
                        FilePath = atlasFilePath,
                        Width = texture.Width_C28,
                        Height = texture.Height_C28,
                        SourceBundle = Path.GetFileName(bundlePath),
                        ExtractedAt = DateTime.Now,
                        IsAtlas = true,
                        AtlasFrames = new List<SpriteCacheManager.SpriteFrame>()
                    };

                    // Generate synthetic frame data for the sprite sheet
                    var frames = GenerateSpriteSheetFrames(spriteName, texture.Width_C28, texture.Height_C28);
                    atlasMetadata.AtlasFrames.AddRange(frames);
                    atlasMetadata.FrameCount = frames.Count;

                    var atlasKey = $"{spriteType.Value}:{atlasId}";
                    sprites[atlasKey] = atlasMetadata;
                    
                    // Register sprite PathID for correlation if service is available
                    _pathIdCorrelationService?.RegisterSpritePathId(sprite.PathID, atlasId);
                    
                    Logger.Debug($"Extracted {spriteType.Value} sprite sheet atlas: {atlasMetadata.Name} -> {atlasId} ({texture.Width_C28}x{texture.Height_C28}) with {frames.Count} frames");
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error processing sprite sheet {sprite.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates synthetic frame data for a sprite sheet
        /// </summary>
        private List<SpriteCacheManager.SpriteFrame> GenerateSpriteSheetFrames(string spriteName, int width, int height)
        {
            var frames = new List<SpriteCacheManager.SpriteFrame>();
            
            // Try to detect the frame layout
            var frameWidth = 0;
            var frameHeight = 0;
            var rows = 1;
            var cols = 1;
            
            // Common frame sizes to try
            var possibleFrameSizes = new[] { 16, 24, 32, 48, 64, 80, 96, 128, 160 };
            
            // First, try horizontal strips (common for animation)
            if (height <= 160)
            {
                foreach (var frameSize in possibleFrameSizes)
                {
                    if (height == frameSize && width % frameSize == 0 && (width / frameSize) >= 2)
                    {
                        frameWidth = frameSize;
                        frameHeight = frameSize;
                        cols = width / frameSize;
                        rows = 1;
                        break;
                    }
                }
            }
            
            // If not a horizontal strip, try vertical strips
            if (frameWidth == 0 && width <= 160)
            {
                foreach (var frameSize in possibleFrameSizes)
                {
                    if (width == frameSize && height % frameSize == 0 && (height / frameSize) >= 2)
                    {
                        frameWidth = frameSize;
                        frameHeight = frameSize;
                        cols = 1;
                        rows = height / frameSize;
                        break;
                    }
                }
            }
            
            // If not a strip, try to find a good grid layout
            if (frameWidth == 0)
            {
                // First try standard frame sizes
                foreach (var frameSize in possibleFrameSizes)
                {
                    if (width % frameSize == 0 && height % frameSize == 0)
                    {
                        var potentialCols = width / frameSize;
                        var potentialRows = height / frameSize;
                        
                        // Prefer reasonable grid sizes (not too many tiny frames)
                        if (potentialCols * potentialRows >= 2 && potentialCols * potentialRows <= 50)
                        {
                            frameWidth = frameSize;
                            frameHeight = frameSize;
                            cols = potentialCols;
                            rows = potentialRows;
                            break;
                        }
                    }
                }
                
                // If standard sizes don't work, try to find common divisors for irregular dimensions
                if (frameWidth == 0)
                {
                    var commonDivisors = FindReasonableFrameSizes(width, height);
                    foreach (var frameSize in commonDivisors)
                    {
                        var fw = frameSize.Item1;
                        var fh = frameSize.Item2;
                        var potentialCols = width / fw;
                        var potentialRows = height / fh;
                        var totalFrames = potentialCols * potentialRows;
                        
                        // Accept reasonable frame counts
                        if (totalFrames >= 2 && totalFrames <= 100 && fw >= 16 && fh >= 16)
                        {
                            frameWidth = fw;
                            frameHeight = fh;
                            cols = potentialCols;
                            rows = potentialRows;
                            break;
                        }
                    }
                }
            }
            
            // Fallback: if we couldn't detect a pattern, assume it's one big sprite
            if (frameWidth == 0)
            {
                frameWidth = width;
                frameHeight = height;
                cols = 1;
                rows = 1;
            }
            
            // Generate frame data
            var frameIndex = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    frames.Add(new SpriteCacheManager.SpriteFrame
                    {
                        Name = $"{spriteName}_frame_{frameIndex:D2}",
                        X = col * frameWidth,
                        Y = row * frameHeight,
                        Width = frameWidth,
                        Height = frameHeight,
                        PivotX = 0.5f,
                        PivotY = 0.5f,
                        SpritePathID = 0 // We don't have individual sprite path IDs for synthetic frames
                    });
                    frameIndex++;
                }
            }
            
            Logger.Debug($"Generated {frames.Count} synthetic frames for sprite sheet {spriteName} ({width}x{height}) - Frame size: {frameWidth}x{frameHeight}, Grid: {cols}x{rows}");
            
            return frames;
        }

        /// <summary>
        /// Finds reasonable frame sizes for irregular sprite sheet dimensions
        /// </summary>
        private List<(int width, int height)> FindReasonableFrameSizes(int totalWidth, int totalHeight)
        {
            var frameSizes = new List<(int, int)>();
            
            // Find factors of width and height
            var widthFactors = GetFactors(totalWidth);
            var heightFactors = GetFactors(totalHeight);
            
            // Try combinations of factors to find reasonable frame sizes
            foreach (var wFactor in widthFactors)
            {
                foreach (var hFactor in heightFactors)
                {
                    var frameWidth = totalWidth / wFactor;
                    var frameHeight = totalHeight / hFactor;
                    
                    // Skip unreasonably small or large frames
                    if (frameWidth < 8 || frameHeight < 8 || frameWidth > 200 || frameHeight > 200)
                        continue;
                        
                    // Skip if it would create too many or too few frames
                    var totalFrames = wFactor * hFactor;
                    if (totalFrames < 2 || totalFrames > 100)
                        continue;
                    
                    frameSizes.Add((frameWidth, frameHeight));
                }
            }
            
            // Sort by preference: prefer square-ish frames and reasonable frame counts
            frameSizes.Sort((a, b) => {
                var aRatio = Math.Max(a.Item1, a.Item2) / (float)Math.Min(a.Item1, a.Item2);
                var bRatio = Math.Max(b.Item1, b.Item2) / (float)Math.Min(b.Item1, b.Item2);
                return aRatio.CompareTo(bRatio); // Prefer more square frames
            });
            
            return frameSizes;
        }

        /// <summary>
        /// Gets all factors of a number (divisors that result in integer quotients)
        /// </summary>
        private List<int> GetFactors(int number)
        {
            var factors = new List<int>();
            for (int i = 1; i <= Math.Sqrt(number); i++)
            {
                if (number % i == 0)
                {
                    factors.Add(i);
                    if (i != number / i) // Avoid adding the same factor twice for perfect squares
                    {
                        factors.Add(number / i);
                    }
                }
            }
            factors.Sort();
            return factors;
        }

        /// <summary>
        /// Processes a Texture2D asset and saves it if it matches sprite patterns
        /// </summary>
        private void ProcessTexture2D(ITexture2D texture, Dictionary<string, SpriteCacheManager.SpriteMetadata> sprites, string bundlePath)
        {
            try
            {
                var textureName = texture.Name;
                if (string.IsNullOrEmpty(textureName))
                    return;

                var spriteType = DetermineSpriteType(textureName);
                if (!spriteType.HasValue)
                    return;

                // Generate sprite ID and metadata
                var spriteId = GenerateSpriteId(textureName, texture.PathID);
                var spriteFilePath = SpriteCacheManager.GetSpritePath(spriteType.Value, spriteId);

                // Convert and save the image as PNG using AssetRipper's TextureConverter
                if (ConvertTextureToPng(texture, spriteFilePath))
                {
                    var metadata = new SpriteCacheManager.SpriteMetadata
                    {
                        Id = spriteId,
                        Name = textureName,
                        Type = spriteType.Value,
                        FilePath = spriteFilePath,
                        Width = texture.Width_C28,
                        Height = texture.Height_C28,
                        FrameWidth = texture.Width_C28,  // For standalone textures, frame = full texture
                        FrameHeight = texture.Height_C28,
                        FrameX = 0,
                        FrameY = 0,
                        SourceBundle = Path.GetFileName(bundlePath),
                        ExtractedAt = DateTime.Now,
                        IsAtlas = false  // Standalone textures are not atlases
                    };

                    var key = $"{spriteType.Value}:{spriteId}";
                    sprites[key] = metadata;
                    
                    // Register sprite PathID for correlation if service is available
                    _pathIdCorrelationService?.RegisterSpritePathId(texture.PathID, spriteId);
                    
                    Logger.Debug($"Extracted {spriteType.Value} sprite: {textureName} -> {spriteId}");
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error processing Texture2D {texture.Name}: {ex.Message}");
            }
        }


        /// <summary>
        /// Determines if an asset name corresponds to a relic or enemy sprite
        /// </summary>
        private SpriteCacheManager.SpriteType? DetermineSpriteType(string assetName)
        {
            var lowerName = assetName.ToLowerInvariant();

            // Check for relic patterns
            if (RelicNamePatterns.Any(pattern => lowerName.Contains(pattern)))
            {
                return SpriteCacheManager.SpriteType.Relic;
            }

            // Check for enemy patterns
            if (EnemyNamePatterns.Any(pattern => lowerName.Contains(pattern)))
            {
                return SpriteCacheManager.SpriteType.Enemy;
            }

            // Check for orb patterns
            if (OrbNamePatterns.Any(pattern => lowerName.Contains(pattern)))
            {
                return SpriteCacheManager.SpriteType.Orb;
            }

            // Additional pattern matching for common Unity naming conventions
            if (lowerName.Contains("ui") && (lowerName.Contains("icon") || lowerName.Contains("button")))
            {
                if (RelicNamePatterns.Any(pattern => lowerName.Contains(pattern)))
                    return SpriteCacheManager.SpriteType.Relic;
            }

            return null; // Not a sprite we're interested in
        }

        /// <summary>
        /// Determines sprite type using entity metadata for more accurate categorization
        /// </summary>
        private SpriteCacheManager.SpriteType? DetermineSpriteTypeWithMetadata(
            string assetName,
            Dictionary<string, RelicData>? relics = null,
            Dictionary<string, EnemyData>? enemies = null,
            Dictionary<string, OrbData>? orbs = null)
        {
            if (string.IsNullOrEmpty(assetName))
                return null;

            var lowerName = assetName.ToLowerInvariant();

            // First try exact matching with entity metadata if available
            if (relics != null && _correlationService != null)
            {
                // Check if this sprite name matches any relic entity
                foreach (var relic in relics.Values)
                {
                    if (!string.IsNullOrEmpty(relic.Name))
                    {
                        var normalizedRelicName = _correlationService.NormalizeName(relic.Name);
                        var normalizedSpriteName = _correlationService.NormalizeName(assetName);
                        
                        if (normalizedRelicName == normalizedSpriteName || 
                            normalizedSpriteName.Contains(normalizedRelicName) ||
                            normalizedRelicName.Contains(normalizedSpriteName))
                        {
                            return SpriteCacheManager.SpriteType.Relic;
                        }
                    }
                }
            }

            if (enemies != null && _correlationService != null)
            {
                // Check if this sprite name matches any enemy entity
                foreach (var enemy in enemies.Values)
                {
                    if (!string.IsNullOrEmpty(enemy.Name))
                    {
                        var normalizedEnemyName = _correlationService.NormalizeName(enemy.Name);
                        var normalizedSpriteName = _correlationService.NormalizeName(assetName);
                        
                        if (normalizedEnemyName == normalizedSpriteName || 
                            normalizedSpriteName.Contains(normalizedEnemyName) ||
                            normalizedEnemyName.Contains(normalizedSpriteName))
                        {
                            return SpriteCacheManager.SpriteType.Enemy;
                        }
                    }
                }
            }

            if (orbs != null && _correlationService != null)
            {
                // Check if this sprite name matches any orb entity
                foreach (var orb in orbs.Values)
                {
                    if (!string.IsNullOrEmpty(orb.Name))
                    {
                        var normalizedOrbName = _correlationService.NormalizeName(orb.Name);
                        var normalizedSpriteName = _correlationService.NormalizeName(assetName);
                        
                        if (normalizedOrbName == normalizedSpriteName || 
                            normalizedSpriteName.Contains(normalizedOrbName) ||
                            normalizedOrbName.Contains(normalizedSpriteName))
                        {
                            return SpriteCacheManager.SpriteType.Orb;
                        }
                    }
                }
            }

            // Fallback to pattern-based detection
            return DetermineSpriteType(assetName);
        }

        /// <summary>
        /// Generates a unique sprite ID from name and path
        /// </summary>
        private string GenerateSpriteId(string spriteName, long pathId)
        {
            // Clean the sprite name for use as ID
            var cleanName = spriteName
                .Replace(" ", "_")
                .Replace("-", "_")
                .ToLowerInvariant();

            // Remove file extensions if present
            cleanName = Regex.Replace(cleanName, @"\.(png|jpg|tga|tiff)$", "", RegexOptions.IgnoreCase);

            // Ensure valid filename characters
            cleanName = Regex.Replace(cleanName, @"[^\w_\-]", "");

            // Append path ID to ensure uniqueness
            return $"{cleanName}_{Math.Abs(pathId)}";
        }

        /// <summary>
        /// Generates a unique atlas ID from texture key
        /// </summary>
        private string GenerateAtlasId(string textureKey)
        {
            // Generate a consistent ID based on the texture key
            var cleanKey = textureKey
                .Replace(" ", "_")
                .Replace("-", "_")
                .ToLowerInvariant();

            // Ensure valid filename characters
            cleanKey = Regex.Replace(cleanKey, @"[^\w_\-]", "");

            return $"atlas_{cleanKey}";
        }

        /// <summary>
        /// Extracts the base name from a sprite name (removes frame numbers/suffixes)
        /// </summary>
        private string GetTextureBaseName(string spriteName)
        {
            // Remove common animation frame suffixes like "_0", "_01", "_001", etc.
            var baseName = Regex.Replace(spriteName, @"_\d+$", "", RegexOptions.IgnoreCase);
            
            // Remove other common suffixes
            baseName = Regex.Replace(baseName, @"_(frame|anim|sprite).*$", "", RegexOptions.IgnoreCase);
            
            return string.IsNullOrEmpty(baseName) ? spriteName : baseName;
        }

        /// <summary>
        /// Extracts sprite frame information from Unity sprite metadata
        /// </summary>
        private SpriteCacheManager.SpriteFrame? ExtractSpriteFrameInfo(ISprite sprite)
        {
            try
            {
                var frame = new SpriteCacheManager.SpriteFrame
                {
                    Name = sprite.Name,
                    SpritePathID = sprite.PathID
                };

                // Get sprite rectangle information
                var rect = sprite.Rect;
                frame.X = (int)rect.X;
                frame.Y = (int)rect.Y;
                frame.Width = (int)rect.Width;
                frame.Height = (int)rect.Height;

                // Get pivot information if available
                if (sprite.Has_Pivot())
                {
                    var pivot = sprite.Pivot;
                    frame.PivotX = pivot.X;
                    frame.PivotY = pivot.Y;
                }
                else
                {
                    // Default pivot to center
                    frame.PivotX = 0.5f;
                    frame.PivotY = 0.5f;
                }

                return frame;
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error extracting sprite frame info for {sprite.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Converts a Texture2D to PNG format and saves it to disk using AssetRipper's TextureConverter
        /// </summary>
        private bool ConvertTextureToPng(ITexture2D texture, string outputPath)
        {
            try
            {
                // Ensure output directory exists
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Use AssetRipper's TextureConverter to convert to DirectBitmap
                if (TextureConverter.TryConvertToBitmap(texture, out DirectBitmap bitmap))
                {
                    // Save as PNG using AssetRipper's built-in PNG export
                    using var fileStream = File.Create(outputPath);
                    bitmap.SaveAsPng(fileStream);
                    
                    Logger.Debug($"Converted and saved PNG: {Path.GetFileName(outputPath)} ({texture.Width_C28}x{texture.Height_C28}, format: {texture.Format_C28E})");
                    return true;
                }
                else
                {
                    Logger.Debug($"Failed to convert texture to bitmap: {Path.GetFileName(outputPath)} (format: {texture.Format_C28E})");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error converting texture to PNG: {ex.Message}");
                return false;
            }
        }

    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using peglin_save_explorer.Data;
using peglin_save_explorer.Utils;
using static peglin_save_explorer.Extractors.Services.SpriteUtilities;

namespace peglin_save_explorer.Extractors.Services
{
    /// <summary>
    /// Service responsible for sprite processing and resolution
    /// </summary>
    public class SpriteProcessingService
    {
        /// <summary>
        /// Resolves a sprite reference to actual sprite metadata using cross-bundle support
        /// </summary>
        public SpriteCacheManager.SpriteMetadata? ResolveSprite(IUnityAssetBase spriteRef, GameBundle gameBundle, SpriteCacheManager.SpriteType? forcedSpriteType = null)
        {
            try
            {
                // Handle direct sprite references
                if (spriteRef is ISprite sprite)
                {
                    Logger.Debug($"✅ Direct sprite reference resolved: {sprite.GetBestName()}");
                    return ExtractSpriteWithImprovedProcessing(sprite, forcedSpriteType);
                }

                // Handle direct texture references
                if (spriteRef is ITexture2D texture)
                {
                    Logger.Debug($"✅ Direct texture reference resolved: {texture.GetBestName()}");
                    return ExtractTextureWithImprovedProcessing(texture, forcedSpriteType);
                }

                // Handle AssetRipper-generated PPtr types (like PPTr_Object_5)
                // These types have a TryGetAsset method that resolves the actual referenced object
                var ptrType = spriteRef.GetType();
                var tryGetAssetMethod = ptrType.GetMethod("TryGetAsset");

                if (tryGetAssetMethod != null)
                {
                    Logger.Debug($"🔍 Attempting to resolve PPtr type: {ptrType.Name}");

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
                                    Logger.Debug($"✅ PPtr resolved to sprite: {resolvedSprite.GetBestName()}");
                                    return ExtractSpriteWithImprovedProcessing(resolvedSprite, forcedSpriteType);
                                }
                                else if (resolvedAsset is ITexture2D resolvedTexture)
                                {
                                    Logger.Debug($"✅ PPtr resolved to texture: {resolvedTexture.GetBestName()}");
                                    return ExtractTextureWithImprovedProcessing(resolvedTexture, forcedSpriteType);
                                }
                                else
                                {
                                    Logger.Debug($"⚠️ PPtr resolved to unexpected type: {resolvedAsset.GetType().Name}");
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
                Logger.Debug($"⚠️ Error resolving sprite: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts sprite with improved texture processing 
        /// </summary>
        public SpriteCacheManager.SpriteMetadata? ExtractSpriteWithImprovedProcessing(ISprite sprite, SpriteCacheManager.SpriteType? forcedType = null)
        {
            try
            {
                if (!sprite.RD.Texture.TryGetAsset(sprite.Collection, out ITexture2D? texture))
                {
                    Logger.Debug($"⚠️ Failed to get texture for sprite {sprite.GetBestName()}");
                    return null;
                }

                var spriteType = forcedType ?? DetermineSpriteType(sprite.GetBestName());
                var relativePath = GetSpriteFilePath(GenerateSpriteId(sprite.GetBestName()), spriteType);
                var displayName = CleanSpriteName(sprite.GetBestName());

                Logger.Debug($"🎨 Processing sprite: {displayName} (Type: {spriteType})");

                var success = ConvertTextureToPngImproved(texture, relativePath, displayName);
                if (!success)
                {
                    Logger.Debug($"⚠️ Failed to convert sprite {displayName} to PNG");
                    return null;
                }

                // Detect frame information for sprite sheets
                var frameInfo = DetectSpriteFrames(displayName, texture.Width_C28, texture.Height_C28);

                return new SpriteCacheManager.SpriteMetadata
                {
                    Id = GenerateSpriteId(sprite.GetBestName()),
                    Name = displayName,
                    Width = texture.Width_C28,
                    Height = texture.Height_C28,
                    Type = spriteType,
                    FilePath = relativePath,
                    SourceBundle = sprite.Collection.Name,
                    FrameX = frameInfo.FrameX,
                    FrameY = frameInfo.FrameY,
                    FrameWidth = frameInfo.FrameWidth,
                    FrameHeight = frameInfo.FrameHeight,
                    FrameCount = frameInfo.FrameCount,
                    IsAtlas = frameInfo.IsAtlas,
                    AtlasFrames = frameInfo.AtlasFrames
                };
            }
            catch (Exception ex)
            {
                Logger.Debug($"⚠️ Error extracting sprite with improved processing: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts texture metadata with improved processing
        /// </summary>
        public SpriteCacheManager.SpriteMetadata? ExtractTextureWithImprovedProcessing(ITexture2D texture, SpriteCacheManager.SpriteType? forcedType = null)
        {
            try
            {
                var spriteType = forcedType ?? DetermineSpriteType(texture.GetBestName());
                var relativePath = GetSpriteFilePath(GenerateSpriteId(texture.GetBestName()), spriteType);
                var displayName = CleanSpriteName(texture.GetBestName());

                Logger.Debug($"🖼️ Processing texture: {displayName} (Type: {spriteType})");

                var success = ConvertTextureToPngImproved(texture, relativePath, displayName);
                if (!success)
                {
                    Logger.Debug($"⚠️ Failed to convert texture {displayName} to PNG");
                    return null;
                }

                // Detect frame information for sprite sheets
                var frameInfo = DetectSpriteFrames(displayName, texture.Width_C28, texture.Height_C28);

                return new SpriteCacheManager.SpriteMetadata
                {
                    Id = GenerateSpriteId(texture.GetBestName()),
                    Name = displayName,
                    Width = texture.Width_C28,
                    Height = texture.Height_C28,
                    Type = spriteType,
                    FilePath = relativePath,
                    SourceBundle = "",
                    FrameX = frameInfo.FrameX,
                    FrameY = frameInfo.FrameY,
                    FrameWidth = frameInfo.FrameWidth,
                    FrameHeight = frameInfo.FrameHeight,
                    FrameCount = frameInfo.FrameCount,
                    IsAtlas = frameInfo.IsAtlas,
                    AtlasFrames = frameInfo.AtlasFrames
                };
            }
            catch (Exception ex)
            {
                Logger.Debug($"⚠️ Error extracting texture with improved processing: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Helper method to check if field is likely a sprite field
        /// </summary>
        public static bool IsSpriteField(string? fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return false;
            var spriteFieldNames = new[] { "sprite", "icon", "image", "texture", "picture", "graphic", "avatar" };
            return spriteFieldNames.Any(name => fieldName.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// More aggressive check for potential sprite fields based on common Unity patterns
        /// </summary>
        public static bool CouldBeSpriteField(string fieldName)
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
        /// Detects frame information for sprite sheets
        /// </summary>
        private static SpriteFrameInfo DetectSpriteFrames(string spriteName, int width, int height)
        {
            // Default single frame
            var frameInfo = new SpriteFrameInfo
            {
                FrameX = 0,
                FrameY = 0,
                FrameWidth = width,
                FrameHeight = height,
                FrameCount = 1,
                IsAtlas = false,
                AtlasFrames = new List<SpriteCacheManager.SpriteFrame>()
            };

            // Skip atlas detection for certain sprite names that are likely single sprites
            if (IsSingleSpriteByName(spriteName))
            {
                Logger.Debug($"Skipping atlas detection for {spriteName} - detected as single sprite by name");
                return frameInfo;
            }

            // Skip atlas detection for very large sprites (likely backgrounds or single large sprites)
            if (width > 512 || height > 512)
            {
                Logger.Debug($"Skipping atlas detection for {spriteName} - too large ({width}x{height})");
                return frameInfo;
            }

            // Common frame sizes to try
            var possibleFrameSizes = new[] { 16, 24, 32, 48, 64, 80, 96, 128, 160 };

            int frameWidth = 0;
            int frameHeight = 0;
            int rows = 1;
            int cols = 1;

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
                // First try standard frame sizes - but be more conservative
                foreach (var frameSize in possibleFrameSizes)
                {
                    if (width % frameSize == 0 && height % frameSize == 0)
                    {
                        var potentialCols = width / frameSize;
                        var potentialRows = height / frameSize;
                        var totalFrames = potentialCols * potentialRows;

                        // More conservative: require at least 4 frames and reasonable grid sizes
                        // Also avoid large single dimensions that could be single sprites
                        if (totalFrames >= 4 && totalFrames <= 50 &&
                            potentialCols >= 2 && potentialRows >= 2 &&
                            potentialCols <= 8 && potentialRows <= 8)
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
                // But be even more conservative here
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

                        // Very conservative: require clear grid pattern evidence
                        if (totalFrames >= 4 && totalFrames <= 50 && fw >= 24 && fh >= 24 &&
                            potentialCols >= 2 && potentialRows >= 2 &&
                            potentialCols <= 6 && potentialRows <= 6)
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

            // If we detected multiple frames, generate frame data
            if (frameWidth > 0 && frameHeight > 0 && (cols > 1 || rows > 1))
            {
                frameInfo.FrameWidth = frameWidth;
                frameInfo.FrameHeight = frameHeight;
                frameInfo.FrameCount = cols * rows;
                frameInfo.IsAtlas = true;

                // Generate individual frame data
                var frameIndex = 0;
                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        // Skip the last frame if it would be empty (for 3x3 grids with 8 actual frames)
                        if (frameIndex == 8 && cols == 3 && rows == 3)
                        {
                            Logger.Debug($"Skipping empty frame {frameIndex} for 3x3 grid sprite {spriteName}");
                            break;
                        }

                        frameInfo.AtlasFrames.Add(new SpriteCacheManager.SpriteFrame
                        {
                            Name = $"{spriteName}_frame_{frameIndex:D2}",
                            X = col * frameWidth,
                            Y = row * frameHeight,
                            Width = frameWidth,
                            Height = frameHeight,
                            PivotX = 0.5f,
                            PivotY = 0.5f,
                            SpritePathID = 0
                        });
                        frameIndex++;
                    }
                }

                // Update frame count to actual number of frames generated
                frameInfo.FrameCount = frameInfo.AtlasFrames.Count;

                Logger.Debug($"Detected sprite sheet {spriteName}: {frameInfo.FrameCount} frames ({cols}x{rows}) - Frame size: {frameWidth}x{frameHeight}");
            }

            return frameInfo;
        }

        /// <summary>
        /// Finds reasonable frame sizes for irregular sprite sheet dimensions
        /// </summary>
        private static List<(int width, int height)> FindReasonableFrameSizes(int totalWidth, int totalHeight)
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

                    // More conservative size requirements
                    if (frameWidth < 16 || frameHeight < 16 || frameWidth > 128 || frameHeight > 128)
                        continue;

                    // More conservative frame count requirements
                    var totalFrames = wFactor * hFactor;
                    if (totalFrames < 4 || totalFrames > 50)
                        continue;

                    // Require both dimensions to create a reasonable grid (not 1xN or Nx1)
                    if (wFactor < 2 || hFactor < 2)
                        continue;

                    frameSizes.Add((frameWidth, frameHeight));
                }
            }

            // Sort by preference: prefer square-ish frames and reasonable frame counts
            frameSizes.Sort((a, b) =>
            {
                var aRatio = Math.Max(a.Item1, a.Item2) / (float)Math.Min(a.Item1, a.Item2);
                var bRatio = Math.Max(b.Item1, b.Item2) / (float)Math.Min(b.Item1, b.Item2);
                return aRatio.CompareTo(bRatio); // Prefer more square frames
            });

            return frameSizes;
        }

        /// <summary>
        /// Determines if a sprite should be treated as a single sprite based on its name
        /// </summary>
        private static bool IsSingleSpriteByName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
                return false;

            var lowerName = spriteName.ToLowerInvariant();

            // Common keywords that indicate single sprites (not atlases)
            var singleSpriteKeywords = new[]
            {
                "boulder", "rock", "stone", "background", "bg", "title", "logo",
                "portrait", "avatar", "icon", "ui_", "button", "panel", "menu",
                "inventory", "health", "mana", "coin", "gem", "crystal", "key",
                "shield", "armor", "weapon", "sword", "bow", "staff", "ring",
                "potion", "scroll", "book", "chest", "door", "wall", "floor",
                "ceiling", "platform", "ladder", "bridge", "tree", "grass",
                "water", "cloud", "sun", "moon", "star", "mountain", "hill"
            };

            return singleSpriteKeywords.Any(keyword => lowerName.Contains(keyword));
        }

        /// <summary>
        /// Gets all factors of a number (divisors that result in integer quotients)
        /// </summary>
        private static List<int> GetFactors(int number)
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
        /// Helper class to hold detected frame information
        /// </summary>
        private class SpriteFrameInfo
        {
            public int FrameX { get; set; }
            public int FrameY { get; set; }
            public int FrameWidth { get; set; }
            public int FrameHeight { get; set; }
            public int FrameCount { get; set; }
            public bool IsAtlas { get; set; }
            public List<SpriteCacheManager.SpriteFrame> AtlasFrames { get; set; } = new();
        }
    }
}

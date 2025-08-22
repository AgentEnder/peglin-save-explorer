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

                // Use rect-based sprite cropping if we have sprite rect information
                var success = SpriteUtilities.ConvertSpriteRectToPng(texture, sprite, relativePath, displayName);
                if (!success)
                {
                    Logger.Debug($"⚠️ Failed to convert sprite {displayName} to PNG with rect cropping");
                    return null;
                }

                // Get the actual sprite dimensions from the rect
                var spriteRect = sprite.Rect;
                var spriteWidth = (int)spriteRect.Width;
                var spriteHeight = (int)spriteRect.Height;

                // For sprites from SpriteRenderer components, use sprite-specific metadata if available
                var frameInfo = DetectSpriteFramesFromUnitySprite(sprite, displayName, spriteWidth, spriteHeight);

                return new SpriteCacheManager.SpriteMetadata
                {
                    Id = GenerateSpriteId(sprite.GetBestName()),
                    Name = displayName,
                    Width = spriteWidth,
                    Height = spriteHeight,
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
        /// Detects frame information using Unity sprite metadata for more accurate detection
        /// </summary>
        private static SpriteFrameInfo DetectSpriteFramesFromUnitySprite(ISprite sprite, string spriteName, int textureWidth, int textureHeight)
        {
            // Default single frame
            var frameInfo = new SpriteFrameInfo
            {
                FrameX = 0,
                FrameY = 0,
                FrameWidth = textureWidth,
                FrameHeight = textureHeight,
                FrameCount = 1,
                IsAtlas = false,
                AtlasFrames = new List<SpriteCacheManager.SpriteFrame>()
            };

            try
            {
                // Check if this sprite has specific rect data (indicates it's part of an atlas)
                // Unity sprites that are part of an atlas will have a specific rect within the texture
                var spriteRect = sprite.Rect;
                var spriteWidth = (int)spriteRect.Width;
                var spriteHeight = (int)spriteRect.Height;
                var spriteX = (int)spriteRect.X;
                var spriteY = (int)spriteRect.Y;

                // If the sprite rect covers the entire texture, it's likely a single sprite
                bool coversEntireTexture = spriteX == 0 && spriteY == 0 &&
                                         spriteWidth == textureWidth && spriteHeight == textureHeight;

                if (coversEntireTexture)
                {
                    Logger.Debug($"Sprite {spriteName} covers entire texture ({textureWidth}x{textureHeight}) - treating as single sprite");
                    return frameInfo; // Return single frame info
                }
                else
                {
                    // This sprite references a specific region of a larger texture
                    // Check if this looks like a single sprite from an atlas vs. multiple animation frames

                    // For orbs and entities, even if they're in an atlas, they're typically single sprites
                    // Only treat as multi-frame if there's strong evidence of animation frames
                    bool looksLikeSingleSpriteInAtlas = spriteWidth <= 64 && spriteHeight <= 64 &&
                                                       (spriteName.Contains("orb", StringComparison.OrdinalIgnoreCase) ||
                                                        spriteName.Contains("relic", StringComparison.OrdinalIgnoreCase) ||
                                                        spriteName.Contains("enemy", StringComparison.OrdinalIgnoreCase));

                    if (looksLikeSingleSpriteInAtlas)
                    {
                        Logger.Debug($"Sprite {spriteName} appears to be single sprite in atlas: rect=({spriteX},{spriteY},{spriteWidth},{spriteHeight}) - treating as single sprite");

                        // Use the sprite's specific rect but treat as single sprite
                        frameInfo.FrameX = spriteX;
                        frameInfo.FrameY = spriteY;
                        frameInfo.FrameWidth = spriteWidth;
                        frameInfo.FrameHeight = spriteHeight;
                        frameInfo.IsAtlas = true; // Mark as atlas since it's using specific coordinates
                        frameInfo.FrameCount = 1; // But it's still just one frame

                        return frameInfo;
                    }
                    else
                    {
                        // This might be a multi-frame sprite sheet, use the sprite's rect as frame info
                        Logger.Debug($"Sprite {spriteName} may be multi-frame: rect=({spriteX},{spriteY},{spriteWidth},{spriteHeight}) in texture ({textureWidth}x{textureHeight})");

                        frameInfo.FrameX = spriteX;
                        frameInfo.FrameY = spriteY;
                        frameInfo.FrameWidth = spriteWidth;
                        frameInfo.FrameHeight = spriteHeight;
                        frameInfo.IsAtlas = true;

                        return frameInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to read Unity sprite metadata for {spriteName}, falling back to general detection: {ex.Message}");
            }

            // Fallback to general sprite sheet detection
            return DetectSpriteFrames(spriteName, textureWidth, textureHeight);
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

            // Skip atlas detection for very large sprites (likely backgrounds or single large sprites)
            if (width > 512 || height > 512)
            {
                Logger.Debug($"Skipping atlas detection for {spriteName} - too large ({width}x{height})");
                return frameInfo;
            }

            // Skip atlas detection for very small sprites (likely UI elements or single icons)
            if (width < 32 || height < 32)
            {
                Logger.Debug($"Skipping atlas detection for {spriteName} - too small ({width}x{height})");
                return frameInfo;
            }

            // Common frame sizes to try - but be much more conservative
            var possibleFrameSizes = new[] { 16, 24, 32, 48, 64, 80, 96, 128 };

            int frameWidth = 0;
            int frameHeight = 0;
            int rows = 1;
            int cols = 1;

            // Only attempt atlas detection if sprite dimensions suggest multiple frames
            // Look for clear horizontal or vertical strips with obvious repeating patterns

            // Try horizontal strips (width >> height, common for animation)
            if (height <= 128 && width > height * 2) // Clear horizontal bias
            {
                foreach (var frameSize in possibleFrameSizes)
                {
                    if (height == frameSize && width % frameSize == 0)
                    {
                        var potentialFrames = width / frameSize;
                        // Require strong evidence: at least 3 frames, reasonable frame count
                        if (potentialFrames >= 3 && potentialFrames <= 16)
                        {
                            frameWidth = frameSize;
                            frameHeight = frameSize;
                            cols = potentialFrames;
                            rows = 1;
                            Logger.Debug($"Detected horizontal strip for {spriteName}: {cols} frames of {frameSize}x{frameSize}");
                            break;
                        }
                    }
                }
            }

            // Try vertical strips (height >> width, less common but possible)
            if (frameWidth == 0 && width <= 128 && height > width * 2) // Clear vertical bias
            {
                foreach (var frameSize in possibleFrameSizes)
                {
                    if (width == frameSize && height % frameSize == 0)
                    {
                        var potentialFrames = height / frameSize;
                        // Require strong evidence: at least 3 frames, reasonable frame count
                        if (potentialFrames >= 3 && potentialFrames <= 16)
                        {
                            frameWidth = frameSize;
                            frameHeight = frameSize;
                            cols = 1;
                            rows = potentialFrames;
                            Logger.Debug($"Detected vertical strip for {spriteName}: {rows} frames of {frameSize}x{frameSize}");
                            break;
                        }
                    }
                }
            }

            // Try grid layouts only for clearly grid-like dimensions
            if (frameWidth == 0 && width <= 512 && height <= 512)
            {
                // Only try standard frame sizes with strong grid evidence
                foreach (var frameSize in possibleFrameSizes)
                {
                    if (width % frameSize == 0 && height % frameSize == 0)
                    {
                        var potentialCols = width / frameSize;
                        var potentialRows = height / frameSize;
                        var totalFrames = potentialCols * potentialRows;

                        // Very conservative: require strong evidence of intentional grid
                        // Must be perfect square frames, reasonable grid size, and clear intent
                        if (totalFrames >= 4 && totalFrames <= 25 && // 2x2 to 5x5 max
                            potentialCols >= 2 && potentialRows >= 2 && // No single strips here
                            potentialCols <= 5 && potentialRows <= 5 && // Reasonable grid
                            frameSize >= 32) // No tiny frames
                        {
                            frameWidth = frameSize;
                            frameHeight = frameSize;
                            cols = potentialCols;
                            rows = potentialRows;
                            Logger.Debug($"Detected grid layout for {spriteName}: {cols}x{rows} frames of {frameSize}x{frameSize}");
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

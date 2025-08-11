using System;
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

                return new SpriteCacheManager.SpriteMetadata
                {
                    Id = GenerateSpriteId(sprite.GetBestName()),
                    Name = displayName,
                    Width = texture.Width_C28,
                    Height = texture.Height_C28,
                    Type = spriteType,
                    FilePath = relativePath,
                    SourceBundle = sprite.Collection.Name
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

                return new SpriteCacheManager.SpriteMetadata
                {
                    Id = GenerateSpriteId(texture.GetBestName()),
                    Name = displayName,
                    Width = texture.Width_C28,
                    Height = texture.Height_C28,
                    Type = spriteType,
                    FilePath = relativePath,
                    SourceBundle = ""
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
    }
}

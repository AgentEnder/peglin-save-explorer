using System;
using System.IO;
using AssetRipper.Import.AssetCreation;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.Export.Modules.Textures;
using peglin_save_explorer.Data;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Core;

namespace peglin_save_explorer.Extractors.Services
{
    /// <summary>
    /// Utility methods for sprite processing
    /// </summary>
    public static class SpriteUtilities
    {
        /// <summary>
        /// Generates a unique sprite ID from a name
        /// </summary>
        public static string GenerateSpriteId(string name)
        {
            if (string.IsNullOrEmpty(name)) return $"sprite_{Guid.NewGuid():N}";
            return name.Replace(" ", "_").Replace(".", "_").ToLowerInvariant();
        }

        /// <summary>
        /// Determines sprite type from name
        /// </summary>
        public static SpriteCacheManager.SpriteType DetermineSpriteType(string name)
        {
            var nameLower = name?.ToLowerInvariant() ?? "";

            // More specific patterns first - check for UI/interface elements and skip them
            if (nameLower.Contains("ui") || nameLower.Contains("interface") || nameLower.Contains("button") ||
                nameLower.Contains("panel") || nameLower.Contains("background") || nameLower.Contains("cursor") ||
                nameLower.Contains("frame") || nameLower.Contains("border") || nameLower.Contains("menu") ||
                nameLower.Contains("loading") || nameLower.Contains("effect") || nameLower.Contains("particle"))
            {
                // For UI elements, we'll return null to indicate they shouldn't be processed as entity sprites
                // But since we need to return a type, we'll use Orb as fallback but log this case
                Logger.Debug($"🎨 Skipping UI/interface sprite: {name}");
                return SpriteCacheManager.SpriteType.Orb; // Temporary - these should be filtered out upstream
            }

            // Check for relics
            if (nameLower.Contains("relic") || nameLower.Contains("artifact") || nameLower.Contains("item") ||
                nameLower.Contains("trinket") || nameLower.Contains("amulet") || nameLower.Contains("charm"))
                return SpriteCacheManager.SpriteType.Relic;

            // Check for enemies
            if (nameLower.Contains("enemy") || nameLower.Contains("monster") || nameLower.Contains("boss") ||
                nameLower.Contains("slime") || nameLower.Contains("spider") || nameLower.Contains("bat") ||
                nameLower.Contains("rat") || nameLower.Contains("dragon") || nameLower.Contains("ghost"))
                return SpriteCacheManager.SpriteType.Enemy;

            // Check for orbs - be more specific
            if (nameLower.Contains("orb") || nameLower.Contains("ball") || nameLower.Contains("projectile") ||
                nameLower.Contains("stone") || nameLower.Contains("sphere") || nameLower.Contains("pachinko"))
                return SpriteCacheManager.SpriteType.Orb;

            // For unclear cases, don't default to Orb - this was the main bug
            // Instead, let's be conservative and only classify things we're confident about
            Logger.Debug($"🤔 Uncertain sprite classification for: {name} - defaulting to Orb");
            return SpriteCacheManager.SpriteType.Orb;
        }

        /// <summary>
        /// Gets the file path for a sprite based on its type
        /// </summary>
        public static string GetSpriteFilePath(string spriteId, SpriteCacheManager.SpriteType type)
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
        /// Cleans sprite names by removing common suffixes and prefixes
        /// </summary>
        public static string CleanSpriteName(string name)
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
        /// Converts a Texture2D to PNG format and saves it to disk
        /// </summary>
        public static bool ConvertTextureToPngImproved(ITexture2D texture, string relativePath, string displayName)
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
    }
}

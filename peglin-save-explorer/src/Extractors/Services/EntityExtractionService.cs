using System;
using System.Collections.Generic;
using System.Linq;
using peglin_save_explorer.Data;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Services;

namespace peglin_save_explorer.Extractors.Services
{
    /// <summary>
    /// Service responsible for extracting entity data from asset dictionaries
    /// </summary>
    public class EntityExtractionService
    {
        private readonly LocalizationService _localizationService;

        public EntityExtractionService()
        {
            _localizationService = LocalizationService.Instance;
        }

        /// <summary>
        /// Extracts relic data from the given asset data
        /// </summary>
        public RelicData? ExtractRelic(string assetName, Dictionary<string, object> data, Dictionary<string, string>? localizationParams = null)
        {
            try
            {
                var relic = new RelicData
                {
                    Id = CleanEntityId(assetName),
                    RawData = data // Preserve the raw data for debugging
                };

                if (data.TryGetValue("locKey", out var locKey))
                {
                    relic.LocKey = locKey?.ToString();
                    if (!string.IsNullOrEmpty(relic.LocKey))
                    {
                        var localizedName = _localizationService.GetTranslation($"Relics/{relic.LocKey}_name");
                        if (!string.IsNullOrWhiteSpace(localizedName))
                        {
                            relic.Name = localizedName;
                        }

                        // Description removed - use DescriptionStrings instead
                    }
                }

                if (string.IsNullOrEmpty(relic.Name) || string.IsNullOrEmpty(relic.Description))
                {
                    if (data.TryGetValue("englishDisplayName", out var name))
                    {
                        relic.Name = name?.ToString() ?? relic.Name;
                    }
                    else
                    {
                        relic.Name = assetName ?? "Unknown Relic";
                    }

                    if (data.TryGetValue("englishDescription", out var desc))
                    {
                        relic.Description = desc?.ToString() ?? relic.Description;
                    }
                }

                if (data.TryGetValue("effect", out var effect))
                {
                    relic.Effect = effect?.ToString() ?? "";
                }

                if (data.TryGetValue("globalRarity", out var rarity) && int.TryParse(rarity?.ToString(), out var rarityInt))
                {
                    relic.RarityValue = rarityInt;
                    relic.Rarity = rarityInt switch
                    {
                        0 => "COMMON",
                        1 => "UNCOMMON",
                        2 => "RARE",
                        3 => "BOSS",
                        _ => "UNKNOWN"
                    };
                }

                // Apply localization parameters if available
                if (localizationParams != null && localizationParams.Count > 0)
                {
                    Logger.Debug($"🔤 Applying token resolution to relic {relic.Name ?? relic.Id} with {localizationParams.Count} parameters");

                    if (!string.IsNullOrEmpty(relic.Description))
                    {
                        var resolvedDescription = ResolveTokens(relic.Description, localizationParams);
                        if (resolvedDescription != relic.Description)
                        {
                            Logger.Debug($"🔤 Resolved relic description: '{relic.Description}' -> '{resolvedDescription}'");
                            relic.Description = resolvedDescription;
                        }
                        else
                        {
                            Logger.Debug($"🔤 No tokens resolved in relic description: '{relic.Description}'");
                        }
                    }

                    if (!string.IsNullOrEmpty(relic.Effect))
                    {
                        var resolvedEffect = ResolveTokens(relic.Effect, localizationParams);
                        if (resolvedEffect != relic.Effect)
                        {
                            Logger.Debug($"🔤 Resolved relic effect: '{relic.Effect}' -> '{resolvedEffect}'");
                            relic.Effect = resolvedEffect;
                        }
                    }
                }
                else
                {
                    Logger.Debug($"🔤 No localization parameters available for relic {relic.Name ?? relic.Id} (params: {localizationParams?.Count ?? 0})");
                }

                // Sanitize RawData for JSON serialization
                relic.RawData = SanitizeRawDataForSerialization(relic.RawData);

                return relic;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error extracting relic from {assetName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts enemy data from the given asset data
        /// </summary>
        public EnemyData? ExtractEnemy(string assetName, Dictionary<string, object> data)
        {
            try
            {
                var enemy = new EnemyData
                {
                    Id = CleanEntityId(assetName),
                    RawData = data // Preserve the raw data for debugging
                };

                if (data.TryGetValue("EnglishDisplayName", out var displayName))
                {
                    enemy.Name = displayName?.ToString() ?? assetName ?? "Unknown Enemy";
                }
                else if (data.TryGetValue("LocKey", out var locKey))
                {
                    enemy.LocKey = locKey?.ToString();
                    if (!string.IsNullOrEmpty(enemy.LocKey))
                    {
                        var localizedName = _localizationService.GetTranslation($"Enemies/{enemy.LocKey}");
                        if (!string.IsNullOrWhiteSpace(localizedName))
                        {
                            enemy.Name = localizedName;
                        }
                    }
                }
                else if (data.TryGetValue("enemyName", out var enemyName))
                {
                    enemy.Name = enemyName?.ToString() ?? assetName ?? "Unknown Enemy";
                }

                if (data.TryGetValue("MaxHealth", out var maxHealth) && float.TryParse(maxHealth?.ToString(), out var maxHealthFloat))
                {
                    enemy.Health = maxHealthFloat;
                }
                else if (data.TryGetValue("StartingHealth", out var startingHealth) && float.TryParse(startingHealth?.ToString(), out var startingHealthFloat))
                {
                    enemy.Health = startingHealthFloat;
                }

                if (data.TryGetValue("MeleeAttackDamage", out var meleeAttack) && float.TryParse(meleeAttack?.ToString(), out var meleeAttackFloat))
                {
                    enemy.AttackDamage = meleeAttackFloat;
                }
                else if (data.TryGetValue("DamagePerMeleeAttack", out var damagePerMelee) && float.TryParse(damagePerMelee?.ToString(), out var damagePerMeleeFloat))
                {
                    enemy.AttackDamage = damagePerMeleeFloat;
                }

                // Sanitize RawData for JSON serialization
                enemy.RawData = SanitizeRawDataForSerialization(enemy.RawData);

                return enemy;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error extracting enemy from {assetName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts orb data from the given asset data
        /// </summary>
        public OrbData? ExtractOrb(string assetName, Dictionary<string, object> data, Dictionary<string, string>? localizationParams = null)
        {
            try
            {
                Logger.Debug($"ExtractOrb called for '{assetName}' with {data.Count} fields");

                var orb = new OrbData
                {
                    Id = CleanEntityId(assetName),
                    RawData = data // Preserve the raw data for debugging
                };

                // Extract localization key
                if (data.TryGetValue("locNameString", out var locNameString))
                {
                    orb.LocKey = locNameString?.ToString();
                }

                // Get localized name
                if (!string.IsNullOrEmpty(orb.LocKey))
                {
                    var nameKey = $"{orb.LocKey}_name";
                    var localizedName = _localizationService.GetTranslation($"Orbs/{nameKey}");
                    if (!string.IsNullOrWhiteSpace(localizedName))
                    {
                        orb.Name = localizedName;
                    }
                }

                // Fallback to pre-translated name
                if (string.IsNullOrWhiteSpace(orb.Name) && data.TryGetValue("locName", out var locName))
                {
                    orb.Name = locName?.ToString();
                }

                // Final fallback to asset name
                if (string.IsNullOrWhiteSpace(orb.Name))
                {
                    orb.Name = assetName ?? "Unknown Orb";
                }

                // Extract description strings
                ExtractDescriptionStrings(orb, data);

                // Apply localization parameters if available
                ApplyLocalizationToOrb(orb, localizationParams);

                // Extract damage values
                ExtractDamageValues(orb, data);

                // Extract level
                ExtractLevel(orb, data);

                // Determine orb type
                orb.OrbType = DetermineOrbTypeFromData(assetName, data);

                // Set default rarity
                orb.RarityValue = 1;
                orb.Rarity = "COMMON";

                // Sanitize RawData for JSON serialization
                orb.RawData = SanitizeRawDataForSerialization(orb.RawData);

                return orb;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error extracting orb from {assetName}: {ex.Message}");
                return null;
            }
        }

        private void ExtractDescriptionStrings(OrbData orb, Dictionary<string, object> data)
        {
            List<string>? locDescStrings = null;

            // Try various paths to find locDescStrings
            if (data.TryGetValue("locDescStrings", out var directLocDescStrings) &&
                directLocDescStrings is IEnumerable<object> directArray)
            {
                locDescStrings = directArray.Select(x => x?.ToString()).Where(x => !string.IsNullOrEmpty(x)).Cast<string>().ToList();
            }
            else if (data.TryGetValue("ComponentData", out var componentDataObj) &&
                componentDataObj is Dictionary<string, object> componentData &&
                componentData.TryGetValue("OrbComponent", out var orbComponentObj) &&
                orbComponentObj is Dictionary<string, object> orbComponent &&
                orbComponent.TryGetValue("locDescStrings", out var nestedLocDescStrings) &&
                nestedLocDescStrings is IEnumerable<object> nestedArray)
            {
                locDescStrings = nestedArray.Select(x => x?.ToString()).Where(x => !string.IsNullOrEmpty(x)).Cast<string>().ToList();
            }

            // Translate description strings
            if (locDescStrings != null && locDescStrings.Count > 0)
            {
                var translatedStrings = new List<string>();
                foreach (var descKey in locDescStrings)
                {
                    var translation = _localizationService.GetTranslation($"Orbs/{descKey}");
                    if (!string.IsNullOrWhiteSpace(translation))
                    {
                        translatedStrings.Add(translation);
                    }
                }
                orb.DescriptionStrings = translatedStrings;
            }
        }

        private void ExtractDamageValues(OrbData orb, Dictionary<string, object> data)
        {
            if (data.TryGetValue("DamagePerPeg", out var damagePerPegValue))
            {
                orb.DamagePerPeg = ParseFloatValue(damagePerPegValue);
            }

            if (data.TryGetValue("CritDamagePerPeg", out var critDamagePerPegValue))
            {
                orb.CritDamagePerPeg = ParseFloatValue(critDamagePerPegValue);
            }
        }

        private void ExtractLevel(OrbData orb, Dictionary<string, object> data)
        {
            // Try common level field names
            var searchPaths = new[] { "Level", "level", "orbLevel", "OrbLevel" };

            foreach (var path in searchPaths)
            {
                if (data.TryGetValue(path, out var value))
                {
                    int? extractedLevel = ParseIntValue(value);
                    if (extractedLevel.HasValue)
                    {
                        orb.Level = extractedLevel.Value;
                        return;
                    }
                }
            }

            // Log only if level extraction fails completely
            Logger.Debug($"Level field not found for orb {orb.Id ?? "unknown"}");
        }

        /// <summary>
        /// Applies localization parameter substitution to orb DescriptionStrings
        /// </summary>
        private void ApplyLocalizationToOrb(OrbData orb, Dictionary<string, string>? localizationParams)
        {
            if (localizationParams != null && localizationParams.Count > 0 && orb.DescriptionStrings != null)
            {
                Logger.Debug($"🔤 Applying token resolution to orb {orb.Name ?? orb.Id} with {localizationParams.Count} parameters");

                var resolvedStrings = new List<string>();
                foreach (var desc in orb.DescriptionStrings)
                {
                    var resolved = ResolveTokens(desc, localizationParams);
                    resolvedStrings.Add(resolved);
                    if (resolved != desc)
                    {
                        Logger.Debug($"🔤 Resolved orb description: '{desc}' -> '{resolved}'");
                    }
                }
                orb.DescriptionStrings = resolvedStrings;
            }
            else
            {
                Logger.Debug($"🔤 No localization parameters available for orb {orb.Name ?? orb.Id} (params: {localizationParams?.Count ?? 0})");
            }
        }

        private float? ParseFloatValue(object? value)
        {
            return value switch
            {
                float f => f,
                double d => (float)d,
                int i => (float)i,
                _ => float.TryParse(value?.ToString(), out var parsed) ? parsed : null
            };
        }

        private int? ParseIntValue(object? value)
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                _ => int.TryParse(value?.ToString(), out var parsed) ? parsed : null
            };
        }

        private string DetermineOrbTypeFromData(string assetName, Dictionary<string, object> data)
        {
            var lowerName = assetName.ToLowerInvariant();

            if (lowerName.Contains("heal") || lowerName.Contains("support"))
                return "UTILITY";
            if (lowerName.Contains("special") || lowerName.Contains("unique"))
                return "SPECIAL";
            if (lowerName.Contains("attack") || lowerName.Contains("damage") || data.ContainsKey("DamagePerPeg"))
                return "ATTACK";

            return "ATTACK"; // Default to attack type
        }

        private static string CleanEntityId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "unknown";

            return id.ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("[", "")
                .Replace("]", "");
        }

        private static string ResolveTokens(string input, Dictionary<string, string> parameters)
        {
            if (string.IsNullOrEmpty(input) || parameters.Count == 0)
                return input;

            var result = input;
            foreach (var kvp in parameters)
            {
                // Handle both formats: {PARAMETER_NAME} and {[PARAMETER_NAME]}
                result = result.Replace($"{{{kvp.Key}}}", kvp.Value);
                result = result.Replace($"{{[{kvp.Key}]}}", kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// Sanitizes RawData to ensure all values are JSON-serializable
        /// </summary>
        private static Dictionary<string, object>? SanitizeRawDataForSerialization(Dictionary<string, object>? rawData)
        {
            if (rawData == null) return null;

            var sanitized = new Dictionary<string, object>();

            foreach (var kvp in rawData)
            {
                var value = kvp.Value;

                if (value == null)
                {
                    sanitized[kvp.Key] = null!;
                }
                else if (IsJsonSerializable(value))
                {
                    sanitized[kvp.Key] = value;
                }
                else if (value is System.Collections.IEnumerable enumerable && !(value is string))
                {
                    // Convert collections to arrays of serializable values
                    var items = enumerable.Cast<object>()
                        .Select(item => IsJsonSerializable(item) ? item : item?.ToString() ?? "")
                        .ToArray();
                    sanitized[kvp.Key] = items;
                }
                else
                {
                    // Convert complex objects to their string representation
                    sanitized[kvp.Key] = value.ToString() ?? "";
                }
            }

            return sanitized;
        }

        /// <summary>
        /// Checks if a value is directly JSON-serializable
        /// </summary>
        private static bool IsJsonSerializable(object? value)
        {
            return value == null ||
                   value is string ||
                   value is bool ||
                   value is int ||
                   value is long ||
                   value is float ||
                   value is double ||
                   value is decimal;
        }
    }
}

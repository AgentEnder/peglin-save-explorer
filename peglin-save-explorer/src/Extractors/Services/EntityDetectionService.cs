using System.Collections.Generic;
using System.Linq;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Extractors.Services
{
    /// <summary>
    /// Service responsible for detecting different entity types from asset data
    /// </summary>
    public class EntityDetectionService
    {
        private static readonly string[] RelicFields = { "locKey", "englishDisplayName", "effect", "globalRarity", "sprite" };

        private static readonly string[] EnemyFields =
        {
            "CurrentHealth", "StartingHealth", "DamagePerMeleeAttack", "AttackRange", "enemyTypes",
            "MaxHealth", "MaxHealthCruciball", "MeleeAttackDamage", "RangedAttackDamage", "location", "Type"
        };

        private static readonly string[] EnemyPatterns =
            { "enemy", "boss", "slime", "ballista", "dragon", "demon", "sapper", "knight", "archer" };

        private static readonly string[] RequiredOrbFields =
            { "locNameString", "locName", "DamagePerPeg", "CritDamagePerPeg", "Level" };

        private static readonly string[] AttackTypeFields =
            { "shotPrefab", "_shotPrefab", "_thunderPrefab", "_criticalShotPrefab", "_criticalThunderPrefab", "targetColumn", "verticalAttack", "targetingType" };

        private static readonly string[] PachinkoBallFields =
            { "_renderer", "FireForce", "GravityScale", "MaxBounceCount", "MultiballForceMod" };

        /// <summary>
        /// Determines if the given data represents a relic
        /// </summary>
        public bool IsRelicData(Dictionary<string, object> data)
        {
            var matchCount = RelicFields.Count(field => data.ContainsKey(field));
            return matchCount >= 3;
        }

        /// <summary>
        /// Determines if the given data represents an enemy
        /// </summary>
        public bool IsEnemyData(Dictionary<string, object> data)
        {
            var matchCount = EnemyFields.Count(field => data.ContainsKey(field));

            // Also check for specific patterns in values that indicate enemy data
            if (data.ContainsKey("LocKey") && data["LocKey"] is string locKey)
            {
                if (EnemyPatterns.Any(pattern => locKey.ToLowerInvariant().Contains(pattern)))
                {
                    matchCount += 2; // Boost confidence for pattern match
                }
            }

            return matchCount >= 2;
        }

        /// <summary>
        /// Determines if the given data represents an orb
        /// </summary>
        public bool IsOrbData(Dictionary<string, object> data)
        {
            // Debug logging to see what keys we have
            var keys = string.Join(", ", data.Keys.Take(20)); // Show first 20 keys
            Logger.Debug($"🔍 IsOrbData checking data with keys: {keys}");

            var requiredFieldCount = RequiredOrbFields.Count(field => data.ContainsKey(field));

            Logger.Debug($"🔍 Required orb fields found: {requiredFieldCount}/5 - {string.Join(", ", RequiredOrbFields.Where(field => data.ContainsKey(field)))}");

            // Must have at least 3 of the 5 required orb fields
            if (requiredFieldCount < 3)
            {
                Logger.Debug($"🔍 Not enough required orb fields ({requiredFieldCount} < 3)");
                return false;
            }

            // If we have 4+ required fields, it's definitely an orb (like doctorb)
            if (requiredFieldCount >= 4)
            {
                Logger.Debug($"🔍 Strong match: {requiredFieldCount}/5 required orb fields found - definitely an orb!");
                return true;
            }

            // For 3 required fields, check for additional evidence
            var hasAttackTypeFields = AttackTypeFields.Any(field => data.ContainsKey(field));
            var hasScriptRef = data.ContainsKey("m_Script");

            Logger.Debug($"🔍 Attack type fields: {hasAttackTypeFields}, Script ref: {hasScriptRef}");

            var isOrb = requiredFieldCount >= 3 && (hasAttackTypeFields || hasScriptRef);
            Logger.Debug($"🔍 IsOrb result: {isOrb} (required fields: {requiredFieldCount >= 3}, type indicators: {hasAttackTypeFields || hasScriptRef})");

            return isOrb;
        }

        /// <summary>
        /// Determines if the given data represents a PachinkoBall
        /// </summary>
        public bool IsPachinkoBallData(Dictionary<string, object> data)
        {
            var pachinkoBallCount = PachinkoBallFields.Count(field => data.ContainsKey(field));
            var hasRenderer = data.ContainsKey("_renderer");

            // Debug logging for components that have any PachinkoBall fields
            if (pachinkoBallCount > 0 || hasRenderer)
            {
                Console.WriteLine($"🔍 PachinkoBall check: renderer={hasRenderer}, fields={pachinkoBallCount}/5, keys={string.Join(",", data.Keys.Take(10))}");
                Console.WriteLine($"   PachinkoBall fields found: {string.Join(", ", PachinkoBallFields.Where(f => data.ContainsKey(f)))}");
            }

            // Must have _renderer field and at least 2 other PachinkoBall-specific fields
            var result = hasRenderer && pachinkoBallCount >= 3;
            if (result)
            {
                Console.WriteLine($"✅ Detected PachinkoBall data!");
            }
            return result;
        }

        /// <summary>
        /// Determines if the given data represents a LocalizationParamsManager
        /// </summary>
        public bool IsLocalizationParamsManager(Dictionary<string, object> data)
        {
            return data.ContainsKey("_Params") && data.ContainsKey("_IsGlobalManager");
        }

        /// <summary>
        /// Checks if a GameObject represents an orb based on its data and components
        /// </summary>
        public bool IsOrbGameObject(Models.GameObjectData gameObjectData)
        {
            var name = gameObjectData.Name?.ToLowerInvariant() ?? "";
            var id = gameObjectData.Id?.ToLowerInvariant() ?? "";

            // Debug: Log all GameObjects with "orb" in the name

            // First, check for definite exclusions - UI elements, sprites, and non-gameplay objects
            var strongExclusions = new[] { "ui", "canvas", "text", "button", "panel", "scroll", "image", "background", "main camera", "directional light" };
            var isStronglyExcluded = strongExclusions.Any(exclusion => name.Contains(exclusion));

            if (isStronglyExcluded)
            {
                Logger.Debug($"❌ GameObject {name} excluded by strong exclusion patterns");
                return false;
            }

            // Check for definitive orb data structures
            if (gameObjectData.RawData is Dictionary<string, object> rawData)
            {
                // Debug: log structure for orb GameObjects
                if (name.Contains("debuffOrb", StringComparison.OrdinalIgnoreCase) || name.Contains("debufforb", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"\n🔍 {name} RawData structure:");
                    Console.WriteLine($"   RawData keys: {string.Join(", ", rawData.Keys)}");
                    foreach (var key in rawData.Keys)
                    {
                        if (rawData[key] is Dictionary<string, object> subDict)
                        {
                            Console.WriteLine($"   {key} -> Dictionary with keys: {string.Join(", ", subDict.Keys.Take(10))}");
                        }
                        else
                        {
                            Console.WriteLine($"   {key} -> {rawData[key]?.GetType().Name ?? "null"}");
                        }
                    }
                }

                // Look for ComponentData.OrbComponent structure which indicates actual orb data
                if (rawData.TryGetValue("ComponentData", out var componentDataObj) &&
                    componentDataObj is Dictionary<string, object> componentData &&
                    componentData.ContainsKey("OrbComponent"))
                {
                    Logger.Debug($"✅ GameObject {name} has OrbComponent data - confirmed orb");
                    return true;
                }

                // Look for direct OrbComponent in RawData
                if (rawData.ContainsKey("OrbComponent"))
                {
                    Logger.Debug($"✅ GameObject {name} has direct OrbComponent data - confirmed orb");
                    return true;
                }

                // Look for orb-specific fields that indicate this is actual orb data
                var orbSpecificFields = new[] { "DamagePerPeg", "CritDamagePerPeg", "Level", "locNameString", "locName" };
                var hasOrbFields = orbSpecificFields.Count(field => rawData.ContainsKey(field)) >= 3;

                if (hasOrbFields)
                {
                    Logger.Debug($"✅ GameObject {name} has orb-specific fields - confirmed orb");
                    return true;
                }
            }

            // Check component types for specific orb-related components (more restrictive than before)
            var componentTypes = gameObjectData.Components.Select(c => c.Type).ToList();
            var restrictiveOrbComponents = new[] { "OrbComponent", "AttackComponent", "PachinkoBallComponent" };
            var hasRestrictiveOrbComponents = restrictiveOrbComponents.Any(pattern =>
                componentTypes.Any(type => type.Contains(pattern)));

            if (hasRestrictiveOrbComponents)
            {
                Logger.Debug($"✅ GameObject {name} has restrictive orb components - confirmed orb");
                return true;
            }

            // Only include objects with "orb" or "-lvl" patterns that suggest they're actual orb entities
            var hasOrbPattern = name.Contains("orb") || name.Contains("-lvl") ||
                               id.Contains("orb") || id.Contains("-lvl");

            if (!hasOrbPattern)
            {
                Logger.Debug($"❌ GameObject {name} lacks orb patterns - not an orb");
                return false;
            }

            Logger.Debug($"🔍 GameObject {name} passed basic orb pattern check but lacks component data");
            return false;
        }

    }
}

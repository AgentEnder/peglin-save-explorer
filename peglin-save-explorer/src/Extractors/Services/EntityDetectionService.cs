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
            // Must have _renderer field and at least 2 other PachinkoBall-specific fields
            return data.ContainsKey("_renderer") && pachinkoBallCount >= 3;
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
            
            // Strong exclusion patterns - these are definitely NOT orbs
            var strongExclusions = new[] { "ui", "canvas", "text", "button", "panel", "scroll", "image", "background" };
            var isStronglyExcluded = strongExclusions.Any(exclusion => name.Contains(exclusion));
            
            if (isStronglyExcluded)
            {
                return false;
            }

            if (gameObjectData.RawData is Dictionary<string, object> rawData)
            {
                if (rawData.TryGetValue("ComponentData", out var componentDataObj) &&
                    componentDataObj is Dictionary<string, object> componentData &&
                    componentData.ContainsKey("OrbComponent"))
                {
                    return true;
                }

                if (rawData.ContainsKey("OrbComponent"))
                {
                    return true;
                }

                // Check for orb fields in raw data
                var orbFields = new[] { "DamagePerPeg", "CritDamagePerPeg", "Level", "locNameString", "locDescStrings" };
                var hasOrbFields = orbFields.Any(field => rawData.ContainsKey(field));

                if (hasOrbFields)
                {
                    return true;
                }
            }

            // Check component types for restrictive orb patterns
            var restrictiveOrbComponents = new[] { "OrbComponent", "AttackComponent", "PachinkoBallComponent" };
            var hasRestrictiveOrbComponents = gameObjectData.Components.Any(comp => 
                restrictiveOrbComponents.Any(pattern => comp.Type.ToLowerInvariant().Contains(pattern.ToLowerInvariant())));

            // Check name patterns
            var orbPatterns = new[] { "orb", "ball", "attack", "projectile" };
            var hasOrbPattern = orbPatterns.Any(pattern => name.Contains(pattern));

            if (hasRestrictiveOrbComponents)
            {
                return true;
            }

            if (!hasOrbPattern)
            {
                return false;
            }

            return hasOrbPattern && gameObjectData.Components.Count > 0;
        }
    }
}

using System;
using System.Collections.Generic;

namespace peglin_save_explorer.Data
{
    /// <summary>
    /// Represents orb data extracted from Peglin assets
    /// </summary>
    public class OrbData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string? LocKey { get; set; }
        public float? DamagePerPeg { get; set; }
        public float? CritDamagePerPeg { get; set; }
        public int? RarityValue { get; set; }
        public string? Rarity { get; set; }  // Human-readable rarity name
        public string? OrbType { get; set; } // e.g., "ATTACK", "UTILITY", "SPECIAL"
        public Dictionary<string, object> RawData { get; set; } = new();
        
        // Extended fields for level detection
        public string? BaseId { get; set; } // Base ID without level suffix
        public int? Level { get; set; } // Detected level (1, 2, 3, etc.)
        public string? DisplayName { get; set; } // Localized display name
        public string? LocalizedDescription { get; set; } // Localized description
        
        // Sprite correlation fields
        public string? CorrelatedSpriteId { get; set; }
        public string? SpriteFilePath { get; set; }
        public float CorrelationConfidence { get; set; }
        public string? CorrelationMethod { get; set; }
        public List<string> AlternateSpriteIds { get; set; } = new();
    }

    /// <summary>
    /// Represents relic data extracted from Peglin assets
    /// </summary>
    public class RelicData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Effect { get; set; } = "";
        public int RarityValue { get; set; }
        public string Rarity { get; set; } = "";  // Human-readable rarity name
        public Dictionary<string, object> RawData { get; set; } = new();
        
        // Sprite correlation fields
        public string? CorrelatedSpriteId { get; set; }
        public string? SpriteFilePath { get; set; }
        public float CorrelationConfidence { get; set; }
        public string? CorrelationMethod { get; set; }
        public List<string> AlternateSpriteIds { get; set; } = new();
    }

    /// <summary>
    /// Represents enemy data extracted from Peglin assets
    /// </summary>
    public class EnemyData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = ""; // NORMAL, MINIBOSS, BOSS
        public string? Description { get; set; }
        public string? LocKey { get; set; }
        public float? MaxHealth { get; set; }
        public float? MaxHealthCruciball { get; set; }
        public float? MeleeAttackDamage { get; set; }
        public float? RangedAttackDamage { get; set; }
        public string? Location { get; set; } // FOREST, CASTLE, MINES
        public int? PointsOnKill { get; set; }
        public float? LootDropRate { get; set; }
        public Dictionary<string, object> RawData { get; set; } = new();
        
        // Sprite correlation fields
        public string? CorrelatedSpriteId { get; set; }
        public string? SpriteFilePath { get; set; }
        public float CorrelationConfidence { get; set; }
        public string? CorrelationMethod { get; set; }
        public List<string> AlternateSpriteIds { get; set; } = new();
    }

    /// <summary>
    /// Represents grouped orb data (family of orbs with different levels)
    /// </summary>
    public class OrbGroupedData
    {
        public string Id { get; set; } = "";
        public string? LocKey { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int RarityValue { get; set; }
        public string? Rarity { get; set; }
        public string? OrbType { get; set; }
        public string? CorrelatedSpriteId { get; set; }
        public string? SpriteFilePath { get; set; }
        public float CorrelationConfidence { get; set; }
        public string? CorrelationMethod { get; set; }
        public List<string> AlternateSpriteIds { get; set; } = new();
        public List<OrbLevelData> Levels { get; set; } = new();
        public Dictionary<string, object> RawData { get; set; } = new();
    }

    /// <summary>
    /// Represents individual level data for a grouped orb
    /// </summary>
    public class OrbLevelData
    {
        public int Level { get; set; }
        public OrbData Leaf { get; set; } = new();
        public string LeafId { get; set; } = "";
    }

    /// <summary>
    /// Enumeration of supported sprite types
    /// </summary>
    public enum SpriteType
    {
        Unknown,
        Relic,
        Orb,
        Enemy,
        UI,
        Background,
        Effect
    }
}
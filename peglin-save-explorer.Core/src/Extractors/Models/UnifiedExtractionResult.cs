using System.Collections.Generic;
using peglin_save_explorer.Data;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Extractors.Models
{
    /// <summary>
    /// Contains the complete result of unified asset extraction
    /// </summary>
    public class UnifiedExtractionResult
    {
        public Dictionary<string, RelicData> Relics { get; set; } = new();
        public Dictionary<string, EnemyData> Enemies { get; set; } = new();
        public Dictionary<string, OrbData> Orbs { get; set; } = new();
        public Dictionary<string, OrbGroupedData> OrbFamilies { get; set; } = new();
        public Dictionary<string, SpriteCacheManager.SpriteMetadata> Sprites { get; set; } = new();
        public Dictionary<string, string> RelicSpriteCorrelations { get; set; } = new();
        public Dictionary<string, string> EnemySpriteCorrelations { get; set; } = new();
        public Dictionary<string, string> OrbSpriteCorrelations { get; set; } = new();
    }
}

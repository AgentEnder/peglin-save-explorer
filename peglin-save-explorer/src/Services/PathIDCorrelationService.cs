using System;
using System.Collections.Generic;
using System.Linq;

namespace peglin_save_explorer.Services
{
    /// <summary>
    /// Service for correlating entities with sprites using PathID-based precision matching
    /// This replaces fuzzy name matching with exact Unity asset reference resolution
    /// </summary>
    public class PathIDCorrelationService
    {
        private readonly Dictionary<long, string> _spritePathIdToSpriteId = new();
        private readonly Dictionary<string, long> _relicIdToPathId = new();
        private readonly Dictionary<string, long> _enemyIdToPathId = new();
        private readonly Dictionary<string, long> _orbIdToPathId = new();

        /// <summary>
        /// Registers sprite PathID to sprite ID mappings for correlation
        /// Called during sprite extraction phase
        /// </summary>
        public void RegisterSpritePathId(long pathId, string spriteId)
        {
            _spritePathIdToSpriteId[pathId] = spriteId;
            Console.WriteLine($"[PathIDCorrelation] Registered sprite PathID {pathId} -> {spriteId}");
        }

        /// <summary>
        /// Registers relic sprite references extracted during entity processing
        /// </summary>
        public void RegisterRelicSpriteReferences(Dictionary<string, long> relicSpriteReferences)
        {
            foreach (var kvp in relicSpriteReferences)
            {
                _relicIdToPathId[kvp.Key] = kvp.Value;
            }
            Console.WriteLine($"[PathIDCorrelation] Registered {relicSpriteReferences.Count} relic sprite references");
        }

        /// <summary>
        /// Registers enemy sprite references extracted during entity processing
        /// </summary>
        public void RegisterEnemySpriteReferences(Dictionary<string, long> enemySpriteReferences)
        {
            foreach (var kvp in enemySpriteReferences)
            {
                _enemyIdToPathId[kvp.Key] = kvp.Value;
            }
            Console.WriteLine($"[PathIDCorrelation] Registered {enemySpriteReferences.Count} enemy sprite references");
        }

        /// <summary>
        /// Registers orb sprite references extracted during entity processing
        /// </summary>
        public void RegisterOrbSpriteReferences(Dictionary<string, long> orbSpriteReferences)
        {
            foreach (var kvp in orbSpriteReferences)
            {
                _orbIdToPathId[kvp.Key] = kvp.Value;
            }
            Console.WriteLine($"[PathIDCorrelation] Registered {orbSpriteReferences.Count} orb sprite references");
        }

        /// <summary>
        /// Attempts to find the exact sprite ID for a given relic using PathID correlation
        /// Returns null if no exact match is found
        /// </summary>
        public string? GetCorrelatedSpriteForRelic(string relicId)
        {
            if (_relicIdToPathId.TryGetValue(relicId, out var pathId) &&
                _spritePathIdToSpriteId.TryGetValue(pathId, out var spriteId))
            {
                Console.WriteLine($"[PathIDCorrelation] Found exact sprite match for relic {relicId}: {spriteId} (PathID: {pathId})");
                return spriteId;
            }

            return null;
        }

        /// <summary>
        /// Attempts to find the exact sprite ID for a given enemy using PathID correlation
        /// Returns null if no exact match is found
        /// </summary>
        public string? GetCorrelatedSpriteForEnemy(string enemyId)
        {
            if (_enemyIdToPathId.TryGetValue(enemyId, out var pathId) &&
                _spritePathIdToSpriteId.TryGetValue(pathId, out var spriteId))
            {
                Console.WriteLine($"[PathIDCorrelation] Found exact sprite match for enemy {enemyId}: {spriteId} (PathID: {pathId})");
                return spriteId;
            }

            return null;
        }

        /// <summary>
        /// Attempts to find the exact sprite ID for a given orb using PathID correlation
        /// Returns null if no exact match is found
        /// </summary>
        public string? GetCorrelatedSpriteForOrb(string orbId)
        {
            if (_orbIdToPathId.TryGetValue(orbId, out var pathId) &&
                _spritePathIdToSpriteId.TryGetValue(pathId, out var spriteId))
            {
                Console.WriteLine($"[PathIDCorrelation] Found exact sprite match for orb {orbId}: {spriteId} (PathID: {pathId})");
                return spriteId;
            }

            return null;
        }

        /// <summary>
        /// Gets comprehensive correlation statistics for debugging
        /// </summary>
        public CorrelationStatistics GetCorrelationStatistics()
        {
            var stats = new CorrelationStatistics
            {
                TotalSpritesRegistered = _spritePathIdToSpriteId.Count,
                TotalRelicReferences = _relicIdToPathId.Count,
                TotalEnemyReferences = _enemyIdToPathId.Count,
                TotalOrbReferences = _orbIdToPathId.Count
            };

            // Calculate successful correlations
            stats.RelicCorrelationsFound = _relicIdToPathId.Count(kvp => _spritePathIdToSpriteId.ContainsKey(kvp.Value));
            stats.EnemyCorrelationsFound = _enemyIdToPathId.Count(kvp => _spritePathIdToSpriteId.ContainsKey(kvp.Value));
            stats.OrbCorrelationsFound = _orbIdToPathId.Count(kvp => _spritePathIdToSpriteId.ContainsKey(kvp.Value));

            // Find orphaned sprites (sprites with no entity references)
            var referencedPathIds = new HashSet<long>();
            referencedPathIds.UnionWith(_relicIdToPathId.Values);
            referencedPathIds.UnionWith(_enemyIdToPathId.Values);
            referencedPathIds.UnionWith(_orbIdToPathId.Values);
            
            stats.OrphanedSprites = _spritePathIdToSpriteId.Keys.Count(pathId => !referencedPathIds.Contains(pathId));

            return stats;
        }

        /// <summary>
        /// Clears all correlation data for fresh extraction
        /// </summary>
        public void ClearAll()
        {
            _spritePathIdToSpriteId.Clear();
            _relicIdToPathId.Clear();
            _enemyIdToPathId.Clear();
            _orbIdToPathId.Clear();
            Console.WriteLine("[PathIDCorrelation] Cleared all correlation data");
        }

        /// <summary>
        /// Statistics about PathID-based correlation success rates
        /// </summary>
        public class CorrelationStatistics
        {
            public int TotalSpritesRegistered { get; set; }
            public int TotalRelicReferences { get; set; }
            public int TotalEnemyReferences { get; set; }
            public int TotalOrbReferences { get; set; }
            public int RelicCorrelationsFound { get; set; }
            public int EnemyCorrelationsFound { get; set; }
            public int OrbCorrelationsFound { get; set; }
            public int OrphanedSprites { get; set; }

            public float RelicCorrelationRate => TotalRelicReferences > 0 ? (float)RelicCorrelationsFound / TotalRelicReferences : 0f;
            public float EnemyCorrelationRate => TotalEnemyReferences > 0 ? (float)EnemyCorrelationsFound / TotalEnemyReferences : 0f;
            public float OrbCorrelationRate => TotalOrbReferences > 0 ? (float)OrbCorrelationsFound / TotalOrbReferences : 0f;
            public float OverallCorrelationRate => 
                (TotalRelicReferences + TotalEnemyReferences + TotalOrbReferences) > 0 ? 
                (float)(RelicCorrelationsFound + EnemyCorrelationsFound + OrbCorrelationsFound) / 
                (TotalRelicReferences + TotalEnemyReferences + TotalOrbReferences) : 0f;
        }
    }
}
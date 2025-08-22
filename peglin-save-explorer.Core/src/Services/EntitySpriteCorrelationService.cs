using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using peglin_save_explorer.Data;
using peglin_save_explorer.Extractors;

namespace peglin_save_explorer.Services
{
    /// <summary>
    /// Service for correlating game entities (relics, enemies, classes, orbs) with their corresponding sprites
    /// </summary>
    public class EntitySpriteCorrelationService
    {
        private readonly SpriteCacheManager _spriteCacheManager;
        private readonly string _correlationCachePath;
        private Dictionary<string, EntitySpriteCorrelation> _correlationCache;
        
        public EntitySpriteCorrelationService(SpriteCacheManager spriteCacheManager)
        {
            _spriteCacheManager = spriteCacheManager;
            _correlationCachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PeglinSaveExplorer", "entity-sprite-correlations.json");
            _correlationCache = new Dictionary<string, EntitySpriteCorrelation>();
            LoadCorrelationCache();
        }

        public class EntitySpriteCorrelation
        {
            public string EntityId { get; set; } = "";
            public string EntityName { get; set; } = "";
            public string EntityType { get; set; } = ""; // "relic", "enemy", "class", "orb"
            public string? CorrelatedSpriteId { get; set; }
            public string? SpriteFilePath { get; set; }
            public float CorrelationConfidence { get; set; } // 0.0 - 1.0
            public string CorrelationMethod { get; set; } = ""; // "exact", "fuzzy", "manual"
            public List<string> AlternateSpriteIds { get; set; } = new();
            public DateTime LastUpdated { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// Correlates relic entities with their sprites
        /// </summary>
        public Dictionary<string, EntitySpriteCorrelation> CorrelateRelics(Dictionary<string, AssetRipperRelicExtractor.RelicData> relics)
        {
            var correlations = new Dictionary<string, EntitySpriteCorrelation>();
            
            if (relics == null || !relics.Any())
            {
                Console.WriteLine("[Correlation] No relics to correlate");
                return correlations;
            }
            
            var relicSprites = SpriteCacheManager.GetCachedSprites(SpriteCacheManager.SpriteType.Relic);
            if (relicSprites == null || !relicSprites.Any())
            {
                Console.WriteLine("[Correlation] No relic sprites found in cache");
                return correlations;
            }
            
            var relicSpritesDict = relicSprites.Where(s => s != null && !string.IsNullOrEmpty(s.Id)).ToDictionary(s => s.Id, s => s);
            
            Console.WriteLine($"[Correlation] Correlating {relics.Count} relics with {relicSprites.Count} relic sprites");

            foreach (var relic in relics)
            {
                if (relic.Value == null || string.IsNullOrEmpty(relic.Value.Name))
                {
                    Console.WriteLine($"[Correlation] Skipping relic with null or empty name: {relic.Key}");
                    continue;
                }
                
                var correlation = FindBestSpriteMatch(
                    entityId: relic.Key,
                    entityName: relic.Value.Name,
                    entityType: "relic",
                    availableSprites: relicSpritesDict.Select(kvp => new KeyValuePair<string, SpriteCacheManager.SpriteMetadata>(kvp.Key, kvp.Value)).ToList()
                );
                
                correlations[relic.Key] = correlation;
            }

            // Save correlations to cache
            try
            {
                SaveCorrelationCache(correlations);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Correlation] Failed to save relic correlations to cache: {ex.Message}");
            }
            return correlations;
        }

        /// <summary>
        /// Correlates enemy entities with their sprites
        /// </summary>
        public Dictionary<string, EntitySpriteCorrelation> CorrelateEnemies(Dictionary<string, AssetRipperEnemyExtractor.EnemyData> enemies)
        {
            var correlations = new Dictionary<string, EntitySpriteCorrelation>();
            
            if (enemies == null || !enemies.Any())
            {
                Console.WriteLine("[Correlation] No enemies to correlate");
                return correlations;
            }
            
            var enemySprites = SpriteCacheManager.GetCachedSprites(SpriteCacheManager.SpriteType.Enemy);
            if (enemySprites == null || !enemySprites.Any())
            {
                Console.WriteLine("[Correlation] No enemy sprites found in cache");
                return correlations;
            }
            
            var enemySpritesDict = enemySprites.Where(s => s != null && !string.IsNullOrEmpty(s.Id)).ToDictionary(s => s.Id, s => s);
            
            Console.WriteLine($"[Correlation] Correlating {enemies.Count} enemies with {enemySprites.Count} enemy sprites");

            foreach (var enemy in enemies)
            {
                if (enemy.Value == null || string.IsNullOrEmpty(enemy.Value.Name))
                {
                    Console.WriteLine($"[Correlation] Skipping enemy with null or empty name: {enemy.Key}");
                    continue;
                }
                
                var correlation = FindBestSpriteMatch(
                    entityId: enemy.Key,
                    entityName: enemy.Value.Name,
                    entityType: "enemy",
                    availableSprites: enemySpritesDict.Select(kvp => new KeyValuePair<string, SpriteCacheManager.SpriteMetadata>(kvp.Key, kvp.Value)).ToList()
                );
                
                correlations[enemy.Key] = correlation;
            }

            try
            {
                SaveCorrelationCache(correlations);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Correlation] Failed to save enemy correlations to cache: {ex.Message}");
            }
            return correlations;
        }

        /// <summary>
        /// Finds the best sprite match for a given entity using multiple correlation strategies
        /// </summary>
        private EntitySpriteCorrelation FindBestSpriteMatch(
            string entityId, 
            string entityName, 
            string entityType,
            List<KeyValuePair<string, SpriteCacheManager.SpriteMetadata>> availableSprites)
        {
            var correlation = new EntitySpriteCorrelation
            {
                EntityId = entityId,
                EntityName = entityName,
                EntityType = entityType
            };

            // Check cache first
            var cacheKey = $"{entityType}:{entityId}";
            if (_correlationCache.ContainsKey(cacheKey))
            {
                var cached = _correlationCache[cacheKey];
                // Verify cached sprite still exists
                if (availableSprites.Any(s => s.Key == cached.CorrelatedSpriteId))
                {
                    return cached;
                }
            }

            // Strategy 1: Exact name match (highest confidence)
            var exactMatch = FindExactNameMatch(entityName, availableSprites);
            if (exactMatch != null)
            {
                correlation.CorrelatedSpriteId = exactMatch.Value.Key;
                correlation.SpriteFilePath = exactMatch.Value.Value.FilePath;
                correlation.CorrelationConfidence = 1.0f;
                correlation.CorrelationMethod = "exact";
                return correlation;
            }

            // Strategy 2: Normalized name match (high confidence)
            var normalizedMatch = FindNormalizedNameMatch(entityName, availableSprites);
            if (normalizedMatch != null)
            {
                correlation.CorrelatedSpriteId = normalizedMatch.Value.Key;
                correlation.SpriteFilePath = normalizedMatch.Value.Value.FilePath;
                correlation.CorrelationConfidence = 0.9f;
                correlation.CorrelationMethod = "normalized";
                return correlation;
            }

            // Strategy 3: Fuzzy matching (medium confidence)
            var fuzzyMatch = FindFuzzyNameMatch(entityName, availableSprites);
            if (fuzzyMatch != null)
            {
                correlation.CorrelatedSpriteId = fuzzyMatch.Value.Match.Key;
                correlation.SpriteFilePath = fuzzyMatch.Value.Match.Value.FilePath;
                correlation.CorrelationConfidence = fuzzyMatch.Value.Confidence;
                correlation.CorrelationMethod = "fuzzy";
                return correlation;
            }

            // Strategy 4: Keyword matching (lower confidence)
            var keywordMatch = FindKeywordMatch(entityName, availableSprites);
            if (keywordMatch != null)
            {
                correlation.CorrelatedSpriteId = keywordMatch.Value.Key;
                correlation.SpriteFilePath = keywordMatch.Value.Value.FilePath;
                correlation.CorrelationConfidence = 0.6f;
                correlation.CorrelationMethod = "keyword";
                return correlation;
            }

            // No match found
            correlation.CorrelationConfidence = 0.0f;
            correlation.CorrelationMethod = "none";
            Console.WriteLine($"[Correlation] No sprite match found for {entityType}: {entityName}");
            
            return correlation;
        }

        private KeyValuePair<string, SpriteCacheManager.SpriteMetadata>? FindExactNameMatch(
            string entityName, List<KeyValuePair<string, SpriteCacheManager.SpriteMetadata>> sprites)
        {
            if (string.IsNullOrEmpty(entityName) || sprites == null || !sprites.Any()) 
                return null;
                
            var normalizedEntityName = NormalizeName(entityName);
            return sprites.FirstOrDefault(s => s.Value != null && !string.IsNullOrEmpty(s.Value.Name) && NormalizeName(s.Value.Name) == normalizedEntityName);
        }

        private KeyValuePair<string, SpriteCacheManager.SpriteMetadata>? FindNormalizedNameMatch(
            string entityName, List<KeyValuePair<string, SpriteCacheManager.SpriteMetadata>> sprites)
        {
            var normalizedEntityName = NormalizeName(entityName);
            
            // Try common variations
            var variations = new[]
            {
                normalizedEntityName.Replace(" ", "_"),
                normalizedEntityName.Replace("_", " "),
                normalizedEntityName.Replace(" ", ""),
                normalizedEntityName.Replace("_", "")
            };

            foreach (var variation in variations)
            {
                var match = sprites.FirstOrDefault(s => NormalizeName(s.Value.Name).Contains(variation) || variation.Contains(NormalizeName(s.Value.Name)));
                if (!match.Equals(default(KeyValuePair<string, SpriteCacheManager.SpriteMetadata>)))
                    return match;
            }

            return null;
        }

        private (KeyValuePair<string, SpriteCacheManager.SpriteMetadata> Match, float Confidence)? FindFuzzyNameMatch(
            string entityName, List<KeyValuePair<string, SpriteCacheManager.SpriteMetadata>> sprites)
        {
            var bestMatch = default(KeyValuePair<string, SpriteCacheManager.SpriteMetadata>);
            var bestConfidence = 0.0f;
            var threshold = 0.7f; // Minimum similarity threshold

            var normalizedEntityName = NormalizeName(entityName);

            foreach (var sprite in sprites)
            {
                var spriteName = NormalizeName(sprite.Value.Name);
                var similarity = CalculateStringSimilarity(normalizedEntityName, spriteName);
                
                if (similarity > bestConfidence && similarity >= threshold)
                {
                    bestConfidence = similarity;
                    bestMatch = sprite;
                }
            }

            if (bestConfidence >= threshold)
                return (bestMatch, bestConfidence);

            return null;
        }

        private KeyValuePair<string, SpriteCacheManager.SpriteMetadata>? FindKeywordMatch(
            string entityName, List<KeyValuePair<string, SpriteCacheManager.SpriteMetadata>> sprites)
        {
            var keywords = ExtractKeywords(entityName);
            
            foreach (var sprite in sprites)
            {
                var spriteKeywords = ExtractKeywords(sprite.Value.Name);
                if (keywords.Intersect(spriteKeywords, StringComparer.OrdinalIgnoreCase).Any())
                {
                    return sprite;
                }
            }

            return null;
        }

        public string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";
            return Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]", "");
        }

        private List<string> ExtractKeywords(string text)
        {
            // Remove common words and extract meaningful keywords
            var commonWords = new HashSet<string> { "the", "of", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "with", "by" };
            var words = Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9]+")
                             .Where(w => w.Length > 2 && !commonWords.Contains(w))
                             .ToList();
            return words;
        }

        private float CalculateStringSimilarity(string s1, string s2)
        {
            // Levenshtein distance-based similarity
            var distance = LevenshteinDistance(s1, s2);
            var maxLength = Math.Max(s1.Length, s2.Length);
            return maxLength == 0 ? 1.0f : 1.0f - (float)distance / maxLength;
        }

        private int LevenshteinDistance(string s1, string s2)
        {
            var matrix = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                matrix[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[s1.Length, s2.Length];
        }

        /// <summary>
        /// Gets uncorrelated sprites for the misc bin
        /// </summary>
        public List<SpriteCacheManager.SpriteMetadata> GetUncorrelatedSprites(
            Dictionary<string, EntitySpriteCorrelation> correlations)
        {
            var allSprites = SpriteCacheManager.GetCachedSprites();
            var correlatedSpriteIds = new HashSet<string>(
                correlations.Values
                    .Where(c => !string.IsNullOrEmpty(c.CorrelatedSpriteId))
                    .Select(c => c.CorrelatedSpriteId!)
            );

            return allSprites
                .Where(sprite => !correlatedSpriteIds.Contains(sprite.Id))
                .OrderBy(sprite => sprite.Type)
                .ThenBy(sprite => sprite.Name)
                .ToList();
        }

        private void LoadCorrelationCache()
        {
            try
            {
                if (File.Exists(_correlationCachePath))
                {
                    var json = File.ReadAllText(_correlationCachePath);
                    _correlationCache = JsonSerializer.Deserialize<Dictionary<string, EntitySpriteCorrelation>>(json) 
                                      ?? new Dictionary<string, EntitySpriteCorrelation>();
                    Console.WriteLine($"[Correlation] Loaded {_correlationCache.Count} cached correlations");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Correlation] Failed to load correlation cache: {ex.Message}");
                _correlationCache = new Dictionary<string, EntitySpriteCorrelation>();
            }
        }

        private void SaveCorrelationCache(Dictionary<string, EntitySpriteCorrelation> newCorrelations)
        {
            try
            {
                if (newCorrelations == null || !newCorrelations.Any())
                    return;
                    
                // Merge with existing cache
                foreach (var correlation in newCorrelations)
                {
                    if (correlation.Value == null || string.IsNullOrEmpty(correlation.Value.EntityType) || string.IsNullOrEmpty(correlation.Key))
                        continue;
                        
                    var cacheKey = $"{correlation.Value.EntityType}:{correlation.Key}";
                    _correlationCache[cacheKey] = correlation.Value;
                }

                // Ensure directory exists
                var directory = Path.GetDirectoryName(_correlationCachePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_correlationCache, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_correlationCachePath, json);
                
                Console.WriteLine($"[Correlation] Saved {_correlationCache.Count} correlations to cache");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Correlation] Failed to save correlation cache: {ex.Message}");
            }
        }
    }
}
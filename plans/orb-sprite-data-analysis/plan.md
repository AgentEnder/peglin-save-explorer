# Orb Extraction and Sprite Data Parity Plan

Goal: Bring orbs to feature parity with relics in extraction, localization, sprite correlation, grouping, caching, and API presentation.

Outcomes

- Consolidate per-level orb assets into a single "orb family" with Levels[].
- Resolve localized display name and description (prefer localization service; fallbacks).
- Correlate a stable sprite per orb family and record alternates.
- Persist only the family cache; serve families via API.

Milestones

1. Data modeling
2. Extraction enhancements (per-level -> family consolidation)
3. Localization resolution for orbs
4. Sprite correlation parity for orbs
5. Caching and loaders
6. API updates
7. Telemetry/validation & tooling

---

1. Data modeling

- Add DTO `OrbFamily`:
  - Id (baseId), Name, Description
  - OrbType, Rarity (string), RarityValue (int?)
  - Levels: List<LevelInfo> where LevelInfo { Level:int, DamagePerPeg?:float, CritDamagePerPeg?:float, RawData:Dictionary<string,object> }
  - CorrelatedSpriteId?:string, SpriteFilePath?:string, CorrelationMethod?:string, CorrelationConfidence:float
  - AlternateSpriteIds: List<string>
- Extend `UnifiedExtractionResult` with `Dictionary<string, OrbFamily> OrbFamilies`.
- Extend `AssetRipperOrbExtractor.OrbData`:
  - BaseId:string, Level:int? (detected), DisplayName? and LocalizedDescription? post-resolution.

2. Extraction enhancements

- In `AssetRipperOrbExtractor`:
  - Detect level and baseId during `ExtractOrbFromData`:
    - From asset name: strip suffixes (\_L1, \_L2, Level1, Level2, trailing digits), capture numeric as Level.
    - From data fields: level/upgradeLevel/tier if present.
    - Fallback: Level = 1; BaseId = CleanOrbId(name) without level.
  - Store sprite PathID per orb (already captured) and locKey.
- In `UnifiedAssetExtractor.ProcessCollectionWithCrossReferences` (or a dedicated step):
  - Group `result.Orbs` by BaseId to build `OrbFamilies`.
  - For each family:
    - Sort by Level; build Levels[] with stats.
    - Choose Name/Description (see localization step).
    - Choose sprite: prefer common across levels; else Level 1; keep others in AlternateSpriteIds.
  - Populate `result.OrbFamilies` and `result.OrbSpriteCorrelations` per level; select one for family.

3. Localization resolution for orbs

- Use `Services.LocalizationService.Instance` similar to class extractor:
  - EnsureLoaded() once per extraction.
  - For each orb (or at family stage):
    - If `locKey` present, attempt translations for display name/description.
    - If locKey appears level-specific (e.g., \_L1), also try base locKey for family-level text.
  - Fallbacks: `englishDisplayName`, `englishDescription`, then formatted asset name.

4. Sprite correlation parity

- Ensure orb sprite PPtr resolution path parallels relics:
  - Maintain `result.OrbSpriteCorrelations[orbId]` during bundle processing when resolving sprites.
  - While consolidating families, set `CorrelatedSpriteId` to chosen level’s correlation and collect alternates.

5. Caching and loaders

- Entity cache files:
  - Write `entities/orb_families.json` with `Dictionary<string, OrbFamily>`.
  - Do not persist per-level orbs unless needed for debugging.
- Update `EntityCacheManager`:
  - Add file path, DTO, `SaveOrbFamiliesToCache`, `GetCachedOrbFamilies()` only.
  - Remove compatibility fallbacks; API and UI consume families.
- Ensure SaveToCache keeps in-memory caches synchronized.

6. API updates

- Web `/api/entities` -> orbs:
  - Return orb families only: family-shaped objects with Levels[] and `spriteReference` derived from `CorrelatedSpriteId`.
  - Include `rarity`, `orbType`, localized `name`/`description`.

7. Telemetry/validation & tooling

- Add debug logging counters during consolidation: families count, average levels per family, unmatched levels.
- Add a CLI subcommand or dev route to dump an orb family summary for sanity checks.
- Unit tests (where feasible): level parsing, baseId derivation, localization fallback, family consolidation.

Rollout steps

- Implement DTOs and family cache additions.
- Implement level detection and baseId derivation in orb extractor.
- Implement family consolidation in unified extractor.
- Implement localization resolution.
- Update cache save/load for families and remove old per-level usage from API.
- Update API to serve families.
- Test end-to-end; adjust mappings and patterns as needed.

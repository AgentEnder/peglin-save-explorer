import React from "react";
import { create } from "zustand";
import { subscribeWithSelector } from "zustand/middleware";
import { useShallow } from "zustand/react/shallow";

export interface Sprite {
  id: string;
  name: string;
  type: "relic" | "enemy" | "orb";
  width: number;
  height: number;
  url: string;
  isAtlas: boolean;
  frameCount: number;
  extractedAt: string;
  sourceBundle: string;
}

export interface OrbLevel {
  level: number;
  damagePerPeg?: string;
  critDamagePerPeg?: string;
}

export interface Entity {
  id: string;
  name: string;
  type: "relic" | "enemy" | "orb";
  description?: string;
  rarity?: string;
  effect?: string;
  spriteReference?: Sprite;
  
  // Orb-specific properties for orb families
  orbType?: string;
  levels?: OrbLevel[];
}

interface EntitiesData {
  relics: Entity[];
  enemies: Entity[];
  orbs: Entity[];
}

interface SpriteState {
  // Data
  entities: EntitiesData | null;
  sprites: Sprite[];
  spritesByName: Map<string, Sprite>;
  spritesByType: Map<string, Sprite[]>;
  isLoading: boolean;
  error: string | null;
  isInitialized: boolean;

  // Actions
  initialize: () => Promise<void>;
  refresh: () => Promise<void>;
  clearError: () => void;

  // Sprite utilities
  getSpriteByName: (name: string) => Sprite | null;
  getSpritesByType: (type: "relic" | "enemy" | "orb") => Sprite[];
  getEntitySprite: (entity: Entity) => Sprite | null;

  // Text processing
  substituteSprites: (text: string) => React.ReactElement;
}

// Transform sprite names from TMP format to actual sprite names
function transformSpriteNameForLookup(spriteName: string): string {
  // Convert to lowercase for initial lookup
  const lowerName = spriteName.toLowerCase();
  
  // Handle common sprite name transformations
  const transformations: Record<string, string> = {
    // Status effects (from StatusEffectType enum)
    'strength': 'balance_status',
    'finesse': 'dexspherity_status', 
    'spindividualism': 'spindividualism_status',
    'ballwark': 'ballwark_status',
    'muscircle': 'muscircle_status',
    'refreshing': 'refreshing_status',
    'multiball': 'multiball_status',
    'piercing': 'piercing_status',
    'poison': 'poison_status',
    'burn': 'burn_status',
    'electrify': 'electrify_status',
    
    // Common game elements
    'bomb': 'bomb_regular',
    'bomb_regular': 'bomb_regular',
    'gold': 'coin_only',
    'coin_only': 'coin_only',
    'peg': 'peg',
    'refresh_peg': 'refresh_peg',
    'crit_peg': 'crit_peg',
    'peg_shielded': 'peg_shielded',
    
    // Alternate naming patterns
    'coin': 'coin_only',
    'damage': 'damage_status',
    'health': 'health_status',
    'shield': 'ballwark_status',
    'critical': 'crit_peg',
  };
  
  // Check for direct transformation
  if (transformations[lowerName]) {
    return transformations[lowerName];
  }
  
  // If no transformation found, try the name as-is (lowercased)
  return lowerName;
}

export const useSpriteStore = create<SpriteState>()(
  subscribeWithSelector((set, get) => ({
    // Initial state
    entities: null,
    sprites: [],
    spritesByName: new Map(),
    spritesByType: new Map(),
    isLoading: false,
    error: null,
    isInitialized: false,

    // Initialize the sprite store by loading all entities and sprites
    initialize: async () => {
      if (get().isInitialized) return;

      set({ isLoading: true, error: null });

      try {
        // Load entities with sprite references
        const entitiesResponse = await fetch("/api/entities");
        if (!entitiesResponse.ok) {
          throw new Error(
            `Failed to load entities: ${entitiesResponse.statusText}`
          );
        }
        const entitiesData = await entitiesResponse.json();

        // Load all sprites separately for name-based lookups
        const spritesResponse = await fetch("/api/sprites");
        if (!spritesResponse.ok) {
          throw new Error(
            `Failed to load sprites: ${spritesResponse.statusText}`
          );
        }
        const spritesData = await spritesResponse.json();

        // Build sprite lookup maps
        const spritesByName = new Map<string, Sprite>();
        const spritesByType = new Map<string, Sprite[]>();

        const allSprites: Sprite[] = spritesData.success
          ? spritesData.data.sprites
          : [];

        // Index sprites by name and type
        allSprites.forEach((sprite) => {
          spritesByName.set(sprite.name.toLowerCase(), sprite);
          spritesByName.set(sprite.id.toLowerCase(), sprite);

          if (!spritesByType.has(sprite.type)) {
            spritesByType.set(sprite.type, []);
          }
          spritesByType.get(sprite.type)!.push(sprite);
        });

        // Also add entity sprites to the lookup
        const addEntitySprites = (entities: Entity[]) => {
          entities.forEach((entity) => {
            if (entity.spriteReference) {
              const sprite = entity.spriteReference;
              spritesByName.set(sprite.name.toLowerCase(), sprite);
              spritesByName.set(sprite.id.toLowerCase(), sprite);
              spritesByName.set(
                entity.name.toLowerCase().replace(/[^a-z0-9]/g, ""),
                sprite
              );
              spritesByName.set(entity.id.toLowerCase(), sprite);
            }
          });
        };

        if (entitiesData.success) {
          addEntitySprites(entitiesData.data.relics || []);
          addEntitySprites(entitiesData.data.enemies || []);
          addEntitySprites(entitiesData.data.orbs || []);
        }

        set({
          entities: entitiesData.success ? entitiesData.data : null,
          sprites: allSprites,
          spritesByName,
          spritesByType,
          isLoading: false,
          isInitialized: true,
          error: null,
        });

        console.log("Sprite store initialized:", {
          totalSprites: allSprites.length,
          namedSprites: spritesByName.size,
          entities: entitiesData.success
            ? {
                relics: entitiesData.data.relics?.length || 0,
                enemies: entitiesData.data.enemies?.length || 0,
                orbs: entitiesData.data.orbs?.length || 0,
              }
            : null,
        });
      } catch (error) {
        console.error("Error initializing sprite store:", error);
        set({
          error:
            error instanceof Error
              ? error.message
              : "Failed to initialize sprites",
          isLoading: false,
          isInitialized: true,
        });
      }
    },

    // Refresh sprite data
    refresh: async () => {
      set({ isInitialized: false });
      await get().initialize();
    },

    // Clear error state
    clearError: () => {
      set({ error: null });
    },

    // Get sprite by name (case-insensitive)
    getSpriteByName: (name: string) => {
      const { spritesByName } = get();
      const normalizedName = name.toLowerCase();

      // Try direct lookup first
      let sprite = spritesByName.get(normalizedName);
      if (sprite) return sprite;

      // Try transformed name
      const transformedName = transformSpriteNameForLookup(name);
      sprite = spritesByName.get(transformedName);
      if (sprite) return sprite;

      return null;
    },

    // Get sprites by type
    getSpritesByType: (type: "relic" | "enemy" | "orb") => {
      const { spritesByType } = get();
      return spritesByType.get(type) || [];
    },

    // Get sprite for an entity using its sprite reference
    getEntitySprite: (entity: Entity) => {
      if (entity.spriteReference) {
        return entity.spriteReference;
      }
      return null;
    },

    // Substitute sprite references in text with actual sprite components and strip style tags
    substituteSprites: (text: string) => {
      const { getSpriteByName, spritesByName } = get();

      // First, strip Unity style tags like <style=balance>text</style>
      const processedText = text
        .replace(/<style=[^>]*>/g, '') // Remove opening style tags
        .replace(/<\/style>/g, '');    // Remove closing style tags

      // Parse sprite tags like <sprite name="BOMB">
      const spriteRegex = /<sprite\s+name="([^"]+)"\s*>/g;
      const parts = [];
      let lastIndex = 0;
      let match: RegExpExecArray | null;

      while ((match = spriteRegex.exec(processedText) as RegExpExecArray | null) !== null) {
        // Add text before the sprite tag
        if (match.index > lastIndex) {
          parts.push(processedText.substring(lastIndex, match.index));
        }

        // Transform sprite name and find sprite
        const spriteName = match[1];
        
        // Simple transformation: convert to lowercase and handle common patterns
        const transformedName = transformSpriteNameForLookup(spriteName);
        console.log(`🔍 Sprite transformation: "${spriteName}" -> "${transformedName}"`);
        const sprite = getSpriteByName(transformedName);
        console.log(`🎯 Sprite lookup result:`, sprite ? `found ${sprite.name}` : 'not found');

        if (sprite) {
          parts.push(
            React.createElement("img", {
              key: `sprite-${match.index}`,
              src: sprite.url,
              alt: sprite.name,
              style: {
                width: "16px",
                height: "16px",
                display: "inline-block",
                verticalAlign: "middle",
                margin: "0 2px",
              },
              title: sprite.name,
            })
          );
        } else {
          console.log(`❌ Sprite not found for "${spriteName}" (transformed: "${transformedName}")`);
          console.log('Available sprites:', Array.from(spritesByName.keys()).slice(0, 10), '...');
          // Keep original text if sprite not found
          parts.push(match[0]);
        }

        lastIndex = spriteRegex.lastIndex;
      }

      // Add remaining text
      if (lastIndex < processedText.length) {
        parts.push(processedText.substring(lastIndex));
      }

      return React.createElement(
        React.Fragment,
        { key: "sprite-text" },
        ...parts
      );
    },
  }))
);

// Selector hooks for easy access to specific data
export const useEntities = () => useSpriteStore((state) => state.entities);
export const useSprites = () => useSpriteStore((state) => state.sprites);
export const useSpriteLoading = () =>
  useSpriteStore((state) => state.isLoading);
export const useSpriteError = () => useSpriteStore((state) => state.error);
export const useSpriteInitialized = () =>
  useSpriteStore((state) => state.isInitialized);

// Action hooks
export const useSpriteActions = () =>
  useSpriteStore(
    useShallow((state) => ({
      initialize: state.initialize,
      refresh: state.refresh,
      clearError: state.clearError,
      getSpriteByName: state.getSpriteByName,
      getSpritesByType: state.getSpritesByType,
      getEntitySprite: state.getEntitySprite,
      substituteSprites: state.substituteSprites,
    }))
  );

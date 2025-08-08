import React, { useState, useEffect, useMemo } from "react";
import {
  Box,
  TextField,
  Autocomplete,
  Card,
  CardContent,
  Grid,
  Typography,
  Chip,
  Avatar,
  Paper,
  Alert,
  CircularProgress,
  Tabs,
  Tab,
  FormControlLabel,
  Switch,
  Tooltip,
} from "@mui/material";
import { Search as SearchIcon, HelpOutline } from "@mui/icons-material";
import { useEntities, useSpriteLoading, useSpriteError, useSpriteActions, Entity, Sprite } from "../store/useSpriteStore";
import SpriteText from "./SpriteText";
import { getRarityName, getRarityColor, getRarityTooltip, isUnavailableRarity } from "../utils/rarityHelper";

// Types imported from sprite store

const EntitySpriteBrowser: React.FC = () => {
  // Use Zustand store instead of local state
  const entitiesData = useEntities();
  const loading = useSpriteLoading();
  const error = useSpriteError();
  const { initialize, getEntitySprite } = useSpriteActions();
  
  const [selectedEntity, setSelectedEntity] = useState<Entity | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedTab, setSelectedTab] = useState(0);
  const [showOnlyWithSprites, setShowOnlyWithSprites] = useState(false);

  // Initialize sprite store on component mount
  useEffect(() => {
    initialize();
  }, [initialize]);

  // Combine all entities for search
  const allEntities = useMemo(() => {
    if (!entitiesData) return [];
    return [
      ...entitiesData.relics,
      ...entitiesData.enemies,
      ...entitiesData.orbs,
    ];
  }, [entitiesData]);

  // Filter entities based on current tab and search
  const filteredEntities = useMemo(() => {
    let entities = allEntities;

    // Filter by tab
    const tabFilters = ["relic", "enemy", "orb", "all"];
    const currentFilter = tabFilters[selectedTab];
    if (currentFilter !== "all") {
      entities = entities.filter((entity) => entity.type === currentFilter);
    }

    // Filter by search query
    if (searchQuery) {
      entities = entities.filter(
        (entity) =>
          entity.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
          entity.description?.toLowerCase().includes(searchQuery.toLowerCase()) ||
          entity.orbType?.toLowerCase().includes(searchQuery.toLowerCase())
      );
    }

    // Filter by sprite availability
    if (showOnlyWithSprites && entitiesData) {
      entities = entities.filter((entity) => {
        // Check if entity has a resolved sprite reference
        return entity.spriteReference;
      });
    }

    return entities;
  }, [
    allEntities,
    selectedTab,
    searchQuery,
    showOnlyWithSprites,
    entitiesData,
  ]);

  // Get sprite information for an entity (now using store method)
  const getSprite = (entity: Entity): Sprite | null => {
    return getEntitySprite(entity);
  };

  // Find matching sprites for an entity (for backwards compatibility)
  const findMatchingSprites = (entity: Entity): Sprite[] => {
    const sprite = getSprite(entity);
    return sprite ? [sprite] : [];
  };

  const handleTabChange = (_event: React.SyntheticEvent, newValue: number) => {
    setSelectedTab(newValue);
  };


  if (loading) {
    return (
      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        minHeight="400px"
      >
        <CircularProgress />
        <Typography variant="h6" sx={{ ml: 2 }}>
          Loading entities and sprites...
        </Typography>
      </Box>
    );
  }

  if (error) {
    return (
      <Alert severity="error" sx={{ mb: 2 }}>
        {error}
      </Alert>
    );
  }

  if (!entitiesData) {
    return (
      <Alert severity="info" sx={{ mb: 2 }}>
        No entity data available. Run the extract commands first.
      </Alert>
    );
  }

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Entity-Sprite Browser
      </Typography>

      <Paper sx={{ mb: 3, p: 2 }}>
        <Grid container spacing={2} alignItems="center">
          <Grid item xs={12} md={6}>
            <Autocomplete
              options={allEntities}
              getOptionLabel={(option) => option.name}
              renderInput={(params) => (
                <TextField
                  {...params}
                  label="Search entities"
                  variant="outlined"
                  InputProps={{
                    ...params.InputProps,
                    startAdornment: (
                      <SearchIcon sx={{ mr: 1, color: "text.secondary" }} />
                    ),
                  }}
                />
              )}
              renderOption={(props, option) => (
                <Box component="li" {...props}>
                  <Chip
                    label={option.type}
                    size="small"
                    color={
                      option.type === "relic"
                        ? "primary"
                        : option.type === "enemy"
                        ? "error"
                        : "secondary"
                    }
                    sx={{ mr: 1 }}
                  />
                  {option.name}
                </Box>
              )}
              onChange={(_, value) => setSelectedEntity(value)}
              inputValue={searchQuery}
              onInputChange={(_, value) => setSearchQuery(value)}
              isOptionEqualToValue={(option, value) => option.id === value.id}
            />
          </Grid>
          <Grid item xs={12} md={6}>
            <FormControlLabel
              control={
                <Switch
                  checked={showOnlyWithSprites}
                  onChange={(e) => setShowOnlyWithSprites(e.target.checked)}
                />
              }
              label="Show only entities with sprites"
            />
          </Grid>
        </Grid>
      </Paper>

      <Tabs value={selectedTab} onChange={handleTabChange} sx={{ mb: 2 }}>
        <Tab label={`Relics (${entitiesData.relics.length})`} />
        <Tab label={`Enemies (${entitiesData.enemies.length})`} />
        <Tab label={`Orbs (${entitiesData.orbs.length})`} />
        <Tab label={`All (${allEntities.length})`} />
      </Tabs>

      {selectedEntity && (
        <Card sx={{ mb: 3 }}>
          <CardContent>
            <Grid container spacing={2}>
              <Grid item xs={12} md={8}>
                <Typography variant="h5" gutterBottom>
                  {selectedEntity.name}
                  <Chip
                    label={selectedEntity.type}
                    size="small"
                    color={
                      selectedEntity.type === "relic"
                        ? "primary"
                        : selectedEntity.type === "enemy"
                        ? "error"
                        : "secondary"
                    }
                    sx={{ ml: 1 }}
                  />
                  {selectedEntity.rarity && (
                    <Tooltip title={getRarityTooltip(selectedEntity.rarity) || ""} arrow>
                      <Chip
                        label={getRarityName(selectedEntity.rarity)}
                        size="small"
                        color={getRarityColor(selectedEntity.rarity) as any}
                        sx={{ ml: 1 }}
                        icon={isUnavailableRarity(selectedEntity.rarity) ? <HelpOutline fontSize="small" /> : undefined}
                      />
                    </Tooltip>
                  )}
                </Typography>
                {selectedEntity.description && (
                  <Typography
                    variant="body1"
                    color="text.secondary"
                    gutterBottom
                  >
                    <SpriteText>{selectedEntity.description}</SpriteText>
                  </Typography>
                )}
                {selectedEntity.effect && (
                  <Typography variant="body2" color="text.secondary">
                    <strong>Effect:</strong> <SpriteText>{selectedEntity.effect}</SpriteText>
                  </Typography>
                )}
                {selectedEntity.type === "orb" && selectedEntity.orbType && (
                  <Typography variant="body2" color="text.secondary" gutterBottom>
                    <strong>Orb Type:</strong> {selectedEntity.orbType}
                  </Typography>
                )}
                {selectedEntity.type === "orb" && selectedEntity.levels && selectedEntity.levels.length > 0 && (
                  <Box sx={{ mt: 2 }}>
                    <Typography variant="h6" gutterBottom>
                      Orb Levels
                    </Typography>
                    <Grid container spacing={1}>
                      {selectedEntity.levels.map((level) => (
                        <Grid item xs={12} sm={6} md={4} key={level.level}>
                          <Paper sx={{ p: 2 }} variant="outlined">
                            <Typography variant="subtitle2" color="primary" gutterBottom>
                              Level {level.level}
                            </Typography>
                            {level.damagePerPeg && (
                              <Typography variant="body2">
                                <strong>Damage/Peg:</strong> {level.damagePerPeg}
                              </Typography>
                            )}
                            {level.critDamagePerPeg && (
                              <Typography variant="body2">
                                <strong>Crit Damage/Peg:</strong> {level.critDamagePerPeg}
                              </Typography>
                            )}
                          </Paper>
                        </Grid>
                      ))}
                    </Grid>
                  </Box>
                )}
              </Grid>
              <Grid item xs={12} md={4}>
                <Typography variant="h6" gutterBottom>
                  Associated Sprites
                </Typography>
                {(() => {
                  const sprite = getSprite(selectedEntity);
                  if (!sprite) {
                    return (
                      <Typography variant="body2" color="text.secondary">
                        No matching sprites found
                      </Typography>
                    );
                  }
                  return (
                    <Grid container spacing={1}>
                      <Grid item>
                        <Card
                          variant="outlined"
                          sx={{ p: 1, textAlign: "center" }}
                        >
                          <Avatar
                            src={sprite.url}
                            alt={sprite.name}
                            variant="rounded"
                            sx={{ width: 64, height: 64, mx: "auto", mb: 1 }}
                          />
                          <Typography variant="caption" display="block">
                            {sprite.name}
                          </Typography>
                          <Typography
                            variant="caption"
                            color="text.secondary"
                            display="block"
                          >
                            {sprite.width}×{sprite.height}
                          </Typography>
                        </Card>
                      </Grid>
                    </Grid>
                  );
                })()}
              </Grid>
            </Grid>
          </CardContent>
        </Card>
      )}

      <Grid container spacing={2}>
        {filteredEntities.map((entity) => (
          <Grid item xs={12} sm={6} md={4} key={entity.id}>
            <Card
              sx={{ cursor: "pointer", height: "100%" }}
              onClick={() => setSelectedEntity(entity)}
            >
              <CardContent>
                <Typography variant="h6" gutterBottom>
                  {entity.name}
                  <Chip
                    label={entity.type}
                    size="small"
                    color={
                      entity.type === "relic"
                        ? "primary"
                        : entity.type === "enemy"
                        ? "error"
                        : "secondary"
                    }
                    sx={{ ml: 1 }}
                  />
                </Typography>
                {entity.description && (
                  <Typography
                    variant="body2"
                    color="text.secondary"
                    sx={{
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      display: "-webkit-box",
                      WebkitLineClamp: 2,
                      WebkitBoxOrient: "vertical",
                    }}
                  >
                    <SpriteText>{entity.description}</SpriteText>
                  </Typography>
                )}
                {/* Show orb-specific info in card preview */}
                {entity.type === "orb" && entity.orbType && (
                  <Typography variant="caption" color="text.secondary" display="block">
                    Type: {entity.orbType}
                  </Typography>
                )}
                {entity.type === "orb" && entity.levels && entity.levels.length > 0 && (
                  <Typography variant="caption" color="text.secondary" display="block">
                    Levels: {entity.levels.map(l => l.level).join(", ")}
                  </Typography>
                )}
                <Box sx={{ mt: 1 }}>
                  {(() => {
                    const sprite = getSprite(entity);
                    if (sprite) {
                      return (
                        <Chip label="1 sprite" size="small" color="success" />
                      );
                    }
                    return (
                      <Chip label="No sprites" size="small" color="default" />
                    );
                  })()}
                </Box>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      {filteredEntities.length === 0 && (
        <Alert severity="info" sx={{ mt: 2 }}>
          No entities found matching your search criteria.
        </Alert>
      )}
    </Box>
  );
};

export default EntitySpriteBrowser;

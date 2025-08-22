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
  IconButton,
} from "@mui/material";
import {
  Search as SearchIcon,
  HelpOutline,
  ArrowBackIos,
  ArrowForwardIos,
} from "@mui/icons-material";
import {
  useEntities,
  useSpriteLoading,
  useSpriteError,
  useSpriteActions,
  Entity,
  Sprite,
} from "../store/useSpriteStore";
import SpriteText from "./SpriteText";
import AnimatedSpriteViewer from "./AnimatedSpriteViewer";
import {
  getRarityName,
  getRarityColor,
  getRarityTooltip,
  isUnavailableRarity,
} from "../utils/rarityHelper";

// Types imported from sprite store

interface OrbLevelCarouselProps {
  levels: Array<{
    level: number;
    damagePerPeg?: string;
    critDamagePerPeg?: string;
    description?: string;
  }>;
}

const OrbLevelCarousel: React.FC<OrbLevelCarouselProps> = ({ levels }) => {
  const [currentLevelIndex, setCurrentLevelIndex] = useState(0);

  if (!levels || levels.length === 0) {
    return null;
  }

  const currentLevel = levels[currentLevelIndex];
  const hasMultipleLevels = levels.length > 1;

  const goToPreviousLevel = () => {
    setCurrentLevelIndex((prev) => (prev === 0 ? levels.length - 1 : prev - 1));
  };

  const goToNextLevel = () => {
    setCurrentLevelIndex((prev) => (prev === levels.length - 1 ? 0 : prev + 1));
  };

  return (
    <Box sx={{ mt: 2 }}>
      <Paper sx={{ p: 3 }} variant="outlined">
        {/* Level Navigation Header */}
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            mb: 2,
          }}
        >
          <IconButton
            onClick={goToPreviousLevel}
            disabled={!hasMultipleLevels}
            size="small"
            sx={{ visibility: hasMultipleLevels ? "visible" : "hidden" }}
          >
            <ArrowBackIos />
          </IconButton>

          <Typography
            variant="h6"
            color="primary"
            sx={{
              fontWeight: "bold",
              textAlign: "center",
              minWidth: "100px",
            }}
          >
            Level {currentLevel.level}
          </Typography>

          <IconButton
            onClick={goToNextLevel}
            disabled={!hasMultipleLevels}
            size="small"
            sx={{ visibility: hasMultipleLevels ? "visible" : "hidden" }}
          >
            <ArrowForwardIos />
          </IconButton>
        </Box>

        {/* Level Indicators (dots) */}
        {hasMultipleLevels && (
          <Box
            sx={{
              display: "flex",
              justifyContent: "center",
              gap: 1,
              mb: 2,
            }}
          >
            {levels.map((_, index) => (
              <Box
                key={index}
                onClick={() => setCurrentLevelIndex(index)}
                sx={{
                  width: 8,
                  height: 8,
                  borderRadius: "50%",
                  backgroundColor:
                    index === currentLevelIndex ? "primary.main" : "grey.300",
                  cursor: "pointer",
                  transition: "background-color 0.2s ease",
                  "&:hover": {
                    backgroundColor:
                      index === currentLevelIndex ? "primary.dark" : "grey.400",
                  },
                }}
              />
            ))}
          </Box>
        )}

        {/* Level Description */}
        {currentLevel.description && (
          <Typography
            variant="body2"
            color="text.primary"
            sx={{ whiteSpace: "pre-line" }}
          >
            <SpriteText>{currentLevel.description}</SpriteText>
          </Typography>
        )}
        {/* Level Stats */}
        <Grid container spacing={2} sx={{ mb: 2 }}>
          {(currentLevel.damagePerPeg || currentLevel.critDamagePerPeg) && (
            <Grid size={12}>
              <Box sx={{ textAlign: "right" }}>
                <Typography variant="h6" color="primary">
                  {currentLevel.damagePerPeg || "0"} /{" "}
                  {currentLevel.critDamagePerPeg || "0"}
                </Typography>
              </Box>
            </Grid>
          )}
        </Grid>

        {/* Navigation Help Text */}
        {hasMultipleLevels && (
          <Typography
            variant="caption"
            color="text.secondary"
            sx={{
              display: "block",
              textAlign: "center",
              mt: 2,
            }}
          >
            Click arrows or dots to view different levels
          </Typography>
        )}
      </Paper>
    </Box>
  );
};

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
  const [skipEmptyFrames, setSkipEmptyFrames] = useState(true);

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
          entity.description
            ?.toLowerCase()
            .includes(searchQuery.toLowerCase()) ||
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
          <Grid
            size={{
              xs: 12,
              md: 6
            }}>
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
          <Grid
            size={{
              xs: 12,
              md: 6
            }}>
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
          <Grid
            size={{
              xs: 12,
              md: 6
            }}>
            <FormControlLabel
              control={
                <Switch
                  checked={skipEmptyFrames}
                  onChange={(e) => setSkipEmptyFrames(e.target.checked)}
                />
              }
              label={
                <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                  Skip empty frames
                  <Tooltip title="Automatically detect and skip sprite frames that contain only transparent pixels">
                    <HelpOutline sx={{ fontSize: 16 }} />
                  </Tooltip>
                </Box>
              }
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
              <Grid
                size={{
                  xs: 12,
                  md: 8
                }}>
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
                    <Tooltip
                      title={getRarityTooltip(selectedEntity.rarity) || ""}
                      arrow
                    >
                      <Chip
                        label={getRarityName(selectedEntity.rarity)}
                        size="small"
                        color={getRarityColor(selectedEntity.rarity) as any}
                        sx={{ ml: 1 }}
                        icon={
                          isUnavailableRarity(selectedEntity.rarity) ? (
                            <HelpOutline fontSize="small" />
                          ) : undefined
                        }
                      />
                    </Tooltip>
                  )}
                </Typography>
                {selectedEntity.type !== "orb" ? (
                  <>
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
                        <strong>Effect:</strong>{" "}
                        <SpriteText>{selectedEntity.effect}</SpriteText>
                      </Typography>
                    )}
                  </>
                ) : null}
                {selectedEntity.type === "orb" &&
                  selectedEntity.levels &&
                  selectedEntity.levels.length > 0 && (
                    <OrbLevelCarousel levels={selectedEntity.levels} />
                  )}
              </Grid>
              <Grid
                size={{
                  xs: 12,
                  md: 4
                }}>
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
                    <Box sx={{ display: "flex", justifyContent: "center" }}>
                      <AnimatedSpriteViewer
                        sprite={sprite}
                        size={128}
                        showControls={true}
                        showFrameInfo={true}
                        autoPlay={true}
                        frameRate={10}
                        skipEmptyFrames={skipEmptyFrames}
                      />
                    </Box>
                  );
                })()}
              </Grid>
            </Grid>
          </CardContent>
        </Card>
      )}
      <Grid container spacing={2}>
        {filteredEntities.map((entity) => (
          <Grid
            key={entity.id}
            size={{
              xs: 12,
              sm: 6,
              md: 4
            }}>
            <Card
              sx={{ cursor: "pointer", height: "100%" }}
              onClick={() => setSelectedEntity(entity)}
            >
              <CardContent>
                <Box
                  sx={{
                    display: "flex",
                    alignItems: "flex-start",
                    gap: 2,
                    mb: 1,
                  }}
                >
                  {(() => {
                    const sprite = getSprite(entity);
                    if (sprite) {
                      return (
                        <Box sx={{ flexShrink: 0 }}>
                          <AnimatedSpriteViewer
                            sprite={sprite}
                            size={48}
                            showControls={false}
                            showFrameInfo={false}
                            autoPlay={true}
                            frameRate={3}
                            skipEmptyFrames={skipEmptyFrames}
                          />
                        </Box>
                      );
                    }
                    return null;
                  })()}
                  <Box sx={{ flexGrow: 1, minWidth: 0 }}>
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
                          mb: 1,
                        }}
                      >
                        <SpriteText>{entity.description}</SpriteText>
                      </Typography>
                    )}
                    {entity.type === "orb" &&
                      entity.levels &&
                      entity.levels.length > 0 && (
                        <>
                          {/* Show damage info for first level */}
                          {entity.levels[0] && (
                            <Box sx={{ mt: 0.5 }}>
                              <Typography
                                variant="caption"
                                color="text.primary"
                              >
                                {entity.levels
                                  .map(
                                    (lvl) =>
                                      `${lvl.damagePerPeg ?? 0}/${
                                        lvl.critDamagePerPeg ?? 0
                                      }`
                                  )
                                  .join(", ")}
                              </Typography>
                            </Box>
                          )}
                        </>
                      )}
                  </Box>
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

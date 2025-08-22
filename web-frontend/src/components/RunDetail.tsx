import React, { useMemo, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Paper,
  Typography,
  Box,
  Grid,
  Card,
  CardContent,
  Chip,
  Button,
  Avatar,
  Alert,
  Divider,
  Tooltip,
  IconButton,
} from "@mui/material";
import { ArrowBack, HelpOutline, ExpandMore, ExpandLess } from "@mui/icons-material";
import { useRunsAndConfig } from "../store/useAppStore";
import { useEntities, useSpriteActions } from "../store/useSpriteStore";
import {
  getRarityName,
  getRarityColor,
  getRarityTooltip,
  isUnavailableRarity,
} from "../utils/rarityHelper";
import SpriteText from "./SpriteText";
import FormattedDescription from "./FormattedDescription";
import { ArrowBackIos, ArrowForwardIos } from "@mui/icons-material";
import { RoomInfo } from "../types";

// Utility function to group rooms by acts (separated by boss encounters)
const groupRoomsByAct = (rooms: RoomInfo[]): RoomInfo[][] => {
  const acts: RoomInfo[][] = [];
  let currentAct: RoomInfo[] = [];

  for (const room of rooms) {
    currentAct.push(room);

    // If this is a boss room (id 7), end the current act
    if (room.id === 7) {
      acts.push([...currentAct]);
      currentAct = [];
    }
  }

  // Add any remaining rooms as the final act (if run didn't end with boss)
  if (currentAct.length > 0) {
    acts.push(currentAct);
  }

  return acts;
};

interface RunOrbLevelCarouselProps {
  orbData: any; // Run data for the orb
  orbEntity: any; // Entity data for the orb
}

const RunOrbLevelCarousel: React.FC<RunOrbLevelCarouselProps> = ({
  orbData,
  orbEntity,
}) => {
  const [currentLevelIndex, setCurrentLevelIndex] = useState(0);

  if (!orbData.levelInstances || orbData.levelInstances.length === 0) {
    return null;
  }

  // Filter to only levels that have quantities > 0
  const availableLevels = orbData.levelInstances
    .map((quantity: number, index: number) => ({ level: index + 1, quantity }))
    .filter((levelData: any) => levelData.quantity > 0);

  if (availableLevels.length === 0) {
    return null;
  }

  const currentLevelData = availableLevels[currentLevelIndex];
  const entityLevelData = orbEntity?.levels?.find(
    (l: any) => l.level === currentLevelData.level
  );
  const hasMultipleLevels = availableLevels.length > 1;

  const goToPreviousLevel = () => {
    setCurrentLevelIndex((prev) =>
      prev === 0 ? availableLevels.length - 1 : prev - 1
    );
  };

  const goToNextLevel = () => {
    setCurrentLevelIndex((prev) =>
      prev === availableLevels.length - 1 ? 0 : prev + 1
    );
  };

  return (
    <Box
      sx={{ mt: 2, height: "100%", display: "flex", flexDirection: "column" }}
    >
      <Paper
        sx={{
          p: 2,
          flex: 1,
          display: "flex",
          flexDirection: "column",
          overflow: "hidden",
        }}
        variant="outlined"
      >
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
            <ArrowBackIos fontSize="small" />
          </IconButton>

          <Box sx={{ textAlign: "center" }}>
            <Typography
              variant="h6"
              color="primary"
              sx={{ fontWeight: "bold" }}
            >
              Level {currentLevelData.level}
            </Typography>
            <Typography variant="body2" color="textSecondary">
              Quantity: {currentLevelData.quantity}
            </Typography>
          </Box>

          <IconButton
            onClick={goToNextLevel}
            disabled={!hasMultipleLevels}
            size="small"
            sx={{ visibility: hasMultipleLevels ? "visible" : "hidden" }}
          >
            <ArrowForwardIos fontSize="small" />
          </IconButton>
        </Box>

        {/* Level Indicators (dots) */}
        {hasMultipleLevels && (
          <Box
            sx={{ display: "flex", justifyContent: "center", gap: 1, mb: 2 }}
          >
            {availableLevels.map((levelData: any, index: number) => (
              <Box
                key={levelData.level}
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

        {/* Level Description - scrollable if too long */}
        <Box sx={{ flex: 1, overflow: "auto", mb: 2 }}>
          {entityLevelData?.description && (
            <FormattedDescription
              variant="body2"
              color="textSecondary"
              sx={{ fontStyle: "italic" }}
            >
              {entityLevelData.description}
            </FormattedDescription>
          )}
        </Box>

        {/* Level Stats - fixed at bottom */}
        <Box sx={{ textAlign: "center", mb: 2, mt: "auto" }}>
          <Typography variant="caption" color="textSecondary">
            Damage / Crit Damage per Peg
          </Typography>
          <Typography variant="h6" color="primary">
            {entityLevelData?.damagePerPeg || "0"} /{" "}
            {entityLevelData?.critDamagePerPeg || "0"}
          </Typography>
        </Box>

        {/* Navigation Help Text */}
        {hasMultipleLevels && (
          <Typography
            variant="caption"
            color="text.secondary"
            sx={{ display: "block", textAlign: "center", mt: 1 }}
          >
            Use arrows or dots to navigate between levels
          </Typography>
        )}
      </Paper>
    </Box>
  );
};

const RunDetail: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { runs } = useRunsAndConfig();
  const entities = useEntities();
  const { getEntitySprite } = useSpriteActions();
  
  // Collapsible section states - default to collapsed (false)
  const [relicsExpanded, setRelicsExpanded] = useState(false);
  const [orbsExpanded, setOrbsExpanded] = useState(false);
  const [enemiesExpanded, setEnemiesExpanded] = useState(false);
  const [roomsExpanded, setRoomsExpanded] = useState(false);

  const run = useMemo(() => {
    const decodedId = id ? decodeURIComponent(id) : id;
    return runs.find((r) => r.id === decodedId);
  }, [runs, id]);

  const relicEntities = useMemo(() => {
    if (!run || !entities) return [];

    return run.relicNames
      .map((relicName) => {
        // Find the relic entity by name (case insensitive)
        const relic = entities.relics.find(
          (r) =>
            r.name.toLowerCase() === relicName.toLowerCase() ||
            r.id.toLowerCase() === relicName.toLowerCase()
        );
        return relic;
      })
      .filter(Boolean);
  }, [run, entities]);

  const orbEntities = useMemo(() => {
    if (!run || !entities || !run.orbStats) return {};

    const orbEntityMap: Record<string, any> = {};

    Object.entries(run.orbStats).forEach(([orbKey, orbData]) => {
      const runOrbName = orbData.name.toLowerCase(); // Full name like "stoneorb-lvl1"

      // Find orb by matching runNameEquivalent in levels array (case-insensitive)
      const orb = entities.orbs.find((o) => {
        if (!o.levels) return false;

        // Check if any level has a runNameEquivalent that matches our run orb name (case-insensitive)
        return o.levels.some(
          (level: any) => level.runNameEquivalent?.toLowerCase() === runOrbName
        );
      });

      if (orb) {
        // Find the specific level data that matches
        const matchingLevel = orb.levels?.find(
          (level: any) => level.runNameEquivalent?.toLowerCase() === runOrbName
        );

        // Create enhanced orb object with level-specific data
        orbEntityMap[orbKey] = {
          ...orb,
          // Override with level-specific damage stats if available
          damagePerPeg: matchingLevel?.damagePerPeg || orb.damagePerPeg,
          critDamagePerPeg:
            matchingLevel?.critDamagePerPeg || orb.critDamagePerPeg,
          description: matchingLevel?.description || orb.description,
          level: matchingLevel?.level || 1,
          entityId: matchingLevel?.entityId || orb.id,
        };
      } else {
        console.log(
          `Unmapped orb: runName="${orbData.name}", runId="${orbData.id}"`
        );
      }
    });

    return orbEntityMap;
  }, [run, entities]);

  const enemyEntitiesWithData = useMemo(() => {
    if (!run || !entities || !run.enemyData) return [];

    // Create a probabilistic word-based matching function
    const findEnemyEntity = (runEnemyName: string) => {
      // Normalize and tokenize the run enemy name
      const normalizeForMatching = (name: string) => {
        return name
          .toLowerCase()
          .replace(/[-_]/g, "") // Remove hyphens and underscores
          .replace(/enemy|prefab|container|minion/g, "") // Remove common suffixes
          .replace(/nosword/g, "") // Remove NoSword variants
          .trim();
      };

      const extractWords = (name: string) => {
        return name
          .replace(/([a-z])([A-Z])/g, "$1 $2") // Split camelCase BEFORE lowercasing
          .replace(/([a-z])(\d)/g, "$1 $2") // Split letter-number boundaries
          .replace(/(\d)([a-z])/g, "$1 $2") // Split number-letter boundaries
          .toLowerCase() // Now lowercase after splitting
          .replace(/[-_]/g, " ") // Replace hyphens and underscores with spaces
          .replace(/\benemy\b|\bprefab\b|\bcontainer\b/g, "") // Remove common suffixes (word boundaries)
          .replace(/\bnosword\b/g, "") // Remove NoSword variants
          .split(/\s+/)
          .filter((word) => word.length > 1) // Filter out single characters
          .filter(
            (word) =>
              ![
                "the",
                "of",
                "and",
                "or",
                "but",
                "in",
                "on",
                "at",
                "to",
                "for",
                "with",
                "by",
              ].includes(word)
          ) // Filter common words
          .map((word) => word.trim())
          .filter((word) => word.length > 0);
      };

      const runWords = extractWords(runEnemyName);

      // Calculate match scores for each entity
      let bestMatch: any = null;
      let bestScore = 0;

      entities.enemies.forEach((entity) => {
        const entityIdWords = extractWords(entity.id);
        const entityNameWords = extractWords(entity.name);
        const allEntityWords = [...entityIdWords, ...entityNameWords];

        // Calculate word overlap score
        let score = 0;
        let totalRunWords = runWords.length;

        // Calculate word matches with better scoring
        const matchedWords = new Set<string>();

        runWords.forEach((runWord) => {
          let bestWordScore = 0;

          allEntityWords.forEach((entityWord) => {
            // Exact word match gets highest score
            if (entityWord === runWord) {
              bestWordScore = Math.max(bestWordScore, 3);
              matchedWords.add(runWord);
            }
            // Partial word match (one contains the other) - but be more careful
            else if (runWord.length > 3 && entityWord.includes(runWord)) {
              bestWordScore = Math.max(bestWordScore, 2);
              matchedWords.add(runWord);
            } else if (entityWord.length > 3 && runWord.includes(entityWord)) {
              bestWordScore = Math.max(bestWordScore, 1);
              matchedWords.add(runWord);
            }
          });

          score += bestWordScore;
        });

        // Bonus for matching multiple important words (compound names)
        const importantWords = runWords.filter(
          (word) =>
            !["mines", "single", "hit", "variant", "base"].includes(word)
        );
        const matchedImportantWords = importantWords.filter((word) =>
          matchedWords.has(word)
        );
        const importantWordRatio =
          importantWords.length > 0
            ? matchedImportantWords.length / importantWords.length
            : 0;

        if (importantWordRatio >= 0.6) {
          // At least 60% of important words matched
          score += 2;
        }

        // Special bonus for color words - they should match exactly
        const colorWords = ["green", "blue", "red", "crystal", "rainbow"];
        const runColorWords = runWords.filter((word) =>
          colorWords.includes(word)
        );
        const entityColorWords = [...entityIdWords, ...entityNameWords].filter(
          (word) => colorWords.includes(word)
        );

        // Penalize if colors don't match
        if (runColorWords.length > 0 && entityColorWords.length > 0) {
          const colorMatch = runColorWords.some((runColor) =>
            entityColorWords.includes(runColor)
          );
          if (colorMatch) {
            score += 2; // Bonus for matching color
          } else {
            score -= 5; // Penalty for mismatched color
          }
        }

        // Bonus for direct ID/name matches
        if (
          entity.id.toLowerCase() === runEnemyName.toLowerCase() ||
          entity.name.toLowerCase() === runEnemyName.toLowerCase()
        ) {
          score += 10;
        }

        // Special handling for some tricky cases
        const normalizedRunName = normalizeForMatching(runEnemyName);
        const normalizedEntityId = normalizeForMatching(entity.id);
        const normalizedEntityName = normalizeForMatching(entity.name);

        // Handle "Container-" prefix removal
        if (runEnemyName.toLowerCase().startsWith("container-")) {
          const withoutContainer = runEnemyName.substring(10).toLowerCase();
          if (
            entity.id.toLowerCase().includes(withoutContainer) ||
            entity.name.toLowerCase().includes(withoutContainer)
          ) {
            score += 3;
          }
        }

        // Handle close fuzzy matches after normalization
        if (
          normalizedEntityId === normalizedRunName ||
          normalizedEntityName === normalizedRunName
        ) {
          score += 5;
        }

        // Special handling for compound names - check if the entity ID contains most of the important words
        if (runWords.length >= 3) {
          const entityIdLower = entity.id.toLowerCase();
          const coreWords = runWords.filter(
            (word) =>
              ![
                "mines",
                "single",
                "hit",
                "variant",
                "base",
                "weak",
                "ranged",
              ].includes(word)
          );

          const wordsInEntityId = coreWords.filter((word) =>
            entityIdLower.includes(word)
          );
          if (wordsInEntityId.length >= Math.ceil(coreWords.length * 0.7)) {
            score += 3; // Bonus for compound matching
          }
        }

        // Bonus for close length matches (prevents very short words from matching long names)
        const lengthDiff = Math.abs(entity.id.length - runEnemyName.length);
        if (lengthDiff <= 3) {
          score += 0.5;
        }

        // Calculate confidence as percentage of words matched (adjusted for new scoring)
        const confidence = totalRunWords > 0 ? score / (totalRunWords * 3) : 0;

        // Only consider matches with reasonable confidence (but allow negative scores to eliminate bad matches)
        if (score > 0 && confidence > 0.3 && score > bestScore) {
          bestScore = score;
          bestMatch = entity;
        }
      });

      // Debug logging for development - show more detail
      if (process.env.NODE_ENV === "development") {
        const runWords = extractWords(runEnemyName);
        console.log(`\n=== Enemy Mapping Debug for "${runEnemyName}" ===`);
        console.log(`Run words:`, runWords);

        if (bestMatch) {
          const entityIdWords = extractWords(bestMatch.id);
          const entityNameWords = extractWords(bestMatch.name);
          console.log(
            `Best match: "${bestMatch.id}" (${bestMatch.name}) - Score: ${bestScore}`
          );
          console.log(`Entity ID words:`, entityIdWords);
          console.log(`Entity name words:`, entityNameWords);
        } else {
          console.log(`No suitable match found`);
        }
        console.log(`=== End Debug ===\n`);
      }

      return bestMatch;
    };

    return Object.entries(run.enemyData)
      .map(([enemyName, enemyPlayData]) => {
        // Find the enemy entity using probabilistic word matching
        const enemy = findEnemyEntity(enemyName);

        // Map backend data structure to frontend expected structure
        const mappedPlayData = {
          ...enemyPlayData,
          encounterCount: enemyPlayData.amountFought,
          damageDealt: 0, // This data isn't available in the current backend structure
          damageReceived:
            enemyPlayData.meleeDamageReceived +
            enemyPlayData.rangedDamageReceived,
          killCount: enemyPlayData.defeatedBy ? enemyPlayData.amountFought : 0, // If defeated by this enemy, assume all encounters were kills
        };

        return {
          playData: mappedPlayData,
          entity: enemy,
          name: enemyName,
        };
      })
      .filter((item) => item.entity || item.playData);
  }, [run, entities]);

  const formatDuration = (duration: string) => {
    const timeMatch = duration.match(/^(\d+):(\d+):(\d+)(?:\.(\d+))?$/);
    if (timeMatch) {
      const [, hours, minutes, seconds] = timeMatch;
      const totalMinutes = parseInt(hours) * 60 + parseInt(minutes);
      if (totalMinutes > 60) {
        const hrs = Math.floor(totalMinutes / 60);
        const mins = totalMinutes % 60;
        return `${hrs}h ${mins}m`;
      }
      return `${totalMinutes}m ${seconds}s`;
    }
    return duration;
  };

  const formatNumber = (num: number) => {
    if (num >= 1000000) return `${(num / 1000000).toFixed(1)}M`;
    if (num >= 1000) return `${(num / 1000).toFixed(1)}K`;
    return num.toLocaleString();
  };

  if (!run) {
    return (
      <Box>
        <Button
          startIcon={<ArrowBack />}
          onClick={() => navigate("/runs")}
          sx={{ mb: 2 }}
        >
          Back to Runs
        </Button>
        <Typography variant="h4" gutterBottom>
          Run Details
        </Typography>
        <Alert severity="error">
          Run not found. The run may have been deleted or the ID is invalid.
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      <Button
        startIcon={<ArrowBack />}
        onClick={() => navigate("/runs")}
        sx={{ mb: 2 }}
      >
        Back to Runs
      </Button>
      <Typography variant="h4" gutterBottom>
        Run Details
      </Typography>
      <Grid container spacing={3}>
        {/* Run Overview */}
        <Grid size={12}>
          <Paper sx={{ p: 3 }}>
            <Box
              display="flex"
              justifyContent="space-between"
              alignItems="center"
              mb={2}
            >
              <Typography variant="h5">{run.characterClass}</Typography>
              <Chip
                label={run.won ? "Victory" : "Defeat"}
                color={run.won ? "success" : "error"}
                size="medium"
              />
            </Box>

            <Grid container spacing={2}>
              <Grid
                size={{
                  xs: 6,
                  sm: 3
                }}>
                <Typography variant="body2" color="textSecondary">
                  Damage Dealt
                </Typography>
                <Typography variant="h6">
                  {formatNumber(run.damageDealt)}
                </Typography>
              </Grid>
              <Grid
                size={{
                  xs: 6,
                  sm: 3
                }}>
                <Typography variant="body2" color="textSecondary">
                  Duration
                </Typography>
                <Typography variant="h6">
                  {formatDuration(run.duration)}
                </Typography>
              </Grid>
              <Grid
                size={{
                  xs: 6,
                  sm: 3
                }}>
                <Typography variant="body2" color="textSecondary">
                  Final Level
                </Typography>
                <Typography variant="h6">{run.finalLevel}</Typography>
              </Grid>
              <Grid
                size={{
                  xs: 6,
                  sm: 3
                }}>
                <Typography variant="body2" color="textSecondary">
                  Cruciball Level
                </Typography>
                <Typography variant="h6">{run.cruciballLevel}</Typography>
              </Grid>
            </Grid>

            <Divider sx={{ my: 2 }} />

            <Grid container spacing={2}>
              <Grid
                size={{
                  xs: 6,
                  sm: 3
                }}>
                <Typography variant="body2" color="textSecondary">
                  Pegs Hit
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.pegsHit)}
                </Typography>
              </Grid>
              <Grid
                size={{
                  xs: 6,
                  sm: 3
                }}>
                <Typography variant="body2" color="textSecondary">
                  Coins Earned
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.coinsEarned)}
                </Typography>
              </Grid>
              <Grid
                size={{
                  xs: 6,
                  sm: 3
                }}>
                <Typography variant="body2" color="textSecondary">
                  Final HP
                </Typography>
                <Typography variant="body1">
                  {run.finalHp}/{run.maxHp}
                </Typography>
              </Grid>
              <Grid
                size={{
                  xs: 6,
                  sm: 3
                }}>
                <Typography variant="body2" color="textSecondary">
                  Date
                </Typography>
                <Typography variant="body1">
                  {new Date(run.timestamp).toLocaleDateString()}
                </Typography>
              </Grid>
            </Grid>

            {run.defeatedBy && (
              <Box mt={2}>
                <Typography variant="body2" color="textSecondary">
                  Defeated By
                </Typography>
                <Typography variant="body1" color="error">
                  {run.defeatedBy}
                </Typography>
              </Box>
            )}
          </Paper>
        </Grid>

        {/* Relics */}
        <Grid size={12}>
          <Paper sx={{ p: 3 }}>
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                cursor: "pointer",
                mb: relicsExpanded ? 2 : 0,
              }}
              onClick={() => setRelicsExpanded(!relicsExpanded)}
            >
              <Typography variant="h5">
                Relics ({relicEntities.length})
              </Typography>
              <IconButton size="small">
                {relicsExpanded ? <ExpandLess /> : <ExpandMore />}
              </IconButton>
            </Box>

            {relicsExpanded && (
              relicEntities.length === 0 ? (
                <Typography variant="body2" color="textSecondary">
                  No relics were used in this run.
                </Typography>
              ) : (
                <Grid container spacing={2}>
                  {relicEntities.map((relic, index) => {
                    const sprite = getEntitySprite(relic!);
                    return (
                      <Grid
                        key={index}
                        size={{
                          xs: 12,
                          sm: 6,
                          md: 4
                        }}>
                        <Card variant="outlined">
                          <CardContent
                            sx={{
                              display: "flex",
                              alignItems: "flex-start",
                              gap: 2,
                            }}
                          >
                            <Avatar
                              src={sprite?.url}
                              alt={relic!.name}
                              sx={{
                                width: 48,
                                height: 48,
                                bgcolor: sprite ? "transparent" : "grey.300",
                              }}
                            >
                              {!sprite && relic!.name.charAt(0)}
                            </Avatar>
                            <Box sx={{ flex: 1, minWidth: 0 }}>
                              <Typography variant="subtitle1" fontWeight="bold">
                                {relic!.name}
                              </Typography>
                              {relic!.description && (
                                <FormattedDescription
                                  variant="body2"
                                  color="textSecondary"
                                >
                                  {relic!.description}
                                </FormattedDescription>
                              )}
                              {relic!.effect &&
                                relic!.effect !== relic!.description && (
                                  <FormattedDescription
                                    variant="body2"
                                    color="textSecondary"
                                    sx={{ fontStyle: "italic", mt: 0.5 }}
                                  >
                                    {relic!.effect}
                                  </FormattedDescription>
                                )}
                              {relic!.rarity && (
                                <Tooltip
                                  title={getRarityTooltip(relic!.rarity) || ""}
                                  arrow
                                >
                                  <Chip
                                    label={getRarityName(relic!.rarity)}
                                    size="small"
                                    sx={{ mt: 1 }}
                                    color={getRarityColor(relic!.rarity) as any}
                                    icon={
                                      isUnavailableRarity(relic!.rarity) ? (
                                        <HelpOutline fontSize="small" />
                                      ) : undefined
                                    }
                                  />
                                </Tooltip>
                              )}
                            </Box>
                          </CardContent>
                        </Card>
                      </Grid>
                    );
                  })}
                </Grid>
              )
            )}
          </Paper>
        </Grid>

        {/* Orbs */}
        {(Object.keys(run.orbStats || {}).length > 0 ||
          run.orbsUsed.length > 0) && (
          <Grid size={12}>
            <Paper sx={{ p: 3 }}>
              <Box
                sx={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  cursor: "pointer",
                  mb: orbsExpanded ? 2 : 0,
                }}
                onClick={() => setOrbsExpanded(!orbsExpanded)}
              >
                <Typography variant="h5">
                  Orbs (
                  {Object.keys(run.orbStats || {}).length || run.orbsUsed.length})
                </Typography>
                <IconButton size="small">
                  {orbsExpanded ? <ExpandLess /> : <ExpandMore />}
                </IconButton>
              </Box>

              {orbsExpanded && (
                Object.keys(run.orbStats || {}).length > 0 ? (
                  <Grid container spacing={2}>
                    {Object.entries(run.orbStats).map(([orbName, orbData]) => {
                      const orbEntity = orbEntities[orbName];
                      const sprite = orbEntity
                        ? getEntitySprite(orbEntity)
                        : null;
                      // Use entity name if available, otherwise fall back to cleaned run name
                      const displayName =
                        orbEntity?.name || orbData.name.replace(/-Lvl\d+$/, "");
                      const level =
                        orbEntity?.level ||
                        orbData.name.match(/-Lvl(\d+)$/)?.[1] ||
                        "1";

                      return (
                        <Grid
                          key={orbName}
                          size={{
                            xs: 12,
                            sm: 6,
                            md: 4
                          }}>
                          <Card
                            variant="outlined"
                            sx={{
                              height: "100%",
                              display: "flex",
                              flexDirection: "column",
                            }}
                          >
                            <CardContent
                              sx={{
                                flex: 1,
                                display: "flex",
                                flexDirection: "column",
                              }}
                            >
                              <Box
                                sx={{
                                  display: "flex",
                                  alignItems: "flex-start",
                                  gap: 2,
                                  mb: 2,
                                }}
                              >
                                <Avatar
                                  src={sprite?.url}
                                  alt={displayName}
                                  sx={{
                                    width: 48,
                                    height: 48,
                                    bgcolor: sprite ? "transparent" : "grey.300",
                                  }}
                                >
                                  {!sprite && displayName.charAt(0)}
                                </Avatar>
                                <Box sx={{ flex: 1, minWidth: 0 }}>
                                  <Box
                                    sx={{
                                      display: "flex",
                                      alignItems: "center",
                                      gap: 1,
                                    }}
                                  >
                                    <Typography
                                      variant="subtitle1"
                                      fontWeight="bold"
                                    >
                                      {displayName}
                                    </Typography>
                                    <Chip
                                      label={`Lvl ${level}`}
                                      size="small"
                                      color="secondary"
                                      variant="outlined"
                                    />
                                  </Box>
                                  {orbEntity?.rarity && (
                                    <Tooltip
                                      title={
                                        getRarityTooltip(orbEntity.rarity) || ""
                                      }
                                      arrow
                                    >
                                      <Chip
                                        label={getRarityName(orbEntity.rarity)}
                                        size="small"
                                        sx={{ mt: 0.5 }}
                                        color={
                                          getRarityColor(orbEntity.rarity) as any
                                        }
                                        icon={
                                          isUnavailableRarity(
                                            orbEntity.rarity
                                          ) ? (
                                            <HelpOutline fontSize="small" />
                                          ) : undefined
                                        }
                                      />
                                    </Tooltip>
                                  )}
                                </Box>
                              </Box>

                              <Divider sx={{ mb: 1 }} />

                              {/* Stats section - always takes same height */}
                              <Box sx={{ mb: 2 }}>
                                <Grid container spacing={1}>
                                  <Grid size={4}>
                                    <Typography
                                      variant="body2"
                                      color="textSecondary"
                                    >
                                      Damage Dealt
                                    </Typography>
                                    <Typography variant="h6">
                                      {formatNumber(orbData.damageDealt)}
                                    </Typography>
                                  </Grid>
                                  <Grid size={4}>
                                    <Typography
                                      variant="body2"
                                      color="textSecondary"
                                    >
                                      Times Fired
                                    </Typography>
                                    <Typography variant="h6">
                                      {formatNumber(orbData.timesFired)}
                                    </Typography>
                                  </Grid>
                                  <Grid size={4}>
                                    <Typography
                                      variant="body2"
                                      color="textSecondary"
                                    >
                                      Efficiency
                                    </Typography>
                                    <Typography variant="h6">
                                      {orbData.timesFired > 0
                                        ? formatNumber(
                                            Math.round(
                                              orbData.damageDealt /
                                                orbData.timesFired
                                            )
                                          )
                                        : 0}
                                    </Typography>
                                    <Typography
                                      variant="caption"
                                      color="textSecondary"
                                    >
                                      per shot
                                    </Typography>
                                  </Grid>
                                  {(orbData.timesDiscarded > 0 ||
                                    orbData.timesRemoved > 0) && (
                                    <>
                                      <Grid size={6}>
                                        <Typography
                                          variant="body2"
                                          color="textSecondary"
                                        >
                                          Discarded
                                        </Typography>
                                        <Typography variant="body2">
                                          {orbData.timesDiscarded}
                                        </Typography>
                                      </Grid>
                                      <Grid size={6}>
                                        <Typography
                                          variant="body2"
                                          color="textSecondary"
                                        >
                                          Removed
                                        </Typography>
                                        <Typography variant="body2">
                                          {orbData.timesRemoved}
                                        </Typography>
                                      </Grid>
                                    </>
                                  )}
                                </Grid>
                              </Box>

                              {/* Level Carousel - consistent height */}
                              <Box
                                sx={{
                                  flex: 1,
                                  height: "200px", // Fixed height to ensure consistency
                                  display: "flex",
                                  flexDirection: "column",
                                  justifyContent: "center",
                                }}
                              >
                                <RunOrbLevelCarousel
                                  orbData={orbData}
                                  orbEntity={orbEntity}
                                />
                              </Box>
                            </CardContent>
                          </Card>
                        </Grid>
                      );
                    })}
                  </Grid>
                ) : (
                  // Fallback to simple chip display if detailed orb stats are not available
                  (<Box display="flex" flexWrap="wrap" gap={1}>
                    {run.orbsUsed.map((orb, index) => (
                      <Chip key={index} label={orb} variant="outlined" />
                    ))}
                  </Box>)
                )
              )}
            </Paper>
          </Grid>
        )}

        {/* Enemy Encounters */}
        {enemyEntitiesWithData.length > 0 && (
          <Grid size={12}>
            <Paper sx={{ p: 3 }}>
              <Box
                sx={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  cursor: "pointer",
                  mb: enemiesExpanded ? 2 : 0,
                }}
                onClick={() => setEnemiesExpanded(!enemiesExpanded)}
              >
                <Typography variant="h5">
                  Enemy Encounters ({enemyEntitiesWithData.length})
                </Typography>
                <IconButton size="small">
                  {enemiesExpanded ? <ExpandLess /> : <ExpandMore />}
                </IconButton>
              </Box>

              {enemiesExpanded && (
                <Grid container spacing={2}>
                  {enemyEntitiesWithData.map((enemyItem, index) => {
                    const sprite = enemyItem.entity
                      ? getEntitySprite(enemyItem.entity)
                      : null;
                    return (
                      <Grid
                        key={index}
                        size={{
                          xs: 12,
                          sm: 6,
                          md: 4
                        }}>
                        <Card variant="outlined">
                          <CardContent
                            sx={{
                              display: "flex",
                              alignItems: "flex-start",
                              gap: 2,
                            }}
                          >
                            <Avatar
                              src={sprite?.url}
                              alt={enemyItem.name}
                              sx={{
                                width: 48,
                                height: 48,
                                bgcolor: sprite ? "transparent" : "grey.300",
                              }}
                            >
                              {!sprite && enemyItem.name.charAt(0)}
                            </Avatar>
                            <Box sx={{ flex: 1, minWidth: 0 }}>
                              <Typography variant="subtitle1" fontWeight="bold">
                                {enemyItem.entity?.name || enemyItem.name}
                              </Typography>
                              {enemyItem.entity &&
                                enemyItem.entity.name !== enemyItem.name && (
                                  <Typography
                                    variant="caption"
                                    color="text.secondary"
                                    sx={{ display: "block" }}
                                  >
                                    Run data: {enemyItem.name}
                                  </Typography>
                                )}

                              <Grid container spacing={1} sx={{ mt: 1 }}>
                                <Grid size={6}>
                                  <Typography
                                    variant="body2"
                                    color="textSecondary"
                                  >
                                    Encountered
                                  </Typography>
                                  <Typography variant="body2">
                                    {enemyItem.playData.encounterCount}x
                                  </Typography>
                                </Grid>
                                <Grid size={6}>
                                  <Typography
                                    variant="body2"
                                    color="textSecondary"
                                  >
                                    Damage Dealt
                                  </Typography>
                                  <Typography variant="body2">
                                    {formatNumber(enemyItem.playData.damageDealt)}
                                  </Typography>
                                </Grid>
                                <Grid size={6}>
                                  <Typography
                                    variant="body2"
                                    color="textSecondary"
                                  >
                                    Damage Taken
                                  </Typography>
                                  <Typography variant="body2">
                                    {formatNumber(
                                      enemyItem.playData.damageReceived
                                    )}
                                  </Typography>
                                </Grid>
                                <Grid size={6}>
                                  <Typography
                                    variant="body2"
                                    color="textSecondary"
                                  >
                                    Defeats
                                  </Typography>
                                  <Typography variant="body2">
                                    {enemyItem.playData.killCount}
                                  </Typography>
                                </Grid>
                                {enemyItem.entity && (
                                  <>
                                    {enemyItem.entity.maxHealth && (
                                      <Grid size={6}>
                                        <Typography
                                          variant="body2"
                                          color="textSecondary"
                                        >
                                          Max Health
                                        </Typography>
                                        <Typography variant="body2">
                                          {formatNumber(
                                            enemyItem.entity.maxHealth
                                          )}
                                        </Typography>
                                      </Grid>
                                    )}
                                    {enemyItem.entity.location && (
                                      <Grid size={6}>
                                        <Typography
                                          variant="body2"
                                          color="textSecondary"
                                        >
                                          Location
                                        </Typography>
                                        <Typography variant="body2">
                                          {enemyItem.entity.location}
                                        </Typography>
                                      </Grid>
                                    )}
                                  </>
                                )}
                              </Grid>

                              <Box
                                sx={{
                                  mt: 1,
                                  display: "flex",
                                  flexWrap: "wrap",
                                  gap: 1,
                                }}
                              >
                                {enemyItem.entity?.enemyType && (
                                  <Chip
                                    label={enemyItem.entity.enemyType}
                                    size="small"
                                    color={
                                      enemyItem.entity.enemyType === "BOSS"
                                        ? "error"
                                        : enemyItem.entity.enemyType ===
                                          "MINIBOSS"
                                        ? "warning"
                                        : "default"
                                    }
                                  />
                                )}
                                {enemyItem.playData.killCount > 0 && (
                                  <Chip
                                    label="Defeated"
                                    size="small"
                                    color="success"
                                  />
                                )}
                              </Box>
                            </Box>
                          </CardContent>
                        </Card>
                      </Grid>
                    );
                  })}
                </Grid>
              )}
            </Paper>
          </Grid>
        )}

        {/* Rooms Visited */}
        {run.visitedRoomsInfo && run.visitedRoomsInfo.length > 0 && (
          <Grid size={12}>
            <Paper sx={{ p: 3 }}>
              <Box
                sx={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  cursor: "pointer",
                  mb: roomsExpanded ? 2 : 0,
                }}
                onClick={() => setRoomsExpanded(!roomsExpanded)}
              >
                <Typography variant="h5">
                  Rooms Visited ({run.visitedRoomsInfo.length})
                </Typography>
                <IconButton size="small">
                  {roomsExpanded ? <ExpandLess /> : <ExpandMore />}
                </IconButton>
              </Box>

              {roomsExpanded && (
                <Box>
                  {/* Room Timeline by Act */}
                  <Typography variant="h6" gutterBottom sx={{ mt: 2 }}>
                    Room Timeline
                  </Typography>
                  
                  {(() => {
                    const acts = groupRoomsByAct(run.visitedRoomsInfo);
                    let globalRoomIndex = 0; // Track global room position across all acts
                    
                    return acts.map((act, actIndex) => {
                      const actLabel = actIndex === acts.length - 1 && !act.some(room => room.id === 7) // 7 = BOSS
                        ? `Act ${actIndex + 1} (Incomplete)`
                        : `Act ${actIndex + 1}`;
                      
                      return (
                        <Box key={actIndex} sx={{ mb: 3 }}>
                          <Typography variant="subtitle1" fontWeight="bold" gutterBottom>
                            {actLabel}
                          </Typography>
                          <Box 
                            sx={{ 
                              display: "flex", 
                              flexWrap: "wrap", 
                              gap: 0.5,
                              alignItems: "center",
                            }}
                          >
                            {act.map((room, roomIndex) => {
                              globalRoomIndex++;
                              
                              // Handle boss rooms specially
                              let chipContent;
                              let tooltipTitle = `${room.name} (Room ${globalRoomIndex})`;
                              
                              if (room.id === 7 && run.bossNames && actIndex < run.bossNames.length) {
                                // This is a boss room - find the boss entity and use its sprite
                                const bossName = run.bossNames[actIndex];
                                
                                // First try to find entities with "boss" in the name that match the boss name
                                let bossEntity = entities?.enemies?.find(enemy => 
                                  enemy.name.toLowerCase().includes("boss") &&
                                  (enemy.name.toLowerCase().includes(bossName.toLowerCase()) ||
                                   bossName.toLowerCase().includes(enemy.name.toLowerCase().replace("boss", "").trim()))
                                );
                                
                                // If no boss entity found, try miniboss entities
                                if (!bossEntity) {
                                  bossEntity = entities?.enemies?.find(enemy => 
                                    enemy.name.toLowerCase().includes("miniboss") &&
                                    (enemy.name.toLowerCase().includes(bossName.toLowerCase()) ||
                                     bossName.toLowerCase().includes(enemy.name.toLowerCase().replace("miniboss", "").trim()))
                                  );
                                }
                                
                                // Fallback to any enemy that matches (for edge cases)
                                if (!bossEntity) {
                                  bossEntity = entities?.enemies?.find(enemy => 
                                    enemy.name.toLowerCase().includes(bossName.toLowerCase()) ||
                                    bossName.toLowerCase().includes(enemy.name.toLowerCase())
                                  );
                                }
                                
                                const bossSprite = bossEntity ? getEntitySprite(bossEntity) : null;
                                
                                if (bossSprite) {
                                  chipContent = (
                                    <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                                      <img 
                                        src={bossSprite.url} 
                                        alt={bossName}
                                        style={{ 
                                          width: 16, 
                                          height: 16, 
                                          objectFit: "contain",
                                          imageRendering: "pixelated"
                                        }} 
                                      />
                                      <span>{bossName}</span>
                                    </Box>
                                  );
                                } else {
                                  chipContent = `${room.symbol} ${bossName}`;
                                }
                                
                                tooltipTitle = `${bossName} Boss (Room ${globalRoomIndex})`;
                              } else {
                                chipContent = `${room.symbol} ${room.name}`;
                              }
                              
                              return (
                                <Box key={roomIndex} sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                                  <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 0.25 }}>
                                    <Typography 
                                      variant="caption" 
                                      sx={{ 
                                        fontSize: "0.6rem", 
                                        color: "text.secondary",
                                        lineHeight: 1
                                      }}
                                    >
                                      {globalRoomIndex}
                                    </Typography>
                                    <Tooltip title={tooltipTitle} arrow>
                                      <Chip
                                        label={chipContent}
                                        size="small"
                                        color={room.color as any}
                                        variant={room.id === 7 ? "filled" : "outlined"} // Boss rooms are filled
                                      />
                                    </Tooltip>
                                  </Box>
                                  {roomIndex < act.length - 1 && (
                                    <Typography 
                                      variant="body2" 
                                      sx={{ 
                                        color: "text.secondary",
                                        fontSize: "0.75rem",
                                        mx: 0.5
                                      }}
                                    >
                                      →
                                    </Typography>
                                  )}
                                </Box>
                              );
                            })}
                          </Box>
                        </Box>
                      );
                    });
                  })()}

                  {/* Room Statistics */}
                  <Divider sx={{ my: 3 }} />
                  <Typography variant="h6" gutterBottom>
                    Room Statistics
                  </Typography>
                  <Grid container spacing={2}>
                    {Object.entries(run.roomTypeStatistics)
                      .sort(([, countA], [, countB]) => countB - countA) // Sort by count descending
                      .map(([roomType, count]) => (
                        <Grid
                          key={roomType}
                          size={{
                            xs: 6,
                            sm: 4,
                            md: 3
                          }}>
                          <Card variant="outlined" sx={{ textAlign: "center", p: 1 }}>
                            <Typography variant="body2" color="textSecondary">
                              {roomType}
                            </Typography>
                            <Typography variant="h6">
                              {count}
                            </Typography>
                          </Card>
                        </Grid>
                      ))}
                  </Grid>
                </Box>
              )}
            </Paper>
          </Grid>
        )}

        {/* Additional Stats */}
        <Grid
          size={{
            xs: 12,
            md: 6
          }}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Combat Stats
            </Typography>
            <Grid container spacing={2}>
              <Grid size={6}>
                <Typography variant="body2" color="textSecondary">
                  Shots Taken
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.shotsTaken)}
                </Typography>
              </Grid>
              <Grid size={6}>
                <Typography variant="body2" color="textSecondary">
                  Crit Shots
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.critShotsTaken)}
                </Typography>
              </Grid>
              <Grid size={6}>
                <Typography variant="body2" color="textSecondary">
                  Bombs Thrown
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.bombsThrown)}
                </Typography>
              </Grid>
              <Grid size={6}>
                <Typography variant="body2" color="textSecondary">
                  Damage Negated
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.totalDamageNegated)}
                </Typography>
              </Grid>
            </Grid>
          </Paper>
        </Grid>

        <Grid
          size={{
            xs: 12,
            md: 6
          }}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Peg Stats
            </Typography>
            <Grid container spacing={2}>
              <Grid size={6}>
                <Typography variant="body2" color="textSecondary">
                  Pegs Hit (Refresh)
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.pegsHitRefresh)}
                </Typography>
              </Grid>
              <Grid size={6}>
                <Typography variant="body2" color="textSecondary">
                  Pegs Hit (Crit)
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.pegsHitCrit)}
                </Typography>
              </Grid>
              <Grid size={6}>
                <Typography variant="body2" color="textSecondary">
                  Pegs Refreshed
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.pegsRefreshed)}
                </Typography>
              </Grid>
              <Grid size={6}>
                <Typography variant="body2" color="textSecondary">
                  Max Single Attack
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.mostDamageDealtWithSingleAttack)}
                </Typography>
              </Grid>
            </Grid>
          </Paper>
        </Grid>
      </Grid>
    </Box>
  );
};

export default RunDetail;

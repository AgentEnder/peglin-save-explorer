import React, { useMemo } from "react";
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
} from "@mui/material";
import { ArrowBack, HelpOutline } from "@mui/icons-material";
import { useRunsAndConfig } from "../store/useAppStore";
import { useEntities, useSpriteActions } from "../store/useSpriteStore";
import { getRarityName, getRarityColor, getRarityTooltip, isUnavailableRarity } from "../utils/rarityHelper";

const RunDetail: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { runs } = useRunsAndConfig();
  const entities = useEntities();
  const { getEntitySprite } = useSpriteActions();

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
          (r) => r.name.toLowerCase() === relicName.toLowerCase() || 
                 r.id.toLowerCase() === relicName.toLowerCase()
        );
        return relic;
      })
      .filter(Boolean);
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
        <Grid item xs={12}>
          <Paper sx={{ p: 3 }}>
            <Box display="flex" justifyContent="space-between" alignItems="center" mb={2}>
              <Typography variant="h5">
                {run.characterClass}
              </Typography>
              <Chip
                label={run.won ? "Victory" : "Defeat"}
                color={run.won ? "success" : "error"}
                size="medium"
              />
            </Box>
            
            <Grid container spacing={2}>
              <Grid item xs={6} sm={3}>
                <Typography variant="body2" color="textSecondary">
                  Damage Dealt
                </Typography>
                <Typography variant="h6">
                  {formatNumber(run.damageDealt)}
                </Typography>
              </Grid>
              <Grid item xs={6} sm={3}>
                <Typography variant="body2" color="textSecondary">
                  Duration
                </Typography>
                <Typography variant="h6">
                  {formatDuration(run.duration)}
                </Typography>
              </Grid>
              <Grid item xs={6} sm={3}>
                <Typography variant="body2" color="textSecondary">
                  Final Level
                </Typography>
                <Typography variant="h6">
                  {run.finalLevel}
                </Typography>
              </Grid>
              <Grid item xs={6} sm={3}>
                <Typography variant="body2" color="textSecondary">
                  Cruciball Level
                </Typography>
                <Typography variant="h6">
                  {run.cruciballLevel}
                </Typography>
              </Grid>
            </Grid>

            <Divider sx={{ my: 2 }} />

            <Grid container spacing={2}>
              <Grid item xs={6} sm={3}>
                <Typography variant="body2" color="textSecondary">
                  Pegs Hit
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.pegsHit)}
                </Typography>
              </Grid>
              <Grid item xs={6} sm={3}>
                <Typography variant="body2" color="textSecondary">
                  Coins Earned
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.coinsEarned)}
                </Typography>
              </Grid>
              <Grid item xs={6} sm={3}>
                <Typography variant="body2" color="textSecondary">
                  Final HP
                </Typography>
                <Typography variant="body1">
                  {run.finalHp}/{run.maxHp}
                </Typography>
              </Grid>
              <Grid item xs={6} sm={3}>
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
        <Grid item xs={12}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h5" gutterBottom>
              Relics ({relicEntities.length})
            </Typography>
            
            {relicEntities.length === 0 ? (
              <Typography variant="body2" color="textSecondary">
                No relics were used in this run.
              </Typography>
            ) : (
              <Grid container spacing={2}>
                {relicEntities.map((relic, index) => {
                  const sprite = getEntitySprite(relic!);
                  return (
                    <Grid item xs={12} sm={6} md={4} key={index}>
                      <Card variant="outlined">
                        <CardContent sx={{ display: 'flex', alignItems: 'flex-start', gap: 2 }}>
                          <Avatar
                            src={sprite?.url}
                            alt={relic!.name}
                            sx={{ 
                              width: 48, 
                              height: 48,
                              bgcolor: sprite ? 'transparent' : 'grey.300'
                            }}
                          >
                            {!sprite && relic!.name.charAt(0)}
                          </Avatar>
                          <Box sx={{ flex: 1, minWidth: 0 }}>
                            <Typography variant="subtitle1" fontWeight="bold">
                              {relic!.name}
                            </Typography>
                            {relic!.description && (
                              <Typography variant="body2" color="textSecondary">
                                {relic!.description}
                              </Typography>
                            )}
                            {relic!.rarity && (
                              <Tooltip title={getRarityTooltip(relic!.rarity) || ""} arrow>
                                <Chip 
                                  label={getRarityName(relic!.rarity)} 
                                  size="small" 
                                  sx={{ mt: 1 }}
                                  color={getRarityColor(relic!.rarity) as any}
                                  icon={isUnavailableRarity(relic!.rarity) ? <HelpOutline fontSize="small" /> : undefined}
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
            )}
          </Paper>
        </Grid>

        {/* Orbs */}
        {(Object.keys(run.orbStats || {}).length > 0 || run.orbsUsed.length > 0) && (
          <Grid item xs={12}>
            <Paper sx={{ p: 3 }}>
              <Typography variant="h5" gutterBottom>
                Orbs ({Object.keys(run.orbStats || {}).length || run.orbsUsed.length})
              </Typography>
              
              {Object.keys(run.orbStats || {}).length > 0 ? (
                <Grid container spacing={2}>
                  {Object.entries(run.orbStats).map(([orbName, orbData]) => {
                    // Clean up orb name by removing level suffix like "-Lvl1", "-Lvl2", etc.
                    const cleanOrbName = orbData.name.replace(/-Lvl\d+$/, '');
                    return (
                      <Grid item xs={12} sm={6} md={4} key={orbName}>
                        <Card variant="outlined">
                          <CardContent>
                            <Typography variant="subtitle1" fontWeight="bold" gutterBottom>
                              {cleanOrbName}
                            </Typography>
                          <Grid container spacing={1}>
                            <Grid item xs={6}>
                              <Typography variant="body2" color="textSecondary">
                                Quantity
                              </Typography>
                              <Typography variant="h6">
                                {orbData.amountInDeck}
                              </Typography>
                            </Grid>
                            <Grid item xs={6}>
                              <Typography variant="body2" color="textSecondary">
                                Damage Dealt
                              </Typography>
                              <Typography variant="h6">
                                {formatNumber(orbData.damageDealt)}
                              </Typography>
                            </Grid>
                            <Grid item xs={6}>
                              <Typography variant="body2" color="textSecondary">
                                Times Fired
                              </Typography>
                              <Typography variant="body2">
                                {formatNumber(orbData.timesFired)}
                              </Typography>
                            </Grid>
                            <Grid item xs={6}>
                              <Typography variant="body2" color="textSecondary">
                                Efficiency
                              </Typography>
                              <Typography variant="body2">
                                {orbData.timesFired > 0 
                                  ? formatNumber(Math.round(orbData.damageDealt / orbData.timesFired))
                                  : 0
                                } per shot
                              </Typography>
                            </Grid>
                            {(orbData.timesDiscarded > 0 || orbData.timesRemoved > 0) && (
                              <>
                                <Grid item xs={6}>
                                  <Typography variant="body2" color="textSecondary">
                                    Discarded
                                  </Typography>
                                  <Typography variant="body2">
                                    {orbData.timesDiscarded}
                                  </Typography>
                                </Grid>
                                <Grid item xs={6}>
                                  <Typography variant="body2" color="textSecondary">
                                    Removed
                                  </Typography>
                                  <Typography variant="body2">
                                    {orbData.timesRemoved}
                                  </Typography>
                                </Grid>
                              </>
                            )}
                          </Grid>
                          <Box sx={{ mt: 1, display: 'flex', flexWrap: 'wrap', gap: 1 }}>
                            {orbData.starting && (
                              <Chip 
                                label="Starting Orb" 
                                size="small" 
                                color="primary" 
                              />
                            )}
                            {orbData.levelInstances && orbData.levelInstances.length > 0 && (
                              <>
                                {orbData.levelInstances.map((count, level) => 
                                  count > 0 && (
                                    <Chip 
                                      key={level}
                                      label={`Lvl ${level + 1}: ${count}`} 
                                      size="small" 
                                      variant="outlined"
                                      color="secondary"
                                    />
                                  )
                                )}
                              </>
                            )}
                          </Box>
                        </CardContent>
                      </Card>
                    </Grid>
                    );
                  })}
                </Grid>
              ) : (
                // Fallback to simple chip display if detailed orb stats are not available
                <Box display="flex" flexWrap="wrap" gap={1}>
                  {run.orbsUsed.map((orb, index) => (
                    <Chip key={index} label={orb} variant="outlined" />
                  ))}
                </Box>
              )}
            </Paper>
          </Grid>
        )}

        {/* Additional Stats */}
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Combat Stats
            </Typography>
            <Grid container spacing={2}>
              <Grid item xs={6}>
                <Typography variant="body2" color="textSecondary">
                  Shots Taken
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.shotsTaken)}
                </Typography>
              </Grid>
              <Grid item xs={6}>
                <Typography variant="body2" color="textSecondary">
                  Crit Shots
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.critShotsTaken)}
                </Typography>
              </Grid>
              <Grid item xs={6}>
                <Typography variant="body2" color="textSecondary">
                  Bombs Thrown
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.bombsThrown)}
                </Typography>
              </Grid>
              <Grid item xs={6}>
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

        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Peg Stats
            </Typography>
            <Grid container spacing={2}>
              <Grid item xs={6}>
                <Typography variant="body2" color="textSecondary">
                  Pegs Hit (Refresh)
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.pegsHitRefresh)}
                </Typography>
              </Grid>
              <Grid item xs={6}>
                <Typography variant="body2" color="textSecondary">
                  Pegs Hit (Crit)
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.pegsHitCrit)}
                </Typography>
              </Grid>
              <Grid item xs={6}>
                <Typography variant="body2" color="textSecondary">
                  Pegs Refreshed
                </Typography>
                <Typography variant="body1">
                  {formatNumber(run.pegsRefreshed)}
                </Typography>
              </Grid>
              <Grid item xs={6}>
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

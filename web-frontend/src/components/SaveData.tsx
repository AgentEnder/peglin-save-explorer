import React, { useState, useMemo } from "react";
import {
  Paper,
  Typography,
  Box,
  Grid,
  Card,
  CardContent,
  TextField,
  Button,
  Alert,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  IconButton,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
} from "@mui/material";
import {
  Edit as EditIcon,
  Save as SaveIcon,
  Cancel as CancelIcon,
  Info as InfoIcon,
} from "@mui/icons-material";
import {
  useRunsAndConfig,
  usePlayerStatistics,
  useAppActions,
} from "../store/useAppStore";
import { api } from "../api";

const SaveData: React.FC = () => {
  const { runs } = useRunsAndConfig();
  const playerStatistics = usePlayerStatistics();
  const { refresh } = useAppActions();

  const [editingCruciball, setEditingCruciball] = useState<string | null>(null);
  const [cruciballValues, setCruciballValues] = useState<
    Record<string, number>
  >({});
  const [saveStatus, setSaveStatus] = useState<{
    type: "success" | "error";
    message: string;
  } | null>(null);
  const [infoDialogOpen, setInfoDialogOpen] = useState(false);

  // Get cruciball levels from player statistics
  const cruciballLevels = useMemo(() => {
    if (playerStatistics?.gameplayStats?.["Cruciball Levels"]) {
      return playerStatistics.gameplayStats[
        "Cruciball Levels"
      ] as unknown as Record<string, number>;
    }
    return {};
  }, [playerStatistics]);

  // Calculate save data statistics from runs
  const saveStats = useMemo(() => {
    if (!runs.length) return null;

    // Group by character class
    const classCruciballLevels = runs.reduce(
      (acc, run) => {
        const className = run.characterClass;
        if (!acc[className]) {
          acc[className] = {
            maxCruciball: 0,
            totalRuns: 0,
            wins: 0,
            totalDamage: 0,
            bestDamage: 0,
            avgLevel: 0,
            totalLevels: 0,
          };
        }

        acc[className].maxCruciball = Math.max(
          acc[className].maxCruciball,
          run.cruciballLevel
        );
        acc[className].totalRuns++;
        if (run.won) acc[className].wins++;
        acc[className].totalDamage += run.damageDealt;
        acc[className].bestDamage = Math.max(
          acc[className].bestDamage,
          run.damageDealt
        );
        acc[className].totalLevels += run.finalLevel;
        acc[className].avgLevel =
          acc[className].totalLevels / acc[className].totalRuns;

        return acc;
      },
      {} as Record<
        string,
        {
          maxCruciball: number;
          totalRuns: number;
          wins: number;
          totalDamage: number;
          bestDamage: number;
          avgLevel: number;
          totalLevels: number;
        }
      >
    );

    // Overall stats
    const totalRuns = runs.length;
    const totalWins = runs.filter((r) => r.won).length;
    const totalDamage = runs.reduce((sum, r) => sum + r.damageDealt, 0);
    const bestRun = runs.reduce((best, current) =>
      current.damageDealt > best.damageDealt ? current : best
    );
    const avgCruciball =
      runs.reduce((sum, r) => sum + r.cruciballLevel, 0) / runs.length;

    return {
      classCruciballLevels,
      totalRuns,
      totalWins,
      winRate: totalWins / totalRuns,
      totalDamage,
      avgDamage: totalDamage / totalRuns,
      bestRun,
      avgCruciball,
    };
  }, [runs]);

  const handleEditCruciball = (className: string, currentValue: number) => {
    setEditingCruciball(className);
    setCruciballValues({ ...cruciballValues, [className]: currentValue });
  };

  const handleSaveCruciball = async (className: string) => {
    try {
      const newValue = cruciballValues[className];
      if (newValue < 0 || newValue > 20) {
        setSaveStatus({
          type: "error",
          message: "Cruciball level must be between 0 and 20",
        });
        return;
      }

      // Call the API to update the cruciball level
      const result = await api.updateCruciballLevel(className, newValue);
      setSaveStatus({ type: "success", message: result.message });
      setEditingCruciball(null);

      // Refresh the data to reflect the changes
      await refresh();

      // Clear the status after 3 seconds
      setTimeout(() => setSaveStatus(null), 3000);
    } catch {
      setSaveStatus({
        type: "error",
        message: "Failed to update cruciball level",
      });
    }
  };

  // Removed class lock/unlock functionality - class locking is managed through achievements,
  // not cruciball levels. Setting cruciball to -1 does not lock a class.

  const handleCancelEdit = () => {
    setEditingCruciball(null);
    setCruciballValues({});
  };

  const formatNumber = (num: number) => {
    if (num >= 1000000) return `${(num / 1000000).toFixed(1)}M`;
    if (num >= 1000) return `${(num / 1000).toFixed(1)}K`;
    return num.toLocaleString();
  };

  if (!saveStats) {
    return (
      <Box>
        <Typography variant="h4" gutterBottom>
          Save Data
        </Typography>
        <Alert severity="info">
          No data available. Please upload a save file to see your save data.
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      <Box display="flex" alignItems="center" mb={3}>
        <Typography variant="h4" gutterBottom sx={{ mb: 0, mr: 2 }}>
          Save Data Overview
        </Typography>
        <IconButton onClick={() => setInfoDialogOpen(true)} color="primary">
          <InfoIcon />
        </IconButton>
      </Box>
      {saveStatus && (
        <Alert
          severity={saveStatus.type}
          sx={{ mb: 3 }}
          onClose={() => setSaveStatus(null)}
        >
          {saveStatus.message}
        </Alert>
      )}
      <Grid container spacing={3}>
        {/* Overall Statistics */}
        <Grid size={12}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              Overall Statistics
            </Typography>
            <Grid container spacing={2}>
              <Grid
                size={{
                  xs: 6,
                  sm: 3
                }}>
                <Card variant="outlined">
                  <CardContent>
                    <Typography color="textSecondary" gutterBottom>
                      Total Runs
                    </Typography>
                    <Typography variant="h5">{saveStats.totalRuns}</Typography>
                  </CardContent>
                </Card>
              </Grid>
              <Grid
                size={{
                  xs: 6,
                  sm: 3
                }}>
                <Card variant="outlined">
                  <CardContent>
                    <Typography color="textSecondary" gutterBottom>
                      Win Rate
                    </Typography>
                    <Typography variant="h5">
                      {(saveStats.winRate * 100).toFixed(1)}%
                    </Typography>
                  </CardContent>
                </Card>
              </Grid>
              <Grid
                size={{
                  xs: 6,
                  sm: 3
                }}>
                <Card variant="outlined">
                  <CardContent>
                    <Typography color="textSecondary" gutterBottom>
                      Avg Damage
                    </Typography>
                    <Typography variant="h5">
                      {formatNumber(saveStats.avgDamage)}
                    </Typography>
                  </CardContent>
                </Card>
              </Grid>
              <Grid
                size={{
                  xs: 6,
                  sm: 3
                }}>
                <Card variant="outlined">
                  <CardContent>
                    <Typography color="textSecondary" gutterBottom>
                      Avg Cruciball
                    </Typography>
                    <Typography variant="h5">
                      {saveStats.avgCruciball.toFixed(1)}
                    </Typography>
                  </CardContent>
                </Card>
              </Grid>
            </Grid>
          </Paper>
        </Grid>

        {/* Best Run */}
        <Grid
          size={{
            xs: 12,
            md: 6
          }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              Best Run
            </Typography>
            <Card variant="outlined">
              <CardContent>
                <Box
                  display="flex"
                  justifyContent="space-between"
                  alignItems="center"
                  mb={1}
                >
                  <Typography variant="h6">
                    {saveStats.bestRun.characterClass}
                  </Typography>
                  <Chip
                    label={saveStats.bestRun.won ? "Victory" : "Defeat"}
                    color={saveStats.bestRun.won ? "success" : "error"}
                    size="small"
                  />
                </Box>
                <Typography variant="body2" color="textSecondary">
                  Damage: {formatNumber(saveStats.bestRun.damageDealt)}
                </Typography>
                <Typography variant="body2" color="textSecondary">
                  Level: {saveStats.bestRun.finalLevel}
                </Typography>
                <Typography variant="body2" color="textSecondary">
                  Cruciball: {saveStats.bestRun.cruciballLevel}
                </Typography>
                <Typography variant="body2" color="textSecondary">
                  Date:{" "}
                  {new Date(saveStats.bestRun.timestamp).toLocaleDateString()}
                </Typography>
              </CardContent>
            </Card>
          </Paper>
        </Grid>

        {/* Player Statistics */}
        {playerStatistics && (
          <Grid
            size={{
              xs: 12,
              md: 6
            }}>
            <Paper sx={{ p: 2 }}>
              <Typography variant="h6" gutterBottom>
                Player Statistics
              </Typography>
              <Grid container spacing={1}>
                {Object.entries(playerStatistics.gameplayStats).map(
                  ([key, value]) => (
                    <Grid key={key} size={6}>
                      <Typography variant="body2" color="textSecondary">
                        {key}:
                      </Typography>
                      <Typography variant="body2">
                        {typeof value === "number"
                          ? formatNumber(value)
                          : String(value)}
                      </Typography>
                    </Grid>
                  )
                )}
              </Grid>
            </Paper>
          </Grid>
        )}

        {/* Class Cruciball Levels */}
        <Grid
          size={{
            xs: 12,
            md: 6
          }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              Class Cruciball Levels
            </Typography>
            <Typography variant="body2" color="textSecondary" sx={{ mb: 2 }}>
              Highest cruciball level achieved for each class. Class unlocking
              is managed through achievements, not cruciball levels.
            </Typography>
            <Grid container spacing={2}>
              {Object.entries(cruciballLevels).map(([className, level]) => {
                const hasPlayed = level >= 0;

                return (
                  <Grid
                    key={className}
                    size={{
                      xs: 12,
                      sm: 6
                    }}>
                    <Card
                      variant="outlined"
                      sx={{
                        border: hasPlayed
                          ? "1px solid #e0e0e0"
                          : "1px solid #fafafa",
                      }}
                    >
                      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
                        <Box>
                          <Typography variant="subtitle1" fontWeight="bold">
                            {className}
                          </Typography>
                          <Typography variant="body2" color="textSecondary">
                            Cruciball Level: {hasPlayed ? level : "Not played"}
                          </Typography>
                        </Box>
                      </CardContent>
                    </Card>
                  </Grid>
                );
              })}
            </Grid>
            <Typography
              variant="caption"
              color="textSecondary"
              sx={{ mt: 2, display: "block" }}
            >
              Note: To unlock/lock classes, use the CLI command:
              peglin-save-explorer manage-classes
            </Typography>
          </Paper>
        </Grid>

        {/* Class Statistics Table */}
        <Grid size={12}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              Character Class Statistics
            </Typography>
            <TableContainer>
              <Table>
                <TableHead>
                  <TableRow>
                    <TableCell>Class</TableCell>
                    <TableCell align="right">Cruciball Level</TableCell>
                    <TableCell align="right">Total Runs</TableCell>
                    <TableCell align="right">Win Rate</TableCell>
                    <TableCell align="right">Best Damage</TableCell>
                    <TableCell align="right">Avg Level</TableCell>
                    <TableCell align="center">Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {Object.entries(saveStats.classCruciballLevels).map(
                    ([className, stats]) => {
                      const actualCruciballLevel =
                        cruciballLevels[className] ?? stats.maxCruciball;

                      return (
                        <TableRow key={className}>
                          <TableCell component="th" scope="row">
                            {className}
                          </TableCell>
                          <TableCell align="right">
                            {editingCruciball === className ? (
                              <Box display="flex" alignItems="center" gap={1}>
                                <TextField
                                  size="small"
                                  type="number"
                                  value={
                                    cruciballValues[className] ||
                                    actualCruciballLevel
                                  }
                                  onChange={(e) =>
                                    setCruciballValues({
                                      ...cruciballValues,
                                      [className]:
                                        parseInt(e.target.value) || 0,
                                    })
                                  }
                                  inputProps={{ min: 0, max: 20 }}
                                  sx={{ width: 80 }}
                                />
                                <IconButton
                                  size="small"
                                  color="primary"
                                  onClick={() => handleSaveCruciball(className)}
                                >
                                  <SaveIcon />
                                </IconButton>
                                <IconButton
                                  size="small"
                                  onClick={handleCancelEdit}
                                >
                                  <CancelIcon />
                                </IconButton>
                              </Box>
                            ) : (
                              <Box display="flex" alignItems="center" gap={1}>
                                <Typography>{actualCruciballLevel}</Typography>
                                <IconButton
                                  size="small"
                                  onClick={() =>
                                    handleEditCruciball(
                                      className,
                                      actualCruciballLevel
                                    )
                                  }
                                >
                                  <EditIcon />
                                </IconButton>
                              </Box>
                            )}
                          </TableCell>
                          <TableCell align="right">{stats.totalRuns}</TableCell>
                          <TableCell align="right">
                            {((stats.wins / stats.totalRuns) * 100).toFixed(1)}%
                          </TableCell>
                          <TableCell align="right">
                            {formatNumber(stats.bestDamage)}
                          </TableCell>
                          <TableCell align="right">
                            {stats.avgLevel.toFixed(1)}
                          </TableCell>
                          <TableCell align="center">-</TableCell>
                        </TableRow>
                      );
                    }
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          </Paper>
        </Grid>
      </Grid>
      {/* Info Dialog */}
      <Dialog open={infoDialogOpen} onClose={() => setInfoDialogOpen(false)}>
        <DialogTitle>Save Data Information</DialogTitle>
        <DialogContent>
          <Typography paragraph>
            This page shows overall statistics from your save file, aggregated
            from all your runs.
          </Typography>
          <Typography paragraph>
            <strong>Cruciball Level:</strong> You can edit the cruciball level
            for each character class. This represents the highest cruciball
            level you've achieved with that class. Valid values are 0-20.
          </Typography>
          <Typography paragraph>
            <strong>Class Unlocking:</strong> Class unlocking is managed through
            achievements in the game. To manually unlock or lock classes, use
            the CLI command: peglin-save-explorer manage-classes
          </Typography>
          <Typography paragraph>
            <strong>Note:</strong> Editing values here will modify your save
            file. Changes are permanent once saved.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setInfoDialogOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default SaveData;

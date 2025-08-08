import React, { useMemo } from "react";
import {
  Paper,
  Typography,
  Box,
  Grid,
  Card,
  CardContent,
  Chip,
  Alert,
  CardActionArea,
} from "@mui/material";
import {
  TrendingUp,
  TrendingDown,
  Casino,
  EmojiEvents,
} from "@mui/icons-material";
import { BarChart, PieChart } from "@mui/x-charts";
import { useNavigate } from "react-router-dom";
import {
  useRunHistoryData,
  useSummary,
  useRunsAndConfig,
} from "../store/useAppStore";

const Dashboard: React.FC = () => {
  const navigate = useNavigate();
  const runHistoryData = useRunHistoryData();
  const summary = useSummary();
  const { runs, excludeCustomRuns } = useRunsAndConfig();

  // Filter runs based on config - using useMemo to prevent infinite loops
  const filteredRuns = useMemo(() => {
    return excludeCustomRuns ? runs.filter((run) => !run.isCustomRun) : runs;
  }, [runs, excludeCustomRuns]);

  // Calculate class statistics from filtered runs
  const classStats = useMemo(() => {
    return filteredRuns.reduce((acc, run) => {
      const className = run.characterClass;
      if (!acc[className]) {
        acc[className] = { totalRuns: 0, wins: 0, totalDamage: 0 };
      }
      acc[className].totalRuns++;
      if (run.won) acc[className].wins++;
      acc[className].totalDamage += run.damageDealt;
      return acc;
    }, {} as Record<string, { totalRuns: number; wins: number; totalDamage: number }>);
  }, [filteredRuns]);

  // Calculate summary stats from filtered data
  const summaryStats = useMemo(() => {
    const totalRuns = filteredRuns.length;
    const totalWins = filteredRuns.filter((run) => run.won).length;
    const winRate = totalRuns > 0 ? totalWins / totalRuns : 0;
    return { totalRuns, totalWins, winRate };
  }, [filteredRuns]);

  const recentRuns = useMemo(() => filteredRuns.slice(0, 5), [filteredRuns]);

  const winsByClass = useMemo(() => {
    return Object.entries(classStats).map(([className, stats]) => ({
      label: className,
      value: stats.wins,
    }));
  }, [classStats]);

  const damageByClass = useMemo(() => {
    return Object.entries(classStats).map(([className, stats]) => ({
      className,
      damage: stats.totalRuns > 0 ? stats.totalDamage / stats.totalRuns : 0,
    }));
  }, [classStats]);

  // Calculate best class from filtered data
  const bestClass = useMemo(() => {
    return (
      Object.entries(classStats).sort((a, b) => {
        const aWinRate = a[1].totalRuns > 0 ? a[1].wins / a[1].totalRuns : 0;
        const bWinRate = b[1].totalRuns > 0 ? b[1].wins / b[1].totalRuns : 0;
        return bWinRate - aWinRate;
      })[0]?.[0] || "N/A"
    );
  }, [classStats]);

  if (!runHistoryData || !summary) {
    return (
      <Box>
        <Typography variant="h4" gutterBottom>
          Dashboard
        </Typography>
        <Alert severity="info">
          No data available. Please upload a save file to see your statistics.
        </Alert>
      </Box>
    );
  }

  const formatDuration = (value: string | number) => {
    let totalSeconds: number;

    if (typeof value === "string") {
      // Parse TimeSpan format like "00:06:43.2200000"
      const timeMatch = value.match(/^(\d+):(\d+):(\d+)(?:\.(\d+))?$/);
      if (timeMatch) {
        const [, hours, minutes, seconds] = timeMatch;
        totalSeconds =
          parseInt(hours) * 3600 + parseInt(minutes) * 60 + parseInt(seconds);
      } else {
        totalSeconds = 0;
      }
    } else {
      totalSeconds = value || 0;
    }

    const minutes = Math.floor(totalSeconds / 60);
    const hours = Math.floor(minutes / 60);
    if (hours > 0) {
      return `${hours}h ${minutes % 60}m`;
    }
    return `${minutes}m`;
  };

  const formatNumber = (num: number) => {
    if (num >= 1000000) {
      return `${(num / 1000000).toFixed(1)}M`;
    }
    if (num >= 1000) {
      return `${(num / 1000).toFixed(1)}K`;
    }
    return num.toString();
  };

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Dashboard
      </Typography>

      <Grid container spacing={3}>
        {/* Summary Cards */}
        <Grid item xs={12} sm={6} md={3}>
          <Card>
            <CardContent>
              <Box display="flex" alignItems="center">
                <Casino color="primary" sx={{ mr: 2 }} />
                <Box>
                  <Typography color="textSecondary" gutterBottom>
                    Total Runs
                  </Typography>
                  <Typography variant="h5">{summaryStats.totalRuns}</Typography>
                  {excludeCustomRuns && (
                    <Typography variant="caption" color="textSecondary">
                      (Custom runs excluded)
                    </Typography>
                  )}
                </Box>
              </Box>
            </CardContent>
          </Card>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Card>
            <CardContent>
              <Box display="flex" alignItems="center">
                <EmojiEvents color="success" sx={{ mr: 2 }} />
                <Box>
                  <Typography color="textSecondary" gutterBottom>
                    Total Wins
                  </Typography>
                  <Typography variant="h5">{summaryStats.totalWins}</Typography>
                </Box>
              </Box>
            </CardContent>
          </Card>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Card>
            <CardContent>
              <Box display="flex" alignItems="center">
                <TrendingUp color="info" sx={{ mr: 2 }} />
                <Box>
                  <Typography color="textSecondary" gutterBottom>
                    Win Rate
                  </Typography>
                  <Typography variant="h5">
                    {(summaryStats.winRate * 100).toFixed(1)}%
                  </Typography>
                </Box>
              </Box>
            </CardContent>
          </Card>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Card>
            <CardContent>
              <Box display="flex" alignItems="center">
                <TrendingDown color="warning" sx={{ mr: 2 }} />
                <Box>
                  <Typography color="textSecondary" gutterBottom>
                    Best Class
                  </Typography>
                  <Typography variant="h6">{bestClass}</Typography>
                </Box>
              </Box>
            </CardContent>
          </Card>
        </Grid>

        {/* Charts */}
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              Wins by Character Class
            </Typography>
            {winsByClass.length > 0 && (
              <Box
                sx={{
                  display: "flex",
                  justifyContent: "center",
                  alignItems: "center",
                }}
              >
                <PieChart
                  series={[
                    {
                      data: winsByClass,
                      innerRadius: 30,
                      outerRadius: 80,
                      paddingAngle: 2,
                      cornerRadius: 5,
                    },
                  ]}
                  width={400}
                  height={300}
                  slotProps={{
                    legend: {
                      direction: "row",
                      position: { vertical: "bottom", horizontal: "middle" },
                      padding: 0,
                    },
                  }}
                  margin={{ top: 20, bottom: 60, left: 60, right: 60 }}
                />
              </Box>
            )}
          </Paper>
        </Grid>

        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              Average Damage by Class
            </Typography>
            {damageByClass.length > 0 && (
              <Box sx={{ display: "flex", justifyContent: "center" }}>
                <BarChart
                  xAxis={[
                    {
                      scaleType: "band",
                      data: damageByClass.map((d) => d.className),
                      tickLabelStyle: { angle: -45, textAnchor: "end" },
                    },
                  ]}
                  series={[
                    {
                      data: damageByClass.map((d) => d.damage),
                      label: "Average Damage",
                      color: "#1976d2",
                    },
                  ]}
                  width={400}
                  height={300}
                  margin={{ top: 40, bottom: 80, left: 60, right: 40 }}
                />
              </Box>
            )}
          </Paper>
        </Grid>

        {/* Recent Runs */}
        <Grid item xs={12}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              Recent Runs
            </Typography>
            <Grid container spacing={2}>
              {recentRuns.map((run) => (
                <Grid item xs={12} sm={6} md={4} key={run.id}>
                  <Card variant="outlined">
                    <CardActionArea onClick={() => navigate(`/runs/${encodeURIComponent(run.id)}`)}>
                      <CardContent>
                        <Box
                          display="flex"
                          justifyContent="space-between"
                          alignItems="center"
                          mb={1}
                        >
                          <Chip
                            label={run.won ? "Victory" : "Defeat"}
                            color={run.won ? "success" : "error"}
                            size="small"
                          />
                          <Typography variant="caption" color="textSecondary">
                            {new Date(run.timestamp).toLocaleDateString()}
                          </Typography>
                        </Box>
                        <Typography variant="subtitle1" gutterBottom>
                          {run.characterClass}
                        </Typography>
                        <Typography variant="body2" color="textSecondary">
                          Damage: {formatNumber(run.damageDealt)}
                        </Typography>
                        <Typography variant="body2" color="textSecondary">
                          Duration: {formatDuration(run.duration)}
                        </Typography>
                        <Typography variant="body2" color="textSecondary">
                          Level: {run.finalLevel}
                        </Typography>
                      </CardContent>
                    </CardActionArea>
                  </Card>
                </Grid>
              ))}
            </Grid>
          </Paper>
        </Grid>
      </Grid>
    </Box>
  );
};

export default Dashboard;

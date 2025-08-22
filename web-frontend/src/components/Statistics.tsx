import React, { useMemo } from "react";
import {
  Paper,
  Typography,
  Box,
  Grid,
  Card,
  CardContent,
  Alert,
} from "@mui/material";
import { BarChart, PieChart, LineChart } from "@mui/x-charts";
import { useRunHistoryData, useRunsAndConfig } from "../store/useAppStore";

const Statistics: React.FC = () => {
  const runHistoryData = useRunHistoryData();
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

  const classData = useMemo(() => {
    return Object.entries(classStats).map(([name, stats]) => ({
      className: name,
      winRate: stats.totalRuns > 0 ? (stats.wins / stats.totalRuns) * 100 : 0,
      totalRuns: stats.totalRuns,
      averageDamage:
        stats.totalRuns > 0 ? stats.totalDamage / stats.totalRuns : 0,
    }));
  }, [classStats]);

  // Calculate orb statistics from filtered runs
  const orbStats = useMemo(() => {
    return filteredRuns.reduce((acc, run) => {
      run.orbsUsed.forEach((orb) => {
        if (!acc[orb]) {
          acc[orb] = { timesUsed: 0, wins: 0 };
        }
        acc[orb].timesUsed++;
        if (run.won) acc[orb].wins++;
      });
      return acc;
    }, {} as Record<string, { timesUsed: number; wins: number }>);
  }, [filteredRuns]);

  const orbData = useMemo(() => {
    return Object.entries(orbStats)
      .sort((a, b) => b[1].timesUsed - a[1].timesUsed)
      .slice(0, 10)
      .map(([name, stats]) => ({
        orbName: name,
        timesUsed: stats.timesUsed,
        winRate: stats.timesUsed > 0 ? (stats.wins / stats.timesUsed) * 100 : 0,
      }));
  }, [orbStats]);

  const runsByMonth = useMemo(() => {
    return filteredRuns.reduce((acc, run) => {
      const month = new Date(run.timestamp).toISOString().slice(0, 7);
      acc[month] = (acc[month] || 0) + 1;
      return acc;
    }, {} as Record<string, number>);
  }, [filteredRuns]);

  if (!runHistoryData) {
    return (
      <Box>
        <Typography variant="h4" gutterBottom>
          Statistics
        </Typography>
        <Alert severity="info">
          No data available. Please upload a save file to see your statistics.
        </Alert>
      </Box>
    );
  }

  const monthlyData = Object.entries(runsByMonth)
    .sort()
    .map(([month, count]) => ({ month, count }));

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Statistics
      </Typography>
      <Grid container spacing={3}>
        {/* Class Statistics */}
        <Grid
          size={{
            xs: 12,
            md: 6
          }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              Win Rate by Character Class
            </Typography>
            {classData.length > 0 && (
              <Box sx={{ display: "flex", justifyContent: "center" }}>
                <BarChart
                  xAxis={[
                    {
                      scaleType: "band",
                      data: classData.map((d) => d.className),
                      tickLabelStyle: { angle: -45, textAnchor: "end" },
                    },
                  ]}
                  series={[
                    {
                      data: classData.map((d) => d.winRate),
                      label: "Win Rate (%)",
                      color: "#1976d2",
                    },
                  ]}
                  width={500}
                  height={350}
                  margin={{ top: 40, bottom: 80, left: 40, right: 40 }}
                />
              </Box>
            )}
          </Paper>
        </Grid>

        <Grid
          size={{
            xs: 12,
            md: 6
          }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              Total Runs by Class
            </Typography>
            {classData.length > 0 && (
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
                      data: classData.map((d) => ({
                        label: d.className,
                        value: d.totalRuns,
                      })),
                      innerRadius: 30,
                      outerRadius: 100,
                      paddingAngle: 2,
                      cornerRadius: 5,
                    },
                  ]}
                  width={500}
                  height={350}
                  slotProps={{
                    legend: {
                      position: { vertical: "bottom", horizontal: "center" },
                    },
                  }}
                  margin={{ top: 40, bottom: 80, left: 80, right: 80 }}
                />
              </Box>
            )}
          </Paper>
        </Grid>

        {/* Orb Statistics */}
        <Grid
          size={{
            xs: 12,
            md: 6
          }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              Most Used Orbs
            </Typography>
            {orbData.length > 0 && (
              <Box sx={{ display: "flex", justifyContent: "center" }}>
                <BarChart
                  xAxis={[
                    {
                      scaleType: "band",
                      data: orbData.map((d) => d.orbName),
                      tickLabelStyle: { angle: -45, textAnchor: "end" },
                    },
                  ]}
                  series={[
                    {
                      data: orbData.map((d) => d.timesUsed),
                      label: "Times Used",
                      color: "#ed6c02",
                    },
                  ]}
                  width={500}
                  height={350}
                  margin={{ top: 40, bottom: 80, left: 40, right: 40 }}
                />
              </Box>
            )}
          </Paper>
        </Grid>

        <Grid
          size={{
            xs: 12,
            md: 6
          }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              Orb Win Rates (Top 10)
            </Typography>
            {orbData.length > 0 && (
              <Box sx={{ display: "flex", justifyContent: "center" }}>
                <BarChart
                  xAxis={[
                    {
                      scaleType: "band",
                      data: orbData.map((d) => d.orbName),
                      tickLabelStyle: { angle: -45, textAnchor: "end" },
                    },
                  ]}
                  series={[
                    {
                      data: orbData.map((d) => d.winRate),
                      label: "Win Rate (%)",
                      color: "#2e7d32",
                    },
                  ]}
                  width={500}
                  height={350}
                  margin={{ top: 40, bottom: 80, left: 40, right: 40 }}
                />
              </Box>
            )}
          </Paper>
        </Grid>

        {/* Activity Over Time */}
        <Grid size={12}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              Runs Over Time
            </Typography>
            {monthlyData.length > 0 && (
              <Box sx={{ display: "flex", justifyContent: "center" }}>
                <LineChart
                  xAxis={[
                    {
                      scaleType: "point",
                      data: monthlyData.map((d) => d.month),
                      tickLabelStyle: { angle: -45, textAnchor: "end" },
                    },
                  ]}
                  series={[
                    {
                      data: monthlyData.map((d) => d.count),
                      label: "Runs per Month",
                      color: "#9c27b0",
                    },
                  ]}
                  width={900}
                  height={350}
                  margin={{ top: 40, bottom: 80, left: 60, right: 40 }}
                />
              </Box>
            )}
          </Paper>
        </Grid>

        {/* Summary Cards */}
        {classData.map((cls) => (
          <Grid
            key={cls.className}
            size={{
              xs: 12,
              sm: 6,
              md: 4
            }}>
            <Card>
              <CardContent>
                <Typography variant="h6" gutterBottom>
                  {cls.className}
                </Typography>
                <Typography variant="body2" color="textSecondary">
                  Total Runs: {cls.totalRuns}
                </Typography>
                <Typography variant="body2" color="textSecondary">
                  Win Rate: {cls.winRate.toFixed(1)}%
                </Typography>
                <Typography variant="body2" color="textSecondary">
                  Avg Damage: {cls.averageDamage.toLocaleString()}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Box>
  );
};

export default Statistics;

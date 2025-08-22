import React from "react";
import {
  Paper,
  Typography,
  Box,
  FormControlLabel,
  Switch,
  Grid,
  Card,
  CardContent,
  Divider,
  Alert,
  Button,
} from "@mui/material";
import { useAppConfig, useAppActions } from "../store/useAppStore";

const Config: React.FC = () => {
  const config = useAppConfig();
  const { updateConfig, refresh } = useAppActions();

  const handleConfigChange = async (key: string, value: boolean) => {
    await updateConfig({ [key]: value });
    // Refresh data when configuration changes
    await refresh();
  };

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Configuration
      </Typography>
      <Grid container spacing={3}>
        {/* Data Filtering */}
        <Grid
          size={{
            xs: 12,
            md: 8
          }}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Data Filtering
            </Typography>
            <Typography variant="body2" color="textSecondary" sx={{ mb: 2 }}>
              Configure how your run data is processed and displayed.
            </Typography>

            <FormControlLabel
              control={
                <Switch
                  checked={config.excludeCustomRuns}
                  onChange={(e) =>
                    handleConfigChange("excludeCustomRuns", e.target.checked)
                  }
                />
              }
              label="Exclude Custom Runs"
            />
            <Typography
              variant="body2"
              color="textSecondary"
              sx={{ mt: 1, mb: 2 }}
            >
              When enabled, runs with custom seeds or modified game settings
              will be excluded from statistics and analysis. This provides a
              more accurate representation of standard gameplay performance.
            </Typography>

            <Divider sx={{ my: 2 }} />

            <FormControlLabel
              control={
                <Switch
                  checked={config.excludeTestRuns}
                  onChange={(e) =>
                    handleConfigChange("excludeTestRuns", e.target.checked)
                  }
                />
              }
              label="Exclude Test Runs"
            />
            <Typography
              variant="body2"
              color="textSecondary"
              sx={{ mt: 1, mb: 2 }}
            >
              Filter out very short runs (less than 5 minutes) that might be
              test runs or quick exits.
            </Typography>

            <Divider sx={{ my: 2 }} />

            <FormControlLabel
              control={
                <Switch
                  checked={config.excludeIncompletRuns}
                  onChange={(e) =>
                    handleConfigChange("excludeIncompletRuns", e.target.checked)
                  }
                />
              }
              label="Exclude Incomplete Runs"
            />
            <Typography
              variant="body2"
              color="textSecondary"
              sx={{ mt: 1, mb: 2 }}
            >
              Filter out runs that ended before reaching the first boss (level
              1-0).
            </Typography>
          </Paper>
        </Grid>

        {/* Current Settings Summary */}
        <Grid
          size={{
            xs: 12,
            md: 4
          }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Current Settings
              </Typography>

              <Box sx={{ mb: 2 }}>
                <Typography variant="body2" fontWeight="bold">
                  Custom Runs:{" "}
                  {config.excludeCustomRuns ? "Excluded" : "Included"}
                </Typography>
                <Typography variant="body2" color="textSecondary">
                  {config.excludeCustomRuns
                    ? "Only standard gameplay runs are shown"
                    : "All runs including custom seeds are shown"}
                </Typography>
              </Box>

              <Box sx={{ mb: 2 }}>
                <Typography variant="body2" fontWeight="bold">
                  Test Runs: {config.excludeTestRuns ? "Excluded" : "Included"}
                </Typography>
                <Typography variant="body2" color="textSecondary">
                  {config.excludeTestRuns
                    ? "Short test runs are filtered out"
                    : "All runs regardless of duration"}
                </Typography>
              </Box>

              <Box sx={{ mb: 2 }}>
                <Typography variant="body2" fontWeight="bold">
                  Incomplete Runs:{" "}
                  {config.excludeIncompletRuns ? "Excluded" : "Included"}
                </Typography>
                <Typography variant="body2" color="textSecondary">
                  {config.excludeIncompletRuns
                    ? "Only runs that reached the first boss"
                    : "All runs including early exits"}
                </Typography>
              </Box>

              <Button
                variant="outlined"
                fullWidth
                onClick={refresh}
                sx={{ mt: 2 }}
              >
                Refresh Data
              </Button>
            </CardContent>
          </Card>
        </Grid>

        {/* Information */}
        <Grid size={12}>
          <Alert severity="info">
            <Typography variant="body2">
              Configuration changes will automatically refresh your data and
              update all statistics, charts, and run lists. These settings are
              saved in your browser and will persist between sessions.
            </Typography>
          </Alert>
        </Grid>
      </Grid>
    </Box>
  );
};

export default Config;

import React, { useState, useMemo, useEffect, useCallback } from "react";
import {
  Paper,
  Typography,
  Box,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Chip,
  Grid,
  FormControlLabel,
  Checkbox,
  CircularProgress,
  Alert,
} from "@mui/material";
import { DataGrid, GridColDef } from "@mui/x-data-grid";
import { useNavigate } from "react-router-dom";
import { RunRecord } from "../types";
import {
  useRunHistoryData,
  useAppActions,
  useRunsAndConfig,
} from "../store/useAppStore";

const RunList: React.FC = () => {
  const navigate = useNavigate();
  const runHistoryData = useRunHistoryData();
  const { runs, excludeCustomRuns } = useRunsAndConfig();
  const { getFilteredRuns } = useAppActions();

  const [filters, setFilters] = useState({
    characterClass: "",
    won: null as boolean | null,
    startDate: null as Date | null,
    endDate: null as Date | null,
    minDamage: "",
    maxDamage: "",
    minDuration: "",
    maxDuration: "",
  });

  const [apiFilteredRuns, setApiFilteredRuns] = useState<RunRecord[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [totalCount, setTotalCount] = useState(0);

  // Create a stable reference for the filter function
  const applyFilters = useCallback(
    async (currentFilters: typeof filters) => {
      try {
        setLoading(true);
        setError(null);

        const apiFilters: Record<string, string | number | boolean> = {};
        if (currentFilters.characterClass)
          apiFilters.characterClass = currentFilters.characterClass;
        if (currentFilters.won !== null) apiFilters.won = currentFilters.won;
        if (currentFilters.startDate)
          apiFilters.startDate = currentFilters.startDate.toISOString();
        if (currentFilters.endDate)
          apiFilters.endDate = currentFilters.endDate.toISOString();
        if (currentFilters.minDamage)
          apiFilters.minDamage = parseInt(currentFilters.minDamage);
        if (currentFilters.maxDamage)
          apiFilters.maxDamage = parseInt(currentFilters.maxDamage);
        if (currentFilters.minDuration)
          apiFilters.minDuration = parseFloat(currentFilters.minDuration);
        if (currentFilters.maxDuration)
          apiFilters.maxDuration = parseFloat(currentFilters.maxDuration);

        const result = await getFilteredRuns(apiFilters);
        setApiFilteredRuns(result.runs);
        setTotalCount(result.totalCount);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to filter runs");
        // Fallback to original runs if API fails
        if (runHistoryData) {
          setApiFilteredRuns(runHistoryData.runs);
          setTotalCount(runHistoryData.runs.length);
        }
      } finally {
        setLoading(false);
      }
    },
    [getFilteredRuns, runHistoryData]
  );

  const characterClasses = useMemo(() => {
    const filteredRuns = excludeCustomRuns
      ? runs.filter((run) => !run.isCustomRun)
      : runs;
    const classes = new Set(filteredRuns.map((run) => run.characterClass));
    return Array.from(classes).sort();
  }, [runs, excludeCustomRuns]);

  // Apply filters via API when they change
  useEffect(() => {
    if (!runHistoryData) return;

    // Debounce the API call
    const timeoutId = setTimeout(() => applyFilters(filters), 300);
    return () => clearTimeout(timeoutId);
  }, [filters, runHistoryData, applyFilters]);

  // Initialize with all runs from the store
  useEffect(() => {
    if (runHistoryData) {
      setApiFilteredRuns(runHistoryData.runs);
      setTotalCount(runHistoryData.runs.length);
    }
  }, [runHistoryData]);

  if (!runHistoryData) {
    return (
      <Box>
        <Typography variant="h4" gutterBottom>
          Run History
        </Typography>
        <Alert severity="info">
          No data available. Please upload a save file to see your run history.
        </Alert>
      </Box>
    );
  }

  const columns: GridColDef[] = [
    {
      field: "timestamp",
      headerName: "Date",
      width: 130,
      valueFormatter: (value) => new Date(value).toLocaleDateString(),
    },
    {
      field: "characterClass",
      headerName: "Class",
      width: 120,
    },
    {
      field: "won",
      headerName: "Result",
      width: 100,
      renderCell: (params) => (
        <Chip
          label={params.value ? "Victory" : "Defeat"}
          color={params.value ? "success" : "error"}
          size="small"
        />
      ),
    },
    {
      field: "damageDealt",
      headerName: "Damage",
      width: 120,
      type: "number",
      valueFormatter: (value: number) => {
        if (value >= 1000000) return `${(value / 1000000).toFixed(1)}M`;
        if (value >= 1000) return `${(value / 1000).toFixed(1)}K`;
        return value.toString();
      },
    },
    {
      field: "duration",
      headerName: "Duration",
      width: 100,
      valueFormatter: (value: string | number) => {
        let totalSeconds: number;

        if (typeof value === "string") {
          // Parse TimeSpan format like "00:06:43.2200000"
          const timeMatch = value.match(/^(\d+):(\d+):(\d+)(?:\.(\d+))?$/);
          if (timeMatch) {
            const [, hours, minutes, seconds] = timeMatch;
            totalSeconds =
              parseInt(hours) * 3600 +
              parseInt(minutes) * 60 +
              parseInt(seconds);
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
      },
    },
    {
      field: "finalLevel",
      headerName: "Level",
      width: 80,
      type: "number",
    },
    {
      field: "cruciballLevel",
      headerName: "Cruciball",
      width: 100,
      type: "number",
    },
    {
      field: "coinsEarned",
      headerName: "Coins",
      width: 100,
      type: "number",
    },
    {
      field: "pegsHit",
      headerName: "Pegs Hit",
      width: 100,
      type: "number",
    },
  ];

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Run History ({apiFilteredRuns.length} runs)
      </Typography>
      {/* Filters */}
      <Paper sx={{ p: 2, mb: 2 }}>
        <Typography variant="h6" gutterBottom>
          Filters
        </Typography>
        <Grid container spacing={2}>
          <Grid
            size={{
              xs: 12,
              sm: 6,
              md: 3
            }}>
            <FormControl fullWidth>
              <InputLabel>Character Class</InputLabel>
              <Select
                value={filters.characterClass}
                label="Character Class"
                onChange={(e) =>
                  setFilters((prev) => ({
                    ...prev,
                    characterClass: e.target.value,
                  }))
                }
              >
                <MenuItem value="">All Classes</MenuItem>
                {characterClasses.map((cls) => (
                  <MenuItem key={cls} value={cls}>
                    {cls}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Grid>

          <Grid
            size={{
              xs: 12,
              sm: 6,
              md: 3
            }}>
            <FormControlLabel
              control={
                <Checkbox
                  checked={filters.won === true}
                  onChange={(e) =>
                    setFilters((prev) => ({
                      ...prev,
                      won: e.target.checked ? true : null,
                    }))
                  }
                />
              }
              label="Wins Only"
            />
          </Grid>

          <Grid
            size={{
              xs: 12,
              sm: 6,
              md: 3
            }}>
            <TextField
              fullWidth
              label="Start Date"
              type="date"
              value={
                filters.startDate
                  ? filters.startDate.toISOString().split("T")[0]
                  : ""
              }
              onChange={(e) =>
                setFilters((prev) => ({
                  ...prev,
                  startDate: e.target.value ? new Date(e.target.value) : null,
                }))
              }
              InputLabelProps={{ shrink: true }}
            />
          </Grid>

          <Grid
            size={{
              xs: 12,
              sm: 6,
              md: 3
            }}>
            <TextField
              fullWidth
              label="End Date"
              type="date"
              value={
                filters.endDate
                  ? filters.endDate.toISOString().split("T")[0]
                  : ""
              }
              onChange={(e) =>
                setFilters((prev) => ({
                  ...prev,
                  endDate: e.target.value ? new Date(e.target.value) : null,
                }))
              }
              InputLabelProps={{ shrink: true }}
            />
          </Grid>

          <Grid
            size={{
              xs: 12,
              sm: 6,
              md: 3
            }}>
            <TextField
              fullWidth
              label="Min Damage"
              type="number"
              value={filters.minDamage}
              onChange={(e) =>
                setFilters((prev) => ({ ...prev, minDamage: e.target.value }))
              }
            />
          </Grid>

          <Grid
            size={{
              xs: 12,
              sm: 6,
              md: 3
            }}>
            <TextField
              fullWidth
              label="Max Damage"
              type="number"
              value={filters.maxDamage}
              onChange={(e) =>
                setFilters((prev) => ({ ...prev, maxDamage: e.target.value }))
              }
            />
          </Grid>

          <Grid
            size={{
              xs: 12,
              sm: 6,
              md: 3
            }}>
            <TextField
              fullWidth
              label="Min Duration (minutes)"
              type="number"
              value={filters.minDuration}
              onChange={(e) =>
                setFilters((prev) => ({ ...prev, minDuration: e.target.value }))
              }
            />
          </Grid>

          <Grid
            size={{
              xs: 12,
              sm: 6,
              md: 3
            }}>
            <TextField
              fullWidth
              label="Max Duration (minutes)"
              type="number"
              value={filters.maxDuration}
              onChange={(e) =>
                setFilters((prev) => ({ ...prev, maxDuration: e.target.value }))
              }
            />
          </Grid>
        </Grid>

        {loading && (
          <Box sx={{ display: "flex", justifyContent: "center", mt: 2 }}>
            <CircularProgress size={24} />
            <Typography variant="body2" sx={{ ml: 1 }}>
              Filtering {totalCount} runs...
            </Typography>
          </Box>
        )}

        {error && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {error}
          </Alert>
        )}
      </Paper>
      {/* Data Grid */}
      <Paper sx={{ width: "100%", minHeight: 400 }}>
        <DataGrid
          rows={apiFilteredRuns.map((run, index) => ({ ...run, id: run.id || `${run.timestamp}-${index}` }))}
          columns={columns}
          rowCount={totalCount}
          loading={loading}
          initialState={{
            pagination: {
              paginationModel: { pageSize: 25 },
            },
          }}
          pageSizeOptions={[25, 50, 100]}
          disableRowSelectionOnClick
          onRowClick={(params) => navigate(`/runs/${encodeURIComponent(params.id)}`)}
          sx={{ 
            cursor: "pointer",
            '& .MuiDataGrid-root': {
              border: 'none',
            },
            '& .MuiDataGrid-cell': {
              borderBottom: '1px solid rgba(224, 224, 224, 1)',
            },
            '& .MuiDataGrid-columnHeaders': {
              backgroundColor: 'rgba(0, 0, 0, 0.04)',
            },
          }}
          autoHeight
        />
      </Paper>
    </Box>
  );
};

export default RunList;

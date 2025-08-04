import React from "react";
import { useParams } from "react-router-dom";
import {
  Paper,
  Typography,
  Box,
  Grid,
  Card,
  CardContent,
  Chip,
  List,
  ListItem,
  ListItemText,
} from "@mui/material";

const RunDetail: React.FC = () => {
  const { id } = useParams<{ id: string }>();

  // TODO: Fetch run details from API using the id
  // For now, this is a placeholder

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Run Details
      </Typography>

      <Paper sx={{ p: 2 }}>
        <Typography variant="body1">Run ID: {id}</Typography>
        <Typography variant="body2" color="textSecondary">
          Detailed run information will be displayed here once the API is
          implemented.
        </Typography>
      </Paper>
    </Box>
  );
};

export default RunDetail;

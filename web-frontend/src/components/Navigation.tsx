import React from "react";
import { Link, useLocation } from "react-router-dom";
import { Tabs, Tab, Box } from "@mui/material";
import {
  Dashboard as DashboardIcon,
  List as ListIcon,
  Analytics as AnalyticsIcon,
  Storage as StorageIcon,
  Upload as UploadIcon,
  Settings as SettingsIcon,
  Image as ImageIcon,
  Link as LinkIcon,
} from "@mui/icons-material";

const Navigation: React.FC = () => {
  const location = useLocation();

  const getCurrentTab = () => {
    if (location.pathname.startsWith("/runs")) {
      return 1; // Run History tab for both /runs and /runs/:id
    }
    switch (location.pathname) {
      case "/":
        return 0;
      case "/statistics":
        return 2;
      case "/save-data":
        return 3;
      case "/gallery":
        return 4;
      case "/entities":
        return 5;
      case "/config":
        return 6;
      case "/upload":
        return 7;
      default:
        return 0;
    }
  };

  return (
    <Box sx={{ borderBottom: 1, borderColor: "divider" }}>
      <Tabs value={getCurrentTab()} aria-label="navigation tabs">
        <Tab
          icon={<DashboardIcon />}
          label="Dashboard"
          component={Link}
          to="/"
        />
        <Tab
          icon={<ListIcon />}
          label="Run History"
          component={Link}
          to="/runs"
        />
        <Tab
          icon={<AnalyticsIcon />}
          label="Statistics"
          component={Link}
          to="/statistics"
        />
        <Tab
          icon={<StorageIcon />}
          label="Save Data"
          component={Link}
          to="/save-data"
        />
        <Tab
          icon={<ImageIcon />}
          label="Sprite Gallery"
          component={Link}
          to="/gallery"
        />
        <Tab
          icon={<LinkIcon />}
          label="Entity Browser"
          component={Link}
          to="/entities"
        />
        <Tab
          icon={<SettingsIcon />}
          label="Config"
          component={Link}
          to="/config"
        />
        <Tab
          icon={<UploadIcon />}
          label="Upload Save"
          component={Link}
          to="/upload"
        />
      </Tabs>
    </Box>
  );
};

export default Navigation;

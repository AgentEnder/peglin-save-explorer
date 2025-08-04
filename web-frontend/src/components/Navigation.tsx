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
} from "@mui/icons-material";

const Navigation: React.FC = () => {
  const location = useLocation();

  const getCurrentTab = () => {
    switch (location.pathname) {
      case "/":
        return 0;
      case "/runs":
        return 1;
      case "/statistics":
        return 2;
      case "/save-data":
        return 3;
      case "/config":
        return 4;
      case "/upload":
        return 5;
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

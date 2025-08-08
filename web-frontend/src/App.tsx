import React, { useEffect } from "react";
import { Routes, Route } from "react-router-dom";
import {
  AppBar,
  Toolbar,
  Typography,
  Container,
  Box,
  Alert,
  CircularProgress,
  Backdrop,
} from "@mui/material";
import Dashboard from "./components/Dashboard";
import RunList from "./components/RunList";
import RunDetail from "./components/RunDetail";
import Statistics from "./components/Statistics";
import SaveData from "./components/SaveData";
import ImageGallery from "./components/ImageGallery";
import Config from "./components/Config";
import FileUpload from "./components/FileUpload";
import EntitySpriteBrowser from "./components/EntitySpriteBrowser";
import Navigation from "./components/Navigation";
import { useAppStore } from "./store/useAppStore";
import { useSpriteActions } from "./store/useSpriteStore";

function App() {
  const { isLoading, error, isInitialized, hasData, initialize, clearError } =
    useAppStore();
  const { initialize: initializeSprites } = useSpriteActions();

  useEffect(() => {
    if (!isInitialized) {
      initialize();
    }
    // Initialize sprites store as well
    initializeSprites();
  }, [isInitialized, initialize, initializeSprites]);

  const handleUploadComplete = async () => {
    // Data will be automatically refreshed by the store action
  };

  if (!isInitialized || isLoading) {
    return (
      <Backdrop
        open={true}
        sx={{ color: "#fff", zIndex: (theme) => theme.zIndex.drawer + 1 }}
      >
        <CircularProgress color="inherit" />
        <Typography variant="h6" sx={{ ml: 2 }}>
          {!isInitialized ? "Initializing..." : "Loading..."}
        </Typography>
      </Backdrop>
    );
  }

  return (
    <Box sx={{ flexGrow: 1 }}>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
            Peglin Save Explorer
          </Typography>
        </Toolbar>
      </AppBar>

      <Navigation />

      <Container maxWidth="xl" sx={{ mt: 2, mb: 2 }}>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }} onClose={() => clearError()}>
            {error}
          </Alert>
        )}

        {!hasData() ? (
          <FileUpload onUploadComplete={handleUploadComplete} />
        ) : (
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/runs" element={<RunList />} />
            <Route path="/runs/:id" element={<RunDetail />} />
            <Route path="/statistics" element={<Statistics />} />
            <Route path="/save-data" element={<SaveData />} />
            <Route path="/gallery" element={<ImageGallery />} />
            <Route path="/entities" element={<EntitySpriteBrowser />} />
            <Route path="/config" element={<Config />} />
            <Route
              path="/upload"
              element={<FileUpload onUploadComplete={handleUploadComplete} />}
            />
          </Routes>
        )}
      </Container>
    </Box>
  );
}

export default App;

import React, { useState, useEffect } from "react";
import {
  Box,
  Typography,
  Grid,
  Card,
  CardMedia,
  CardContent,
  Chip,
  Tabs,
  Tab,
  Alert,
  CircularProgress,
  Dialog,
  DialogTitle,
  DialogContent,
  IconButton,
  Paper,
  Stack,
  Divider,
} from "@mui/material";
import {
  Close as CloseIcon,
  Image as ImageIcon,
  Animation as AnimationIcon,
  Info as InfoIcon,
} from "@mui/icons-material";

interface SpriteFrame {
  name: string;
  x: number;
  y: number;
  width: number;
  height: number;
  pivotX: number;
  pivotY: number;
  normalizedX: number;
  normalizedY: number;
  normalizedWidth: number;
  normalizedHeight: number;
}

interface Sprite {
  id: string;
  name: string;
  type: string;
  width: number;
  height: number;
  url: string;
  isAtlas: boolean;
  frameCount: number;
  extractedAt: string;
  frames?: SpriteFrame[];
}

interface SpritesResponse {
  success: boolean;
  data: {
    sprites: Sprite[];
    total: number;
    relicCount: number;
    enemyCount: number;
    atlasCount: number;
  };
}

interface AnimationData {
  atlasId: string;
  atlasName: string;
  atlasUrl: string;
  atlasWidth: number;
  atlasHeight: number;
  frameCount: number;
  frames: SpriteFrame[];
}

const ImageGallery: React.FC = () => {
  const [sprites, setSprites] = useState<Sprite[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedTab, setSelectedTab] = useState(0);
  const [selectedSprite, setSelectedSprite] = useState<Sprite | null>(null);
  const [animationData, setAnimationData] = useState<AnimationData | null>(
    null
  );
  const [dialogOpen, setDialogOpen] = useState(false);

  useEffect(() => {
    fetchSprites();
  }, []);

  const fetchSprites = async () => {
    try {
      setLoading(true);
      const response = await fetch("/api/sprites");
      const data: SpritesResponse = await response.json();

      if (data.success) {
        setSprites(data.data.sprites);
        setError(null);
      } else {
        setError("Failed to load sprites");
      }
    } catch (err) {
      setError("Error connecting to server");
      console.error("Error fetching sprites:", err);
    } finally {
      setLoading(false);
    }
  };

  const fetchAnimationData = async (sprite: Sprite) => {
    if (!sprite.isAtlas) return;

    try {
      const response = await fetch(
        `/api/sprites/${sprite.type}/${sprite.id}/frames`
      );
      const data = await response.json();

      if (data.success) {
        setAnimationData(data.data);
      }
    } catch (err) {
      console.error("Error fetching animation data:", err);
    }
  };

  const handleSpriteClick = async (sprite: Sprite) => {
    setSelectedSprite(sprite);
    setAnimationData(null);

    if (sprite.isAtlas) {
      await fetchAnimationData(sprite);
    }

    setDialogOpen(true);
  };

  const handleCloseDialog = () => {
    setDialogOpen(false);
    setSelectedSprite(null);
    setAnimationData(null);
  };

  const getFilteredSprites = () => {
    switch (selectedTab) {
      case 0: // All
        return sprites;
      case 1: // Relics
        return sprites.filter((s) => s.type === "relic");
      case 2: // Enemies
        return sprites.filter((s) => s.type === "enemy");
      case 3: // Atlases
        return sprites.filter((s) => s.isAtlas);
      default:
        return sprites;
    }
  };

  if (loading) {
    return (
      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        minHeight="400px"
      >
        <CircularProgress />
        <Typography variant="h6" sx={{ ml: 2 }}>
          Loading sprites...
        </Typography>
      </Box>
    );
  }

  if (error) {
    return (
      <Alert severity="error" sx={{ mb: 2 }}>
        {error}
      </Alert>
    );
  }

  const filteredSprites = getFilteredSprites();
  const relicCount = sprites.filter((s) => s.type === "relic").length;
  const enemyCount = sprites.filter((s) => s.type === "enemy").length;
  const atlasCount = sprites.filter((s) => s.isAtlas).length;

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Sprite Gallery
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 2 }}>
        Extracted sprites from Peglin game assets. Atlases contain multiple
        animation frames.
      </Typography>
      <Paper sx={{ mb: 3, p: 2 }}>
        <Stack direction="row" spacing={2} alignItems="center">
          <Chip
            icon={<ImageIcon />}
            label={`Total: ${sprites.length}`}
            color="primary"
          />
          <Chip label={`Relics: ${relicCount}`} color="secondary" />
          <Chip label={`Enemies: ${enemyCount}`} color="error" />
          <Chip
            icon={<AnimationIcon />}
            label={`Atlases: ${atlasCount}`}
            color="success"
          />
        </Stack>
      </Paper>
      <Box sx={{ borderBottom: 1, borderColor: "divider", mb: 3 }}>
        <Tabs
          value={selectedTab}
          onChange={(e, newValue) => setSelectedTab(newValue)}
        >
          <Tab label={`All (${sprites.length})`} />
          <Tab label={`Relics (${relicCount})`} />
          <Tab label={`Enemies (${enemyCount})`} />
          <Tab label={`Atlases (${atlasCount})`} />
        </Tabs>
      </Box>
      <Grid container spacing={2}>
        {filteredSprites.map((sprite) => (
          <Grid
            sx={{ imageRendering: "pixelated" }}
            key={sprite.id}
            size={{
              xs: 12,
              sm: 6,
              md: 4,
              lg: 3
            }}>
            <Card
              sx={{
                cursor: "pointer",
                transition: "transform 0.2s",
                "&:hover": {
                  transform: "scale(1.02)",
                },
              }}
              onClick={() => handleSpriteClick(sprite)}
            >
              {sprite.isAtlas && sprite.frameCount > 1 ? (
                <Box
                  sx={{
                    height: "200px",
                    backgroundImage: `url(${sprite.url})`,
                    backgroundRepeat: "no-repeat",
                    backgroundPosition: "center",
                    backgroundSize: "contain",
                    backgroundColor: "rgba(0,0,0,0.05)",
                    position: "relative",
                    "&::after": {
                      content: '"🎬"',
                      position: "absolute",
                      top: "8px",
                      right: "8px",
                      fontSize: "20px",
                      backgroundColor: "rgba(0,0,0,0.7)",
                      borderRadius: "50%",
                      width: "32px",
                      height: "32px",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                    },
                  }}
                  title={`Atlas with ${sprite.frameCount} animation frames`}
                />
              ) : (
                <CardMedia
                  component="img"
                  height="200"
                  image={sprite.url}
                  alt={sprite.name}
                  sx={{
                    objectFit: "contain",
                    backgroundColor: "rgba(0,0,0,0.05)",
                  }}
                  onError={(e) => {
                    const target = e.target as HTMLImageElement;
                    target.style.display = "none";
                  }}
                />
              )}
              <CardContent>
                <Typography variant="h6" noWrap title={sprite.name}>
                  {sprite.name}
                </Typography>
                <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                  <Chip
                    label={sprite.type}
                    size="small"
                    color={sprite.type === "relic" ? "secondary" : "error"}
                  />
                  {sprite.isAtlas && (
                    <Chip
                      icon={<AnimationIcon />}
                      label={`${sprite.frameCount} frames`}
                      size="small"
                      color="success"
                    />
                  )}
                </Stack>
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ mt: 1, display: "block" }}
                >
                  {sprite.width} × {sprite.height}px
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
      {filteredSprites.length === 0 && (
        <Box textAlign="center" py={4}>
          <Typography variant="h6" color="text.secondary">
            No sprites found
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Run the sprite extraction command to generate sprite data.
          </Typography>
        </Box>
      )}
      {/* Sprite Detail Dialog */}
      <Dialog
        open={dialogOpen}
        onClose={handleCloseDialog}
        maxWidth="lg"
        fullWidth
      >
        {selectedSprite && (
          <>
            <DialogTitle>
              <Stack
                direction="row"
                justifyContent="space-between"
                alignItems="center"
              >
                <Typography variant="h6">{selectedSprite.name}</Typography>
                <IconButton onClick={handleCloseDialog}>
                  <CloseIcon />
                </IconButton>
              </Stack>
            </DialogTitle>
            <DialogContent>
              <Grid container spacing={3} sx={{ imageRendering: "pixelated" }}>
                <Grid
                  size={{
                    xs: 12,
                    md: 6
                  }}>
                  <Box
                    component="img"
                    src={selectedSprite.url}
                    alt={selectedSprite.name}
                    sx={{
                      width: "100%",
                      height: "auto",
                      maxHeight: "400px",
                      objectFit: "contain",
                      backgroundColor: "rgba(0,0,0,0.05)",
                      borderRadius: 1,
                    }}
                  />
                </Grid>
                <Grid
                  size={{
                    xs: 12,
                    md: 6
                  }}>
                  <Stack spacing={2}>
                    <Box>
                      <Typography variant="h6" gutterBottom>
                        <InfoIcon sx={{ mr: 1, verticalAlign: "middle" }} />
                        Sprite Information
                      </Typography>
                      <Typography variant="body2">
                        <strong>ID:</strong> {selectedSprite.id}
                      </Typography>
                      <Typography variant="body2">
                        <strong>Type:</strong> {selectedSprite.type}
                      </Typography>
                      <Typography variant="body2">
                        <strong>Dimensions:</strong> {selectedSprite.width} ×{" "}
                        {selectedSprite.height}px
                      </Typography>
                      <Typography variant="body2">
                        <strong>Is Atlas:</strong>{" "}
                        {selectedSprite.isAtlas ? "Yes" : "No"}
                      </Typography>
                      {selectedSprite.isAtlas && (
                        <Typography variant="body2">
                          <strong>Frame Count:</strong>{" "}
                          {selectedSprite.frameCount}
                        </Typography>
                      )}
                      <Typography variant="body2">
                        <strong>Extracted:</strong>{" "}
                        {new Date(selectedSprite.extractedAt).toLocaleString()}
                      </Typography>
                    </Box>

                    {animationData && (
                      <Box>
                        <Divider sx={{ my: 2 }} />
                        <Typography variant="h6" gutterBottom>
                          <AnimationIcon
                            sx={{ mr: 1, verticalAlign: "middle" }}
                          />
                          Animation Frames ({animationData.frameCount})
                        </Typography>
                        <Box sx={{ maxHeight: "400px", overflow: "auto" }}>
                          <Grid container spacing={1}>
                            {animationData.frames.map((frame, index) => (
                              <Grid
                                key={index}
                                size={{
                                  xs: 12,
                                  sm: 6,
                                  md: 4
                                }}>
                                <Paper sx={{ p: 1, textAlign: "center" }}>
                                  <Box
                                    sx={{
                                      width: `${frame.width}px`,
                                      height: `${frame.height}px`,
                                      backgroundImage: `url(${animationData.atlasUrl})`,
                                      backgroundPosition: `-${frame.x}px -${
                                        animationData.atlasHeight -
                                        frame.y -
                                        frame.height
                                      }px`,
                                      backgroundRepeat: "no-repeat",
                                      backgroundSize: `${animationData.atlasWidth}px ${animationData.atlasHeight}px`,
                                      border: "1px solid #ddd",
                                      borderRadius: "4px",
                                      margin: "0 auto 8px auto",
                                      backgroundColor: "rgba(0,0,0,0.05)",
                                      imageRendering: "pixelated", // Better for pixel art
                                      // Scale up small sprites for better visibility
                                      transform:
                                        frame.width < 32
                                          ? "scale(2)"
                                          : "scale(1)",
                                      transformOrigin: "center",
                                    }}
                                    title={`${frame.name} - ${frame.width}×${frame.height}`}
                                  />
                                  <Typography
                                    variant="caption"
                                    display="block"
                                    sx={{ fontWeight: "bold", mb: 0.5 }}
                                  >
                                    {frame.name}
                                  </Typography>
                                  <Typography
                                    variant="caption"
                                    color="text.secondary"
                                    display="block"
                                  >
                                    {frame.width} × {frame.height}
                                  </Typography>
                                  <Typography
                                    variant="caption"
                                    color="text.secondary"
                                    display="block"
                                  >
                                    ({frame.x}, {frame.y})
                                  </Typography>
                                </Paper>
                              </Grid>
                            ))}
                          </Grid>
                        </Box>
                      </Box>
                    )}
                  </Stack>
                </Grid>
              </Grid>
            </DialogContent>
          </>
        )}
      </Dialog>
    </Box>
  );
};

export default ImageGallery;

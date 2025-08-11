import React, { useState, useMemo } from "react";
import {
  Box,
  IconButton,
  Typography,
  Card,
  CardContent,
  BoxProps,
} from "@mui/material";
import { 
  ChevronLeft, 
  ChevronRight, 
  PlayArrow, 
  Pause 
} from "@mui/icons-material";
import { Sprite } from "../store/useSpriteStore";
import SpriteRenderer from "./SpriteRenderer";

interface AnimatedSpriteViewerProps extends Omit<BoxProps, 'component'> {
  sprite: Sprite;
  size?: number | string;
  showControls?: boolean;
  showFrameInfo?: boolean;
  autoPlay?: boolean;
  frameRate?: number; // frames per second for auto-play
}

/**
 * AnimatedSpriteViewer component that displays sprites with frame navigation controls
 * for animated sprites and single frame display for static sprites.
 */
const AnimatedSpriteViewer: React.FC<AnimatedSpriteViewerProps> = ({
  sprite,
  size = 64,
  showControls = true,
  showFrameInfo = true,
  autoPlay = false,
  frameRate = 2,
  sx,
  ...boxProps
}) => {
  const [currentFrame, setCurrentFrame] = useState(0);
  const [isPlaying, setIsPlaying] = useState(autoPlay);

  // Calculate frame information
  const frameInfo = useMemo(() => {
    // Check if this sprite has explicit frame count (for sprite sheets)
    const hasFrameCount = sprite.frameCount && sprite.frameCount > 1;
    
    if (!hasFrameCount) {
      // Static sprite or single frame
      return {
        isAnimated: false,
        totalFrames: 1,
        frameWidth: sprite.width,
        frameHeight: sprite.height,
        frames: [{ x: 0, y: 0, width: sprite.width, height: sprite.height }]
      };
    }

    // This is an animated sprite sheet - calculate frame layout
    const totalFrames = sprite.frameCount;
    
    // For sprite sheets, calculate individual frame dimensions
    // Common layouts: horizontal strip, square grid, or 2x3 grid
    let framesX: number, framesY: number;
    
    if (totalFrames <= 6) {
      // For small frame counts, try common layouts
      if (totalFrames <= 3) {
        // Horizontal strip for 2-3 frames
        framesX = totalFrames;
        framesY = 1;
      } else if (totalFrames === 4) {
        // 2x2 grid
        framesX = 2;
        framesY = 2;
      } else if (totalFrames === 5 || totalFrames === 6) {
        // 2x3 grid (as you mentioned)
        framesX = 2;
        framesY = 3;
      } else {
        framesX = totalFrames;
        framesY = 1;
      }
    } else {
      // For larger frame counts, try to make a square-ish grid
      framesX = Math.ceil(Math.sqrt(totalFrames));
      framesY = Math.ceil(totalFrames / framesX);
    }
    
    // Calculate individual frame dimensions
    const frameWidth = Math.floor(sprite.width / framesX);
    const frameHeight = Math.floor(sprite.height / framesY);
    
    // Generate frame positions (reading left-to-right, top-to-bottom)
    const frames = [];
    for (let i = 0; i < totalFrames; i++) {
      const x = (i % framesX) * frameWidth;
      const y = Math.floor(i / framesX) * frameHeight;
      frames.push({
        x,
        y,
        width: frameWidth,
        height: frameHeight
      });
    }

    return {
      isAnimated: true,
      totalFrames,
      frameWidth,
      frameHeight,
      frames
    };
  }, [sprite]);

  // Auto-play functionality
  React.useEffect(() => {
    if (!isPlaying || !frameInfo.isAnimated) return;

    const interval = setInterval(() => {
      setCurrentFrame((prev) => (prev + 1) % frameInfo.totalFrames);
    }, 1000 / frameRate);

    return () => clearInterval(interval);
  }, [isPlaying, frameInfo.isAnimated, frameInfo.totalFrames, frameRate]);

  // Navigation functions
  const goToPreviousFrame = () => {
    setCurrentFrame((prev) => 
      prev === 0 ? frameInfo.totalFrames - 1 : prev - 1
    );
  };

  const goToNextFrame = () => {
    setCurrentFrame((prev) => (prev + 1) % frameInfo.totalFrames);
  };

  const togglePlayPause = () => {
    setIsPlaying(!isPlaying);
  };

  // Create a modified sprite for the current frame
  const currentFrameSprite = useMemo(() => {
    if (!frameInfo.isAnimated) return sprite;

    const frame = frameInfo.frames[currentFrame];
    return {
      ...sprite,
      frameX: frame.x,
      frameY: frame.y,
      frameWidth: frame.width,
      frameHeight: frame.height
    };
  }, [sprite, frameInfo, currentFrame]);

  return (
    <Box
      sx={{
        display: "inline-block",
        textAlign: "center",
        ...sx
      }}
      {...boxProps}
    >
      <Card variant="outlined" sx={{ p: 1 }}>
        <CardContent sx={{ p: 1, "&:last-child": { pb: 1 } }}>
          {/* Sprite Display */}
          <Box sx={{ mb: frameInfo.isAnimated && showControls ? 1 : 0 }}>
            <SpriteRenderer
              sprite={currentFrameSprite}
              size={size}
              sx={{ mx: "auto", borderRadius: 1 }}
            />
          </Box>

          {/* Animation Controls */}
          {frameInfo.isAnimated && showControls && (
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 1 }}>
              <IconButton
                size="small"
                onClick={goToPreviousFrame}
                disabled={isPlaying}
                title="Previous frame"
              >
                <ChevronLeft />
              </IconButton>

              <IconButton
                size="small"
                onClick={togglePlayPause}
                title={isPlaying ? "Pause" : "Play"}
              >
                {isPlaying ? <Pause /> : <PlayArrow />}
              </IconButton>

              <IconButton
                size="small"
                onClick={goToNextFrame}
                disabled={isPlaying}
                title="Next frame"
              >
                <ChevronRight />
              </IconButton>
            </Box>
          )}

          {/* Frame Information */}
          {showFrameInfo && (
            <Box sx={{ mt: 1 }}>
              <Typography variant="caption" display="block">
                {sprite.name}
              </Typography>
              
              {frameInfo.isAnimated ? (
                <Typography
                  variant="caption"
                  color="text.secondary"
                  display="block"
                >
                  Frame {currentFrame + 1} of {frameInfo.totalFrames}
                </Typography>
              ) : (
                <Typography
                  variant="caption"
                  color="text.secondary"
                  display="block"
                >
                  {frameInfo.frameWidth}×{frameInfo.frameHeight}
                  {sprite.frameWidth !== undefined && sprite.frameHeight !== undefined
                    ? ` | Texture: ${sprite.width}×${sprite.height}`
                    : ""
                  }
                </Typography>
              )}
            </Box>
          )}
        </CardContent>
      </Card>
    </Box>
  );
};

export default AnimatedSpriteViewer;
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
  Pause,
} from "@mui/icons-material";
import { Sprite } from "../store/useSpriteStore";
import SpriteRenderer from "./SpriteRenderer";

interface AnimatedSpriteViewerProps extends Omit<BoxProps, "component"> {
  sprite: Sprite;
  size?: number | string;
  showControls?: boolean;
  showFrameInfo?: boolean;
  autoPlay?: boolean;
  frameRate?: number; // frames per second for auto-play
  skipEmptyFrames?: boolean; // whether to skip frames that contain only transparent pixels
}

/**
 * Utility function to check if a frame region contains only transparent pixels
 */
const isFrameEmpty = async (
  imageUrl: string,
  frameX: number,
  frameY: number,
  frameWidth: number,
  frameHeight: number
): Promise<boolean> => {
  return new Promise((resolve) => {
    const img = new Image();
    img.crossOrigin = "anonymous";

    img.onload = () => {
      // Create a temporary canvas to analyze the frame
      const canvas = document.createElement("canvas");
      const ctx = canvas.getContext("2d");
      if (!ctx) {
        resolve(false);
        return;
      }

      canvas.width = frameWidth;
      canvas.height = frameHeight;

      // Draw the frame region
      ctx.drawImage(
        img,
        frameX,
        frameY,
        frameWidth,
        frameHeight,
        0,
        0,
        frameWidth,
        frameHeight
      );

      // Get pixel data
      const imageData = ctx.getImageData(0, 0, frameWidth, frameHeight);
      const data = imageData.data;

      // Check if all pixels are transparent (alpha channel = 0)
      for (let i = 3; i < data.length; i += 4) {
        // Check every 4th value (alpha channel)
        if (data[i] > 0) {
          resolve(false); // Found a non-transparent pixel
          return;
        }
      }

      resolve(true); // All pixels are transparent
    };

    img.onerror = () => resolve(false);
    img.src = imageUrl;
  });
};

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
  skipEmptyFrames = true,
  sx,
  ...boxProps
}) => {
  const [currentFrame, setCurrentFrame] = useState(0);
  const [isPlaying, setIsPlaying] = useState(autoPlay);
  const [nonEmptyFrames, setNonEmptyFrames] = useState<number[]>([]);

  // Calculate frame information
  const frameInfo = useMemo(() => {
    // Check if this sprite has frame dimensions (indicating it's from a sprite sheet)
    const hasFrameDimensions =
      sprite.frameWidth &&
      sprite.frameHeight &&
      sprite.frameWidth > 0 &&
      sprite.frameHeight > 0;

    // If we have frame dimensions, calculate the frame count if not provided
    let calculatedFrameCount = sprite.frameCount || 1;

    if (
      hasFrameDimensions &&
      sprite.width &&
      sprite.height &&
      sprite.width > 0 &&
      sprite.height > 0
    ) {
      // Calculate how many frames fit in the total texture
      const framesX = Math.floor(sprite.width / sprite.frameWidth);
      const framesY = Math.floor(sprite.height / sprite.frameHeight);

      // Use explicit frameCount if provided, otherwise calculate from grid
      if (sprite.frameCount !== undefined && sprite.frameCount > 0) {
        calculatedFrameCount = sprite.frameCount;
      } else {
        calculatedFrameCount = Math.max(framesX * framesY, 1); // Ensure at least 1 frame
      }
    }

    // Check if this sprite has multiple frames (either explicit count or calculated)
    const hasMultipleFrames = calculatedFrameCount > 1;

    if (!hasMultipleFrames) {
      // Static sprite or single frame
      // Use reasonable defaults if dimensions are invalid
      const safeWidth = sprite.width && sprite.width > 0 ? sprite.width : 16;
      const safeHeight =
        sprite.height && sprite.height > 0 ? sprite.height : 16;

      return {
        isAnimated: false,
        totalFrames: 1,
        frameWidth: safeWidth,
        frameHeight: safeHeight,
        frames: [{ x: 0, y: 0, width: safeWidth, height: safeHeight }],
        validFrames: [0], // Always include the single frame
      };
    }

    // This is an animated sprite sheet - calculate frame layout
    const totalFrames = calculatedFrameCount;

    // For sprite sheets, calculate individual frame dimensions
    // Common layouts: horizontal strip, square grid, or 2x3 grid
    let framesX: number, framesY: number;

    // If we have frame dimensions, use them to calculate layout
    if (hasFrameDimensions && sprite.width && sprite.height) {
      framesX = Math.floor(sprite.width / sprite.frameWidth);
      framesY = Math.floor(sprite.height / sprite.frameHeight);
    } else {
      // Fall back to guessing layout based on total frame count
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
          // 2x3 or 3x2 grid
          framesX = totalFrames <= 4 ? 2 : 3;
          framesY = Math.ceil(totalFrames / framesX);
        } else {
          framesX = totalFrames;
          framesY = 1;
        }
      } else {
        // For larger frame counts, try to make a square-ish grid
        framesX = Math.ceil(Math.sqrt(totalFrames));
        framesY = Math.ceil(totalFrames / framesX);
      }
    }

    // Calculate individual frame dimensions
    // Use safe values to prevent division by zero or invalid calculations
    const frameWidth = hasFrameDimensions
      ? sprite.frameWidth
      : sprite.width && sprite.width > 0
      ? Math.floor(sprite.width / framesX)
      : 16;
    const frameHeight = hasFrameDimensions
      ? sprite.frameHeight
      : sprite.height && sprite.height > 0
      ? Math.floor(sprite.height / framesY)
      : 16;

    // Generate frame positions (reading left-to-right, top-to-bottom)
    const frames = [];
    for (let i = 0; i < totalFrames; i++) {
      const x = (i % framesX) * frameWidth;
      const y = Math.floor(i / framesX) * frameHeight;
      frames.push({
        x,
        y,
        width: frameWidth,
        height: frameHeight,
      });
    }

    return {
      isAnimated: true,
      totalFrames,
      frameWidth,
      frameHeight,
      frames,
      validFrames: Array.from({ length: totalFrames }, (_, i) => i), // Initialize with all frames, will be filtered later
    };
  }, [sprite]);

  // Effect to detect empty frames when skipEmptyFrames is enabled
  React.useEffect(() => {
    if (!skipEmptyFrames || !frameInfo.isAnimated) {
      setNonEmptyFrames(frameInfo.validFrames || []);
      return;
    }

    const detectEmptyFrames = async () => {
      const validFrameIndices: number[] = [];

      for (let i = 0; i < frameInfo.totalFrames; i++) {
        const frame = frameInfo.frames[i];
        const isEmpty = await isFrameEmpty(
          sprite.url,
          frame.x,
          frame.y,
          frame.width,
          frame.height
        );

        if (!isEmpty) {
          validFrameIndices.push(i);
        }
      }

      // Ensure we always have at least one frame
      if (validFrameIndices.length === 0) {
        validFrameIndices.push(0);
      }

      setNonEmptyFrames(validFrameIndices);

      // Reset current frame to first valid frame if current frame is empty
      setCurrentFrame((prevFrame) => {
        if (!validFrameIndices.includes(prevFrame)) {
          return validFrameIndices[0];
        }
        return prevFrame;
      });
    };

    detectEmptyFrames();
  }, [sprite.url, frameInfo, skipEmptyFrames]);

  // Get the frames we should actually display (either all frames or just non-empty ones)
  const displayFrames = useMemo(() => {
    return skipEmptyFrames ? nonEmptyFrames : frameInfo.validFrames || [];
  }, [skipEmptyFrames, nonEmptyFrames, frameInfo.validFrames]);

  const displayFrameCount = displayFrames.length;

  // Auto-play functionality - updated to use display frames
  React.useEffect(() => {
    if (!isPlaying || !frameInfo.isAnimated || displayFrameCount === 0) return;

    const interval = setInterval(() => {
      setCurrentFrame((prev) => {
        const currentDisplayIndex = displayFrames.indexOf(prev);
        const nextDisplayIndex = (currentDisplayIndex + 1) % displayFrameCount;
        return displayFrames[nextDisplayIndex];
      });
    }, 1000 / frameRate);

    return () => clearInterval(interval);
  }, [
    isPlaying,
    frameInfo.isAnimated,
    displayFrames,
    displayFrameCount,
    frameRate,
  ]);

  // Navigation functions - updated to use display frames
  const goToPreviousFrame = () => {
    setCurrentFrame((prev) => {
      const currentDisplayIndex = displayFrames.indexOf(prev);
      const prevDisplayIndex =
        currentDisplayIndex === 0
          ? displayFrameCount - 1
          : currentDisplayIndex - 1;
      return displayFrames[prevDisplayIndex];
    });
  };

  const goToNextFrame = () => {
    setCurrentFrame((prev) => {
      const currentDisplayIndex = displayFrames.indexOf(prev);
      const nextDisplayIndex = (currentDisplayIndex + 1) % displayFrameCount;
      return displayFrames[nextDisplayIndex];
    });
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
      frameHeight: frame.height,
    };
  }, [sprite, frameInfo, currentFrame]);

  return (
    <Box
      sx={{
        display: "inline-block",
        textAlign: "center",
        ...sx,
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
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                gap: 1,
              }}
            >
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
                <>
                  <Typography
                    variant="caption"
                    color="text.secondary"
                    display="block"
                  >
                    Frame {displayFrames.indexOf(currentFrame) + 1} of{" "}
                    {displayFrameCount}
                  </Typography>
                </>
              ) : (
                <Typography
                  variant="caption"
                  color="text.secondary"
                  display="block"
                >
                  {frameInfo.frameWidth}×{frameInfo.frameHeight}
                  {sprite.frameWidth !== undefined &&
                  sprite.frameHeight !== undefined
                    ? ` | Texture: ${sprite.width}×${sprite.height}`
                    : ""}
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

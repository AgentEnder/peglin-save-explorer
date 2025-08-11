import React from "react";
import { Box, BoxProps } from "@mui/material";
import { Sprite } from "../store/useSpriteStore";

interface SpriteRendererProps extends Omit<BoxProps, 'component'> {
  sprite: Sprite;
  size?: number | string;
  fallbackSrc?: string;
}

/**
 * SpriteRenderer component that properly displays sprites with frame clipping
 * when frame dimensions are available (for individual frames within larger textures).
 */
const SpriteRenderer: React.FC<SpriteRendererProps> = ({
  sprite,
  size = 64,
  fallbackSrc,
  sx,
  ...boxProps
}) => {
  const hasFrameData = sprite.frameWidth !== undefined && 
                      sprite.frameHeight !== undefined &&
                      sprite.frameX !== undefined &&
                      sprite.frameY !== undefined;

  if (!hasFrameData) {
    // If no frame data, display the full sprite
    return (
      <Box
        component="img"
        src={sprite.url}
        alt={sprite.name}
        sx={{
          width: size,
          height: size,
          objectFit: "contain",
          display: "inline-block",
          verticalAlign: "middle",
          ...sx,
        }}
        {...boxProps}
      />
    );
  }

  // Calculate scale to fit the frame within the desired size
  const frameWidth = sprite.frameWidth!;
  const frameHeight = sprite.frameHeight!;
  const frameX = sprite.frameX!;
  const frameY = sprite.frameY!;
  
  const aspectRatio = frameWidth / frameHeight;
  let displayWidth: number | string;
  let displayHeight: number | string;
  
  if (typeof size === 'number') {
    if (aspectRatio > 1) {
      // Wide frame - fit to width
      displayWidth = size;
      displayHeight = size / aspectRatio;
    } else {
      // Tall frame - fit to height
      displayHeight = size;
      displayWidth = size * aspectRatio;
    }
  } else {
    displayWidth = size;
    displayHeight = size;
  }

  // Calculate the scale factor to make the frame fit the desired size
  const scaleX = (typeof displayWidth === 'number' ? displayWidth : parseFloat(displayWidth)) / frameWidth;
  const scaleY = (typeof displayHeight === 'number' ? displayHeight : parseFloat(displayHeight)) / frameHeight;
  
  // Use the smaller scale to maintain aspect ratio
  const scale = Math.min(scaleX, scaleY);
  
  // Calculate the scaled full texture size
  const scaledTextureWidth = sprite.width * scale;
  const scaledTextureHeight = sprite.height * scale;
  
  // Calculate the position offset to show the correct frame
  const offsetX = -frameX * scale;
  const offsetY = -frameY * scale;

  return (
    <Box
      sx={{
        width: displayWidth,
        height: displayHeight,
        overflow: "hidden",
        display: "inline-block",
        verticalAlign: "middle",
        position: "relative",
        ...sx,
      }}
      {...boxProps}
    >
      <Box
        component="img"
        src={sprite.url}
        alt={sprite.name}
        onError={(e) => {
          if (fallbackSrc) {
            (e.target as HTMLImageElement).src = fallbackSrc;
          }
        }}
        sx={{
          width: scaledTextureWidth,
          height: scaledTextureHeight,
          position: "absolute",
          left: offsetX,
          top: offsetY,
          objectFit: "none",
          imageRendering: "pixelated", // For crisp pixel art
        }}
      />
    </Box>
  );
};

export default SpriteRenderer;
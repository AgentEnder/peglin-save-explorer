import React, { useRef, useEffect } from "react";
import { Box, BoxProps } from "@mui/material";
import { Sprite } from "../store/useSpriteStore";

interface SpriteRendererProps extends Omit<BoxProps, "component"> {
  sprite: Sprite;
  size?: number | string;
  fallbackSrc?: string;
}

/**
 * SpriteRenderer component that properly displays sprites with frame clipping
 * using canvas for precise pixel control.
 */
const SpriteRenderer: React.FC<SpriteRendererProps> = ({
  sprite,
  size = 64,
  fallbackSrc,
  sx,
  ...boxProps
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const imgRef = useRef<HTMLImageElement>(null);

  const hasFrameData =
    sprite.isAtlas && // Only use frame data if this is explicitly an atlas
    sprite.frameWidth !== undefined &&
    sprite.frameHeight !== undefined &&
    sprite.frameX !== undefined &&
    sprite.frameY !== undefined &&
    sprite.frameWidth > 0 &&
    sprite.frameHeight > 0 &&
    // Additional checks for valid frame data
    (sprite.frameWidth !== sprite.width ||
      sprite.frameHeight !== sprite.height ||
      sprite.frameX !== 0 ||
      sprite.frameY !== 0 ||
      sprite.frameCount > 1);

  // Calculate display dimensions
  const frameWidth =
    hasFrameData && sprite.frameWidth ? sprite.frameWidth : sprite.width || 16;
  const frameHeight =
    hasFrameData && sprite.frameHeight
      ? sprite.frameHeight
      : sprite.height || 16;

  const aspectRatio = frameWidth / frameHeight;
  let displayWidth: number;
  let displayHeight: number;

  if (typeof size === "number") {
    if (aspectRatio > 1) {
      displayWidth = size;
      displayHeight = size / aspectRatio;
    } else {
      displayHeight = size;
      displayWidth = size * aspectRatio;
    }
  } else {
    displayWidth = parseFloat(size);
    displayHeight = parseFloat(size);
  }

  const drawSprite = React.useCallback(() => {
    const canvas = canvasRef.current;
    const img = imgRef.current;

    if (!canvas || !img || !img.complete) return;

    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    // Set canvas size
    canvas.width = displayWidth;
    canvas.height = displayHeight;

    // Clear canvas
    ctx.clearRect(0, 0, displayWidth, displayHeight);

    // Set pixel-perfect rendering
    ctx.imageSmoothingEnabled = false;

    if (hasFrameData) {
      // Draw specific frame region
      const frameX = sprite.frameX || 0;
      const frameY = sprite.frameY || 0;
      const sourceWidth = sprite.frameWidth || frameWidth;
      const sourceHeight = sprite.frameHeight || frameHeight;

      console.log(
        `Drawing frame: frameX=${frameX}, frameY=${frameY}, sourceWidth=${sourceWidth}, sourceHeight=${sourceHeight} to ${displayWidth}x${displayHeight}`
      );

      // Draw the frame region to fill the entire canvas
      ctx.drawImage(
        img,
        frameX,
        frameY,
        sourceWidth,
        sourceHeight, // Source rectangle (frame in sprite sheet)
        0,
        0,
        displayWidth,
        displayHeight // Destination rectangle (entire canvas)
      );
    } else {
      console.log(`Drawing full image to ${displayWidth}x${displayHeight}`);
      // Draw entire image scaled to fit canvas
      ctx.drawImage(img, 0, 0, displayWidth, displayHeight);
    }
  }, [
    sprite.frameX,
    sprite.frameY,
    sprite.frameWidth,
    sprite.frameHeight,
    displayWidth,
    displayHeight,
    hasFrameData,
    frameWidth,
    frameHeight,
  ]);

  useEffect(() => {
    const img = new Image();

    const handleLoad = () => {
      imgRef.current = img;
      drawSprite();
    };

    const handleError = () => {
      if (fallbackSrc) {
        img.src = fallbackSrc;
      }
    };

    img.onload = handleLoad;
    img.onerror = handleError;
    img.src = sprite.url;

    // Cleanup function
    return () => {
      img.onload = null;
      img.onerror = null;
      imgRef.current = null;
    };
  }, [sprite.url, fallbackSrc, drawSprite]);

  // Redraw when frame data changes (but image is already loaded)
  useEffect(() => {
    if (imgRef.current && imgRef.current.complete) {
      drawSprite();
    }
  }, [
    sprite.frameX,
    sprite.frameY,
    sprite.frameWidth,
    sprite.frameHeight,
    drawSprite,
  ]);

  return (
    <Box
      sx={{
        display: "inline-block",
        verticalAlign: "middle",
        ...sx,
      }}
      {...boxProps}
    >
      <canvas
        ref={canvasRef}
        style={{
          width: displayWidth,
          height: displayHeight,
          imageRendering: "pixelated",
        }}
      />
    </Box>
  );
};

export default SpriteRenderer;

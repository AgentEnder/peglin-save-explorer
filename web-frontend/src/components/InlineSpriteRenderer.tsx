import React from "react";
import { Sprite } from "../store/useSpriteStore";

interface InlineSpriteRendererProps {
  sprite: Sprite;
  size?: number | string;
  title?: string;
}

/**
 * InlineSpriteRenderer component for inline text sprites with frame clipping
 */
const InlineSpriteRenderer: React.FC<InlineSpriteRendererProps> = ({
  sprite,
  size = 16,
  title,
}) => {
  const hasFrameData = sprite.frameWidth !== undefined && 
                      sprite.frameHeight !== undefined &&
                      sprite.frameX !== undefined &&
                      sprite.frameY !== undefined;

  if (!hasFrameData) {
    // If no frame data, display the full sprite
    return (
      <img
        src={sprite.url}
        alt={sprite.name}
        title={title || sprite.name}
        style={{
          width: typeof size === 'number' ? `${size}px` : size,
          height: typeof size === 'number' ? `${size}px` : size,
          display: "inline-block",
          verticalAlign: "middle",
          margin: "0 2px",
          objectFit: "contain",
          imageRendering: "pixelated",
        }}
      />
    );
  }

  // Calculate scale to fit the frame within the desired size
  const frameWidth = sprite.frameWidth!;
  const frameHeight = sprite.frameHeight!;
  const frameX = sprite.frameX!;
  const frameY = sprite.frameY!;
  
  const numericSize = typeof size === 'number' ? size : parseInt(size.toString().replace('px', ''));
  const aspectRatio = frameWidth / frameHeight;
  
  let displayWidth: number;
  let displayHeight: number;
  
  if (aspectRatio > 1) {
    // Wide frame - fit to width
    displayWidth = numericSize;
    displayHeight = numericSize / aspectRatio;
  } else {
    // Tall frame - fit to height
    displayHeight = numericSize;
    displayWidth = numericSize * aspectRatio;
  }

  // Calculate the scale factor to make the frame fit the desired size
  const scaleX = displayWidth / frameWidth;
  const scaleY = displayHeight / frameHeight;
  
  // Use the smaller scale to maintain aspect ratio
  const scale = Math.min(scaleX, scaleY);
  
  // Calculate the scaled full texture size
  const scaledTextureWidth = sprite.width * scale;
  const scaledTextureHeight = sprite.height * scale;
  
  // Calculate the position offset to show the correct frame
  const offsetX = -frameX * scale;
  const offsetY = -frameY * scale;

  return (
    <span
      title={title || sprite.name}
      style={{
        width: `${displayWidth}px`,
        height: `${displayHeight}px`,
        overflow: "hidden",
        display: "inline-block",
        verticalAlign: "middle",
        position: "relative",
        margin: "0 2px",
      }}
    >
      <img
        src={sprite.url}
        alt={sprite.name}
        style={{
          width: `${scaledTextureWidth}px`,
          height: `${scaledTextureHeight}px`,
          position: "absolute",
          left: `${offsetX}px`,
          top: `${offsetY}px`,
          imageRendering: "pixelated",
        }}
      />
    </span>
  );
};

export default InlineSpriteRenderer;
import { describe, test, expect } from "vitest";
import { Sprite } from "../../store/useSpriteStore";

// Mock sprite data based on the go_for_the_icircle example
const mockSprite: Sprite = {
  id: "sprite_-1262638818327671067",
  name: "go_for_the_icircle_0.png",
  type: "orb",
  url: "/sprites/orbs/go_for_the_icircle_0.png",
  width: 48,
  height: 32,
  frameWidth: 16,
  frameHeight: 16,
  frameX: 0,
  frameY: 0,
  isAtlas: true,
  frameCount: 6,
  extractedAt: "2025-01-01T00:00:00Z",
  sourceBundle: "test-bundle",
};

// Extract the frame calculation logic from AnimatedSpriteViewer
function calculateFrameInfo(sprite: Sprite) {
  const hasFrameDimensions =
    sprite.frameWidth &&
    sprite.frameHeight &&
    sprite.frameWidth > 0 &&
    sprite.frameHeight > 0;

  let calculatedFrameCount = sprite.frameCount || 1;

  if (
    hasFrameDimensions &&
    sprite.width &&
    sprite.height &&
    sprite.width > 0 &&
    sprite.height > 0
  ) {
    const framesX = Math.floor(sprite.width / sprite.frameWidth);
    const framesY = Math.floor(sprite.height / sprite.frameHeight);

    if (sprite.frameCount !== undefined && sprite.frameCount > 0) {
      calculatedFrameCount = sprite.frameCount;
    } else {
      calculatedFrameCount = Math.max(framesX * framesY, 1);
    }
  }

  const hasMultipleFrames = calculatedFrameCount > 1;

  if (!hasMultipleFrames) {
    const safeWidth = sprite.width && sprite.width > 0 ? sprite.width : 16;
    const safeHeight = sprite.height && sprite.height > 0 ? sprite.height : 16;

    return {
      isAnimated: false,
      totalFrames: 1,
      frameWidth: safeWidth,
      frameHeight: safeHeight,
      frames: [{ x: 0, y: 0, width: safeWidth, height: safeHeight }],
    };
  }

  // This is an animated sprite sheet - calculate frame layout
  const totalFrames = calculatedFrameCount;

  let framesX: number, framesY: number;

  // If we have frame dimensions, use them to calculate layout
  if (hasFrameDimensions && sprite.width && sprite.height) {
    framesX = Math.floor(sprite.width / sprite.frameWidth);
    framesY = Math.floor(sprite.height / sprite.frameHeight);
  } else {
    // Fall back to guessing layout based on total frame count
    if (totalFrames <= 6) {
      if (totalFrames <= 3) {
        framesX = totalFrames;
        framesY = 1;
      } else if (totalFrames === 4) {
        framesX = 2;
        framesY = 2;
      } else if (totalFrames === 5 || totalFrames === 6) {
        framesX = totalFrames <= 4 ? 2 : 3;
        framesY = Math.ceil(totalFrames / framesX);
      } else {
        framesX = totalFrames;
        framesY = 1;
      }
    } else {
      framesX = Math.ceil(Math.sqrt(totalFrames));
      framesY = Math.ceil(totalFrames / framesX);
    }
  }

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
    framesX,
    framesY,
  };
}

describe("AnimatedSpriteViewer Frame Calculations", () => {
  test("should calculate correct frame count for 3x2 grid", () => {
    const frameInfo = calculateFrameInfo(mockSprite);

    expect(frameInfo.isAnimated).toBe(true);
    expect(frameInfo.totalFrames).toBe(6);
    expect(frameInfo.framesX).toBe(3);
    expect(frameInfo.framesY).toBe(2);
  });

  test("should generate correct frame positions for 3x2 grid", () => {
    const frameInfo = calculateFrameInfo(mockSprite);

    expect(frameInfo.frames).toHaveLength(6);

    // Expected frame positions for a 3x2 grid with 16x16 frames:
    const expectedFrames = [
      { x: 0, y: 0, width: 16, height: 16 }, // Frame 0: top-left
      { x: 16, y: 0, width: 16, height: 16 }, // Frame 1: top-middle
      { x: 32, y: 0, width: 16, height: 16 }, // Frame 2: top-right
      { x: 0, y: 16, width: 16, height: 16 }, // Frame 3: bottom-left
      { x: 16, y: 16, width: 16, height: 16 }, // Frame 4: bottom-middle
      { x: 32, y: 16, width: 16, height: 16 }, // Frame 5: bottom-right
    ];

    frameInfo.frames.forEach((frame, index) => {
      console.log(
        `Generated Frame ${index}: x=${frame.x}, y=${frame.y}, width=${frame.width}, height=${frame.height}`
      );
      expect(frame).toEqual(expectedFrames[index]);
    });
  });

  test("should handle sprite with explicit frameCount of 5", () => {
    const spriteWith5Frames: Sprite = {
      ...mockSprite,
      frameCount: 5, // Only 5 frames, not 6
    };

    const frameInfo = calculateFrameInfo(spriteWith5Frames);

    expect(frameInfo.totalFrames).toBe(5);
    expect(frameInfo.frames).toHaveLength(5);

    // Should still use 3x2 grid layout but only generate 5 frames
    expect(frameInfo.framesX).toBe(3);
    expect(frameInfo.framesY).toBe(2);

    const expectedFrames = [
      { x: 0, y: 0, width: 16, height: 16 }, // Frame 0
      { x: 16, y: 0, width: 16, height: 16 }, // Frame 1
      { x: 32, y: 0, width: 16, height: 16 }, // Frame 2
      { x: 0, y: 16, width: 16, height: 16 }, // Frame 3
      { x: 16, y: 16, width: 16, height: 16 }, // Frame 4 (no frame 5)
    ];

    frameInfo.frames.forEach((frame, index) => {
      console.log(`5-Frame Sprite Frame ${index}: x=${frame.x}, y=${frame.y}`);
      expect(frame).toEqual(expectedFrames[index]);
    });
  });

  test("should handle sprite without explicit frameCount", () => {
    const spriteWithoutFrameCount: Sprite = {
      ...mockSprite,
      frameCount: 0, // No explicit frame count
    };

    const frameInfo = calculateFrameInfo(spriteWithoutFrameCount);

    // Should calculate 6 frames from 3x2 grid
    expect(frameInfo.totalFrames).toBe(6);
    expect(frameInfo.frames).toHaveLength(6);
  });
});

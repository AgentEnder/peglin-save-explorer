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

// Extract the calculation logic into testable functions
function hasFrameData(sprite: Sprite): boolean {
  return (
    sprite.frameWidth !== undefined &&
    sprite.frameHeight !== undefined &&
    sprite.frameX !== undefined &&
    sprite.frameY !== undefined &&
    sprite.frameWidth > 0 &&
    sprite.frameHeight > 0 &&
    (sprite.frameWidth !== sprite.width ||
      sprite.frameHeight !== sprite.height ||
      sprite.frameX !== 0 ||
      sprite.frameY !== 0 ||
      (sprite.width &&
        sprite.height &&
        Math.floor(sprite.width / sprite.frameWidth) *
          Math.floor(sprite.height / sprite.frameHeight) >
          1))
  );
}

function calculateDisplayDimensions(
  sprite: Sprite,
  size: number | string
): { displayWidth: number; displayHeight: number } {
  const frameWidth =
    sprite.frameWidth && sprite.frameWidth > 0 ? sprite.frameWidth : 16;
  const frameHeight =
    sprite.frameHeight && sprite.frameHeight > 0 ? sprite.frameHeight : 16;

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

  return { displayWidth, displayHeight };
}

function calculateScaleFactor(
  sprite: Sprite,
  displayWidth: number,
  displayHeight: number
): number {
  const frameWidth =
    sprite.frameWidth && sprite.frameWidth > 0 ? sprite.frameWidth : 16;
  const frameHeight =
    sprite.frameHeight && sprite.frameHeight > 0 ? sprite.frameHeight : 16;

  const scaleX = displayWidth / frameWidth;
  const scaleY = displayHeight / frameHeight;

  return Math.min(scaleX, scaleY);
}

function calculateTextureSize(
  sprite: Sprite,
  scale: number
): { width: number; height: number } {
  const frameWidth =
    sprite.frameWidth && sprite.frameWidth > 0 ? sprite.frameWidth : 16;
  const frameHeight =
    sprite.frameHeight && sprite.frameHeight > 0 ? sprite.frameHeight : 16;
  const safeWidth =
    sprite.width && sprite.width > 0 ? sprite.width : frameWidth;
  const safeHeight =
    sprite.height && sprite.height > 0 ? sprite.height : frameHeight;

  return {
    width: safeWidth * scale,
    height: safeHeight * scale,
  };
}

function calculateOffset(
  sprite: Sprite,
  scale: number
): { x: number; y: number } {
  const frameX = sprite.frameX || 0;
  const frameY = sprite.frameY || 0;

  return {
    x: -frameX * scale,
    y: -frameY * scale,
  };
}

describe("SpriteRenderer Calculations", () => {
  describe("Frame Detection", () => {
    test("should detect frame data for multi-frame sprite", () => {
      expect(hasFrameData(mockSprite)).toBe(true);
    });

    test("should not detect frame data for simple sprite matching full size", () => {
      const simpleSprite: Sprite = {
        ...mockSprite,
        width: 16,
        height: 16,
        frameWidth: 16,
        frameHeight: 16,
        frameX: 0,
        frameY: 0,
      };

      expect(hasFrameData(simpleSprite)).toBe(false);
    });

    test("should detect frame data when frameX or frameY is non-zero", () => {
      const offsetSprite: Sprite = {
        ...mockSprite,
        frameX: 16,
        frameY: 0,
      };

      expect(hasFrameData(offsetSprite)).toBe(true);
    });
  });

  describe("Display Dimensions", () => {
    test("should calculate correct dimensions for square frames", () => {
      const { displayWidth, displayHeight } = calculateDisplayDimensions(
        mockSprite,
        64
      );

      // Frame is 16x16 (square), so display should be 64x64
      expect(displayWidth).toBe(64);
      expect(displayHeight).toBe(64);
    });

    test("should handle non-square frames", () => {
      const tallSprite: Sprite = {
        ...mockSprite,
        frameWidth: 16,
        frameHeight: 32, // 2:1 aspect ratio (tall)
      };

      const { displayWidth, displayHeight } = calculateDisplayDimensions(
        tallSprite,
        64
      );

      // aspectRatio = 16/32 = 0.5 (tall)
      // displayHeight = 64, displayWidth = 64 * 0.5 = 32
      expect(displayWidth).toBe(32);
      expect(displayHeight).toBe(64);
    });

    test("should handle wide frames", () => {
      const wideSprite: Sprite = {
        ...mockSprite,
        frameWidth: 32,
        frameHeight: 16, // 2:1 aspect ratio (wide)
      };

      const { displayWidth, displayHeight } = calculateDisplayDimensions(
        wideSprite,
        64
      );

      // aspectRatio = 32/16 = 2 (wide)
      // displayWidth = 64, displayHeight = 64 / 2 = 32
      expect(displayWidth).toBe(64);
      expect(displayHeight).toBe(32);
    });
  });

  describe("Scale Factor", () => {
    test("should calculate correct scale for square frames", () => {
      const { displayWidth, displayHeight } = calculateDisplayDimensions(
        mockSprite,
        64
      );
      const scale = calculateScaleFactor(
        mockSprite,
        displayWidth,
        displayHeight
      );

      // To scale 16px frame to 64px display: scale = 64/16 = 4
      expect(scale).toBe(4);
    });

    test("should calculate correct scale for non-square frames", () => {
      const tallSprite: Sprite = {
        ...mockSprite,
        frameWidth: 16,
        frameHeight: 32,
      };

      const { displayWidth, displayHeight } = calculateDisplayDimensions(
        tallSprite,
        64
      );
      const scale = calculateScaleFactor(
        tallSprite,
        displayWidth,
        displayHeight
      );

      // displayWidth=32, displayHeight=64
      // scaleX = 32/16 = 2, scaleY = 64/32 = 2
      // scale = min(2, 2) = 2
      expect(scale).toBe(2);
    });
  });

  describe("Texture Size Calculation", () => {
    test("should calculate correct texture size", () => {
      const { displayWidth, displayHeight } = calculateDisplayDimensions(
        mockSprite,
        64
      );
      const scale = calculateScaleFactor(
        mockSprite,
        displayWidth,
        displayHeight
      );
      const textureSize = calculateTextureSize(mockSprite, scale);

      // Original texture: 48x32, scale: 4
      // Scaled texture: 192x128
      expect(textureSize.width).toBe(192);
      expect(textureSize.height).toBe(128);
    });
  });

  describe("Frame Positioning", () => {
    test("should calculate correct offset for frame 0 (top-left)", () => {
      const frame0Sprite = { ...mockSprite, frameX: 0, frameY: 0 };
      const { displayWidth, displayHeight } = calculateDisplayDimensions(
        frame0Sprite,
        64
      );
      const scale = calculateScaleFactor(
        frame0Sprite,
        displayWidth,
        displayHeight
      );
      const offset = calculateOffset(frame0Sprite, scale);

      expect(offset.x).toBe(0);
      expect(offset.y).toBe(0);
    });

    test("should calculate correct offset for frame 1 (top-middle)", () => {
      const frame1Sprite = { ...mockSprite, frameX: 16, frameY: 0 };
      const { displayWidth, displayHeight } = calculateDisplayDimensions(
        frame1Sprite,
        64
      );
      const scale = calculateScaleFactor(
        frame1Sprite,
        displayWidth,
        displayHeight
      );
      const offset = calculateOffset(frame1Sprite, scale);

      // frameX=16, scale=4, so offset.x = -16*4 = -64
      expect(offset.x).toBe(-64);
      expect(offset.y).toBe(0);
    });

    test("should calculate correct offset for frame 2 (top-right)", () => {
      const frame2Sprite = { ...mockSprite, frameX: 32, frameY: 0 };
      const { displayWidth, displayHeight } = calculateDisplayDimensions(
        frame2Sprite,
        64
      );
      const scale = calculateScaleFactor(
        frame2Sprite,
        displayWidth,
        displayHeight
      );
      const offset = calculateOffset(frame2Sprite, scale);

      // frameX=32, scale=4, so offset.x = -32*4 = -128
      expect(offset.x).toBe(-128);
      expect(offset.y).toBe(0);
    });

    test("should calculate correct offset for frame 3 (bottom-left)", () => {
      const frame3Sprite = { ...mockSprite, frameX: 0, frameY: 16 };
      const { displayWidth, displayHeight } = calculateDisplayDimensions(
        frame3Sprite,
        64
      );
      const scale = calculateScaleFactor(
        frame3Sprite,
        displayWidth,
        displayHeight
      );
      const offset = calculateOffset(frame3Sprite, scale);

      // frameY=16, scale=4, so offset.y = -16*4 = -64
      expect(offset.x).toBe(0);
      expect(offset.y).toBe(-64);
    });

    test("should calculate correct offset for frame 4 (bottom-middle)", () => {
      const frame4Sprite = { ...mockSprite, frameX: 16, frameY: 16 };
      const { displayWidth, displayHeight } = calculateDisplayDimensions(
        frame4Sprite,
        64
      );
      const scale = calculateScaleFactor(
        frame4Sprite,
        displayWidth,
        displayHeight
      );
      const offset = calculateOffset(frame4Sprite, scale);

      expect(offset.x).toBe(-64);
      expect(offset.y).toBe(-64);
    });

    test("should calculate correct offset for frame 5 (bottom-right)", () => {
      const frame5Sprite = { ...mockSprite, frameX: 32, frameY: 16 };
      const { displayWidth, displayHeight } = calculateDisplayDimensions(
        frame5Sprite,
        64
      );
      const scale = calculateScaleFactor(
        frame5Sprite,
        displayWidth,
        displayHeight
      );
      const offset = calculateOffset(frame5Sprite, scale);

      expect(offset.x).toBe(-128);
      expect(offset.y).toBe(-64);
    });
  });

  describe("Grid Layout Verification", () => {
    test("should correctly map 6 frames in a 3x2 grid", () => {
      const frames = [
        { frameX: 0, frameY: 0 }, // Frame 0: top-left
        { frameX: 16, frameY: 0 }, // Frame 1: top-middle
        { frameX: 32, frameY: 0 }, // Frame 2: top-right
        { frameX: 0, frameY: 16 }, // Frame 3: bottom-left
        { frameX: 16, frameY: 16 }, // Frame 4: bottom-middle
        { frameX: 32, frameY: 16 }, // Frame 5: bottom-right
      ];

      frames.forEach((frame, index) => {
        const frameSprite = {
          ...mockSprite,
          frameX: frame.frameX,
          frameY: frame.frameY,
        };
        const { displayWidth, displayHeight } = calculateDisplayDimensions(
          frameSprite,
          64
        );
        const scale = calculateScaleFactor(
          frameSprite,
          displayWidth,
          displayHeight
        );
        const offset = calculateOffset(frameSprite, scale);

        const expectedX = -frame.frameX * scale;
        const expectedY = -frame.frameY * scale;

        expect(offset.x).toBe(expectedX);
        expect(offset.y).toBe(expectedY);

        console.log(
          `Frame ${index}: frameX=${frame.frameX}, frameY=${frame.frameY} -> offsetX=${offset.x}, offsetY=${offset.y}`
        );
      });
    });
  });
});

import { describe, test, expect } from "vitest";
import { Sprite } from "../../store/useSpriteStore";

// Mock sprite data
const mockSprite: Sprite = {
  id: "test-sprite",
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

// Extract clipPath calculation logic
function calculateClipPath(sprite: Sprite): string {
  const frameWidth =
    sprite.frameWidth && sprite.frameWidth > 0 ? sprite.frameWidth : 16;
  const frameHeight =
    sprite.frameHeight && sprite.frameHeight > 0 ? sprite.frameHeight : 16;
  const frameX = sprite.frameX || 0;
  const frameY = sprite.frameY || 0;
  const safeWidth =
    sprite.width && sprite.width > 0 ? sprite.width : frameWidth;
  const safeHeight =
    sprite.height && sprite.height > 0 ? sprite.height : frameHeight;

  const clipLeft = (frameX / safeWidth) * 100;
  const clipTop = (frameY / safeHeight) * 100;
  const clipRight = ((frameX + frameWidth) / safeWidth) * 100;
  const clipBottom = ((frameY + frameHeight) / safeHeight) * 100;

  return `polygon(${clipLeft}% ${clipTop}%, ${clipRight}% ${clipTop}%, ${clipRight}% ${clipBottom}%, ${clipLeft}% ${clipBottom}%)`;
}

describe("SpriteRenderer ClipPath Calculations", () => {
  test("should calculate correct clipPath for frame 0 (top-left)", () => {
    const frame0Sprite = { ...mockSprite, frameX: 0, frameY: 0 };
    const clipPath = calculateClipPath(frame0Sprite);

    // Frame 0: x=0, y=0, width=16, height=16 in a 48x32 texture
    // clipLeft = 0/48 * 100 = 0%
    // clipTop = 0/32 * 100 = 0%
    // clipRight = 16/48 * 100 = 33.333%
    // clipBottom = 16/32 * 100 = 50%
    expect(clipPath).toBe(
      "polygon(0% 0%, 33.333333333333336% 0%, 33.333333333333336% 50%, 0% 50%)"
    );
  });

  test("should calculate correct clipPath for frame 1 (top-middle)", () => {
    const frame1Sprite = { ...mockSprite, frameX: 16, frameY: 0 };
    const clipPath = calculateClipPath(frame1Sprite);

    // Frame 1: x=16, y=0, width=16, height=16
    // clipLeft = 16/48 * 100 = 33.333%
    // clipTop = 0/32 * 100 = 0%
    // clipRight = 32/48 * 100 = 66.667%
    // clipBottom = 16/32 * 100 = 50%
    expect(clipPath).toBe(
      "polygon(33.333333333333336% 0%, 66.66666666666667% 0%, 66.66666666666667% 50%, 33.333333333333336% 50%)"
    );
  });

  test("should calculate correct clipPath for frame 2 (top-right)", () => {
    const frame2Sprite = { ...mockSprite, frameX: 32, frameY: 0 };
    const clipPath = calculateClipPath(frame2Sprite);

    // Frame 2: x=32, y=0, width=16, height=16
    // clipLeft = 32/48 * 100 = 66.667%
    // clipTop = 0/32 * 100 = 0%
    // clipRight = 48/48 * 100 = 100%
    // clipBottom = 16/32 * 100 = 50%
    expect(clipPath).toBe(
      "polygon(66.66666666666667% 0%, 100% 0%, 100% 50%, 66.66666666666667% 50%)"
    );
  });

  test("should calculate correct clipPath for frame 3 (bottom-left)", () => {
    const frame3Sprite = { ...mockSprite, frameX: 0, frameY: 16 };
    const clipPath = calculateClipPath(frame3Sprite);

    // Frame 3: x=0, y=16, width=16, height=16
    // clipLeft = 0/48 * 100 = 0%
    // clipTop = 16/32 * 100 = 50%
    // clipRight = 16/48 * 100 = 33.333%
    // clipBottom = 32/32 * 100 = 100%
    expect(clipPath).toBe(
      "polygon(0% 50%, 33.333333333333336% 50%, 33.333333333333336% 100%, 0% 100%)"
    );
  });

  test("should calculate correct clipPath for frame 4 (bottom-middle)", () => {
    const frame4Sprite = { ...mockSprite, frameX: 16, frameY: 16 };
    const clipPath = calculateClipPath(frame4Sprite);

    // Frame 4: x=16, y=16, width=16, height=16
    expect(clipPath).toBe(
      "polygon(33.333333333333336% 50%, 66.66666666666667% 50%, 66.66666666666667% 100%, 33.333333333333336% 100%)"
    );
  });

  test("should calculate correct clipPath for frame 5 (bottom-right)", () => {
    const frame5Sprite = { ...mockSprite, frameX: 32, frameY: 16 };
    const clipPath = calculateClipPath(frame5Sprite);

    // Frame 5: x=32, y=16, width=16, height=16
    expect(clipPath).toBe(
      "polygon(66.66666666666667% 50%, 100% 50%, 100% 100%, 66.66666666666667% 100%)"
    );
  });

  test("should handle full sprite (no clipping)", () => {
    const fullSprite = {
      ...mockSprite,
      frameX: 0,
      frameY: 0,
      frameWidth: 48,
      frameHeight: 32,
    };
    const clipPath = calculateClipPath(fullSprite);

    // Full sprite should clip the entire image (0% to 100%)
    expect(clipPath).toBe("polygon(0% 0%, 100% 0%, 100% 100%, 0% 100%)");
  });

  test("should handle edge case with small frame in large texture", () => {
    const smallFrameSprite = {
      ...mockSprite,
      width: 100,
      height: 100,
      frameX: 10,
      frameY: 20,
      frameWidth: 5,
      frameHeight: 8,
    };

    const clipPath = calculateClipPath(smallFrameSprite);

    // clipLeft = 10/100 * 100 = 10%
    // clipTop = 20/100 * 100 = 20%
    // clipRight = 15/100 * 100 = 15%
    // clipBottom = 28/100 * 100 = 28%
    expect(clipPath).toBe("polygon(10% 20%, 15% 20%, 15% 28%, 10% 28%)");
  });
});

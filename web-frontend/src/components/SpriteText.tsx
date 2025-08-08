import React from "react";
import {
  useSpriteActions,
  useSpriteInitialized,
} from "../store/useSpriteStore";

interface SpriteTextProps {
  children: string;
  className?: string;
  style?: React.CSSProperties;
}

const SpriteText: React.FC<SpriteTextProps> = ({
  children,
  className,
  style,
}) => {
  const { substituteSprites } = useSpriteActions();
  const isInitialized = useSpriteInitialized();

  // If sprites aren't initialized yet, just render plain text
  if (!isInitialized) {
    return (
      <span className={className} style={style}>
        (not init){children}
      </span>
    );
  }

  // Substitute sprites in the text
  const content = substituteSprites(children);

  return (
    <span className={className} style={style}>
      {content}
    </span>
  );
};

export default SpriteText;

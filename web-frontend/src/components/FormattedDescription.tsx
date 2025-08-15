import React from "react";
import { Typography, TypographyProps } from "@mui/material";
import SpriteText from "./SpriteText";

interface FormattedDescriptionProps extends Omit<TypographyProps, 'children'> {
  children: string;
}

const FormattedDescription: React.FC<FormattedDescriptionProps> = ({ 
  children, 
  ...typographyProps 
}) => {
  if (!children) {
    return null;
  }

  // Split the text on \n and render each line separately
  const lines = children.split('\n').filter(line => line.trim().length > 0);

  if (lines.length === 1) {
    // Single line - just use SpriteText directly
    return (
      <Typography {...typographyProps}>
        <SpriteText>{lines[0]}</SpriteText>
      </Typography>
    );
  }

  // Multiple lines - render each on its own line
  return (
    <>
      {lines.map((line, index) => (
        <Typography key={index} {...typographyProps}>
          <SpriteText>{line.trim()}</SpriteText>
        </Typography>
      ))}
    </>
  );
};

export default FormattedDescription;
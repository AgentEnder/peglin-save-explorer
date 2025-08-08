// Rarity names are now reflected from Peglin's Assembly-CSharp.dll enums
// The backend sends the actual enum name as a string (e.g., "COMMON", "RARE", "BOSS")

export const getRarityName = (rarity: string | number): string => {
  // If it's already a string enum name, just format it nicely
  if (typeof rarity === 'string' && isNaN(parseInt(rarity))) {
    const formatted = rarity.charAt(0).toUpperCase() + rarity.slice(1).toLowerCase();
    return formatted;
  }
  
  // Fallback for any numeric values that might still come through
  return rarity.toString();
};

export const getRarityTooltip = (rarity: string | number): string | null => {
  const rarityStr = typeof rarity === 'string' ? rarity.toUpperCase() : rarity.toString();
  
  if (rarityStr === "UNAVAILABLE" || rarityStr === "4") {
    return "Used internally by Peglin for secret relics, class starting relics, and other relics that can't be obtained during a run";
  }
  
  return null;
};

export const isUnavailableRarity = (rarity: string | number): boolean => {
  const rarityStr = typeof rarity === 'string' ? rarity.toUpperCase() : rarity.toString();
  return rarityStr === "UNAVAILABLE" || rarityStr === "4";
};

export const getRarityColor = (rarity: string | number): "default" | "primary" | "secondary" | "warning" | "error" | "info" => {
  // Convert to uppercase string for comparison
  const rarityStr = typeof rarity === 'string' ? rarity.toUpperCase() : rarity.toString();
  
  switch (rarityStr) {
    case "NONE":
    case "0":
      return "default";  // None - gray
    case "COMMON":
    case "1":
      return "primary";  // Common - blue
    case "RARE":
    case "2":
      return "secondary"; // Rare - purple
    case "BOSS":
    case "3":
      return "warning";   // Boss - orange/yellow
    case "UNAVAILABLE":
    case "4":
      return "info";      // Unavailable - light blue
    default:
      return "default";
  }
};
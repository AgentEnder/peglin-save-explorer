export interface RunRecord {
  id: string;
  timestamp: string;
  won: boolean;
  score: number;
  damageDealt: number;
  pegsHit: number;
  duration: string; // TimeSpan format like "00:06:43.2200000"
  characterClass: string;
  seed?: string;
  finalLevel: number;
  coinsEarned: number;
  orbsUsed: string[];
  relicsUsed: string[];
  isReconstructed: boolean;

  // Additional stats file fields
  isCustomRun: boolean;
  cruciballLevel: number;
  defeatedBy?: string;
  finalHp: number;
  maxHp: number;
  mostDamageDealtWithSingleAttack: number;
  totalDamageNegated: number;
  pegsHitRefresh: number;
  pegsHitCrit: number;
  pegsRefreshed: number;
  bombsThrown: number;
  shotsTaken: number;
  critShotsTaken: number;

  // Enriched data
  relicNames: string[];
  bossNames: string[];
  roomTypeStatistics: Record<string, number>;
  activeStatusEffects: string[];
  activeSlimePegs: string[];
}

export interface ClassStatistics {
  className: string;
  totalRuns: number;
  wins: number;
  winRate: number;
  totalDamage: number;
  totalPegsHit: number;
  totalDuration: number;
  totalCoinsEarned: number;
  highestCruciball: number;
  bestDamageRun: number;
  averageDamage: number;
  averagePegsHit: number;
  averageDuration: number;
  averageCoinsEarned: number;
}

export interface OrbStatistics {
  orbName: string;
  timesUsed: number;
  winsWithOrb: number;
  totalRunsWithOrb: number;
  totalDamageWithOrb: number;
  winRateWithOrb: number;
  averageDamageWithOrb: number;
}

export interface PlayerStatistics {
  gameplayStats: Record<string, string | number>;
  combatStats: Record<string, string | number>;
  pegStats: Record<string, string | number>;
  economyStats: Record<string, string | number>;
}

export interface Summary {
  totalRuns: number;
  totalWins: number;
  winRate: number;
  averageDamage: number;
  averageDuration: { totalSeconds: number };
  topClasses: Record<string, ClassStatistics>;
}

export interface RunHistoryData {
  runs: RunRecord[];
  classStatistics: Record<string, ClassStatistics>;
  orbStatistics: Record<string, OrbStatistics>;
  playerStatistics?: PlayerStatistics;
  totalRuns: number;
  totalWins: number;
  winRate: number;
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
}

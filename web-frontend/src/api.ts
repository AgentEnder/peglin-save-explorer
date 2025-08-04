import {
  ApiResponse,
  RunHistoryData,
  RunRecord,
  PlayerStatistics,
  Summary,
  ClassStatistics,
  OrbStatistics,
} from "./types";

const API_BASE = "/api";

export const api = {
  async getHealth(): Promise<{ status: string; timestamp: string }> {
    const response = await fetch(`${API_BASE}/health`);
    const result: ApiResponse<{ status: string; timestamp: string }> =
      await response.json();

    if (!result.success || !result.data) {
      throw new Error(result.error || "Failed to check health");
    }

    return result.data;
  },

  async getRunHistory(): Promise<RunHistoryData> {
    const response = await fetch(`${API_BASE}/runs`);
    const result: ApiResponse<RunHistoryData> = await response.json();

    if (!result.success || !result.data) {
      throw new Error(result.error || "Failed to fetch run history");
    }

    return result.data;
  },

  async getRuns(filters?: {
    characterClass?: string;
    won?: boolean;
    startDate?: string;
    endDate?: string;
    minDamage?: number;
    maxDamage?: number;
    minDuration?: number;
    maxDuration?: number;
  }): Promise<{ runs: RunRecord[]; totalCount: number }> {
    const params = new URLSearchParams();
    if (filters) {
      Object.entries(filters).forEach(([key, value]) => {
        if (value !== undefined && value !== null) {
          params.append(key, value.toString());
        }
      });
    }

    const url = `${API_BASE}/runs/filtered${
      params.toString() ? `?${params.toString()}` : ""
    }`;
    const response = await fetch(url);
    const result: ApiResponse<{ runs: RunRecord[]; totalCount: number }> =
      await response.json();

    if (!result.success || !result.data) {
      throw new Error(result.error || "Failed to fetch filtered runs");
    }

    return result.data;
  },

  async getRun(id: string): Promise<RunRecord> {
    const response = await fetch(`${API_BASE}/runs/${id}`);
    const result: ApiResponse<RunRecord> = await response.json();

    if (!result.success || !result.data) {
      throw new Error(result.error || "Failed to fetch run details");
    }

    return result.data;
  },

  async getStatistics(): Promise<{
    classStatistics: Record<string, ClassStatistics>;
    orbStatistics: Record<string, OrbStatistics>;
    playerStatistics: PlayerStatistics;
    summary: {
      totalRuns: number;
      totalWins: number;
      winRate: number;
    };
  }> {
    const response = await fetch(`${API_BASE}/statistics`);
    const result = await response.json();

    if (!result.success || !result.data) {
      throw new Error(result.error || "Failed to fetch statistics");
    }

    return result.data;
  },

  async getSummary(): Promise<Summary> {
    const response = await fetch(`${API_BASE}/summary`);
    const result: ApiResponse<Summary> = await response.json();

    if (!result.success || !result.data) {
      throw new Error(result.error || "Failed to fetch summary");
    }

    return result.data;
  },

  async getClasses(): Promise<string[]> {
    const response = await fetch(`${API_BASE}/classes`);
    const result: ApiResponse<string[]> = await response.json();

    if (!result.success || !result.data) {
      throw new Error(result.error || "Failed to fetch class list");
    }

    return result.data;
  },

  async getClassStats(className: string): Promise<ClassStatistics> {
    const response = await fetch(
      `${API_BASE}/classes/${encodeURIComponent(className)}/stats`
    );
    const result: ApiResponse<ClassStatistics> = await response.json();

    if (!result.success || !result.data) {
      throw new Error(result.error || "Failed to fetch class statistics");
    }

    return result.data;
  },

  async loadSaveFile(file: File): Promise<{
    message: string;
    totalRuns: number;
    totalWins: number;
    winRate: number;
  }> {
    const formData = new FormData();
    formData.append("saveFile", file);

    const response = await fetch(`${API_BASE}/load`, {
      method: "POST",
      body: formData,
    });

    const result = await response.json();

    if (!result.success || !result.data) {
      throw new Error(result.error || "Failed to load save file");
    }

    return result.data;
  },

  async exportRuns(format: "json" | "csv" = "json"): Promise<Blob> {
    const response = await fetch(`${API_BASE}/export?format=${format}`);

    if (!response.ok) {
      throw new Error("Failed to export runs");
    }

    return await response.blob();
  },

  async updateCruciballLevel(
    characterClass: string,
    cruciballLevel: number
  ): Promise<{ message: string; characterClass: string; newLevel: number }> {
    const response = await fetch(`${API_BASE}/update-cruciball`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        characterClass,
        cruciballLevel,
      }),
    });

    const result: ApiResponse<{
      message: string;
      characterClass: string;
      newLevel: number;
    }> = await response.json();

    if (!result.success || !result.data) {
      throw new Error(result.error || "Failed to update cruciball level");
    }

    return result.data;
  },
};

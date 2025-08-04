import { create } from "zustand";
import { subscribeWithSelector } from "zustand/middleware";
import { useShallow } from "zustand/react/shallow";
import { api } from "../api";
import { RunHistoryData, PlayerStatistics, Summary, RunRecord } from "../types";

interface AppConfig {
  excludeCustomRuns: boolean;
  excludeTestRuns: boolean;
  excludeIncompletRuns: boolean;
}

interface AppState {
  // Data
  runHistoryData: RunHistoryData | null;
  playerStatistics: PlayerStatistics | null;
  summary: Summary | null;

  // Configuration
  config: AppConfig;

  // UI State
  isLoading: boolean;
  error: string | null;
  isInitialized: boolean;

  // Actions - these are stable references
  initialize: () => Promise<void>;
  refresh: () => Promise<void>;
  uploadSaveFile: (file: File) => Promise<void>;
  exportData: (format: "json" | "csv") => Promise<Blob>;
  clearError: () => void;
  updateConfig: (updates: Partial<AppConfig>) => Promise<void>;
  getFilteredRuns: (filters?: {
    characterClass?: string;
    won?: boolean;
    startDate?: string;
    endDate?: string;
    minDamage?: number;
    maxDamage?: number;
    minDuration?: number;
    maxDuration?: number;
  }) => Promise<{ runs: RunRecord[]; totalCount: number }>;

  // Computed getters
  hasData: () => boolean;
}

export const useAppStore = create<AppState>()(
  subscribeWithSelector((set, get) => ({
    // Initial state
    runHistoryData: null,
    playerStatistics: null,
    summary: null,
    config: {
      excludeCustomRuns: true,
      excludeTestRuns: false,
      excludeIncompletRuns: false,
    },
    isLoading: false,
    error: null,
    isInitialized: false,

    // Initialize the app by loading all data
    initialize: async () => {
      if (get().isInitialized) return;

      set({ isLoading: true, error: null });

      try {
        // Load all data in parallel
        const [runHistoryData, statisticsData, summaryData] = await Promise.all(
          [
            api.getRunHistory().catch(() => null), // Allow null if no data
            api.getStatistics().catch(() => null),
            api.getSummary().catch(() => null),
          ]
        );

        set({
          runHistoryData,
          playerStatistics: statisticsData?.playerStatistics || null,
          summary: summaryData,
          isLoading: false,
          isInitialized: true,
          error: null,
        });

        console.log("App initialized with data:", {
          hasRunData: !!runHistoryData,
          totalRuns: runHistoryData?.totalRuns || 0,
          hasPlayerStats: !!statisticsData?.playerStatistics,
          hasSummary: !!summaryData,
        });
      } catch (error) {
        console.error("Error initializing app:", error);
        set({
          error:
            error instanceof Error ? error.message : "Failed to initialize app",
          isLoading: false,
          isInitialized: true,
        });
      }
    },

    // Refresh all data
    refresh: async () => {
      set({ isLoading: true, error: null });

      try {
        const [runHistoryData, statisticsData, summaryData] = await Promise.all(
          [api.getRunHistory(), api.getStatistics(), api.getSummary()]
        );

        set({
          runHistoryData,
          playerStatistics: statisticsData.playerStatistics,
          summary: summaryData,
          isLoading: false,
          error: null,
        });
      } catch (error) {
        set({
          error:
            error instanceof Error ? error.message : "Failed to refresh data",
          isLoading: false,
        });
      }
    },

    // Upload a new save file
    uploadSaveFile: async (file: File) => {
      set({ isLoading: true, error: null });

      try {
        await api.loadSaveFile(file);

        // Refresh all data after successful upload
        await get().refresh();
      } catch (error) {
        set({
          error:
            error instanceof Error
              ? error.message
              : "Failed to upload save file",
          isLoading: false,
        });
        throw error; // Re-throw so UI can handle it
      }
    },

    // Export data
    exportData: async (format: "json" | "csv" = "json") => {
      try {
        return await api.exportRuns(format);
      } catch (error) {
        set({
          error:
            error instanceof Error ? error.message : "Failed to export data",
        });
        throw error;
      }
    },

    // Update configuration
    updateConfig: async (updates: Partial<AppConfig>) => {
      set((state) => ({
        config: { ...state.config, ...updates },
      }));
    },

    // Clear error state
    clearError: () => {
      set({ error: null });
    },

    // Check if we have meaningful data
    hasData: () => {
      const state = get();
      return !!(state.runHistoryData && state.runHistoryData.totalRuns > 0);
    },

    // Get filtered runs (delegates to API for server-side filtering)
    getFilteredRuns: async (filters) => {
      try {
        return await api.getRuns(filters);
      } catch (error) {
        set({
          error:
            error instanceof Error ? error.message : "Failed to filter runs",
        });
        throw error;
      }
    },
  }))
);

// Selector hooks for easy access to specific data
export const useRunHistoryData = () =>
  useAppStore((state) => state.runHistoryData);
export const usePlayerStatistics = () =>
  useAppStore((state) => state.playerStatistics);
export const useSummary = () => useAppStore((state) => state.summary);
export const useAppConfig = () => useAppStore((state) => state.config);
export const useAppLoading = () => useAppStore((state) => state.isLoading);
export const useAppError = () => useAppStore((state) => state.error);
export const useAppInitialized = () =>
  useAppStore((state) => state.isInitialized);
export const useHasData = () => useAppStore((state) => state.hasData());

// Filtered data selector that returns both runs and config for memoization in components
export const useRunsAndConfig = () =>
  useAppStore(
    useShallow((state) => ({
      runs: state.runHistoryData?.runs || [],
      excludeCustomRuns: state.config.excludeCustomRuns,
    }))
  );

// Action hooks - these return stable references using useShallow
export const useAppActions = () =>
  useAppStore(
    useShallow((state) => ({
      initialize: state.initialize,
      refresh: state.refresh,
      uploadSaveFile: state.uploadSaveFile,
      exportData: state.exportData,
      clearError: state.clearError,
      updateConfig: state.updateConfig,
      getFilteredRuns: state.getFilteredRuns,
    }))
  );

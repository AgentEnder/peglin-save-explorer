using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using peglin_save_explorer.Core;
using peglin_save_explorer.Data;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Services
{
    /// <summary>
    /// Centralized service for loading run history data from save files
    /// </summary>
    public static class RunDataService
    {
        /// <summary>
        /// Load run history from save file or use default path
        /// </summary>
        public static List<RunRecord> LoadRunHistory(FileInfo? file, ConfigurationManager configManager)
        {
            try
            {
                var runHistoryManager = new RunHistoryManager(configManager);

                // Determine save file path
                string saveFilePath;
                if (file != null && file.Exists)
                {
                    saveFilePath = file.FullName;
                }
                else
                {
                    // Use default save file path
                    var defaultPath = configManager.GetEffectiveSaveFilePath();
                    if (string.IsNullOrEmpty(defaultPath) || !File.Exists(defaultPath))
                    {
                        Logger.Error("No save file specified and no default save file found.");
                        return new List<RunRecord>();
                    }
                    saveFilePath = defaultPath;
                }

                var statsFilePath = GetStatsFilePath(saveFilePath);
                if (string.IsNullOrEmpty(statsFilePath))
                {
                    Logger.Error("Could not determine stats file path. Save file should be named like 'Save_0.data'.");
                    return new List<RunRecord>();
                }

                if (!File.Exists(statsFilePath))
                {
                    Logger.Error($"Stats file not found: {statsFilePath}");
                    Logger.Info("Run history is stored in the Stats file, not the Save file.");
                    return new List<RunRecord>();
                }

                Logger.Debug($"Loading run history from: {statsFilePath}");
                var statsBytes = File.ReadAllBytes(statsFilePath);
                var dumper = new SaveFileDumper(configManager);
                var statsJson = dumper.DumpSaveFile(statsBytes);
                var statsData = JObject.Parse(statsJson);
                var runs = runHistoryManager.ExtractRunHistory(statsData);

                Logger.Debug($"Successfully loaded {runs.Count} runs from stats file.");
                return runs;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load run history: {ex}");
                return new List<RunRecord>();
            }
        }

        /// <summary>
        /// Get run by index with validation
        /// </summary>
        public static RunRecord? GetRunByIndex(List<RunRecord> runs, int index)
        {
            if (index < 0 || index >= runs.Count)
            {
                Logger.Error($"Invalid run index {index}. Available runs: 0-{runs.Count - 1}");
                return null;
            }
            return runs[index];
        }

        /// <summary>
        /// Convert save file path to corresponding stats file path
        /// </summary>
        public static string GetStatsFilePath(string saveFilePath)
        {
            // Stats file has same name pattern but with Stats_ prefix
            var saveFileName = Path.GetFileName(saveFilePath);
            if (saveFileName.StartsWith("Save_") && saveFileName.EndsWith(".data"))
            {
                var saveNumber = saveFileName.Substring(5, saveFileName.Length - 10);
                var statsFileName = $"Stats_{saveNumber}.data";
                var saveDir = Path.GetDirectoryName(saveFilePath);
                return Path.Combine(saveDir ?? "", statsFileName);
            }
            return "";
        }

        /// <summary>
        /// Load run history with debug output for file paths
        /// </summary>
        public static List<RunRecord> LoadRunHistoryWithDebug(FileInfo? saveFileInfo, ConfigurationManager configManager)
        {
            var debugInfo = new List<string>();
            
            try
            {
                debugInfo.Add($"Loading run history from save file: {saveFileInfo?.FullName ?? "default"}");

                var runHistoryManager = new RunHistoryManager(configManager);
                string saveFilePath;

                if (saveFileInfo != null && saveFileInfo.Exists)
                {
                    saveFilePath = saveFileInfo.FullName;
                    debugInfo.Add($"Using provided save file: {saveFilePath}");
                }
                else
                {
                    var defaultPath = configManager.GetEffectiveSaveFilePath();
                    if (string.IsNullOrEmpty(defaultPath) || !File.Exists(defaultPath))
                    {
                        debugInfo.Add("No valid save file found");
                        Logger.Debug($"Run history loading debug: {string.Join(", ", debugInfo)}");
                        return new List<RunRecord>();
                    }
                    saveFilePath = defaultPath;
                    debugInfo.Add($"Using default save file: {saveFilePath}");
                }

                var statsFilePath = GetStatsFilePath(saveFilePath);
                debugInfo.Add($"Stats file path: {statsFilePath}");

                if (File.Exists(statsFilePath))
                {
                    debugInfo.Add("Stats file exists, extracting run history");
                    var statsBytes = File.ReadAllBytes(statsFilePath);
                    var dumper = new SaveFileDumper(configManager);
                    var statsJson = dumper.DumpSaveFile(statsBytes);
                    var statsData = JObject.Parse(statsJson);
                    var runs = runHistoryManager.ExtractRunHistory(statsData);
                    debugInfo.Add($"Extracted {runs.Count} runs from stats data");

                    Logger.Debug($"Run history loading: {string.Join(", ", debugInfo)}");
                    return runs;
                }
                else
                {
                    debugInfo.Add("Stats file not found");
                    Logger.Debug($"Run history loading: {string.Join(", ", debugInfo)}");
                }
            }
            catch (Exception ex)
            {
                debugInfo.Add($"Error loading run history: {ex.Message}");
                Logger.Debug($"Run history loading failed: {string.Join(", ", debugInfo)}");
            }

            return new List<RunRecord>();
        }
    }
}

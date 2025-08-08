using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using peglin_save_explorer.Data;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Extractors
{
    /// <summary>
    /// Extracts sprite information using AssetRipper's CLI export functionality
    /// This approach dumps assets to files and parses them to find SpriteInformationObject structures
    /// </summary>
    public class AssetRipperCliExtractor
    {
        private static readonly string TempDumpDirectory = Path.Combine(Path.GetTempPath(), "peglin-assetripper-dump");
        private readonly Dictionary<long, long> _spriteToTextureLookup = new();

        /// <summary>
        /// Extracts sprite information by dumping assets via AssetRipper CLI
        /// </summary>
        public async Task<Dictionary<long, long>> ExtractSpriteInformationAsync(string peglinPath, IProgress<string>? progress = null)
        {
            try
            {
                progress?.Report("🔧 Setting up AssetRipper CLI extraction...");
                
                // Clean up any previous dump
                if (Directory.Exists(TempDumpDirectory))
                {
                    Directory.Delete(TempDumpDirectory, true);
                }
                Directory.CreateDirectory(TempDumpDirectory);

                // Use AssetRipper CLI to dump assets
                progress?.Report("📤 Dumping assets via AssetRipper CLI...");
                var success = await DumpAssetsViaCli(peglinPath, progress);
                if (!success)
                {
                    Logger.Warning("Failed to dump assets via AssetRipper CLI");
                    return _spriteToTextureLookup;
                }

                // Parse the dumped files to find sprite information objects
                progress?.Report("🔍 Parsing dumped assets for sprite information...");
                await ParseDumpedAssetsForSpriteInfo(progress);

                Logger.Info($"✅ Built sprite information lookup with {_spriteToTextureLookup.Count} mappings via CLI");
                return _spriteToTextureLookup;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in CLI-based sprite extraction: {ex.Message}");
                return _spriteToTextureLookup;
            }
            finally
            {
                // Clean up temp directory
                try
                {
                    if (Directory.Exists(TempDumpDirectory))
                    {
                        Directory.Delete(TempDumpDirectory, true);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Could not clean up temp directory: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Uses AssetRipper CLI to dump assets to temporary directory
        /// </summary>
        private async Task<bool> DumpAssetsViaCli(string peglinPath, IProgress<string>? progress)
        {
            try
            {
                // Find AssetRipper executable
                var assetRipperPath = FindAssetRipperExecutable();
                if (string.IsNullOrEmpty(assetRipperPath))
                {
                    Logger.Warning("AssetRipper executable not found");
                    return false;
                }

                var bundleDirectory = PeglinPathHelper.GetStreamingAssetsBundlePath(peglinPath);
                if (string.IsNullOrEmpty(bundleDirectory) || !Directory.Exists(bundleDirectory))
                {
                    Logger.Warning($"Bundle directory not found: {bundleDirectory}");
                    return false;
                }

                progress?.Report($"🚀 Running AssetRipper CLI on {bundleDirectory}...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = assetRipperPath,
                    Arguments = $"\"{bundleDirectory}\" -o \"{TempDumpDirectory}\" -q --export-format json",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    Logger.Warning("Failed to start AssetRipper process");
                    return false;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Logger.Warning($"AssetRipper CLI failed with exit code {process.ExitCode}");
                    Logger.Debug($"Error output: {error}");
                    return false;
                }

                progress?.Report("✅ AssetRipper CLI dump completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error running AssetRipper CLI: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Attempts to find AssetRipper executable
        /// </summary>
        private string? FindAssetRipperExecutable()
        {
            // Common locations for AssetRipper
            var possiblePaths = new[]
            {
                "AssetRipper.exe",
                "AssetRipper",
                "/usr/local/bin/AssetRipper",
                "/opt/AssetRipper/AssetRipper",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AssetRipper", "AssetRipper.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "AssetRipper", "AssetRipper.exe")
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
                catch
                {
                    // Ignore access errors
                }
            }

            // Try to find in PATH
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "where" : "which",
                    Arguments = "AssetRipper",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        var firstPath = output.Split('\n')[0].Trim();
                        if (File.Exists(firstPath))
                        {
                            return firstPath;
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        /// <summary>
        /// Parses dumped asset files to extract sprite information
        /// </summary>
        private async Task ParseDumpedAssetsForSpriteInfo(IProgress<string>? progress)
        {
            try
            {
                // Look for JSON files in the dump directory
                var jsonFiles = Directory.GetFiles(TempDumpDirectory, "*.json", SearchOption.AllDirectories);
                progress?.Report($"📁 Found {jsonFiles.Length} JSON files to analyze");

                var processedFiles = 0;
                foreach (var jsonFile in jsonFiles)
                {
                    processedFiles++;
                    if (processedFiles % 100 == 0)
                    {
                        progress?.Report($"🔍 Processed {processedFiles}/{jsonFiles.Length} files...");
                    }

                    await ProcessJsonFile(jsonFile);
                }

                progress?.Report($"✅ Processed all {jsonFiles.Length} JSON files");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error parsing dumped assets: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a single JSON file looking for sprite information
        /// </summary>
        private async Task ProcessJsonFile(string jsonFile)
        {
            try
            {
                var content = await File.ReadAllTextAsync(jsonFile);
                
                // Look for sprite information patterns in the JSON
                if (content.Contains("\"Texture\"") && content.Contains("\"Sprites\""))
                {
                    Logger.Debug($"🗺️ Found potential sprite information in: {Path.GetFileName(jsonFile)}");
                    await ExtractSpriteInfoFromJson(content, jsonFile);
                }
                
                // Also look for individual sprite objects that might reference textures
                if (content.Contains("\"m_PathID\"") && (content.Contains("sprite") || content.Contains("Sprite")))
                {
                    await ExtractSpriteReferencesFromJson(content, jsonFile);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error processing JSON file {jsonFile}: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts sprite information from JSON content that matches SpriteInformationObject pattern
        /// </summary>
        private async Task ExtractSpriteInfoFromJson(string content, string fileName)
        {
            try
            {
                using var document = JsonDocument.Parse(content);
                var root = document.RootElement;

                // Look for Texture property
                if (root.TryGetProperty("Texture", out var textureElement))
                {
                    var texturePathId = ExtractPathIdFromJsonElement(textureElement);
                    if (texturePathId == 0) return;

                    Logger.Debug($"🎯 Found texture PathID {texturePathId} in {Path.GetFileName(fileName)}");

                    // Look for Sprites property
                    if (root.TryGetProperty("Sprites", out var spritesElement))
                    {
                        await ProcessSpritesArrayFromJson(spritesElement, texturePathId, fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error extracting sprite info from JSON: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a Sprites array from JSON to extract sprite PathID mappings
        /// </summary>
        private async Task ProcessSpritesArrayFromJson(JsonElement spritesElement, long texturePathId, string fileName)
        {
            try
            {
                if (spritesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var spriteItem in spritesElement.EnumerateArray())
                    {
                        // Look for Key property (sprite PathID)
                        if (spriteItem.TryGetProperty("Key", out var keyElement))
                        {
                            var spritePathId = ExtractPathIdFromJsonElement(keyElement);
                            if (spritePathId != 0)
                            {
                                _spriteToTextureLookup[spritePathId] = texturePathId;
                                Logger.Debug($"🗺️ Mapped sprite PathID {spritePathId} -> texture PathID {texturePathId} from {Path.GetFileName(fileName)}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error processing sprites array: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts sprite references from JSON content for correlation analysis
        /// </summary>
        private async Task ExtractSpriteReferencesFromJson(string content, string fileName)
        {
            try
            {
                // Use regex to find m_PathID values in sprite contexts
                var pathIdPattern = @"""m_PathID"":\s*(\d+)";
                var matches = Regex.Matches(content, pathIdPattern);

                foreach (Match match in matches)
                {
                    if (long.TryParse(match.Groups[1].Value, out var pathId) && pathId > 0)
                    {
                        // This is just for logging - we collect all PathIDs that might be sprites
                        Logger.Debug($"📎 Found PathID reference {pathId} in {Path.GetFileName(fileName)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error extracting sprite references: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts PathID from a JSON element that represents a Unity reference
        /// </summary>
        private long ExtractPathIdFromJsonElement(JsonElement element)
        {
            try
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty("m_PathID", out var pathIdElement) && 
                        pathIdElement.TryGetInt64(out var pathId))
                    {
                        return pathId;
                    }
                    
                    if (element.TryGetProperty("PathID", out var pathIdElement2) && 
                        pathIdElement2.TryGetInt64(out var pathId2))
                    {
                        return pathId2;
                    }
                }
                else if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var directPathId))
                {
                    return directPathId;
                }
            }
            catch
            {
                // Ignore extraction errors
            }

            return 0;
        }

        /// <summary>
        /// Gets the built sprite to texture lookup table
        /// </summary>
        public Dictionary<long, long> GetSpriteToTextureLookup()
        {
            return new Dictionary<long, long>(_spriteToTextureLookup);
        }
    }
}
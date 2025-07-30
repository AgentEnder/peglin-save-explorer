using System.CommandLine;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Extractors;
using peglin_save_explorer.Core;

namespace peglin_save_explorer.Commands
{
    public class ExtractRelicsCommand : ICommand
    {
        public Command CreateCommand()
        {
            var peglinPathOption = new Option<string>(
                new[] { "--peglin-path", "-p" },
                description: "Path to Peglin installation directory");

            var outputOption = new Option<string>(
                new[] { "--output", "-o" },
                description: "Output file path for relic extraction",
                getDefaultValue: () => "relics.json");

            var command = new Command("extract-relics", "Extract relics using AssetRipper (recommended method)")
            {
                peglinPathOption,
                outputOption
            };

            command.SetHandler((string peglinPath, string output) => Execute(peglinPath, output), 
                peglinPathOption, outputOption);

            return command;
        }

        private static void Execute(string? peglinPath, string? outputPath)
        {
            try
            {
                var configManager = new ConfigurationManager();
                
                // Use the smart path detection that tries override, config, then auto-detection
                peglinPath = configManager.GetEffectivePeglinPath(peglinPath);

                if (string.IsNullOrEmpty(peglinPath) || !Directory.Exists(peglinPath))
                {
                    Logger.Error("Peglin installation not found.");
                    Logger.Info("Searched common locations:");
                    var detectedPaths = configManager.DetectPeglinInstallations();
                    if (detectedPaths.Count > 0)
                    {
                        Logger.Info("  Found installations at:");
                        foreach (var path in detectedPaths)
                        {
                            Logger.Info($"    {path}");
                        }
                        Logger.Info("  But none contain valid Peglin data.");
                    }
                    else
                    {
                        Logger.Info("  No Peglin installations detected in common Steam locations.");
                    }
                    Logger.Info("Use --peglin-path to specify the path manually.");
                    return;
                }

                var bundlePath = Path.Combine(peglinPath, "Peglin_Data", "StreamingAssets", "aa", "StandaloneWindows64");
                if (!Directory.Exists(bundlePath))
                {
                    DisplayHelper.PrintError($"Bundle directory not found at: {bundlePath}");
                    return;
                }

                // Set default output path if not provided
                if (string.IsNullOrEmpty(outputPath))
                {
                    outputPath = "relics";
                }

                // Determine cache file path
                var cacheFileName = "relics-cache.json";
                var outputFile = outputPath.EndsWith(".json") ? outputPath : Path.Combine(outputPath, cacheFileName);
                
                // Create output directory if needed
                var outputDir = Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                Logger.Info($"AssetRipper relic extraction");
                Logger.Debug($"Bundle directory: {bundlePath}");
                Logger.Debug($"Output file: {outputFile}");
                Logger.Info("");

                var extractor = new AssetRipperRelicExtractor(null);
                
                // Try to load existing cache first
                if (File.Exists(outputFile))
                {
                    Logger.Debug("Loading existing relic cache...");
                    extractor.LoadRelicCache(outputFile);
                }

                var allRelics = new Dictionary<string, AssetRipperRelicExtractor.RelicData>();

                // Process all bundle files
                var bundleFiles = Directory.GetFiles(bundlePath, "*.bundle", SearchOption.AllDirectories);
                foreach (var bundleFile in bundleFiles)
                {
                    Logger.Verbose($"Processing: {Path.GetFileName(bundleFile)}");
                    var relics = extractor.ExtractRelics(bundleFile);
                    
                    foreach (var kvp in relics)
                    {
                        allRelics[kvp.Key] = kvp.Value;
                    }
                }

                // Save results
                extractor.SaveRelicCache(outputFile);

                Logger.Info($"\n✓ AssetRipper relic extraction completed!");
                Logger.Info($"Extracted {allRelics.Count} relics");
                Logger.Info($"Results saved to: {outputFile}");
            }
            catch (Exception ex)
            {
                DisplayHelper.PrintError($"Error in AssetRipper relic extraction: {ex.Message}");
                if (ex.InnerException != null)
                {
                    DisplayHelper.PrintError($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }
    }
}
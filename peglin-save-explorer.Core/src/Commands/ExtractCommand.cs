using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using peglin_save_explorer.Core;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Commands
{
    /// <summary>
    /// Unified command to extract all Peglin game data
    /// Uses hash-based caching and provides progress feedback with spinners
    /// </summary>
    public class ExtractCommand : ICommand
    {
        public Command CreateCommand()
        {
            var command = new Command("extract", "Extract all Peglin game data with intelligent caching");

            var peglinPathOption = new Option<string?>(
                name: "--peglin-path",
                description: "Path to Peglin installation directory (auto-detected if not specified)"
            );

            var forceOption = new Option<bool>(
                name: "--force",
                description: "Force re-extraction even if cache is valid"
            );

            var verboseOption = new Option<bool>(
                name: "--verbose",
                description: "Enable verbose logging output"
            );

            var statusOption = new Option<bool>(
                name: "--status",
                description: "Show extraction status and exit"
            );

            var clearCacheOption = new Option<bool>(
                name: "--clear-cache",
                description: "Clear all extraction caches and exit"
            );

            var noSpinnerOption = new Option<bool>(
                name: "--no-spinner",
                description: "Disable progress spinner (useful for scripts)"
            );

            command.AddOption(peglinPathOption);
            command.AddOption(forceOption);
            command.AddOption(verboseOption);
            command.AddOption(statusOption);
            command.AddOption(clearCacheOption);
            command.AddOption(noSpinnerOption);

            command.SetHandler(Execute, 
                peglinPathOption, 
                forceOption, 
                verboseOption, 
                statusOption,
                clearCacheOption,
                noSpinnerOption);

            return command;
        }

        private void Execute(
            string? peglinPath, 
            bool force, 
            bool verbose, 
            bool status,
            bool clearCache,
            bool noSpinner)
        {
            try
            {
                // Set up logging
                if (verbose)
                {
                    Logger.SetLogLevel(LogLevel.Debug);
                }

                // Handle cache operations first
                if (clearCache)
                {
                    PeglinDataExtractor.ClearAllCaches();
                    return;
                }

                if (status)
                {
                    PeglinDataExtractor.ShowExtractionStatus();
                    return;
                }

                // Always extract all data (no more type selection)
                var extractionType = PeglinDataExtractor.ExtractionType.All;

                // Auto-detect Peglin installation path if not provided
                if (string.IsNullOrEmpty(peglinPath))
                {
                    var configManager = new ConfigurationManager();
                    var detectedPaths = configManager.DetectPeglinInstallations();
                    if (!detectedPaths.Any())
                    {
                        Console.WriteLine("❌ No Peglin installation found. Please specify --peglin-path.");
                        Console.WriteLine();
                        Console.WriteLine("Searched in common Steam/GOG installation directories.");
                        Console.WriteLine("You can also manually specify the path with:");
                        Console.WriteLine("  peglin-save-explorer extract --peglin-path \"/path/to/peglin\"");
                        return;
                    }

                    peglinPath = detectedPaths.First();
                    Console.WriteLine($"📁 Auto-detected Peglin installation: {peglinPath}");
                }

                // Validate Peglin installation
                if (!Directory.Exists(peglinPath))
                {
                    Console.WriteLine($"❌ Peglin installation directory not found: {peglinPath}");
                    return;
                }

                // Show what we're about to do
                Console.WriteLine();
                Console.WriteLine("🎮 Peglin Data Extraction");
                Console.WriteLine($"   Source: {peglinPath}");
                Console.WriteLine($"   Output: {PeglinDataExtractor.GetExtractionCacheDirectory()}");
                Console.WriteLine($"   Force: {(force ? "Yes" : "No")}");
                Console.WriteLine();

                // Perform extraction
                var result = PeglinDataExtractor.ExtractPeglinData(
                    peglinPath, 
                    extractionType, 
                    force,
                    !noSpinner);

                if (!result.Success)
                {
                    Console.WriteLine($"❌ Extraction failed: {result.ErrorMessage}");
                    return;
                }

                // Show results
                Console.WriteLine();
                if (result.UsedCache)
                {
                    Console.WriteLine("✅ All data is up to date (used existing cache)");
                }
                else
                {
                    Console.WriteLine($"✅ Extraction completed successfully in {result.Duration.TotalSeconds:F1}s");
                }

                if (result.ExtractedCounts.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine("📊 Extraction Summary:");
                    foreach (var kvp in result.ExtractedCounts.OrderBy(k => k.Key))
                    {
                        var icon = kvp.Key switch
                        {
                            "Relics" => "🔮",
                            "Sprites" => "🎨",
                            "RelicSprites" => "🖼️",
                            "EnemySprites" => "👹",
                            "Classes" => "👤",
                            "EntityCorrelations" => "🔗",
                            "RelicCorrelations" => "🔮🔗",
                            "EnemyCorrelations" => "👹🔗",
                            "UncorrelatedSprites" => "📦",
                            "GameObjects" => "🎮",
                            "OrbGameObjects" => "🔮",
                            _ => "📊"
                        };
                        Console.WriteLine($"   {icon} {kvp.Key}: {kvp.Value}");
                    }
                }

                Console.WriteLine();
                Console.WriteLine("📁 Extracted Data Location:");
                Console.WriteLine($"   {PeglinDataExtractor.GetExtractionCacheDirectory()}");
                
                // Show specific subdirectories based on what was extracted
                if (result.ExtractedCounts.ContainsKey("Sprites") && result.ExtractedCounts["Sprites"] > 0)
                {
                    Console.WriteLine($"   ├── extracted-data/sprites/relics/ (relic sprites)");
                    Console.WriteLine($"   └── extracted-data/sprites/enemies/ (enemy sprites)");
                }

                Console.WriteLine();
                Console.WriteLine("💡 Next steps:");
                Console.WriteLine("   - Use 'peglin-save-explorer web' to explore data in the web interface");
                Console.WriteLine("   - Use 'peglin-save-explorer extract --status' to check extraction status");
                Console.WriteLine("   - Data is cached and won't be re-extracted unless Peglin is updated");

                if (!result.UsedCache)
                {
                    Console.WriteLine();
                    Console.WriteLine("🔄 The extraction cache will automatically detect when Peglin is updated");
                    Console.WriteLine("   and re-extract data as needed. You can also force re-extraction with --force");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Extraction command failed: {ex.Message}");
                if (verbose)
                {
                    Logger.Error($"Stack trace: {ex.StackTrace}");
                }
                Console.WriteLine();
                Console.WriteLine("🔧 Troubleshooting:");
                Console.WriteLine("   - Ensure Peglin is not running during extraction");
                Console.WriteLine("   - Try --force to bypass cache validation");
                Console.WriteLine("   - Use --verbose for detailed error information");
                Console.WriteLine("   - Check that the Peglin installation is complete and valid");
                Console.WriteLine("   - Use --clear-cache to reset all caches if needed");
            }
        }
    }
}
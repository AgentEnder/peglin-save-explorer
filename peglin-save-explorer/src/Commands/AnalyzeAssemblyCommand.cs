using System.CommandLine;
using System.Reflection;
using peglin_save_explorer.Core;
using peglin_save_explorer.Data;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Commands
{
    public class AnalyzeAssemblyCommand : ICommand
    {
        public Command CreateCommand()
        {
            var command = new Command("analyze-assembly", "Analyze Peglin assembly for game data mappings");

            command.SetHandler(() => Execute());
            return command;
        }

        private static void Execute()
        {
            try
            {
                Console.WriteLine("Starting assembly analysis...");

                // Get the Peglin path using the config manager
                var configManager = new ConfigurationManager();
                var peglinPath = configManager.GetEffectivePeglinPath();

                if (string.IsNullOrEmpty(peglinPath) || !Directory.Exists(peglinPath))
                {
                    Console.WriteLine("Peglin path not configured or directory doesn't exist.");
                    Console.WriteLine("Please configure the Peglin path using the interactive mode settings.");
                    return;
                }

                Console.WriteLine($"Using Peglin path: {peglinPath}");

                // Analyze the assembly
                var result = AssemblyAnalyzer.AnalyzePeglinAssembly(peglinPath);

                Console.WriteLine("\n=== Assembly Analysis Results ===");
                Console.WriteLine($"Analysis successful: {result.Success}");

                if (result.Messages.Any())
                {
                    Console.WriteLine("\nAnalysis Messages:");
                    foreach (var message in result.Messages)
                    {
                        Console.WriteLine($"  {message}");
                    }
                }

                Console.WriteLine($"\nMappings found:");
                Console.WriteLine($"  Relics: {result.RelicMappings.Count}");
                Console.WriteLine($"  Rooms: {result.RoomMappings.Count}");
                Console.WriteLine($"  Bosses: {result.BossMappings.Count}");
                Console.WriteLine($"  Status Effects: {result.StatusEffectMappings.Count}");
                Console.WriteLine($"  Slime Pegs: {result.SlimePegMappings.Count}");
                Console.WriteLine($"  Character Classes: {result.CharacterClassMappings.Count}");

                if (result.RoomMappings.Any())
                {
                    Console.WriteLine("\nRoom mappings:");
                    foreach (var mapping in result.RoomMappings.OrderBy(x => x.Key))
                    {
                        Console.WriteLine($"  {mapping.Key}: {mapping.Value}");
                    }
                }

                if (result.BossMappings.Any())
                {
                    Console.WriteLine("\nBoss mappings:");
                    foreach (var mapping in result.BossMappings.OrderBy(x => x.Key))
                    {
                        Console.WriteLine($"  {mapping.Key}: {mapping.Value}");
                    }
                }

                if (result.StatusEffectMappings.Any())
                {
                    Console.WriteLine("\nStatus effect mappings:");
                    foreach (var mapping in result.StatusEffectMappings.OrderBy(x => x.Key))
                    {
                        Console.WriteLine($"  {mapping.Key}: {mapping.Value}");
                    }
                }

                if (result.CharacterClassMappings.Any())
                {
                    Console.WriteLine("\nCharacter class mappings:");
                    foreach (var mapping in result.CharacterClassMappings.OrderBy(x => x.Key))
                    {
                        Console.WriteLine($"  {mapping.Key}: {mapping.Value}");
                    }
                }

                // Analyze Stats types for orb data debugging
                Console.WriteLine("\n=== Analyzing Stats Types for Orb Data ===");
                try
                {
                    var assemblyPath = PeglinPathHelper.GetAssemblyPath(peglinPath);
                    if (string.IsNullOrEmpty(assemblyPath))
                    {
                        Logger.Error($"Could not find Assembly-CSharp.dll in: {peglinPath}");
                        return;
                    }
                    var assembly = Assembly.LoadFrom(assemblyPath);
                    
                    // Generate detailed stats analysis
                    var statsAnalysisPath = "peglin-stats-analysis.txt";
                    AssemblyAnalyzer.AnalyzeStatsTypes(assembly, statsAnalysisPath);
                    Console.WriteLine($"✓ Detailed stats types analysis written to: {statsAnalysisPath}");
                    
                    // Quick summary for console
                    var allTypes = assembly.GetTypes();
                    var statsTypes = allTypes.Where(t => 
                        t.Name.Contains("Stats", StringComparison.OrdinalIgnoreCase) ||
                        t.FullName?.Contains("Stats", StringComparison.OrdinalIgnoreCase) == true
                    ).ToArray();
                    
                    var orbTypes = allTypes.Where(t => 
                        t.Name.Contains("Orb", StringComparison.OrdinalIgnoreCase) ||
                        t.FullName?.Contains("Orb", StringComparison.OrdinalIgnoreCase) == true
                    ).ToArray();
                    
                    Console.WriteLine($"Stats-related types found: {statsTypes.Length}");
                    Console.WriteLine($"Orb-related types found: {orbTypes.Length}");
                    
                    // Show a few key stats types in console
                    var keyStatsTypes = statsTypes.Where(t => 
                        t.Name.Contains("RunStats", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Contains("OrbPlay", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Contains("PlayData", StringComparison.OrdinalIgnoreCase)
                    ).Take(5);
                    
                    if (keyStatsTypes.Any())
                    {
                        Console.WriteLine("\nKey Stats Types (likely related to orb data):");
                        foreach (var type in keyStatsTypes)
                        {
                            Console.WriteLine($"  - {type.FullName}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error analyzing stats types: {ex.Message}");
                }

                // List all enum types for debugging
                Console.WriteLine("\n=== Debug: All Enum Types Found ===");
                try
                {
                    var assemblyPath = PeglinPathHelper.GetAssemblyPath(peglinPath);
                    if (string.IsNullOrEmpty(assemblyPath))
                    {
                        Logger.Error($"Could not find Assembly-CSharp.dll in: {peglinPath}");
                        return;
                    }
                    var assembly = Assembly.LoadFrom(assemblyPath);

                    var allEnums = assembly.GetTypes()
                        .Where(t => t.IsEnum)
                        .OrderBy(t => t.FullName ?? t.Name)
                        .ToList();

                    Console.WriteLine($"Total enums found: {allEnums.Count}");

                    // Show enums that might be relevant to rooms/bosses/status
                    var relevantEnums = allEnums.Where(t =>
                        (t.FullName ?? t.Name).ToLower().Contains("room") ||
                        (t.FullName ?? t.Name).ToLower().Contains("boss") ||
                        (t.FullName ?? t.Name).ToLower().Contains("status") ||
                        (t.FullName ?? t.Name).ToLower().Contains("world") ||
                        (t.FullName ?? t.Name).ToLower().Contains("map") ||
                        (t.FullName ?? t.Name).ToLower().Contains("stats") ||
                        (t.FullName ?? t.Name).ToLower().Contains("scene"))
                        .ToList();

                    Console.WriteLine($"\nRelevant enums ({relevantEnums.Count}):");
                    foreach (var enumType in relevantEnums)
                    {
                        var values = Enum.GetValues(enumType);
                        Console.WriteLine($"  {enumType.FullName ?? enumType.Name} ({values.Length} values)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error listing enums: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during assembly analysis: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}

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

                // Analyze cruciball-related types and fields
                Console.WriteLine("\n=== Analyzing Cruciball-Related Data Structures ===");
                try
                {
                    var assemblyPath = PeglinPathHelper.GetAssemblyPath(peglinPath);
                    if (string.IsNullOrEmpty(assemblyPath))
                    {
                        Logger.Error($"Could not find Assembly-CSharp.dll in: {peglinPath}");
                        return;
                    }
                    var assembly = Assembly.LoadFrom(assemblyPath);

                    var allTypes = assembly.GetTypes();
                    
                    // Find types that might contain cruciball data
                    var cruciballTypes = allTypes.Where(t =>
                        (t.Name.Contains("Cruci", StringComparison.OrdinalIgnoreCase) ||
                         t.FullName?.Contains("Cruci", StringComparison.OrdinalIgnoreCase) == true ||
                         t.Name.Contains("Ball", StringComparison.OrdinalIgnoreCase) ||
                         t.FullName?.Contains("Ball", StringComparison.OrdinalIgnoreCase) == true) &&
                        !t.Name.Contains("Callback") && !t.Name.Contains("Football"))
                        .OrderBy(t => t.FullName ?? t.Name)
                        .ToList();

                    Console.WriteLine($"Cruciball-related types found: {cruciballTypes.Count}");
                    foreach (var type in cruciballTypes)
                    {
                        Console.WriteLine($"  - {type.FullName ?? type.Name}");
                        
                        // Show fields and properties
                        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        
                        if (fields.Length > 0)
                        {
                            Console.WriteLine($"    Fields:");
                            foreach (var field in fields.Take(10))
                            {
                                Console.WriteLine($"      {field.FieldType.Name} {field.Name}");
                            }
                        }
                        
                        if (properties.Length > 0)
                        {
                            Console.WriteLine($"    Properties:");
                            foreach (var prop in properties.Take(10))
                            {
                                Console.WriteLine($"      {prop.PropertyType.Name} {prop.Name}");
                            }
                        }
                        Console.WriteLine();
                    }

                    // Search for types containing cruciball-related fields
                    Console.WriteLine("\n=== Types Containing Cruciball Fields ===");
                    var typesWithCruciballFields = new List<(Type type, List<string> fields)>();
                    
                    foreach (var type in allTypes.Where(t => t.IsClass && !t.IsAbstract))
                    {
                        try
                        {
                            var cruciballFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                .Where(f => f.Name.Contains("cruci", StringComparison.OrdinalIgnoreCase) ||
                                           f.Name.Contains("ball", StringComparison.OrdinalIgnoreCase))
                                .Select(f => $"{f.FieldType.Name} {f.Name}")
                                .ToList();
                                
                            var cruciballProps = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                .Where(p => p.Name.Contains("cruci", StringComparison.OrdinalIgnoreCase) ||
                                           p.Name.Contains("ball", StringComparison.OrdinalIgnoreCase))
                                .Select(p => $"{p.PropertyType.Name} {p.Name}")
                                .ToList();
                                
                            var allCruciballMembers = cruciballFields.Concat(cruciballProps).ToList();
                            
                            if (allCruciballMembers.Any())
                            {
                                typesWithCruciballFields.Add((type, allCruciballMembers));
                            }
                        }
                        catch (Exception)
                        {
                            // Skip types that can't be analyzed
                        }
                    }
                    
                    Console.WriteLine($"Types with cruciball fields found: {typesWithCruciballFields.Count}");
                    foreach (var (type, fields) in typesWithCruciballFields.Take(20))
                    {
                        Console.WriteLine($"  {type.FullName ?? type.Name}:");
                        foreach (var field in fields)
                        {
                            Console.WriteLine($"    - {field}");
                        }
                        Console.WriteLine();
                    }

                    // Analyze save data types more thoroughly
                    Console.WriteLine("\n=== Save Data Types Analysis ===");
                    var saveDataTypes = allTypes.Where(t =>
                        t.Name.Contains("Save", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Contains("Data", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Contains("Stats", StringComparison.OrdinalIgnoreCase))
                        .Where(t => t.IsClass && !t.IsAbstract)
                        .OrderBy(t => t.FullName ?? t.Name)
                        .ToList();
                        
                    Console.WriteLine($"Save/Data/Stats types found: {saveDataTypes.Count}");
                    foreach (var type in saveDataTypes.Take(30))
                    {
                        Console.WriteLine($"  - {type.FullName ?? type.Name}");
                        
                        // Check if this type has cruciball-related members
                        try
                        {
                            var hasCruciball = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                .Any(f => f.Name.Contains("cruci", StringComparison.OrdinalIgnoreCase) ||
                                         f.Name.Contains("ball", StringComparison.OrdinalIgnoreCase)) ||
                                type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                .Any(p => p.Name.Contains("cruci", StringComparison.OrdinalIgnoreCase) ||
                                         p.Name.Contains("ball", StringComparison.OrdinalIgnoreCase));
                                         
                            if (hasCruciball)
                            {
                                Console.WriteLine($"    *** HAS CRUCIBALL FIELDS ***");
                            }
                        }
                        catch (Exception)
                        {
                            // Skip types that can't be analyzed
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error analyzing cruciball data: {ex.Message}");
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer
{
    /// <summary>
    /// Shared assembly analysis functionality for extracting game data from Peglin's Assembly-CSharp.dll
    /// </summary>
    public static class AssemblyAnalyzer
    {
        private static bool _unityMockingSetup = false;

        /// <summary>
        /// Represents the result of analyzing the Peglin assembly
        /// </summary>
        public class AnalysisResult
        {
            public Dictionary<int, string> RelicMappings { get; set; } = new();
            public Dictionary<int, string> RoomMappings { get; set; } = new();
            public Dictionary<int, string> BossMappings { get; set; } = new();
            public Dictionary<int, string> StatusEffectMappings { get; set; } = new();
            public Dictionary<int, string> SlimePegMappings { get; set; } = new();
            public Dictionary<int, string> CharacterClassMappings { get; set; } = new();

            public Assembly? LoadedAssembly { get; set; }
            public string? SourcePath { get; set; }
            public List<string> Messages { get; set; } = new();
            public List<string> Errors { get; set; } = new();
            public bool Success { get; set; }

            public void AddMessage(string message)
            {
                Messages.Add(message);
                Logger.Debug($"AssemblyAnalyzer: {message}");
            }

            public void AddError(string error)
            {
                Errors.Add(error);
                Logger.Error($"AssemblyAnalyzer: {error}");
            }
        }

        /// <summary>
        /// Loads and analyzes the Peglin assembly from the given path
        /// </summary>
        public static AnalysisResult AnalyzePeglinAssembly(string? peglinPath)
        {
            var result = new AnalysisResult();

            if (string.IsNullOrEmpty(peglinPath))
            {
                result.AddError("No Peglin path provided");
                return result;
            }

            var assemblyPath = Path.Combine(peglinPath, "Peglin_Data", "Managed", "Assembly-CSharp.dll");
            if (!File.Exists(assemblyPath))
            {
                result.AddError($"Assembly not found at: {assemblyPath}");
                return result;
            }

            try
            {
                result.AddMessage($"Loading assembly from: {assemblyPath}");
                result.SourcePath = assemblyPath;

                // Set up Unity mocking before loading assembly
                SetupUnityMocking();

                var assembly = Assembly.LoadFrom(assemblyPath);
                result.LoadedAssembly = assembly;
                result.AddMessage("Successfully loaded assembly");

                // Extract all game data mappings
                ExtractRelicMappings(assembly, result);
                ExtractRoomMappings(assembly, result);
                ExtractBossMappings(assembly, result);
                ExtractStatusEffectMappings(assembly, result);
                ExtractSlimePegMappings(assembly, result);
                ExtractCharacterClassMappings(assembly, result);

                result.Success = true;
                result.AddMessage($"Analysis complete - Relics: {result.RelicMappings.Count}, Rooms: {result.RoomMappings.Count}, Bosses: {result.BossMappings.Count}, Status Effects: {result.StatusEffectMappings.Count}, Slimes: {result.SlimePegMappings.Count}, Classes: {result.CharacterClassMappings.Count}");
            }
            catch (Exception ex)
            {
                result.AddError($"Error analyzing assembly: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Writes detailed assembly analysis to a file (for debugging/research purposes)
        /// </summary>
        public static void WriteDetailedAnalysisToFile(Assembly assembly, string outputPath)
        {
            using var writer = new StreamWriter(outputPath);

            writer.WriteLine("PEGLIN ASSEMBLY ANALYSIS");
            writer.WriteLine("=" + new string('=', 50));
            writer.WriteLine($"Assembly: {assembly.FullName}");
            writer.WriteLine($"Location: {assembly.Location}");
            writer.WriteLine($"Analysis Time: {DateTime.Now}");
            writer.WriteLine();

            // Get all types
            var types = assembly.GetTypes();
            var classes = types.Where(t => t.IsClass && !t.IsAbstract).ToArray();
            var enums = types.Where(t => t.IsEnum).ToArray();
            var interfaces = types.Where(t => t.IsInterface).ToArray();

            writer.WriteLine($"SUMMARY:");
            writer.WriteLine($"- Total Types: {types.Length}");
            writer.WriteLine($"- Classes: {classes.Length}");
            writer.WriteLine($"- Enums: {enums.Length}");
            writer.WriteLine($"- Interfaces: {interfaces.Length}");
            writer.WriteLine();

            // Analyze RelicManager in detail
            AnalyzeRelicManagerDetailed(assembly, writer);

            // Analyze all enums
            AnalyzeEnumsDetailed(assembly, writer, enums);

            // Look for other interesting types
            AnalyzeOtherGameTypes(assembly, writer, classes);
        }

        private static void SetupUnityMocking()
        {
            if (_unityMockingSetup) return;

            try
            {
                Logger.Debug("AssemblyAnalyzer: Setting up Unity mocking...");

                // Set up assembly resolve handler for Unity dependencies
                AppDomain.CurrentDomain.AssemblyResolve += MockUnityAssemblyResolve;

                _unityMockingSetup = true;
                Logger.Debug("AssemblyAnalyzer: Unity mocking setup complete");
            }
            catch (Exception ex)
            {
                Logger.Warning($"AssemblyAnalyzer: Error setting up Unity mocking: {ex.Message}");
            }
        }

        private static Assembly? MockUnityAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name).Name;

            // Create mock assemblies for Unity dependencies
            if (assemblyName != null)
            {
                if (assemblyName.StartsWith("UnityEngine"))
                {
                    Logger.Verbose($"AssemblyAnalyzer: Mocking Unity assembly: {assemblyName}");
                    return null; // Return null to handle gracefully
                }
                else if (assemblyName.StartsWith("Unity."))
                {
                    Logger.Verbose($"AssemblyAnalyzer: Mocking Unity module: {assemblyName}");
                    return null; // Return null to handle gracefully
                }
            }

            return null;
        }

        private static void ExtractRelicMappings(Assembly assembly, AnalysisResult result)
        {
            try
            {
                result.AddMessage("Extracting relic mappings...");

                // First try the RelicEffect enum approach (more reliable for getting all relics)
                var relicEffectType = assembly.GetTypes().FirstOrDefault(t => t.Name == "RelicEffect" && t.IsEnum);

                if (relicEffectType != null)
                {
                    result.AddMessage("Found RelicEffect enum, extracting values...");

                    var enumValues = Enum.GetValues(relicEffectType);
                    foreach (var value in enumValues)
                    {
                        var intValue = Convert.ToInt32(value);
                        var enumName = value.ToString();
                        var cleanName = CleanupName(enumName ?? "Unknown");

                        result.RelicMappings[intValue] = cleanName;
                    }

                    result.AddMessage($"Successfully loaded {result.RelicMappings.Count} relic mappings from RelicEffect enum");
                    return; // Use enum approach as primary source
                }

                // Fallback: Try RelicManager instantiation approach if enum not found
                result.AddMessage("RelicEffect enum not found, falling back to RelicManager approach...");
                var relicManagerType = assembly.GetTypes().FirstOrDefault(t => t.FullName == "Relics.RelicManager");

                if (relicManagerType != null)
                {
                    result.AddMessage("Found Relics.RelicManager class");

                    var relicMappings = TryExtractRelicsFromManager(relicManagerType, result);
                    if (relicMappings.Any())
                    {
                        result.RelicMappings = relicMappings;
                        result.AddMessage($"Successfully extracted {relicMappings.Count} relic mappings from RelicManager");
                        return;
                    }
                }

                result.AddError("No RelicEffect enum or RelicManager found");
            }
            catch (Exception ex)
            {
                result.AddError($"Error extracting relic mappings: {ex.Message}");
            }
        }

        private static Dictionary<int, string> TryExtractRelicsFromManager(Type relicManagerType, AnalysisResult result)
        {
            var mappings = new Dictionary<int, string>();

            try
            {
                result.AddMessage("Attempting RelicManager instantiation with Unity mocking...");

                object? relicManagerInstance = null;

                // Try multiple instantiation approaches
                try
                {
                    relicManagerInstance = Activator.CreateInstance(relicManagerType);
                    result.AddMessage("Created RelicManager via default constructor");
                }
                catch (Exception ex1)
                {
                    result.AddMessage($"Default constructor failed: {ex1.Message}");

                    try
                    {
                        var constructor = relicManagerType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                        if (constructor != null)
                        {
                            relicManagerInstance = constructor.Invoke(null);
                            result.AddMessage("Created RelicManager via reflection constructor");
                        }
                    }
                    catch (Exception ex2)
                    {
                        result.AddMessage($"Reflection constructor failed: {ex2.Message}");
                    }
                }

                if (relicManagerInstance != null)
                {
                    result.AddMessage("Successfully created RelicManager instance");

                    // Try to call LoadRelicData method
                    var loadRelicDataMethod = relicManagerType.GetMethod("LoadRelicData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (loadRelicDataMethod != null)
                    {
                        result.AddMessage("Found LoadRelicData method, attempting safe call...");

                        try
                        {
                            loadRelicDataMethod.Invoke(relicManagerInstance, null);
                            result.AddMessage("Successfully called LoadRelicData");
                        }
                        catch (Exception ex)
                        {
                            result.AddMessage($"LoadRelicData failed (expected due to Unity dependencies): {ex.GetType().Name}");
                        }
                    }

                    // Try to access globalRelics
                    var globalRelicsProperty = relicManagerType.GetProperty("globalRelics", BindingFlags.Public | BindingFlags.Instance);
                    var globalRelicsField = relicManagerType.GetField("globalRelics", BindingFlags.Public | BindingFlags.Instance);

                    object? globalRelicsValue = null;

                    if (globalRelicsProperty != null && globalRelicsProperty.CanRead)
                    {
                        globalRelicsValue = globalRelicsProperty.GetValue(relicManagerInstance);
                    }
                    else if (globalRelicsField != null)
                    {
                        globalRelicsValue = globalRelicsField.GetValue(relicManagerInstance);
                    }

                    if (globalRelicsValue != null)
                    {
                        result.AddMessage($"Got globalRelics value of type: {globalRelicsValue.GetType().FullName}");
                        mappings = ExtractRelicDataFromRelicSet(globalRelicsValue, result);
                    }
                    else
                    {
                        result.AddMessage("globalRelics value is null - data may not be initialized");
                    }
                }
                else
                {
                    result.AddMessage("Failed to create RelicManager instance");
                }
            }
            catch (Exception ex)
            {
                result.AddMessage($"Error in RelicManager extraction: {ex.Message}");
            }

            return mappings;
        }

        private static Dictionary<int, string> ExtractRelicDataFromRelicSet(object globalRelicsValue, AnalysisResult result)
        {
            var mappings = new Dictionary<int, string>();

            try
            {
                // Try to access the relics property of RelicSet
                var relicsProperty = globalRelicsValue.GetType().GetProperty("relics");
                if (relicsProperty != null && relicsProperty.CanRead)
                {
                    var relicsList = relicsProperty.GetValue(globalRelicsValue);
                    if (relicsList != null)
                    {
                        result.AddMessage($"Got relics list of type: {relicsList.GetType().FullName}");
                        mappings = ExtractRelicDataFromList(relicsList, result);
                    }
                    else
                    {
                        result.AddMessage("Relics list is null");
                    }
                }
                else
                {
                    result.AddMessage("Could not find 'relics' property on RelicSet");

                    // Try to find any list/collection properties
                    var allProperties = globalRelicsValue.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in allProperties)
                    {
                        if (prop.PropertyType.IsGenericType &&
                            (prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>) ||
                             prop.PropertyType.GetGenericTypeDefinition() == typeof(IList<>) ||
                             prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)))
                        {
                            result.AddMessage($"Found list property {prop.Name}, trying to access...");
                            try
                            {
                                var listValue = prop.GetValue(globalRelicsValue);
                                if (listValue != null)
                                {
                                    var extractedMappings = ExtractRelicDataFromList(listValue, result);
                                    if (extractedMappings.Any())
                                    {
                                        mappings = extractedMappings;
                                        break;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                result.AddMessage($"Error accessing {prop.Name}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Error extracting relic data from RelicSet: {ex.Message}");
            }

            return mappings;
        }

        private static Dictionary<int, string> ExtractRelicDataFromList(object relicsList, AnalysisResult result)
        {
            var mappings = new Dictionary<int, string>();

            try
            {
                if (relicsList is System.Collections.IEnumerable enumerable)
                {
                    result.AddMessage("Processing relics list...");

                    int count = 0;
                    int successCount = 0;

                    foreach (var relic in enumerable)
                    {
                        if (relic != null)
                        {
                            try
                            {
                                var relicType = relic.GetType();

                                // Look for ID property/field
                                var idProperty = relicType.GetProperty("id") ??
                                               relicType.GetProperty("ID") ??
                                               relicType.GetProperty("Id") ??
                                               relicType.GetProperty("relicId") ??
                                               relicType.GetProperty("RelicId");

                                var idField = relicType.GetField("id") ??
                                            relicType.GetField("ID") ??
                                            relicType.GetField("Id") ??
                                            relicType.GetField("relicId") ??
                                            relicType.GetField("RelicId");

                                // Look for name property/field
                                var nameProperty = relicType.GetProperty("name") ??
                                                 relicType.GetProperty("Name") ??
                                                 relicType.GetProperty("displayName") ??
                                                 relicType.GetProperty("DisplayName") ??
                                                 relicType.GetProperty("title") ??
                                                 relicType.GetProperty("Title");

                                var nameField = relicType.GetField("name") ??
                                              relicType.GetField("Name") ??
                                              relicType.GetField("displayName") ??
                                              relicType.GetField("DisplayName") ??
                                              relicType.GetField("title") ??
                                              relicType.GetField("Title");

                                int? relicId = null;
                                string? relicName = null;

                                // Extract ID
                                if (idProperty != null && idProperty.CanRead)
                                {
                                    var idValue = idProperty.GetValue(relic);
                                    if (idValue != null && int.TryParse(idValue.ToString(), out var parsedId))
                                    {
                                        relicId = parsedId;
                                    }
                                }
                                else if (idField != null)
                                {
                                    var idValue = idField.GetValue(relic);
                                    if (idValue != null && int.TryParse(idValue.ToString(), out var parsedId))
                                    {
                                        relicId = parsedId;
                                    }
                                }

                                // Extract name
                                if (nameProperty != null && nameProperty.CanRead)
                                {
                                    var nameValue = nameProperty.GetValue(relic);
                                    relicName = nameValue?.ToString();
                                }
                                else if (nameField != null)
                                {
                                    var nameValue = nameField.GetValue(relic);
                                    relicName = nameValue?.ToString();
                                }

                                if (relicId.HasValue && !string.IsNullOrEmpty(relicName))
                                {
                                    var cleanName = CleanupName(relicName);
                                    mappings[relicId.Value] = cleanName;
                                    successCount++;
                                }

                                count++;
                            }
                            catch (Exception ex)
                            {
                                result.AddMessage($"Error processing individual relic: {ex.Message}");
                            }
                        }
                    }

                    result.AddMessage($"Processed {count} relics from list, successfully extracted {successCount} mappings");
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Error extracting relic data from list: {ex.Message}");
            }

            return mappings;
        }

        private static void ExtractRoomMappings(Assembly assembly, AnalysisResult result)
        {
            try
            {
                result.AddMessage("Extracting room mappings...");

                // Look specifically for WorldMap.RoomType enum first (try both cases)
                var worldMapRoomType = assembly.GetTypes().FirstOrDefault(t =>
                    (t.FullName == "WorldMap.RoomType" || t.FullName == "Worldmap.RoomType") && t.IsEnum);

                if (worldMapRoomType != null)
                {
                    result.AddMessage($"Found {worldMapRoomType.FullName} enum");

                    var enumValues = Enum.GetValues(worldMapRoomType);
                    foreach (var value in enumValues)
                    {
                        var intValue = Convert.ToInt32(value);
                        var enumName = value.ToString();
                        var cleanName = CleanupName(enumName ?? "Unknown");

                        result.RoomMappings[intValue] = cleanName;
                    }

                    result.AddMessage($"Successfully loaded {result.RoomMappings.Count} room mappings from {worldMapRoomType.FullName}");
                    return;
                }

                // Fallback: Look for Room, RoomType, or similar enums
                var roomType = assembly.GetTypes().FirstOrDefault(t =>
                    (t.Name == "RoomType" || t.Name == "Room" || t.Name == "RoomId" ||
                     t.Name == "LevelType" || t.Name == "SceneType") && t.IsEnum);

                if (roomType != null)
                {
                    result.AddMessage($"Found room enum: {roomType.Name}");

                    var enumValues = Enum.GetValues(roomType);
                    foreach (var value in enumValues)
                    {
                        var intValue = Convert.ToInt32(value);
                        var enumName = value.ToString();
                        var cleanName = CleanupName(enumName ?? "Unknown");

                        result.RoomMappings[intValue] = cleanName;
                    }

                    result.AddMessage($"Successfully loaded {result.RoomMappings.Count} room mappings");
                }
                else
                {
                    // Look for any enum containing "room", "level", or "scene" in the name
                    var candidateEnums = assembly.GetTypes().Where(t =>
                        t.IsEnum &&
                        (t.Name.ToLower().Contains("room") ||
                         t.Name.ToLower().Contains("level") ||
                         t.Name.ToLower().Contains("scene") ||
                         t.Name.ToLower().Contains("area") ||
                         t.Name.ToLower().Contains("zone"))).ToList();

                    if (candidateEnums.Any())
                    {
                        result.AddMessage($"Found {candidateEnums.Count} candidate room enums: {string.Join(", ", candidateEnums.Select(e => e.Name))}");

                        // Look for the most relevant one
                        var bestEnum = candidateEnums.FirstOrDefault(e => e.Name.ToLower().Contains("room")) ??
                                      candidateEnums.FirstOrDefault(e => e.Name.ToLower().Contains("level")) ??
                                      candidateEnums.OrderByDescending(e => Enum.GetValues(e).Length).First();

                        result.AddMessage($"Using enum: {bestEnum.Name} with {Enum.GetValues(bestEnum).Length} values");

                        var enumValues = Enum.GetValues(bestEnum);
                        foreach (var value in enumValues)
                        {
                            var intValue = Convert.ToInt32(value);
                            var enumName = value.ToString();
                            var cleanName = CleanupName(enumName ?? "Unknown");

                            result.RoomMappings[intValue] = cleanName;
                        }

                        result.AddMessage($"Successfully loaded {result.RoomMappings.Count} room mappings from {bestEnum.Name}");
                    }
                    else
                    {
                        result.AddMessage("No room enums found");
                    }
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Error extracting room mappings: {ex.Message}");
            }
        }

        private static void ExtractBossMappings(Assembly assembly, AnalysisResult result)
        {
            try
            {
                result.AddMessage("Extracting boss mappings...");

                // Look specifically for Stats.BossType enum first
                var statsBossType = assembly.GetTypes().FirstOrDefault(t =>
                    t.FullName == "Stats.BossType" && t.IsEnum);

                if (statsBossType != null)
                {
                    result.AddMessage($"Found Stats.BossType enum");

                    var enumValues = Enum.GetValues(statsBossType);
                    foreach (var value in enumValues)
                    {
                        var intValue = Convert.ToInt32(value);
                        var enumName = value.ToString();
                        var cleanName = CleanupName(enumName ?? "Unknown");

                        result.BossMappings[intValue] = cleanName;
                    }

                    result.AddMessage($"Successfully loaded {result.BossMappings.Count} boss mappings from Stats.BossType");
                    return;
                }

                // Fallback: Look for Boss, BossType, or similar enums
                var bossType = assembly.GetTypes().FirstOrDefault(t =>
                    (t.Name == "BossType" || t.Name == "Boss" || t.Name == "BossId" ||
                     t.Name == "EnemyType" || t.Name == "Enemy") && t.IsEnum);

                if (bossType != null)
                {
                    result.AddMessage($"Found boss enum: {bossType.Name}");

                    var enumValues = Enum.GetValues(bossType);
                    foreach (var value in enumValues)
                    {
                        var intValue = Convert.ToInt32(value);
                        var enumName = value.ToString();
                        var cleanName = CleanupName(enumName ?? "Unknown");

                        result.BossMappings[intValue] = cleanName;
                    }

                    result.AddMessage($"Successfully loaded {result.BossMappings.Count} boss mappings");
                }
                else
                {
                    // Look for any enum containing "boss" or "enemy" in the name
                    var candidateEnums = assembly.GetTypes().Where(t =>
                        t.IsEnum &&
                        (t.Name.ToLower().Contains("boss") ||
                         t.Name.ToLower().Contains("enemy") ||
                         t.Name.ToLower().Contains("monster") ||
                         t.Name.ToLower().Contains("creature"))).ToList();

                    if (candidateEnums.Any())
                    {
                        result.AddMessage($"Found {candidateEnums.Count} candidate boss enums: {string.Join(", ", candidateEnums.Select(e => e.Name))}");

                        // Look for the most relevant one - prefer boss over enemy
                        var bestEnum = candidateEnums.FirstOrDefault(e => e.Name.ToLower().Contains("boss")) ??
                                      candidateEnums.FirstOrDefault(e => e.Name.ToLower().Contains("enemy")) ??
                                      candidateEnums.OrderByDescending(e => Enum.GetValues(e).Length).First();

                        result.AddMessage($"Using enum: {bestEnum.Name} with {Enum.GetValues(bestEnum).Length} values");

                        var enumValues = Enum.GetValues(bestEnum);
                        foreach (var value in enumValues)
                        {
                            var intValue = Convert.ToInt32(value);
                            var enumName = value.ToString();
                            var cleanName = CleanupName(enumName ?? "Unknown");

                            result.BossMappings[intValue] = cleanName;
                        }

                        result.AddMessage($"Successfully loaded {result.BossMappings.Count} boss mappings from {bestEnum.Name}");
                    }
                    else
                    {
                        result.AddMessage("No boss enums found");
                    }
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Error extracting boss mappings: {ex.Message}");
            }
        }

        private static void ExtractStatusEffectMappings(Assembly assembly, AnalysisResult result)
        {
            try
            {
                result.AddMessage("Extracting status effect mappings...");

                // Look for StatusEffect enum
                var statusEffectType = assembly.GetTypes().FirstOrDefault(t =>
                    (t.Name == "StatusEffect" || t.Name == "StatusEffectType" || t.Name == "StatusEffects") && t.IsEnum);

                if (statusEffectType != null)
                {
                    result.AddMessage($"Found status effect enum: {statusEffectType.Name}");

                    var enumValues = Enum.GetValues(statusEffectType);
                    foreach (var value in enumValues)
                    {
                        var intValue = Convert.ToInt32(value);
                        var enumName = value.ToString();
                        var cleanName = CleanupName(enumName ?? "Unknown");

                        result.StatusEffectMappings[intValue] = cleanName;
                    }

                    result.AddMessage($"Successfully loaded {result.StatusEffectMappings.Count} status effect mappings");
                }
                else
                {
                    // Look for any enum containing "status" or "effect" in the name
                    var candidateEnums = assembly.GetTypes().Where(t =>
                        t.IsEnum &&
                        (t.Name.ToLower().Contains("status") ||
                         t.Name.ToLower().Contains("effect") ||
                         t.Name.ToLower().Contains("buff") ||
                         t.Name.ToLower().Contains("debuff"))).ToList();

                    if (candidateEnums.Any())
                    {
                        result.AddMessage($"Found {candidateEnums.Count} candidate status effect enums: {string.Join(", ", candidateEnums.Select(e => e.Name))}");

                        // Use the largest enum as it's likely the main one
                        var largestEnum = candidateEnums.OrderByDescending(e => Enum.GetValues(e).Length).First();
                        result.AddMessage($"Using largest enum: {largestEnum.Name} with {Enum.GetValues(largestEnum).Length} values");

                        var enumValues = Enum.GetValues(largestEnum);
                        foreach (var value in enumValues)
                        {
                            var intValue = Convert.ToInt32(value);
                            var enumName = value.ToString();
                            var cleanName = CleanupName(enumName ?? "Unknown");

                            result.StatusEffectMappings[intValue] = cleanName;
                        }

                        result.AddMessage($"Successfully loaded {result.StatusEffectMappings.Count} status effect mappings from {largestEnum.Name}");
                    }
                    else
                    {
                        result.AddMessage("No status effect enums found");
                    }
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Error extracting status effect mappings: {ex.Message}");
            }
        }

        private static void ExtractSlimePegMappings(Assembly assembly, AnalysisResult result)
        {
            try
            {
                result.AddMessage("Extracting slime peg mappings...");

                // Look for SlimePeg, SlimeType, or similar enums
                var slimePegType = assembly.GetTypes().FirstOrDefault(t =>
                    (t.Name == "SlimePeg" || t.Name == "SlimeType" || t.Name == "SlimePegType" ||
                     t.Name == "PegType" || t.Name == "SlimeVariant") && t.IsEnum);

                if (slimePegType != null)
                {
                    result.AddMessage($"Found slime peg enum: {slimePegType.Name}");

                    var enumValues = Enum.GetValues(slimePegType);
                    foreach (var value in enumValues)
                    {
                        var intValue = Convert.ToInt32(value);
                        var enumName = value.ToString();
                        var cleanName = CleanupName(enumName ?? "Unknown");

                        result.SlimePegMappings[intValue] = cleanName;
                    }

                    result.AddMessage($"Successfully loaded {result.SlimePegMappings.Count} slime peg mappings");
                }
                else
                {
                    // Look for any enum containing "slime" or "peg" in the name
                    var candidateEnums = assembly.GetTypes().Where(t =>
                        t.IsEnum &&
                        (t.Name.ToLower().Contains("slime") ||
                         t.Name.ToLower().Contains("peg") ||
                         t.Name.ToLower().Contains("ball") ||
                         t.FullName?.ToLower().Contains("slime") == true)).ToList();

                    if (candidateEnums.Any())
                    {
                        result.AddMessage($"Found {candidateEnums.Count} candidate slime/peg enums: {string.Join(", ", candidateEnums.Select(e => e.Name))}");

                        // Look for the most relevant one
                        var bestEnum = candidateEnums.FirstOrDefault(e => e.Name.ToLower().Contains("slime")) ??
                                      candidateEnums.FirstOrDefault(e => e.Name.ToLower().Contains("peg")) ??
                                      candidateEnums.OrderByDescending(e => Enum.GetValues(e).Length).First();

                        result.AddMessage($"Using enum: {bestEnum.Name} with {Enum.GetValues(bestEnum).Length} values");

                        var enumValues = Enum.GetValues(bestEnum);
                        foreach (var value in enumValues)
                        {
                            var intValue = Convert.ToInt32(value);
                            var enumName = value.ToString();
                            var cleanName = CleanupName(enumName ?? "Unknown");

                            result.SlimePegMappings[intValue] = cleanName;
                        }

                        result.AddMessage($"Successfully loaded {result.SlimePegMappings.Count} slime peg mappings from {bestEnum.Name}");
                    }
                    else
                    {
                        result.AddMessage("No slime peg enums found");
                    }
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Error extracting slime peg mappings: {ex.Message}");
            }
        }

        private static void ExtractCharacterClassMappings(Assembly assembly, AnalysisResult result)
        {
            try
            {
                result.AddMessage("Extracting character class mappings...");

                // Look for Peglin.ClassSystem.Class enum
                var characterClassType = assembly.GetTypes().FirstOrDefault(t =>
                    t.FullName == "Peglin.ClassSystem.Class" && t.IsEnum);

                if (characterClassType != null)
                {
                    result.AddMessage($"Found character class enum: {characterClassType.FullName}");

                    var enumValues = Enum.GetValues(characterClassType);
                    foreach (var value in enumValues)
                    {
                        var intValue = Convert.ToInt32(value);
                        var enumName = value.ToString();
                        var cleanName = CleanupName(enumName ?? "Unknown");

                        result.CharacterClassMappings[intValue] = cleanName;
                    }

                    result.AddMessage($"Successfully loaded {result.CharacterClassMappings.Count} character class mappings");
                }
                else
                {
                    // Look for alternative class-related enums
                    var candidateEnums = assembly.GetTypes().Where(t =>
                        t.IsEnum &&
                        (t.Name.ToLower().Contains("class") ||
                         t.Name.ToLower().Contains("character") ||
                         t.FullName?.ToLower().Contains("class") == true ||
                         t.FullName?.ToLower().Contains("character") == true)).ToList();

                    if (candidateEnums.Any())
                    {
                        result.AddMessage($"Found {candidateEnums.Count} candidate character class enums: {string.Join(", ", candidateEnums.Select(e => e.FullName))}");

                        // Prefer enums with "Class" in the name
                        var bestEnum = candidateEnums.FirstOrDefault(e => e.Name.ToLower().Contains("class")) ??
                                      candidateEnums.FirstOrDefault(e => e.FullName?.ToLower().Contains("class") == true) ??
                                      candidateEnums.First();

                        result.AddMessage($"Using enum: {bestEnum.FullName} with {Enum.GetValues(bestEnum).Length} values");

                        var enumValues = Enum.GetValues(bestEnum);
                        foreach (var value in enumValues)
                        {
                            var intValue = Convert.ToInt32(value);
                            var enumName = value.ToString();
                            var cleanName = CleanupName(enumName ?? "Unknown");

                            result.CharacterClassMappings[intValue] = cleanName;
                        }

                        result.AddMessage($"Successfully loaded {result.CharacterClassMappings.Count} character class mappings from {bestEnum.FullName}");
                    }
                    else
                    {
                        result.AddMessage("No character class enums found");
                    }
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Error extracting character class mappings: {ex.Message}");
            }
        }

        private static string CleanupName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unknown";

            // Handle common enum prefixes
            var cleanName = name;
            if (cleanName.StartsWith("RELIC_", StringComparison.OrdinalIgnoreCase))
                cleanName = cleanName.Substring(6);
            else if (cleanName.StartsWith("STATUS_", StringComparison.OrdinalIgnoreCase))
                cleanName = cleanName.Substring(7);
            else if (cleanName.StartsWith("SLIME_", StringComparison.OrdinalIgnoreCase))
                cleanName = cleanName.Substring(6);
            else if (cleanName.StartsWith("BOSS_", StringComparison.OrdinalIgnoreCase))
                cleanName = cleanName.Substring(5);
            else if (cleanName.StartsWith("ROOM_", StringComparison.OrdinalIgnoreCase))
                cleanName = cleanName.Substring(5);

            // Convert from enum-style names to readable names
            return cleanName
                .Replace("_", " ")
                .Split(' ')
                .Select(word => word.Length > 0 ? char.ToUpper(word[0]) + word.Substring(1).ToLower() : word)
                .Aggregate((a, b) => a + " " + b)
                .Trim();
        }

        // Detailed analysis methods for debugging (used by dump command)
        private static void AnalyzeRelicManagerDetailed(Assembly assembly, StreamWriter writer)
        {
            writer.WriteLine("RELICMANAGER DETAILED ANALYSIS");
            writer.WriteLine("-" + new string('-', 30));

            var relicManagerType = assembly.GetTypes().FirstOrDefault(t => t.FullName == "Relics.RelicManager");
            if (relicManagerType != null)
            {
                writer.WriteLine($"Found RelicManager: {relicManagerType.FullName}");
                writer.WriteLine();

                // Show all members
                var allMembers = relicManagerType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                var relicMembers = allMembers.Where(m => m.Name.ToLower().Contains("relic")).ToList();

                writer.WriteLine($"RelicManager relic-related members ({relicMembers.Count}):");
                foreach (var member in relicMembers)
                {
                    writer.WriteLine($"  {member.MemberType}: {member.Name}");
                    if (member is PropertyInfo prop)
                    {
                        writer.WriteLine($"    Type: {prop.PropertyType.FullName}");
                        writer.WriteLine($"    CanRead: {prop.CanRead}, CanWrite: {prop.CanWrite}");
                    }
                    else if (member is FieldInfo field)
                    {
                        writer.WriteLine($"    Type: {field.FieldType.FullName}");
                    }
                    else if (member is MethodInfo method)
                    {
                        writer.WriteLine($"    Returns: {method.ReturnType.Name}");
                        writer.WriteLine($"    Parameters: {method.GetParameters().Length}");
                    }
                }

                // Try instantiation test
                writer.WriteLine("\nRELICMANAGER INSTANTIATION TEST:");
                var analysisResult = new AnalysisResult();
                var mappings = TryExtractRelicsFromManager(relicManagerType, analysisResult);

                foreach (var message in analysisResult.Messages)
                {
                    writer.WriteLine($"  {message}");
                }

                foreach (var error in analysisResult.Errors)
                {
                    writer.WriteLine($"  ERROR: {error}");
                }

                if (mappings.Any())
                {
                    writer.WriteLine($"\nExtracted {mappings.Count} relic mappings:");
                    foreach (var mapping in mappings.Take(10))
                    {
                        writer.WriteLine($"  {mapping.Key}: {mapping.Value}");
                    }
                    if (mappings.Count > 10)
                    {
                        writer.WriteLine($"  ... and {mappings.Count - 10} more");
                    }
                }
            }
            else
            {
                writer.WriteLine("RelicManager not found");
            }

            writer.WriteLine();
        }

        private static void AnalyzeEnumsDetailed(Assembly assembly, StreamWriter writer, Type[] enums)
        {
            writer.WriteLine("ENUM ANALYSIS");
            writer.WriteLine("-" + new string('-', 30));

            var gameRelatedEnums = enums.Where(e =>
                e.Name.ToLower().Contains("relic") ||
                e.Name.ToLower().Contains("room") ||
                e.Name.ToLower().Contains("boss") ||
                e.Name.ToLower().Contains("status") ||
                e.Name.ToLower().Contains("effect") ||
                e.Name.ToLower().Contains("slime")
            ).ToList();

            writer.WriteLine($"Found {gameRelatedEnums.Count} game-related enums:");

            foreach (var enumType in gameRelatedEnums)
            {
                writer.WriteLine($"\n{enumType.FullName}:");
                var values = Enum.GetValues(enumType);
                writer.WriteLine($"  Values: {values.Length}");

                foreach (var value in values.Cast<object>().Take(20))
                {
                    var intValue = Convert.ToInt32(value);
                    writer.WriteLine($"    {intValue}: {value}");
                }

                if (values.Length > 20)
                {
                    writer.WriteLine($"    ... and {values.Length - 20} more values");
                }
            }

            writer.WriteLine();
        }

        private static void AnalyzeOtherGameTypes(Assembly assembly, StreamWriter writer, Type[] classes)
        {
            writer.WriteLine("OTHER GAME TYPES");
            writer.WriteLine("-" + new string('-', 30));

            var gameTypes = classes.Where(t =>
                t.FullName?.Contains("Relic") == true ||
                t.FullName?.Contains("Room") == true ||
                t.FullName?.Contains("Boss") == true ||
                t.FullName?.Contains("Manager") == true
            ).ToList();

            writer.WriteLine($"Found {gameTypes.Count} potentially interesting game types:");

            foreach (var type in gameTypes.Take(50))
            {
                writer.WriteLine($"  {type.FullName}");

                // Show key properties for data types
                if (type.Name.Contains("Relic") && !type.Name.Contains("Manager"))
                {
                    var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    var relevantProps = props.Where(p =>
                        p.Name.ToLower().Contains("id") ||
                        p.Name.ToLower().Contains("name") ||
                        p.Name.ToLower().Contains("title")
                    ).ToList();

                    if (relevantProps.Any())
                    {
                        writer.WriteLine($"    Key properties: {string.Join(", ", relevantProps.Select(p => $"{p.Name}:{p.PropertyType.Name}"))}");
                    }
                }
            }

            if (gameTypes.Count > 50)
            {
                writer.WriteLine($"  ... and {gameTypes.Count - 50} more types");
            }

            writer.WriteLine();
        }
    }
}

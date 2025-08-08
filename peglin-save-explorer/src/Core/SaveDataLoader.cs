using Newtonsoft.Json.Linq;
using OdinSerializer;
using ToolBox.Serialization;
using Newtonsoft.Json;
using System.Reflection;
using System.Linq;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Core
{
    public static class SaveDataLoader
    {
        private static readonly ConfigurationManager configManager = new ConfigurationManager();

        public static JObject? LoadSaveData(FileInfo? file)
        {
            // Try to get effective file path
            string? filePath = null;

            if (file != null && file.Exists)
            {
                filePath = file.FullName;
            }
            else
            {
                filePath = configManager.GetEffectiveSaveFilePath();
                if (string.IsNullOrEmpty(filePath))
                {
                    Logger.Error("No save file specified and no default save file found.");
                    Logger.Error("Please specify a save file with -f or configure a default in settings.");
                    return null;
                }
                Logger.Info($"Using default save file: {filePath}");
            }

            if (!File.Exists(filePath))
            {
                Logger.Error($"File '{filePath}' does not exist.");
                return null;
            }

            try
            {
                byte[] saveData = File.ReadAllBytes(filePath);
                var dumper = new SaveFileDumper(configManager);
                var result = dumper.DumpSaveFile(saveData);
                return JObject.Parse(result);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading save file: {ex.Message}");
                return null;
            }
        }

        public static bool UpdateCruciballLevel(string characterClass, int cruciballLevel, FileInfo? file = null)
        {
            try
            {
                var configManager = new ConfigurationManager();
                
                // Try to get effective file path
                string? filePath = null;

                if (file != null && file.Exists)
                {
                    filePath = file.FullName;
                }
                else
                {
                    filePath = configManager.GetEffectiveSaveFilePath();
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Logger.Error("No save file specified and no default save file found.");
                        return false;
                    }
                }

                if (!File.Exists(filePath))
                {
                    Logger.Error($"File '{filePath}' does not exist.");
                    return false;
                }

                Logger.Info($"Setting cruciball level for {characterClass} to {cruciballLevel}...");

                // First, discover the correct save data types from the assembly
                var peglinPath = configManager.GetEffectivePeglinPath();
                Assembly? peglinAssembly = null;
                
                if (!string.IsNullOrEmpty(peglinPath) && Directory.Exists(peglinPath))
                {
                    var assemblyResult = AssemblyAnalyzer.AnalyzePeglinAssembly(peglinPath);
                    if (assemblyResult.Success && assemblyResult.LoadedAssembly != null)
                    {
                        peglinAssembly = assemblyResult.LoadedAssembly;
                        Logger.Verbose("Loaded Peglin assembly for type-safe deserialization");
                    }
                    else
                    {
                        Logger.Verbose("Could not load Peglin assembly. Using generic deserialization.");
                    }
                }
                else
                {
                    Logger.Verbose("Peglin path not configured. Using generic deserialization.");
                }

                // Now directly modify the binary save file with assembly type information
                return UpdateCruciballInBinarySaveFile(filePath, characterClass, cruciballLevel, peglinAssembly);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating cruciball level: {ex.Message}");
                return false;
            }
        }

        private static bool UpdateCruciballInBinarySaveFile(string saveFilePath, string characterClass, int cruciballLevel, Assembly? peglinAssembly = null)
        {
            try
            {
                // Read the original save file
                var saveData = File.ReadAllBytes(saveFilePath);
                
                // Deserialize using OdinSerializer with proper context and discovered types
                // Try Unity context FIRST since Peglin is a Unity game
                object? deserializedData = null;
                SerializationContext? serializationContext = null;
                bool usedUnityContext = false;
                Type? saveDataType = null;
                
                // If we have the assembly, try to find the correct save data type
                if (peglinAssembly != null)
                {
                    Logger.Verbose("Looking for save data types in Peglin assembly...");
                    var saveDataTypes = peglinAssembly.GetTypes()
                        .Where(t => t.Name.Contains("SaveData", StringComparison.OrdinalIgnoreCase) ||
                                   t.Name.Contains("Save", StringComparison.OrdinalIgnoreCase))
                        .Where(t => !t.IsAbstract && !t.IsInterface)
                        .ToList();
                    
                    foreach (var type in saveDataTypes.Take(5))
                    {
                        Logger.Verbose($"  Found save type: {type.FullName}");
                    }
                    
                    // Look for the main save data type (likely to contain a dictionary or be serializable)
                    saveDataType = saveDataTypes.FirstOrDefault(t => 
                        t.Name.Equals("SaveData", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Contains("MainSave", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Contains("GameSave", StringComparison.OrdinalIgnoreCase) ||
                        t.GetFields().Any(f => f.Name.Contains("PersistentPlayer", StringComparison.OrdinalIgnoreCase)));
                    
                    if (saveDataType != null)
                    {
                        Logger.Verbose($"Selected save data type: {saveDataType.FullName}");
                    }
                }
                
                // Use the exact same approach as DumpSaveJsonCommand which works
                try
                {
                    Logger.Verbose("Attempting with Everything serialization policy (matching working dump command)...");
                    var context = new DeserializationContext();
                    context.Config.SerializationPolicy = SerializationPolicies.Everything;
                    deserializedData = SerializationUtility.DeserializeValue<Dictionary<string, ISerializable>>(saveData, DataFormat.Binary, context);
                    Logger.Verbose("✓ Deserialized using Everything policy");
                    
                    // Create a fresh context with Everything policy for serialization
                    // Must match the deserialization policy to preserve data integrity
                    serializationContext = new SerializationContext();
                    serializationContext.Config.SerializationPolicy = SerializationPolicies.Everything;
                    usedUnityContext = true;
                }
                catch (Exception ex)
                {
                    Logger.Verbose($"Failed with Everything policy: {ex.Message}");
                    
                    // Fallback to Unity policy like DumpSaveJsonCommand does
                    try
                    {
                        Logger.Verbose("Trying Unity serialization policy...");
                        var context = new DeserializationContext();
                        context.Config.SerializationPolicy = SerializationPolicies.Unity;
                        deserializedData = SerializationUtility.DeserializeValue<Dictionary<string, ISerializable>>(saveData, DataFormat.Binary, context);
                        Logger.Verbose("✓ Deserialized using Unity policy");
                        
                        // Create matching serialization context
                        serializationContext = new SerializationContext();
                        serializationContext.Config.SerializationPolicy = SerializationPolicies.Unity;
                        usedUnityContext = true;
                    }
                    catch (Exception ex2)
                    {
                        Logger.Verbose($"Failed with Unity policy: {ex2.Message}");
                        
                        // Final fallback to no context (like original)
                        try
                        {
                            Logger.Verbose("Final fallback to no-context deserialization...");
                            deserializedData = SerializationUtility.DeserializeValue<Dictionary<string, ISerializable>>(saveData, DataFormat.Binary, null);
                            Logger.Verbose("✓ Deserialized using no-context method");
                            
                            // For no-context, we need to serialize back with no context too
                            serializationContext = null;
                            usedUnityContext = false;
                        }
                        catch (Exception ex3)
                        {
                            Logger.Error($"❌ All deserialization attempts failed: {ex3.Message}");
                        }
                    }
                }

                if (deserializedData == null)
                {
                    Logger.Error("Error: Could not deserialize save data with any context");
                    return false;
                }
                
                Logger.Verbose($"✓ Deserialization successful. Data type: {deserializedData.GetType().FullName}");

                // Map character class name to index
                var classIndex = GameDataMappings.GetCharacterClassIndex(characterClass);
                if (classIndex == -1)
                {
                    Logger.Error($"Unknown character class '{characterClass}'.");
                    return false;
                }

                // Update the cruciball data in the deserialized object
                Logger.Info($"Attempting to update cruciball data for {characterClass} (index {classIndex}) to level {cruciballLevel}...");
                bool updated = UpdateCruciballInDeserializedData(deserializedData, classIndex, cruciballLevel, characterClass);
                if (!updated)
                {
                    Logger.Error("Error: Could not find or update cruciball data in save file.");
                    Logger.Error("This likely means the save file structure is different than expected or cruciball data hasn't been initialized yet.");
                    return false;
                }
                
                Logger.Info("✓ Successfully updated cruciball data in memory");
                
                // Validate the modified data structure before serialization
                Logger.Verbose("Validating modified data structure...");
                try
                {
                    // Try a test serialization to make sure the structure is still valid
                    byte[] testSerialization;
                    if (saveDataType != null && usedUnityContext && serializationContext != null)
                    {
                        // Use type-specific serialization if we have the exact type
                        var method = typeof(SerializationUtility).GetMethod("SerializeValue", new[] { saveDataType, typeof(DataFormat), typeof(SerializationContext) });
                        testSerialization = (byte[])method.Invoke(null, new object[] { deserializedData, DataFormat.Binary, serializationContext });
                        Logger.Verbose($"✓ Type-specific validation passed with {saveDataType.Name}");
                    }
                    else if (usedUnityContext && serializationContext != null)
                    {
                        testSerialization = SerializationUtility.SerializeValue(deserializedData, DataFormat.Binary, serializationContext);
                        Logger.Verbose("✓ Generic Unity context validation passed");
                    }
                    else
                    {
                        testSerialization = SerializationUtility.SerializeValue(deserializedData, DataFormat.Binary);
                        Logger.Verbose("✓ Default context validation passed");
                    }
                    
                    if (testSerialization?.Length == 0)
                    {
                        Logger.Error("Error: Test serialization produced empty data");
                        return false;
                    }
                    
                    Logger.Verbose($"✓ Data structure validation passed (test size: {testSerialization?.Length} bytes)");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Data structure validation failed: {ex.Message}");
                    Logger.Error("The modification may have corrupted the save data structure.");
                    return false;
                }

                // Serialize back with the SAME context and type that was used for deserialization
                byte[]? updatedSaveData = null;
                try
                {
                    if (saveDataType != null && usedUnityContext && serializationContext != null)
                    {
                        Logger.Verbose($"Serializing with Unity context using specific type: {saveDataType.Name}...");
                        var method = typeof(SerializationUtility).GetMethod("SerializeValue", new[] { saveDataType, typeof(DataFormat), typeof(SerializationContext) });
                        updatedSaveData = (byte[])method.Invoke(null, new object[] { deserializedData, DataFormat.Binary, serializationContext });
                    }
                    else if (usedUnityContext && serializationContext != null)
                    {
                        Logger.Verbose("Serializing with fresh Everything policy context (avoiding validation contamination)...");
                        // Create a completely fresh context to avoid validation contamination
                        var freshContext = new SerializationContext();
                        freshContext.Config.SerializationPolicy = SerializationPolicies.Everything;
                        updatedSaveData = SerializationUtility.SerializeValue(deserializedData, DataFormat.Binary, freshContext);
                    }
                    else
                    {
                        Logger.Verbose("Serializing with fresh Everything policy context (matching deserialization)...");
                        // Ensure we have a fresh Everything policy context
                        if (serializationContext == null)
                        {
                            serializationContext = new SerializationContext();
                            serializationContext.Config.SerializationPolicy = SerializationPolicies.Everything;
                        }
                        updatedSaveData = SerializationUtility.SerializeValue(deserializedData, DataFormat.Binary, serializationContext);
                    }
                    
                    // Validate the serialized data
                    if (updatedSaveData == null)
                    {
                        Logger.Error("❌ ERROR: Serialization returned null");
                        return false;
                    }
                    
                    if (updatedSaveData.Length == 0)
                    {
                        Logger.Error("❌ ERROR: Serialization returned empty byte array");
                        return false;
                    }
                    
                    if (updatedSaveData.Length < 1000)
                    {
                        Logger.Error($"⚠️ WARNING: Serialized data is only {updatedSaveData.Length} bytes (original was {saveData.Length} bytes)");
                        Logger.Error("This is likely corrupted data. Aborting save to prevent data loss.");
                        return false;
                    }
                    
                    Logger.Info($"✓ Serialization successful: {updatedSaveData.Length} bytes (original: {saveData.Length} bytes)");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Serialization failed: {ex.Message}");
                    return false;
                }

                // Create backup before writing
                var backupPath = saveFilePath + ".backup_cruciball";
                File.Copy(saveFilePath, backupPath, true);
                Logger.Info($"Created backup: {backupPath}");

                // Write the updated save data
                File.WriteAllBytes(saveFilePath, updatedSaveData);
                Logger.Info($"Successfully updated {characterClass} cruciball level to {cruciballLevel}.");
                Logger.Info("Save file has been modified. Restart Peglin to see the changes.");
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating binary save file: {ex.Message}");
                return false;
            }
        }

        private static bool UpdateCruciballInDeserializedData(object data, int classIndex, int cruciballLevel, string className)
        {
            try
            {
                // Use direct object manipulation to find CruciballManagerSaveData
                Logger.Verbose($"Received data type: {data?.GetType()?.FullName ?? "null"}");
                Logger.Info($"Target class: {className} (index {classIndex}), level: {cruciballLevel}");
                
                if (data is not System.Collections.IDictionary dict)
                {
                    Logger.Error("Error: Expected Dictionary structure for save data.");
                    Logger.Error($"Actual type: {data?.GetType()}");
                    // Object structure inspection moved to verbose logging
                    if (data != null)
                    {
                        Logger.Verbose($"Data has {data.GetType().GetProperties().Length} properties and {data.GetType().GetFields().Length} fields");
                    }
                    return false;
                }

                return UpdateCruciballInPersistentData(dict, classIndex, cruciballLevel, className);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating cruciball in deserialized data: {ex.Message}");
                return false;
            }
        }

        public static void AnalyzeSaveStructure(string saveFilePath)
        {
            try
            {
                Logger.Verbose("=== COMPREHENSIVE SAVE FILE ANALYSIS ===");
                Logger.Verbose($"Analyzing: {saveFilePath}");
                
                // Read and deserialize the save file
                var saveData = File.ReadAllBytes(saveFilePath);
                Logger.Verbose($"Save file size: {saveData.Length} bytes");
                
                // Deserialize with Unity context
                DeserializationContext deserializationContext = new DeserializationContext();
                deserializationContext.Config.SerializationPolicy = SerializationPolicies.Everything;
                
                var deserializedData = SerializationUtility.DeserializeValue<object>(new MemoryStream(saveData), DataFormat.Binary, deserializationContext);
                if (deserializedData == null)
                {
                    Logger.Error("❌ Failed to deserialize save file");
                    return;
                }
                
                Logger.Verbose($"✓ Deserialized as: {deserializedData.GetType().FullName}");
                
                if (deserializedData is System.Collections.IDictionary dict)
                {
                    Logger.Verbose($"TOP-LEVEL DICTIONARY ({dict.Count} keys)");
                    foreach (var key in dict.Keys.Cast<object>())
                    {
                        var value = dict[key];
                        Logger.Verbose($"Key: {key}");
                        Logger.Verbose($"  Type: {value?.GetType().FullName ?? "null"}");
                        
                        // Deep dive into each major section
                        AnalyzeDataSection(key.ToString(), value, 1);
                    }
                }
                
                Logger.Verbose("ANALYSIS COMPLETE");
            }
            catch (Exception ex)
            {
                Logger.Error($"❌ Analysis failed: {ex.Message}");
            }
        }
        
        private static void AnalyzeDataSection(string sectionName, object data, int depth)
        {
            string indent = new string(' ', depth * 2);
            
            if (data == null)
            {
                Logger.Verbose($"{indent}└─ null");
                return;
            }
            
            Logger.Verbose($"{indent}└─ {data.GetType().Name}");
            
            if (depth > 3) // Prevent infinite recursion
            {
                Logger.Verbose($"{indent}   (max depth reached)");
                return;
            }
            
            // Check if it's an OdinSerializer Item wrapper
            var valueField = data.GetType().GetField("Value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (valueField != null)
            {
                var innerValue = valueField.GetValue(data);
                Logger.Verbose($"{indent}  ├─ Contains Value field:");
                AnalyzeDataSection("Value", innerValue, depth + 1);
                return;
            }
            
            // Analyze object fields
            var fields = data.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (fields.Length > 0)
            {
                Logger.Verbose($"{indent}  ├─ Fields ({fields.Length}):");
                foreach (var field in fields.Take(20)) // Limit to first 20 fields
                {
                    var fieldValue = field.GetValue(data);
                    string valueStr = FormatFieldValue(fieldValue);
                    Logger.Verbose($"{indent}    ├─ {field.FieldType.Name} {field.Name} = {valueStr}");
                    
                    // Special analysis for important fields
                    if (IsImportantField(field.Name))
                    {
                        Logger.Verbose($"{indent}      ★ IMPORTANT FIELD ★");
                        if (fieldValue != null && depth < 3)
                        {
                            AnalyzeDataSection(field.Name, fieldValue, depth + 2);
                        }
                    }
                }
                
                if (fields.Length > 20)
                {
                    Logger.Verbose($"{indent}    └─ ... and {fields.Length - 20} more fields");
                }
            }
            
            // Analyze arrays/collections
            if (data is Array array)
            {
                Logger.Verbose($"{indent}  ├─ Array[{array.Length}]:");
                for (int i = 0; i < Math.Min(array.Length, 5); i++)
                {
                    var item = array.GetValue(i);
                    string valueStr = FormatFieldValue(item);
                    Logger.Verbose($"{indent}    [{i}] = {valueStr}");
                }
                if (array.Length > 5)
                {
                    Logger.Verbose($"{indent}    ... and {array.Length - 5} more items");
                }
            }
        }
        
        private static string FormatFieldValue(object value)
        {
            if (value == null) return "null";
            if (value is string str) return $"\"{str}\"";
            if (value is Array arr) return $"Array[{arr.Length}]";
            if (value.GetType().IsPrimitive || value is DateTime) return value.ToString();
            return $"{value.GetType().Name} object";
        }
        
        private static bool IsImportantField(string fieldName)
        {
            return fieldName.Contains("cruci", StringComparison.OrdinalIgnoreCase) ||
                   fieldName.Contains("unlock", StringComparison.OrdinalIgnoreCase) ||
                   fieldName.Contains("achievement", StringComparison.OrdinalIgnoreCase) ||
                   fieldName.Contains("character", StringComparison.OrdinalIgnoreCase) ||
                   fieldName.Contains("class", StringComparison.OrdinalIgnoreCase) ||
                   fieldName.Contains("level", StringComparison.OrdinalIgnoreCase) ||
                   fieldName.Contains("progress", StringComparison.OrdinalIgnoreCase) ||
                   fieldName.Contains("stat", StringComparison.OrdinalIgnoreCase);
        }

        private static bool UpdateCruciballInPersistentData(System.Collections.IDictionary dict, int classIndex, int cruciballLevel, string className)
        {
            // First check PersistentPlayerSaveData
            Logger.Info("Checking PersistentPlayerSaveData for cruciball data...");
            if (dict.Contains("PersistentPlayerSaveData"))
            {
                var persistentPlayerItem = dict["PersistentPlayerSaveData"];
                if (persistentPlayerItem != null)
                {
                    var valueField = persistentPlayerItem.GetType().GetField("Value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (valueField != null)
                    {
                        var persistentPlayerData = valueField.GetValue(persistentPlayerItem);
                        if (persistentPlayerData != null)
                        {
                            Logger.Verbose("Examining PersistentPlayerSaveData structure:");
                            var fields = persistentPlayerData.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            
                            // First, show ALL fields to understand what might be getting corrupted
                            Logger.Verbose("=== ALL FIELDS IN PersistentPlayerSaveData ===");
                            foreach (var field in fields)
                            {
                                var fieldValue = field.GetValue(persistentPlayerData);
                                var valueDescription = fieldValue?.ToString() ?? "null";
                                if (fieldValue is Array arr)
                                {
                                    valueDescription = $"Array[{arr.Length}]: [{string.Join(", ", arr.Cast<object>().Take(5))}...]";
                                }
                                else if (valueDescription.Length > 50)
                                {
                                    valueDescription = valueDescription.Substring(0, 50) + "...";
                                }
                                Logger.Verbose($"  - {field.FieldType.Name} {field.Name} = {valueDescription}");
                            }
                            Logger.Verbose("=== END ALL FIELDS ===");
                            
                            foreach (var field in fields.Take(20))
                            {
                                // Check if this field might contain cruciball data
                                if (field.Name.Contains("cruci", StringComparison.OrdinalIgnoreCase) ||
                                    field.Name.Contains("ball", StringComparison.OrdinalIgnoreCase) ||
                                    field.Name.Contains("unlock", StringComparison.OrdinalIgnoreCase) ||
                                    field.Name.Contains("level", StringComparison.OrdinalIgnoreCase))
                                {
                                    Logger.Verbose($"    *** POTENTIAL CRUCIBALL FIELD: {field.FieldType.Name} {field.Name} ***");
                                    
                                    var fieldValue = field.GetValue(persistentPlayerData);
                                    if (fieldValue != null)
                                    {
                                        Logger.Verbose($"    Current value: {fieldValue}");
                                        Logger.Verbose($"    Type: {fieldValue.GetType().Name}");
                                        
                                        // If it's an array, show its contents
                                        if (fieldValue is Array array)
                                        {
                                            Logger.Verbose($"    Array length: {array.Length}");
                                            for (int i = 0; i < Math.Min(array.Length, 10); i++)
                                            {
                                                Logger.Verbose($"      [{i}]: {array.GetValue(i)}");
                                            }
                                            
                                            // Try to update if this looks like a cruciball levels array
                                            if ((field.Name.Contains("cruciball", StringComparison.OrdinalIgnoreCase) ||
                                                 field.Name.Contains("CruciballLevel", StringComparison.OrdinalIgnoreCase)) &&
                                                array.Length >= 4 && classIndex < array.Length)
                                            {
                                                var currentValue = array.GetValue(classIndex);
                                                Logger.Info($"    Current {className} value: {currentValue}");
                                                
                                                // Update the value
                                                array.SetValue(cruciballLevel, classIndex);
                                                Logger.Info($"    Updated {className} cruciball level to {cruciballLevel} in field {field.Name}");
                                                return true;
                                            }
                                        }
                                    }
                                    else if (field.FieldType.IsArray && field.FieldType.GetElementType() == typeof(int))
                                    {
                                        Logger.Verbose($"    Field is null, but is an int array type");
                                        
                                        // Check if this is a cruciball-related field that we should initialize
                                        if (field.Name.Contains("cruciball", StringComparison.OrdinalIgnoreCase) ||
                                            field.Name.Contains("CruciballLevel", StringComparison.OrdinalIgnoreCase))
                                        {
                                            Logger.Verbose($"    WARNING: {field.Name} is null - this may indicate the save hasn't unlocked cruciball yet");
                                            
                                            // Check if this appears to be a completely fresh save
                                            var allFields = persistentPlayerData.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                            var progressionFields = allFields.Where(f => 
                                                f.Name.Contains("unlock", StringComparison.OrdinalIgnoreCase) ||
                                                f.Name.Contains("achievement", StringComparison.OrdinalIgnoreCase) ||
                                                f.Name.Contains("queue", StringComparison.OrdinalIgnoreCase)).ToArray();
                                            
                                            bool appearsFreshSave = progressionFields.All(f => f.GetValue(persistentPlayerData) == null);
                                            
                                            if (appearsFreshSave)
                                            {
                                                Logger.Warning($"    ⚠️  SAFETY CHECK: This appears to be a fresh save (all progression arrays are null)");
                                                Logger.Warning($"    For safety, we'll skip modifying cruciball data to avoid inconsistent save state.");
                                                Logger.Warning($"    Recommendation: Play the game for a bit first to initialize save progression, then try again.");
                                                return false;
                                            }
                                            
                                            Logger.Info($"    Save appears to have progression data - proceeding with cruciball modification...");
                                            
                                            try
                                            {
                                                // Create new array with 4 elements (for 4 classes), initialized to -1 like Peglin does
                                                var newArray = new int[4];
                                                for (int i = 0; i < newArray.Length; i++)
                                                {
                                                    newArray[i] = -1; // Initialize with -1 like Peglin's DataSerializer
                                                }
                                                newArray[classIndex] = cruciballLevel;
                                                
                                                // Verify the field can be set
                                                field.SetValue(persistentPlayerData, newArray);
                                                
                                                // Verify the value was set correctly
                                                var verifyArray = field.GetValue(persistentPlayerData) as int[];
                                                if (verifyArray != null && verifyArray[classIndex] == cruciballLevel)
                                                {
                                                    Logger.Verbose($"Successfully created and updated {className} cruciball level to {cruciballLevel} in field {field.Name}");
                                                    return true;
                                                }
                                                else
                                                {
                                                    Logger.Warning($"Failed to verify array update");
                                                    return false;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger.Error($"Failed to initialize array: {ex.Message}");
                                                return false;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // Fall back to PermanentStats if nothing found in PersistentPlayerSaveData
            Logger.Info("No cruciball data found in PersistentPlayerSaveData, checking PermanentStats...");
            return UpdateCruciballUnlockInPermanentStats(dict, classIndex, cruciballLevel, className);
        }

        private static bool UpdateCruciballUnlockInPermanentStats(System.Collections.IDictionary dict, int classIndex, int cruciballLevel, string className)
        {
            try
            {
                // Find PermanentStats in the dictionary
                if (!dict.Contains("PermanentStats"))
                {
                    Logger.Error("Could not find PermanentStats in save data.");
                    return false;
                }

                var permanentStatsItem = dict["PermanentStats"];
                if (permanentStatsItem == null)
                {
                    Logger.Error("PermanentStats is null.");
                    return false;
                }

                // Get the Value field from the Item<T> wrapper
                var valueField = permanentStatsItem.GetType().GetField("Value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (valueField == null)
                {
                    Logger.Error("Could not find Value field in PermanentStats item.");
                    return false;
                }

                var permanentStatsData = valueField.GetValue(permanentStatsItem);
                if (permanentStatsData == null)
                {
                    Logger.Error("PermanentStats Value is null.");
                    return false;
                }

                // Look for existing cruciball field
                var cruciballField = permanentStatsData.GetType().GetField("maxCruciballUnlockedPerClass", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (cruciballField == null)
                {
                    // Try other possible field names
                    cruciballField = permanentStatsData.GetType().GetField("cruciballUnlockedPerClass", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                
                if (cruciballField == null)
                {
                    cruciballField = permanentStatsData.GetType().GetField("cruciballPerClass", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (cruciballField == null)
                {
                    Logger.Warning("No cruciball tracking field found in PermanentStats.");
                    Logger.Info("This save file may not have cruciball progression unlocked yet.");
                    Logger.Info("Creating a temporary cruciball level indicator...");
                    
                    // For now, we can't dynamically add fields, but we can note the limitation
                    Logger.Info($"Would set {className} (index {classIndex}) cruciball unlock level to {cruciballLevel}");
                    Logger.Info("Note: This change affects only the current run cruciball level in active games.");
                    Logger.Info("Persistent cruciball unlocks require the game to have cruciball progression available.");
                    return false;
                }

                // Get or create the cruciball array
                var cruciballArray = cruciballField.GetValue(permanentStatsData) as int[];
                if (cruciballArray == null)
                {
                    // Create new array with 4 elements (for 4 classes), initialized to -1 like Peglin does
                    cruciballArray = new int[4];
                    for (int i = 0; i < cruciballArray.Length; i++)
                    {
                        cruciballArray[i] = -1; // Initialize with -1 like Peglin's DataSerializer
                    }
                    cruciballField.SetValue(permanentStatsData, cruciballArray);
                    Logger.Info("Created new cruciball unlock array in PermanentStats (initialized with -1 values like Peglin).");
                }

                // Ensure array is large enough
                if (cruciballArray.Length <= classIndex)
                {
                    var newArray = new int[classIndex + 1];
                    Array.Copy(cruciballArray, newArray, cruciballArray.Length);
                    cruciballArray = newArray;
                    cruciballField.SetValue(permanentStatsData, cruciballArray);
                }

                // Get current value
                var currentValue = cruciballArray[classIndex];
                Logger.Verbose($"Current {className} cruciball unlock level: {currentValue}");

                // Update the cruciball unlock level
                cruciballArray[classIndex] = cruciballLevel;
                
                Logger.Info($"Updated {className} cruciball unlock level to {cruciballLevel}.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating cruciball unlock in PermanentStats: {ex.Message}");
                return false;
            }
        }

        private static Tuple<FieldInfo, object?>? FindFieldRecursive(object obj, string fieldName)
        {
            if (obj == null) return null;

            var type = obj.GetType();
            
            // First check direct fields
            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return Tuple.Create(field, field.GetValue(obj));
            }

            // Then check all fields recursively
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var value = f.GetValue(obj);
                if (value != null && !f.FieldType.IsPrimitive && f.FieldType != typeof(string))
                {
                    var result = FindFieldRecursive(value, fieldName);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        private static void DumpDataStructure(object data, string label)
        {
            try
            {
                var dataType = data.GetType();
                Logger.Verbose($"Debug [{label}]: Type = {dataType.FullName}");
                
                if (data is System.Collections.IDictionary dict)
                {
                    Logger.Verbose($"Debug [{label}]: Dictionary with {dict.Count} entries:");
                    int count = 0;
                    foreach (var key in dict.Keys)
                    {
                        if (count >= 5) // Limit output
                        {
                            Logger.Verbose($"Debug [{label}]: ... and {dict.Count - 5} more entries");
                            break;
                        }
                        var value = dict[key];
                        var valueType = value?.GetType()?.Name ?? "null";
                        Logger.Verbose($"Debug [{label}]:   [{key}] => {valueType}");
                        count++;
                    }
                }
                else
                {
                    Logger.Verbose($"Debug [{label}]: Object fields:");
                    var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var field in fields.Take(10)) // Limit output
                    {
                        var value = field.GetValue(data);
                        var valueType = value?.GetType()?.Name ?? "null";
                        Logger.Verbose($"Debug [{label}]:   {field.Name} => {valueType}");
                    }
                    if (fields.Length > 10)
                    {
                        Logger.Verbose($"Debug [{label}]: ... and {fields.Length - 10} more fields");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Verbose($"Debug [{label}]: Error dumping structure: {ex.Message}");
            }
        }

        private static bool UpdateCruciballInSaveData(JObject saveData, string characterClass, int cruciballLevel)
        {
            try
            {
                // Map character class name to index
                var classIndex = GameDataMappings.GetCharacterClassIndex(characterClass);
                if (classIndex == -1)
                {
                    Logger.Error($"Unknown character class '{characterClass}'.");
                    return false;
                }

                // Try to find PermanentStats in the save data structure
                var permanentStatsPaths = new[]
                {
                    new[] { "data", "PermanentStats", "Value" },
                    new[] { "peglinData", "PermanentStats", "Value" },
                    new[] { "PermanentStats", "Value" },
                    new[] { "data", "PermanentStats" },
                    new[] { "peglinData", "PermanentStats" },
                    new[] { "PermanentStats" }
                };

                JToken? permanentStats = null;
                foreach (var path in permanentStatsPaths)
                {
                    JToken? current = saveData;
                    foreach (var key in path)
                    {
                        current = current?[key];
                        if (current == null) break;
                    }
                    
                    if (current != null)
                    {
                        permanentStats = current;
                        break;
                    }
                }

                if (permanentStats == null)
                {
                    Logger.Error("Could not find PermanentStats in save data.");
                    return false;
                }

                // Look for existing cruciball per class data or create it
                var cruciballPerClassKey = "cruciballPerClass";
                JToken? cruciballPerClass = permanentStats[cruciballPerClassKey];
                
                if (cruciballPerClass == null)
                {
                    // Create new cruciball per class array (assuming 4 classes: 0-3)
                    cruciballPerClass = new JArray(new int[4]);
                    permanentStats[cruciballPerClassKey] = cruciballPerClass;
                    Logger.Info("Created new cruciballPerClass array in PermanentStats.");
                }

                if (cruciballPerClass is JArray cruciballArray)
                {
                    // Ensure the array is large enough for the class index
                    while (cruciballArray.Count <= classIndex)
                    {
                        cruciballArray.Add(0);
                    }

                    // Update the cruciball level for the specified class
                    cruciballArray[classIndex] = cruciballLevel;
                    
                    Logger.Info($"Updated cruciball level for class {characterClass} (index {classIndex}) to {cruciballLevel}.");
                    return true;
                }
                else
                {
                    Logger.Error("cruciballPerClass is not an array.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating cruciball in save data: {ex.Message}");
                return false;
            }
        }

        public static Dictionary<string, int> GetCruciballLevelsPerClass(FileInfo? file = null)
        {
            var cruciballLevels = new Dictionary<string, int>();
            
            try
            {
                var saveData = LoadSaveData(file);
                if (saveData == null)
                {
                    return cruciballLevels;
                }

                // Try to find PermanentStats in the save data structure
                var permanentStatsPaths = new[]
                {
                    new[] { "data", "PermanentStats", "Value" },
                    new[] { "peglinData", "PermanentStats", "Value" },
                    new[] { "PermanentStats", "Value" },
                    new[] { "data", "PermanentStats" },
                    new[] { "peglinData", "PermanentStats" },
                    new[] { "PermanentStats" }
                };

                JToken? permanentStats = null;
                foreach (var path in permanentStatsPaths)
                {
                    JToken? current = saveData;
                    foreach (var key in path)
                    {
                        current = current?[key];
                        if (current == null) break;
                    }
                    
                    if (current != null)
                    {
                        permanentStats = current;
                        break;
                    }
                }

                if (permanentStats != null)
                {
                    var cruciballPerClass = permanentStats["cruciballPerClass"] as JArray;
                    if (cruciballPerClass != null)
                    {
                        for (int i = 0; i < cruciballPerClass.Count; i++)
                        {
                            var className = GameDataMappings.GetCharacterClassName(i);
                            var level = cruciballPerClass[i]?.Value<int>() ?? 0;
                            cruciballLevels[className] = level;
                        }
                    }
                    else
                    {
                        // If cruciballPerClass doesn't exist, initialize with zeros using proper class names
                        for (int i = 0; i < 4; i++) // Assuming 4 character classes
                        {
                            var className = GameDataMappings.GetCharacterClassName(i);
                            cruciballLevels[className] = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading cruciball levels: {ex.Message}");
            }

            return cruciballLevels;
        }
    }
}
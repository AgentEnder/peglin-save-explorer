using System.CommandLine;
using peglin_save_explorer.Core;
using Newtonsoft.Json;
using OdinSerializer;
using ToolBox.Serialization;

namespace peglin_save_explorer.Commands
{
    public class DumpSaveJsonCommand : ICommand
    {
        public Command CreateCommand()
        {
            var command = new Command("dump-save-json", "Dump the entire save file structure to JSON for analysis");
            
            var fileOption = new Option<FileInfo?>(
                new[] { "--file", "-f" },
                description: "Path to the save file (defaults to configured save file)"
            );
            
            var outputOption = new Option<string?>(
                new[] { "--output", "-o" },
                description: "Output JSON file path (defaults to save_dump.json)"
            );
            
            command.AddOption(fileOption);
            command.AddOption(outputOption);
            
            command.SetHandler(async (FileInfo? file, string? output) =>
            {
                try
                {
                    var configManager = new ConfigurationManager();
                    string? saveFilePath = null;

                    if (file != null && file.Exists)
                    {
                        saveFilePath = file.FullName;
                    }
                    else
                    {
                        saveFilePath = configManager.GetEffectiveSaveFilePath();
                        if (string.IsNullOrEmpty(saveFilePath))
                        {
                            Program.WriteToConsole("Error: No save file specified and no default save file found.");
                            Program.WriteToConsole("Please specify a save file with -f or configure a default in settings.");
                            return;
                        }
                        Program.WriteToConsole($"Using default save file: {saveFilePath}");
                    }

                    if (!File.Exists(saveFilePath))
                    {
                        Program.WriteToConsole($"❌ Save file not found: {saveFilePath}");
                        return;
                    }
                    
                    string outputPath = output ?? "save_dump.json";
                    DumpSaveToJson(saveFilePath, outputPath);
                }
                catch (Exception ex)
                {
                    Program.WriteToConsole($"❌ Error dumping save: {ex.Message}");
                    Program.WriteToConsole($"Stack trace: {ex.StackTrace}");
                }
            }, fileOption, outputOption);
            
            return command;
        }
        
        private static void DumpSaveToJson(string saveFilePath, string outputPath)
        {
            try
            {
                Program.WriteToConsole("=== DUMPING SAVE FILE TO JSON ===");
                Program.WriteToConsole($"Input: {saveFilePath}");
                Program.WriteToConsole($"Output: {outputPath}");
                
                // Try to load using Peglin's exact method - using their file system interface
                object? deserializedData = null;
                byte[] saveData;
                
                try
                {
                    Program.WriteToConsole("Attempting to use Peglin's StandardFileSystemInterface...");
                    
                    // Use reflection to create Peglin's file system interface
                    var toolboxAssembly = typeof(ISerializable).Assembly;
                    var fileSystemInterfaceType = toolboxAssembly.GetType("ToolBox.Serializer.Platforms.StandardFileSystemInterface");
                    
                    if (fileSystemInterfaceType != null)
                    {
                        var fileSystemInterface = Activator.CreateInstance(fileSystemInterfaceType);
                        var loadFileMethod = fileSystemInterfaceType.GetMethod("LoadFile");
                        
                        if (loadFileMethod != null)
                        {
                            saveData = Array.Empty<byte>();
                            // For ref parameters, we need to box them properly
                            object[] parameters = { saveFilePath, saveData };
                            loadFileMethod.Invoke(fileSystemInterface, parameters);
                            saveData = (byte[])parameters[1]; // Get the ref parameter back
                            
                            Program.WriteToConsole($"✓ Loaded using Peglin's StandardFileSystemInterface: {saveData.Length} bytes");
                        }
                        else
                        {
                            throw new Exception("LoadFile method not found");
                        }
                    }
                    else
                    {
                        throw new Exception("StandardFileSystemInterface type not found");
                    }
                }
                catch (Exception ex)
                {
                    Program.WriteToConsole($"Failed to use Peglin's interface: {ex.Message}");
                    Program.WriteToConsole("Falling back to direct file reading...");
                    saveData = File.ReadAllBytes(saveFilePath);
                }
                
                Program.WriteToConsole($"Save file size: {saveData.Length} bytes");
                
                // Try Everything serialization policy first (most permissive)
                try
                {
                    Program.WriteToConsole("Attempting with Everything serialization policy (most permissive)...");
                    var context = new DeserializationContext();
                    context.Config.SerializationPolicy = SerializationPolicies.Everything;
                    deserializedData = SerializationUtility.DeserializeValue<Dictionary<string, ISerializable>>(saveData, DataFormat.Binary, context);
                    Program.WriteToConsole("✓ Deserialized using Everything policy");
                }
                catch (Exception ex)
                {
                    Program.WriteToConsole($"Failed with Everything policy: {ex.Message}");
                    
                    // Fallback to Unity policy  
                    try
                    {
                        Program.WriteToConsole("Trying Unity serialization policy...");
                        var context = new DeserializationContext();
                        context.Config.SerializationPolicy = SerializationPolicies.Unity;
                        deserializedData = SerializationUtility.DeserializeValue<Dictionary<string, ISerializable>>(saveData, DataFormat.Binary, context);
                        Program.WriteToConsole("✓ Deserialized using Unity policy");
                    }
                    catch (Exception ex2)
                    {
                        Program.WriteToConsole($"Failed with Unity policy: {ex2.Message}");
                        
                        // Final fallback to no context (like original)
                        try
                        {
                            Program.WriteToConsole("Final fallback to no-context deserialization...");
                            deserializedData = SerializationUtility.DeserializeValue<Dictionary<string, ISerializable>>(saveData, DataFormat.Binary, null);
                            Program.WriteToConsole("✓ Deserialized using no-context method");
                        }
                        catch (Exception ex3)
                        {
                            Program.WriteToConsole($"❌ All deserialization attempts failed: {ex3.Message}");
                            return;
                        }
                    }
                }
                
                if (deserializedData == null)
                {
                    Program.WriteToConsole("❌ Deserialized data is null");
                    return;
                }
                
                Program.WriteToConsole($"✓ Deserialized as: {deserializedData.GetType().FullName}");
                
                // Debug PersistentPlayerSaveData specifically
                if (deserializedData is System.Collections.IDictionary dict && dict.Contains("PersistentPlayerSaveData"))
                {
                    Program.WriteToConsole("\n=== DEEP DEBUGGING PERSISTENT PLAYER SAVE DATA ===");
                    var persistentPlayerItem = dict["PersistentPlayerSaveData"];
                    Program.WriteToConsole($"PersistentPlayerSaveData type: {persistentPlayerItem?.GetType().FullName ?? "null"}");
                    
                    if (persistentPlayerItem != null)
                    {
                        // Show ALL fields in the wrapper
                        Program.WriteToConsole("=== WRAPPER FIELDS ===");
                        var wrapperFields = persistentPlayerItem.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        foreach (var field in wrapperFields.Take(10))
                        {
                            try
                            {
                                var value = field.GetValue(persistentPlayerItem);
                                Program.WriteToConsole($"  {field.Name} ({field.FieldType.Name}): {value?.GetType().Name ?? "null"}");
                            }
                            catch (Exception ex)
                            {
                                Program.WriteToConsole($"  {field.Name}: ERROR - {ex.Message}");
                            }
                        }
                        
                        // Check if it has a Value field (OdinSerializer wrapper)
                        var valueField = persistentPlayerItem.GetType().GetField("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (valueField != null)
                        {
                            var valueData = valueField.GetValue(persistentPlayerItem);
                            Program.WriteToConsole($"\nValue field type: {valueData?.GetType().FullName ?? "null"}");
                            
                            if (valueData != null)
                            {
                                // Show ALL fields in the PersistentSaveData
                                Program.WriteToConsole("=== ALL PERSISTENT SAVE DATA FIELDS ===");
                                var type = valueData.GetType();
                                var allFields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                
                                foreach (var field in allFields)
                                {
                                    try
                                    {
                                        var fieldValue = field.GetValue(valueData);
                                        string valueDesc = "null";
                                        
                                        if (fieldValue != null)
                                        {
                                            if (fieldValue is Array arr)
                                            {
                                                valueDesc = $"{fieldValue.GetType().Name}[{arr.Length}]";
                                                if (arr.Length > 0 && arr.Length <= 20)
                                                {
                                                    var items = new List<string>();
                                                    for (int i = 0; i < arr.Length; i++)
                                                    {
                                                        var item = arr.GetValue(i);
                                                        items.Add(item?.ToString() ?? "null");
                                                    }
                                                    valueDesc += $" = [{string.Join(", ", items)}]";
                                                }
                                            }
                                            else if (fieldValue.GetType().IsPrimitive || fieldValue is string || fieldValue is DateTime)
                                            {
                                                valueDesc = $"{fieldValue.GetType().Name} = {fieldValue}";
                                            }
                                            else
                                            {
                                                valueDesc = $"{fieldValue.GetType().Name} object";
                                            }
                                        }
                                        
                                        Program.WriteToConsole($"  {field.Name} ({field.FieldType.Name}): {valueDesc}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Program.WriteToConsole($"  {field.Name}: ERROR - {ex.Message}");
                                    }
                                }
                                
                                // Also show properties
                                Program.WriteToConsole("=== ALL PERSISTENT SAVE DATA PROPERTIES ===");
                                var allProperties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                foreach (var prop in allProperties.Take(20))
                                {
                                    try
                                    {
                                        if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                                        {
                                            var propValue = prop.GetValue(valueData);
                                            string valueDesc = propValue?.ToString() ?? "null";
                                            if (propValue is Array arr)
                                            {
                                                valueDesc = $"Array[{arr.Length}]";
                                            }
                                            Program.WriteToConsole($"  {prop.Name} ({prop.PropertyType.Name}): {valueDesc}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Program.WriteToConsole($"  {prop.Name}: ERROR - {ex.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Program.WriteToConsole("ERROR: No Value field found in wrapper!");
                        }
                    }
                    Program.WriteToConsole("=== END DEEP DEBUG ===\n");
                }
                
                // Try low-level debugging with binary reader
                try
                {
                    Program.WriteToConsole("=== LOW-LEVEL BINARY ANALYSIS ===");
                    using (var stream = new MemoryStream(saveData))
                    {
                        var reader = new BinaryDataReader(stream, new DeserializationContext());
                        
                        // Try to peek at the raw binary structure
                        Program.WriteToConsole($"Stream length: {stream.Length} bytes");
                        stream.Position = 0;
                        
                        // Read the first few bytes to see the format
                        byte[] header = new byte[Math.Min(100, (int)stream.Length)];
                        stream.Read(header, 0, header.Length);
                        stream.Position = 0;
                        
                        Program.WriteToConsole($"First 20 bytes: {string.Join(" ", header.Take(20).Select(b => b.ToString("X2")))}");
                        
                        // Try to manually read the dictionary structure
                        try
                        {
                            reader.ReadInt32(out int entryCount);
                            Program.WriteToConsole($"Dictionary entry count: {entryCount}");
                            
                            if (entryCount > 0 && entryCount < 1000) // Sanity check
                            {
                                for (int i = 0; i < Math.Min(entryCount, 10); i++)
                                {
                                    try
                                    {
                                        reader.ReadString(out string key);
                                        Program.WriteToConsole($"  Key [{i}]: {key}");
                                        
                                        if (key == "PersistentPlayerSaveData")
                                        {
                                            Program.WriteToConsole("    Found PersistentPlayerSaveData! Analyzing...");
                                            var currentPos = stream.Position;
                                            
                                            // Try to read some of the data manually
                                            try
                                            {
                                                // Skip type info and read actual data
                                                reader.ReadInt32(out int typeId);
                                                Program.WriteToConsole($"    Type ID: {typeId}");
                                                
                                                // Read next 50 bytes to see what's there
                                                byte[] dataBytes = new byte[Math.Min(100, (int)(stream.Length - stream.Position))];
                                                stream.Read(dataBytes, 0, dataBytes.Length);
                                                Program.WriteToConsole($"    Next 50 bytes: {string.Join(" ", dataBytes.Take(50).Select(b => b.ToString("X2")))}");
                                                
                                                // Look for patterns that might indicate arrays
                                                for (int j = 0; j < dataBytes.Length - 4; j++)
                                                {
                                                    int possibleLength = BitConverter.ToInt32(dataBytes, j);
                                                    if (possibleLength > 0 && possibleLength < 1000)
                                                    {
                                                        Program.WriteToConsole($"    Possible array length at offset {j}: {possibleLength}");
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Program.WriteToConsole($"    Error reading PersistentPlayerSaveData details: {ex.Message}");
                                            }
                                            
                                            stream.Position = currentPos;
                                            break;
                                        }
                                        
                                        // Skip the value data for other keys
                                        try
                                        {
                                            reader.ReadInt32(out int _); // Skip type id or length
                                        }
                                        catch
                                        {
                                            break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Program.WriteToConsole($"  Error reading entry {i}: {ex.Message}");
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Program.WriteToConsole($"Error reading dictionary structure: {ex.Message}");
                        }
                    }
                    Program.WriteToConsole("=== END LOW-LEVEL ANALYSIS ===\n");
                }
                catch (Exception ex)
                {
                    Program.WriteToConsole($"Low-level analysis failed: {ex.Message}\n");
                }
                
                // Convert to a JSON-serializable structure
                var jsonData = ConvertToJsonFriendly(deserializedData);
                
                // Serialize to JSON with pretty formatting
                var jsonSettings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Include,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    MaxDepth = 10
                };
                
                string jsonString = JsonConvert.SerializeObject(jsonData, jsonSettings);
                
                // Write to file
                File.WriteAllText(outputPath, jsonString);
                
                Program.WriteToConsole($"✓ Successfully dumped save structure to {outputPath}");
                Program.WriteToConsole($"JSON file size: {new FileInfo(outputPath).Length} bytes");
                Program.WriteToConsole("You can now analyze the complete save structure in the JSON file.");
            }
            catch (Exception ex)
            {
                Program.WriteToConsole($"❌ Failed to dump save: {ex.Message}");
                Program.WriteToConsole($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static object ConvertToJsonFriendly(object obj)
        {
            if (obj == null) return null;
            
            var type = obj.GetType();
            
            // Handle primitives and strings
            if (type.IsPrimitive || obj is string || obj is DateTime || obj is decimal)
            {
                return obj;
            }
            
            // Handle arrays
            if (obj is Array array)
            {
                var list = new List<object>();
                for (int i = 0; i < array.Length; i++)
                {
                    list.Add(ConvertToJsonFriendly(array.GetValue(i)));
                }
                return list;
            }
            
            // Handle dictionaries
            if (obj is System.Collections.IDictionary dict)
            {
                var jsonDict = new Dictionary<string, object>();
                foreach (var key in dict.Keys)
                {
                    var keyStr = key?.ToString() ?? "null";
                    jsonDict[keyStr] = ConvertToJsonFriendly(dict[key]);
                }
                return jsonDict;
            }
            
            // Handle collections
            if (obj is System.Collections.IEnumerable enumerable && !(obj is string))
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(ConvertToJsonFriendly(item));
                }
                return list;
            }
            
            // Handle complex objects - extract all fields and properties
            var jsonObj = new Dictionary<string, object>();
            
            // Add type information
            jsonObj["__type"] = type.FullName;
            
            // Check if it's an OdinSerializer Item wrapper
            var valueField = type.GetField("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (valueField != null)
            {
                var innerValue = valueField.GetValue(obj);
                jsonObj["Value"] = ConvertToJsonFriendly(innerValue);
                
                // Also check if this is a SaveObjectData wrapper - if so, include the Name property
                try
                {
                    var nameProperty = type.GetProperty("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (nameProperty != null && nameProperty.CanRead)
                    {
                        var nameValue = nameProperty.GetValue(obj);
                        if (nameValue != null)
                        {
                            jsonObj["SaveObjectName"] = nameValue.ToString();
                        }
                    }
                }
                catch (Exception ex)
                {
                    jsonObj["SaveObjectName"] = $"ERROR_READING_NAME: {ex.Message}";
                }
                
                return jsonObj;
            }
            
            // Extract all fields
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                try
                {
                    var fieldValue = field.GetValue(obj);
                    jsonObj[field.Name] = ConvertToJsonFriendly(fieldValue);
                }
                catch (Exception ex)
                {
                    jsonObj[field.Name] = $"ERROR_READING_FIELD: {ex.Message}";
                }
            }
            
            // Extract all properties
            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var prop in properties)
            {
                try
                {
                    if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                    {
                        var propValue = prop.GetValue(obj);
                        jsonObj[$"prop_{prop.Name}"] = ConvertToJsonFriendly(propValue);
                    }
                }
                catch (Exception ex)
                {
                    jsonObj[$"prop_{prop.Name}"] = $"ERROR_READING_PROPERTY: {ex.Message}";
                }
            }
            
            return jsonObj;
        }
    }
}
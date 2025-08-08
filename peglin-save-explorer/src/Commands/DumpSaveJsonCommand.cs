using System.CommandLine;
using peglin_save_explorer.Core;
using Newtonsoft.Json;
using OdinSerializer;
using ToolBox.Serialization;
using peglin_save_explorer.Utils;

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
                            Logger.Error("No save file specified and no default save file found.");
                            Logger.Info("Please specify a save file with -f or configure a default in settings.");
                            return;
                        }
                        Logger.Info($"Using default save file: {saveFilePath}");
                    }

                    if (!File.Exists(saveFilePath))
                    {
                        Logger.Error($"Save file not found: {saveFilePath}");
                        return;
                    }
                    
                    string outputPath = output ?? "save_dump.json";
                    DumpSaveToJson(saveFilePath, outputPath);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error dumping save: {ex.Message}");
                    Logger.Verbose($"Stack trace: {ex.StackTrace}");
                }
            }, fileOption, outputOption);
            
            return command;
        }
        
        private static void DumpSaveToJson(string saveFilePath, string outputPath)
        {
            try
            {
                Logger.Info($"Input: {saveFilePath}");
                Logger.Info($"Output: {outputPath}");

                // Try to load using Peglin's exact method - using their file system interface
                object? deserializedData = null;
                byte[] saveData;
                
                try
                {
                    Logger.Verbose("Attempting to use Peglin's StandardFileSystemInterface...");

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

                            Logger.Verbose($"✓ Loaded using Peglin's StandardFileSystemInterface: {saveData.Length} bytes");
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
                    Logger.Error($"Failed to use Peglin's interface: {ex.Message}");
                    Logger.Info("Falling back to direct file reading...");
                    saveData = File.ReadAllBytes(saveFilePath);
                }

                Logger.Info($"Save file size: {saveData.Length} bytes");
                
                // Try Everything serialization policy first (most permissive)
                try
                {
                    Logger.Verbose ("Attempting with Everything serialization policy (most permissive)...");
                    var context = new DeserializationContext();
                    context.Config.SerializationPolicy = SerializationPolicies.Everything;
                    deserializedData = SerializationUtility.DeserializeValue<Dictionary<string, ISerializable>>(saveData, DataFormat.Binary, context);
                    Logger.Verbose("✓ Deserialized using Everything policy");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed with Everything policy: {ex.Message}");

                    // Fallback to Unity policy
                    try
                    {
                        Logger.Info("Trying Unity serialization policy...");
                        var context = new DeserializationContext();
                        context.Config.SerializationPolicy = SerializationPolicies.Unity;
                        deserializedData = SerializationUtility.DeserializeValue<Dictionary<string, ISerializable>>(saveData, DataFormat.Binary, context);
                        Logger.Info("✓ Deserialized using Unity policy");
                    }
                    catch (Exception ex2)
                    {
                        Logger.Error($"Failed with Unity policy: {ex2.Message}");

                        // Final fallback to no context (like original)
                        try
                        {
                            Logger.Info("Final fallback to no-context deserialization...");
                            deserializedData = SerializationUtility.DeserializeValue<Dictionary<string, ISerializable>>(saveData, DataFormat.Binary, null);
                            Logger.Info("✓ Deserialized using no-context method");
                        }
                        catch (Exception ex3)
                        {
                            Logger.Error($"❌ All deserialization attempts failed: {ex3.Message}");
                            return;
                        }
                    }
                }
                
                if (deserializedData == null)
                {
                    Logger.Error("❌ Deserialized data is null");
                    return;
                }

                Logger.Verbose($"✓ Deserialized as: {deserializedData.GetType().FullName}");

                
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
                
                Logger.Info($"Successfully dumped save structure to {outputPath}");
                Logger.Info("You can now analyze the complete save structure in the JSON file.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to dump save: {ex.Message}");
                Logger.Verbose($"Stack trace: {ex.StackTrace}");
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
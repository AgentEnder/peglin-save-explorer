using Newtonsoft.Json.Linq;
using OdinSerializer;
using ToolBox.Serialization;
using System.Reflection;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Core
{
    /// <summary>
    /// Provides high-level abstractions for loading, modifying, and saving Peglin save files.
    /// Handles the complexity of OdinSerializer binary format and provides a clean API for modifications.
    /// </summary>
    public class SaveFileManager
    {
        private readonly ConfigurationManager _configManager;
        private string? _saveFilePath;
        private object? _deserializedData;
        private SerializationContext? _serializationContext;
        private Assembly? _peglinAssembly;
        private Type? _saveDataType;
        private bool _usedUnityContext;

        public SaveFileManager()
        {
            _configManager = new ConfigurationManager();
        }

        /// <summary>
        /// Loads a save file and prepares it for modification operations.
        /// </summary>
        /// <param name="saveFile">Optional specific save file. If null, uses default from config.</param>
        /// <returns>True if loaded successfully, false otherwise.</returns>
        public bool LoadSaveFile(FileInfo? saveFile = null)
        {
            try
            {
                // Determine effective file path
                if (saveFile != null && saveFile.Exists)
                {
                    _saveFilePath = saveFile.FullName;
                }
                else
                {
                    _saveFilePath = _configManager.GetEffectiveSaveFilePath();
                    if (string.IsNullOrEmpty(_saveFilePath))
                    {
                        Logger.Error("No save file specified and no default save file found.");
                        return false;
                    }
                }

                if (!File.Exists(_saveFilePath))
                {
                    Logger.Error($"File '{_saveFilePath}' does not exist.");
                    return false;
                }

                Logger.Info($"Loading save file: {_saveFilePath}");

                // Load Peglin assembly for type-safe operations if available
                var peglinPath = _configManager.GetEffectivePeglinPath();
                if (!string.IsNullOrEmpty(peglinPath) && Directory.Exists(peglinPath))
                {
                    var assemblyResult = AssemblyAnalyzer.AnalyzePeglinAssembly(peglinPath);
                    if (assemblyResult.Success && assemblyResult.LoadedAssembly != null)
                    {
                        _peglinAssembly = assemblyResult.LoadedAssembly;
                        Logger.Verbose("Loaded Peglin assembly for type-safe deserialization");
                        
                        // Try to find the correct save data type
                        var saveDataTypes = _peglinAssembly.GetTypes()
                            .Where(t => t.Name.Contains("SaveData", StringComparison.OrdinalIgnoreCase) ||
                                       t.Name.Contains("Save", StringComparison.OrdinalIgnoreCase))
                            .Where(t => !t.IsAbstract && !t.IsInterface)
                            .ToList();
                        
                        _saveDataType = saveDataTypes.FirstOrDefault(t => 
                            t.Name.Equals("SaveData", StringComparison.OrdinalIgnoreCase) ||
                            t.Name.Contains("MainSave", StringComparison.OrdinalIgnoreCase) ||
                            t.Name.Contains("GameSave", StringComparison.OrdinalIgnoreCase) ||
                            t.GetFields().Any(f => f.Name.Contains("PersistentPlayer", StringComparison.OrdinalIgnoreCase)));
                        
                        if (_saveDataType != null)
                        {
                            Logger.Verbose($"Selected save data type: {_saveDataType.FullName}");
                        }
                    }
                }

                // Read and deserialize the save file
                var saveData = File.ReadAllBytes(_saveFilePath);
                return DeserializeSaveData(saveData);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading save file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Executes a read-only function on the deserialized save data.
        /// </summary>
        /// <param name="readAction">Function that reads from the save data.</param>
        /// <returns>True if the read operation was successful.</returns>
        public bool ReadSaveData(Action<object> readAction)
        {
            if (_deserializedData == null)
            {
                Logger.Error("No save data loaded. Call LoadSaveFile first.");
                return false;
            }

            try
            {
                Logger.Verbose("Executing read operation on save data...");
                readAction(_deserializedData);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during save data read: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Executes a modification function on the deserialized save data.
        /// </summary>
        /// <param name="modificationAction">Function that modifies the save data. Returns true if changes were made.</param>
        /// <returns>True if the modification was successful and changes were made.</returns>
        public bool ModifySaveData(Func<object, bool> modificationAction)
        {
            if (_deserializedData == null)
            {
                Logger.Error("No save data loaded. Call LoadSaveFile first.");
                return false;
            }

            try
            {
                Logger.Verbose("Executing modification on save data...");
                bool modified = modificationAction(_deserializedData);
                
                if (modified)
                {
                    Logger.Info("✓ Save data modification completed successfully");
                }
                else
                {
                    Logger.Warning("Modification function reported no changes were made");
                }
                
                return modified;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during save data modification: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves the currently loaded and modified save data back to the file.
        /// Creates a backup before overwriting.
        /// </summary>
        /// <returns>True if saved successfully, false otherwise.</returns>
        public bool SaveToFile()
        {
            if (_deserializedData == null || string.IsNullOrEmpty(_saveFilePath))
            {
                Logger.Error("No save data loaded or no file path available.");
                return false;
            }

            try
            {
                Logger.Info("Validating modified save data structure...");
                
                // Validate the modified data structure before serialization
                if (!ValidateDataStructure())
                {
                    Logger.Error("Data structure validation failed. Aborting save to prevent corruption.");
                    return false;
                }

                Logger.Info("Serializing save data...");
                
                // Serialize back with the same context that was used for deserialization
                byte[]? updatedSaveData = SerializeSaveData();
                if (updatedSaveData == null)
                {
                    Logger.Error("Serialization failed.");
                    return false;
                }

                // Create backup before writing
                var backupPath = _saveFilePath + $".backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                File.Copy(_saveFilePath, backupPath, true);
                Logger.Info($"Created backup: {backupPath}");

                // Write the updated save data
                File.WriteAllBytes(_saveFilePath, updatedSaveData);
                Logger.Info($"Successfully saved changes to {_saveFilePath}");
                Logger.Info("Restart Peglin to see the changes.");
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads a save file as JSON for read-only operations (using the existing SaveDataLoader).
        /// </summary>
        /// <param name="saveFile">Optional specific save file.</param>
        /// <returns>JObject representation of the save data, or null if failed.</returns>
        public JObject? LoadSaveDataAsJson(FileInfo? saveFile = null)
        {
            return SaveDataLoader.LoadSaveData(saveFile);
        }

        /// <summary>
        /// Disposes of loaded data and resets the manager state.
        /// </summary>
        public void Reset()
        {
            _saveFilePath = null;
            _deserializedData = null;
            _serializationContext = null;
            _peglinAssembly = null;
            _saveDataType = null;
            _usedUnityContext = false;
        }

        private bool DeserializeSaveData(byte[] saveData)
        {
            // Use the same deserialization approach as UpdateCruciballLevel
            try
            {
                Logger.Verbose("Attempting with Everything serialization policy...");
                var context = new DeserializationContext();
                context.Config.SerializationPolicy = SerializationPolicies.Everything;
                _deserializedData = SerializationUtility.DeserializeValue<Dictionary<string, ISerializable>>(saveData, DataFormat.Binary, context);
                Logger.Verbose("✓ Deserialized using Everything policy");
                
                _serializationContext = new SerializationContext();
                _serializationContext.Config.SerializationPolicy = SerializationPolicies.Everything;
                _usedUnityContext = true;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Verbose($"Failed with Everything policy: {ex.Message}");
                
                try
                {
                    Logger.Verbose("Trying Unity serialization policy...");
                    var context = new DeserializationContext();
                    context.Config.SerializationPolicy = SerializationPolicies.Unity;
                    _deserializedData = SerializationUtility.DeserializeValue<Dictionary<string, ISerializable>>(saveData, DataFormat.Binary, context);
                    Logger.Verbose("✓ Deserialized using Unity policy");
                    
                    _serializationContext = new SerializationContext();
                    _serializationContext.Config.SerializationPolicy = SerializationPolicies.Unity;
                    _usedUnityContext = true;
                    return true;
                }
                catch (Exception ex2)
                {
                    Logger.Verbose($"Failed with Unity policy: {ex2.Message}");
                    
                    try
                    {
                        Logger.Verbose("Final fallback to no-context deserialization...");
                        _deserializedData = SerializationUtility.DeserializeValue<Dictionary<string, ISerializable>>(saveData, DataFormat.Binary, null);
                        Logger.Verbose("✓ Deserialized using no-context method");
                        
                        _serializationContext = null;
                        _usedUnityContext = false;
                        return true;
                    }
                    catch (Exception ex3)
                    {
                        Logger.Error($"❌ All deserialization attempts failed: {ex3.Message}");
                        return false;
                    }
                }
            }
        }

        private bool ValidateDataStructure()
        {
            try
            {
                // Try a test serialization to make sure the structure is still valid
                byte[] testSerialization;
                if (_saveDataType != null && _usedUnityContext && _serializationContext != null)
                {
                    var method = typeof(SerializationUtility).GetMethod("SerializeValue", new[] { _saveDataType, typeof(DataFormat), typeof(SerializationContext) });
                    testSerialization = (byte[])method.Invoke(null, new object[] { _deserializedData, DataFormat.Binary, _serializationContext });
                    Logger.Verbose($"✓ Type-specific validation passed with {_saveDataType.Name}");
                }
                else if (_usedUnityContext && _serializationContext != null)
                {
                    testSerialization = SerializationUtility.SerializeValue(_deserializedData, DataFormat.Binary, _serializationContext);
                    Logger.Verbose("✓ Generic Unity context validation passed");
                }
                else
                {
                    testSerialization = SerializationUtility.SerializeValue(_deserializedData, DataFormat.Binary);
                    Logger.Verbose("✓ Default context validation passed");
                }
                
                if (testSerialization?.Length == 0)
                {
                    Logger.Error("Error: Test serialization produced empty data");
                    return false;
                }
                
                Logger.Verbose($"✓ Data structure validation passed (test size: {testSerialization?.Length} bytes)");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Data structure validation failed: {ex.Message}");
                Logger.Error("The modification may have corrupted the save data structure.");
                return false;
            }
        }

        private byte[]? SerializeSaveData()
        {
            try
            {
                if (_saveDataType != null && _usedUnityContext && _serializationContext != null)
                {
                    Logger.Verbose($"Serializing with Unity context using specific type: {_saveDataType.Name}...");
                    var method = typeof(SerializationUtility).GetMethod("SerializeValue", new[] { _saveDataType, typeof(DataFormat), typeof(SerializationContext) });
                    return (byte[])method.Invoke(null, new object[] { _deserializedData, DataFormat.Binary, _serializationContext });
                }
                else if (_usedUnityContext && _serializationContext != null)
                {
                    Logger.Verbose("Serializing with fresh Everything policy context...");
                    var freshContext = new SerializationContext();
                    freshContext.Config.SerializationPolicy = SerializationPolicies.Everything;
                    return SerializationUtility.SerializeValue(_deserializedData, DataFormat.Binary, freshContext);
                }
                else
                {
                    Logger.Verbose("Serializing with fresh Everything policy context (matching deserialization)...");
                    if (_serializationContext == null)
                    {
                        _serializationContext = new SerializationContext();
                        _serializationContext.Config.SerializationPolicy = SerializationPolicies.Everything;
                    }
                    return SerializationUtility.SerializeValue(_deserializedData, DataFormat.Binary, _serializationContext);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Serialization failed: {ex.Message}");
                return null;
            }
        }
    }
}
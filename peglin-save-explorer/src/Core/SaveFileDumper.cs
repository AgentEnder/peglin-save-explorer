using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OdinSerializer;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Core
{
    public class SaveFileDumper
    {
        private Assembly? peglinAssembly;
        private readonly ConfigurationManager? _configManager;

        public SaveFileDumper(ConfigurationManager? configManager = null)
        {
            _configManager = configManager;
        }

        private bool IsSuppressed()
        {
            // Check if console output should be suppressed (when in widget mode)
            return Program.suppressConsoleOutput;
        }

        public string DumpSaveFile(byte[] saveData)
        {
            var result = new JObject();

            try
            {
                // Load Peglin assembly for type definitions first
                LoadPeglinAssembly();

                // Try to deserialize using OdinSerializer
                using var stream = new MemoryStream(saveData);

                // First try binary format with different approaches
                try
                {
                    // Try basic deserialization
                    var binaryData = SerializationUtility.DeserializeValue<object>(saveData, DataFormat.Binary);
                    result["format"] = "Binary";
                    result["success"] = true;
                    result["dataType"] = binaryData?.GetType()?.FullName ?? "null";
                    result["dataIsNull"] = binaryData == null;
                    result["data"] = ConvertToJson(binaryData);

                    // Try deserializing without specifying generic type - let OdinSerializer auto-detect
                    try
                    {
                        using var autoStream = new MemoryStream(saveData);
                        var context = new DeserializationContext();
                        var reader = new BinaryDataReader(autoStream, context);
                        var autoData = Serializer.Get<object>().ReadValue(reader);
                        if (autoData != null && autoData != binaryData)
                        {
                            result["autoDeserializationSuccess"] = true;
                            result["autoDataType"] = autoData.GetType().FullName;
                            result["autoData"] = ConvertToJson(autoData);
                        }
                    }
                    catch (Exception autoEx)
                    {
                        result["autoDeserializationError"] = autoEx.Message;
                    }

                    // Try with Unity context if basic fails
                    if (binaryData == null)
                    {
                        try
                        {
                            var context = new DeserializationContext();
                            context.Config.SerializationPolicy = SerializationPolicies.Unity;
                            var unityData = SerializationUtility.DeserializeValue<object>(saveData, DataFormat.Binary, context);
                            if (unityData != null)
                            {
                                result["unityContextSuccess"] = true;
                                result["unityDataType"] = unityData.GetType().FullName;
                                result["unityData"] = ConvertToJson(unityData);
                            }
                        }
                        catch (Exception unityEx)
                        {
                            result["unityContextError"] = unityEx.Message;
                        }
                    }

                    // If data is still empty/null, add raw analysis for comparison
                    if (binaryData == null ||
                        (binaryData is object && binaryData.ToString() == binaryData.GetType().ToString()))
                    {
                        result["rawAnalysisForComparison"] = AnalyzeRawData(saveData);
                    }
                }
                catch (Exception binaryEx)
                {
                    result["binaryError"] = binaryEx.Message;

                    // Try JSON format
                    try
                    {
                        var jsonData = SerializationUtility.DeserializeValue<object>(saveData, DataFormat.JSON);
                        result["format"] = "JSON";
                        result["success"] = true;
                        result["data"] = ConvertToJson(jsonData);
                    }
                    catch (Exception jsonEx)
                    {
                        result["jsonError"] = jsonEx.Message;
                        result["success"] = false;

                        // Fall back to raw analysis
                        result["rawAnalysis"] = AnalyzeRawData(saveData);
                    }
                }

                // Always try with Peglin types if we have the assembly loaded (regardless of basic deserialization results)
                result["peglinAssemblyLoaded"] = peglinAssembly != null;
                if (peglinAssembly != null)
                {
                    result["peglinAssemblyName"] = peglinAssembly.FullName;

                    // Try to find known save data types from the hex analysis
                    var potentialTypes = new[]
                    {
                        "SaveObjectData",
                        "SaveManager+SaveVersion",
                        "PersistentPlayerSaveData",
                        "SaveData",
                        "GameSave",
                        "PlayerSave"
                    };

                    var peglinDeserializationAttempts = new JArray();

                    foreach (var typeName in potentialTypes)
                    {
                        try
                        {
                            var saveType = FindTypeInAssembly(typeName);
                            if (saveType != null)
                            {
                                var attempt = new JObject();
                                attempt["typeName"] = typeName;
                                attempt["fullTypeName"] = saveType.FullName;

                                try
                                {
                                    // Use reflection to call SerializationUtility.DeserializeValue<T>
                                    var method = typeof(SerializationUtility)
                                        .GetMethod(nameof(SerializationUtility.DeserializeValue), new[] { typeof(byte[]), typeof(DataFormat) })
                                        ?.MakeGenericMethod(saveType);

                                    if (method != null)
                                    {
                                        var peglinData = method.Invoke(null, new object[] { saveData, DataFormat.Binary });
                                        if (peglinData != null)
                                        {
                                            attempt["success"] = true;
                                            attempt["dataType"] = peglinData.GetType().FullName;
                                            attempt["data"] = ConvertToJson(peglinData);
                                            result["peglinDeserializationSuccess"] = true;
                                            result["successfulType"] = typeName;
                                            result["peglinData"] = attempt["data"];
                                        }
                                        else
                                        {
                                            attempt["success"] = false;
                                            attempt["reason"] = "Deserialized to null";
                                        }
                                    }
                                }
                                catch (Exception typeEx)
                                {
                                    attempt["success"] = false;
                                    attempt["error"] = typeEx.Message;
                                }

                                peglinDeserializationAttempts.Add(attempt);
                            }
                        }
                        catch (Exception)
                        {
                            // Type not found, continue
                        }
                    }

                    result["peglinTypeAttempts"] = peglinDeserializationAttempts;
                }
            }
            catch (Exception ex)
            {
                result["error"] = ex.Message;
                result["success"] = false;
                result["rawAnalysis"] = AnalyzeRawData(saveData);
            }

            return result.ToString(Formatting.Indented);
        }

        private JToken ConvertToJson(object? obj)
        {
            if (obj == null) return JValue.CreateNull();

            try
            {
                // Use Newtonsoft.Json's serialization with custom settings
                var settings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Include,
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    TypeNameHandling = TypeNameHandling.Auto
                };

                var jsonString = JsonConvert.SerializeObject(obj, Formatting.None, settings);
                return JToken.Parse(jsonString);
            }
            catch (Exception ex)
            {
                // Fallback to basic object inspection
                var fallback = new JObject();
                fallback["_error"] = ex.Message;
                fallback["_type"] = obj.GetType().FullName;
                fallback["_toString"] = obj.ToString();
                return fallback;
            }
        }

        private JObject AnalyzeRawData(byte[] data)
        {
            var analysis = new JObject();

            // Basic stats
            analysis["fileSize"] = data.Length;
            analysis["fileSizeKB"] = Math.Round(data.Length / 1024.0, 2);

            // Check for common OdinSerializer markers
            var markers = new[]
            {
                new byte[] { 0x4F, 0x44, 0x49, 0x4E }, // "ODIN" in ASCII
                new byte[] { 0x00, 0x01, 0x00, 0x00 }, // Common version markers
                new byte[] { 0x01, 0x00, 0x00, 0x00 }, // Little-endian int 1
            };

            var markerInfo = new JArray();
            foreach (var marker in markers)
            {
                var positions = FindByteSequence(data, marker);
                if (positions.Length > 0)
                {
                    var info = new JObject();
                    info["pattern"] = BitConverter.ToString(marker);
                    info["positions"] = new JArray(positions);
                    markerInfo.Add(info);
                }
            }
            analysis["binaryMarkers"] = markerInfo;

            // Hex dump of first 1000 bytes
            var hexDump = BitConverter.ToString(data, 0, Math.Min(1000, data.Length)).Replace("-", " ");
            analysis["hexDumpFirst1000"] = hexDump;

            // Try to detect the data format
            analysis["formatDetection"] = DetectFormat(data);

            return analysis;
        }

        private int[] FindByteSequence(byte[] data, byte[] sequence)
        {
            var positions = new System.Collections.Generic.List<int>();

            for (int i = 0; i <= data.Length - sequence.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < sequence.Length; j++)
                {
                    if (data[i + j] != sequence[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    positions.Add(i);
                }
            }

            return positions.ToArray();
        }

        private JObject DetectFormat(byte[] data)
        {
            var detection = new JObject();

            // Check if it starts with JSON-like characters
            if (data.Length > 0 && (data[0] == '{' || data[0] == '['))
            {
                detection["likelyJSON"] = true;
            }

            // Check for binary format indicators
            if (data.Length >= 4)
            {
                var first4 = BitConverter.ToInt32(data, 0);
                detection["first4BytesAsInt"] = first4;
                detection["first4BytesAsHex"] = BitConverter.ToString(data, 0, 4);
            }

            // Check entropy to determine if it's compressed/encrypted
            detection["entropy"] = CalculateEntropy(data);

            return detection;
        }

        private double CalculateEntropy(byte[] data)
        {
            var frequency = new int[256];
            foreach (byte b in data)
            {
                frequency[b]++;
            }

            double entropy = 0.0;
            double length = data.Length;

            for (int i = 0; i < 256; i++)
            {
                if (frequency[i] > 0)
                {
                    double probability = frequency[i] / length;
                    entropy -= probability * Math.Log2(probability);
                }
            }

            return entropy;
        }

        private void LoadPeglinAssembly()
        {
            if (peglinAssembly != null) return;

            // Set up assembly resolution handler for missing Unity dependencies
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            try
            {
                // Try multiple possible paths
                var assemblyPaths = new List<string>();

                // First check if we have a configured path
                if (_configManager != null)
                {
                    var configuredPath = _configManager.GetEffectivePeglinPath();
                    if (!string.IsNullOrEmpty(configuredPath))
                    {
                        var dllPath = Path.Combine(configuredPath, "Peglin_Data", "Managed", "Assembly-CSharp.dll");
                        if (File.Exists(dllPath))
                        {
                            assemblyPaths.Add(dllPath);
                        }
                    }
                }

                // Then try the actual Peglin installation
                var peglinInstallPath = @"G:\SteamLibrary\steamapps\common\Peglin\Peglin_Data\Managed\Assembly-CSharp.dll";
                if (File.Exists(peglinInstallPath) && !assemblyPaths.Contains(peglinInstallPath))
                {
                    assemblyPaths.Add(peglinInstallPath);
                }

                // Try common Steam installation paths
                var steamPaths = new[]
                {
                    @"C:\Program Files (x86)\Steam\steamapps\common\Peglin\Peglin_Data\Managed\Assembly-CSharp.dll",
                    @"C:\Program Files\Steam\steamapps\common\Peglin\Peglin_Data\Managed\Assembly-CSharp.dll",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Steam\steamapps\common\Peglin\Peglin_Data\Managed\Assembly-CSharp.dll")
                };

                assemblyPaths.AddRange(steamPaths.Where(File.Exists).Where(p => !assemblyPaths.Contains(p)));

                // Fall back to local PeglinDLLs directory for development
                var basePaths = new[]
                {
                    AppDomain.CurrentDomain.BaseDirectory,
                    Directory.GetCurrentDirectory(),
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ""
                };

                foreach (var basePath in basePaths)
                {
                    var localPath = Path.Combine(basePath, "PeglinDLLs", "Assembly-CSharp.dll");
                    if (File.Exists(localPath))
                    {
                        assemblyPaths.Add(localPath);
                    }
                }

                // Try to load from the first available path
                foreach (var assemblyPath in assemblyPaths)
                {
                    Logger.Debug($"Trying assembly path: {assemblyPath}");

                    if (File.Exists(assemblyPath))
                    {
                        Logger.Debug($"Found assembly at: {assemblyPath}");
                        peglinAssembly = Assembly.LoadFrom(assemblyPath);
                        Logger.Debug($"Successfully loaded assembly: {peglinAssembly.FullName}");
                        return;
                    }
                }

                Logger.Warning("Assembly-CSharp.dll not found in any expected location");
                Logger.Info("Please ensure Peglin is installed via Steam");
            }
            catch (Exception ex)
            {
                // Assembly loading failed, continue without it
                Logger.Warning($"Could not load Peglin assembly: {ex.Message}");
            }
            finally
            {
                // Remove the handler after loading
                AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            }
        }

        private Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            // Ignore Unity and other game-specific assemblies
            var assemblyName = new AssemblyName(args.Name).Name;
            if (assemblyName != null &&
                (assemblyName.StartsWith("UnityEngine") ||
                 assemblyName.StartsWith("Unity.") ||
                 assemblyName.StartsWith("DOTween") ||
                 assemblyName.StartsWith("Rewired") ||
                 assemblyName.StartsWith("Assembly-CSharp-firstpass") ||
                 assemblyName == "Peglin.OdinSerializer"))
            {
                // Return null to indicate we can't resolve these
                return null;
            }

            return null;
        }

        private Type? FindTypeInAssembly(string typeName)
        {
            if (peglinAssembly == null) return null;

            try
            {
                // Try exact name first
                var type = peglinAssembly.GetType(typeName);
                if (type != null) return type;

                // Try searching all types for partial matches
                var types = peglinAssembly.GetTypes();
                foreach (var t in types)
                {
                    if (t.Name.Contains(typeName) || t.FullName?.Contains(typeName) == true)
                    {
                        return t;
                    }
                }
            }
            catch (Exception)
            {
                // Type search failed
            }

            return null;
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using peglin_save_explorer.Core;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Extractors
{
    public class EnumExtractor
    {
        private Assembly? _assembly;
        private readonly Dictionary<string, Dictionary<int, string>> _enumCache = new();

        /// <summary>
        /// Load the Assembly-CSharp.dll from the Peglin installation
        /// </summary>
        public bool LoadAssembly(string peglinPath)
        {
            try
            {
                // Find Assembly-CSharp.dll in the Peglin installation
                string assemblyPath = FindAssemblyCSharp(peglinPath);
                if (string.IsNullOrEmpty(assemblyPath))
                {
                    Logger.Error($"[EnumExtractor] Could not find Assembly-CSharp.dll in {peglinPath}");
                    return false;
                }

                Logger.Verbose($"[EnumExtractor] Loading assembly from: {assemblyPath}");
                _assembly = Assembly.LoadFrom(assemblyPath);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[EnumExtractor] Error loading assembly: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Find the Assembly-CSharp.dll file in the Peglin installation directory
        /// </summary>
        private string FindAssemblyCSharp(string peglinPath)
        {
            // Common locations for Assembly-CSharp.dll
            var possiblePaths = new[]
            {
                Path.Combine(peglinPath, "Peglin_Data", "Managed", "Assembly-CSharp.dll"),
                Path.Combine(peglinPath, "Peglin.app", "Contents", "Resources", "Data", "Managed", "Assembly-CSharp.dll"),
                Path.Combine(peglinPath, "Data", "Managed", "Assembly-CSharp.dll"),
                Path.Combine(peglinPath, "Managed", "Assembly-CSharp.dll")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Try to find it recursively
            try
            {
                var files = Directory.GetFiles(peglinPath, "Assembly-CSharp.dll", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[EnumExtractor] Error searching for Assembly-CSharp.dll: {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Get enum values as a dictionary mapping int values to names
        /// </summary>
        public Dictionary<int, string> GetEnumValues(string enumTypeName)
        {
            if (_enumCache.ContainsKey(enumTypeName))
            {
                return _enumCache[enumTypeName];
            }

            if (_assembly == null)
            {
                var configurationManager = new ConfigurationManager();
                LoadAssembly(configurationManager.GetEffectivePeglinPath());
            }

            try
            {
                // Try to find the enum type
                Type? enumType = null;
                
                // Try with namespace first
                if (enumTypeName.Contains("."))
                {
                    enumType = _assembly.GetType(enumTypeName);
                }
                else
                {
                    // Search all types for the enum
                    enumType = _assembly.GetTypes()
                        .FirstOrDefault(t => t.IsEnum && t.Name == enumTypeName);
                    
                    // Also try common namespaces
                    if (enumType == null)
                    {
                        var commonNamespaces = new[] { "Relics", "Orbs", "Battle" };
                        foreach (var ns in commonNamespaces)
                        {
                            enumType = _assembly.GetType($"{ns}.{enumTypeName}");
                            if (enumType != null) break;
                        }
                    }
                }

                if (enumType == null || !enumType.IsEnum)
                {
                    Logger.Error($"[EnumExtractor] Enum type '{enumTypeName}' not found");
                    return new Dictionary<int, string>();
                }

                // Get all enum values
                var values = new Dictionary<int, string>();
                foreach (var value in Enum.GetValues(enumType))
                {
                    int intValue = (int)value;
                    string name = value.ToString() ?? intValue.ToString();
                    values[intValue] = name;
                }

                _enumCache[enumTypeName] = values;
                Logger.Verbose($"[EnumExtractor] Extracted {values.Count} values from enum {enumType.FullName}");
                return values;
            }
            catch (Exception ex)
            {
                Logger.Error($"[EnumExtractor] Error extracting enum values: {ex.Message}");
                return new Dictionary<int, string>();
            }
        }

        /// <summary>
        /// Get the name of an enum value
        /// </summary>
        public string GetEnumName(string enumTypeName, int value)
        {
            var values = GetEnumValues(enumTypeName);
            return values.ContainsKey(value) ? values[value] : value.ToString();
        }
    }
}
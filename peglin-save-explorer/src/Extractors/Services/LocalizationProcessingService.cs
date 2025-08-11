using System;
using System.Collections.Generic;
using System.Linq;
using AssetRipper.Import.Structure.Assembly.Serializable;
using peglin_save_explorer.Data;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Extractors.Services
{
    /// <summary>
    /// Service responsible for localization parameter processing and token resolution
    /// </summary>
    public class LocalizationProcessingService
    {
        /// <summary>
        /// Extracts localization parameters from a LocalizationParamsManager component data
        /// </summary>
        public Dictionary<string, string>? ExtractLocalizationParams(Dictionary<string, object> componentData)
        {
            try
            {
                var parameters = new Dictionary<string, string>();

                Logger.Debug($"🔍 ExtractLocalizationParams: Found {componentData.Count} keys: {string.Join(", ", componentData.Keys)}");

                // Try to extract from _Params field
                if (componentData.TryGetValue("_Params", out var paramsObj))
                {
                    Logger.Debug($"🔍 _Params field found. Type: {paramsObj?.GetType().Name ?? "null"}");
                    
                    if (paramsObj is IEnumerable<object> paramsArray)
                    {
                        var paramsList = paramsArray.ToList();
                        Logger.Debug($"🔍 _Params is enumerable with {paramsList.Count} items");
                        
                        if (paramsList.Count == 0)
                        {
                            Logger.Debug($"🔍 _Params array is empty");
                            return null;
                        }
                        
                        foreach (var param in paramsList)
                        {
                            Logger.Debug($"🔍 Processing param: Type={param?.GetType().Name}");
                            
                            // Handle SerializableStructure objects (the actual format)
                            if (param is SerializableStructure structure)
                            {
                                Logger.Debug($"🔍 Converting SerializableStructure to dictionary...");
                                try
                                {
                                    var paramDict = new Dictionary<string, object>();
                                    foreach (var field in structure.Type.Fields)
                                    {
                                        if (structure.TryGetField(field.Name, out var fieldValue))
                                        {
                                            paramDict[field.Name] = fieldValue.AsString ?? fieldValue.CValue ?? fieldValue.PValue;
                                        }
                                    }
                                    
                                    Logger.Debug($"🔍 SerializableStructure converted to dict with keys: {string.Join(", ", paramDict.Keys)}");
                                    
                                    // Try both "Name"/"Value" (from backup analysis) and "key"/"value" (current assumption)
                                    var key = paramDict.TryGetValue("Name", out var nameObj) ? nameObj?.ToString() :
                                             paramDict.TryGetValue("key", out var keyObj) ? keyObj?.ToString() : null;
                                    var value = paramDict.TryGetValue("Value", out var valueObj) ? valueObj?.ToString() :
                                               paramDict.TryGetValue("value", out var val2Obj) ? val2Obj?.ToString() : null;

                                    Logger.Debug($"🔍 Extracted: key='{key}', value='{value}' (from Name/Value or key/value)");

                                    if (!string.IsNullOrEmpty(key) && value != null)
                                    {
                                        parameters[key] = value;
                                        Logger.Debug($"✅ Added parameter: {key} = {value}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Debug($"⚠️ Error converting SerializableStructure: {ex.Message}");
                                }
                            }
                            // Fallback for dictionary format (original logic)
                            else if (param is Dictionary<string, object> paramDict)
                            {
                                Logger.Debug($"🔍 Param dict has keys: {string.Join(", ", paramDict.Keys)}");
                                
                                // Try both "Name"/"Value" (from backup analysis) and "key"/"value" (current assumption)
                                var key = paramDict.TryGetValue("Name", out var nameObj) ? nameObj?.ToString() :
                                         paramDict.TryGetValue("key", out var keyObj) ? keyObj?.ToString() : null;
                                var value = paramDict.TryGetValue("Value", out var valueObj) ? valueObj?.ToString() :
                                           paramDict.TryGetValue("value", out var val2Obj) ? val2Obj?.ToString() : null;

                                Logger.Debug($"🔍 Extracted: key='{key}', value='{value}' (from Name/Value or key/value)");

                                if (!string.IsNullOrEmpty(key) && value != null)
                                {
                                    parameters[key] = value;
                                }
                            }
                            else
                            {
                                Logger.Debug($"🔍 Param is not a recognized type: {param?.GetType().Name}");
                            }
                        }
                    }
                    else
                    {
                        Logger.Debug($"🔍 _Params is not enumerable. Type: {paramsObj?.GetType().Name}");
                    }
                }
                else
                {
                    Logger.Debug($"🔍 _Params field not found in component data");
                }

                Logger.Debug($"🔍 ExtractLocalizationParams: Extracted {parameters.Count} parameters");
                if (parameters.Count > 0)
                {
                    Logger.Debug($"🔍 Sample parameters: {string.Join(", ", parameters.Take(3).Select(p => $"{p.Key}={p.Value}"))}");
                }
                return parameters.Count > 0 ? parameters : null;
            }
            catch (Exception ex)
            {
                Logger.Debug($"⚠️ Error extracting localization parameters: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts localization parameters directly from raw SerializableStructure
        /// </summary>
        public Dictionary<string, string>? ExtractLocalizationParamsFromRawStructure(SerializableStructure structure)
        {
            try
            {
                var parameters = new Dictionary<string, string>();

                foreach (var field in structure.Type.Fields)
                {
                    try
                    {
                        var fieldName = GetFieldName(field);
                        if (fieldName == "_Params")
                        {
                            if (structure.TryGetField(field.Name, out var value) && value.CValue is IEnumerable<object> paramsArray)
                            {
                                foreach (var param in paramsArray)
                                {
                                    // Extract key-value pairs from each parameter
                                    ExtractParameterKeyValue(param, parameters);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"⚠️ Error processing localization field: {ex.Message}");
                    }
                }

                return parameters.Count > 0 ? parameters : null;
            }
            catch (Exception ex)
            {
                Logger.Debug($"⚠️ Error extracting localization parameters from raw structure: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets known localization parameters for specific orbs based on game knowledge
        /// </summary>
        public Dictionary<string, string>? GetKnownLocalizationParams(string orbName, int level)
        {
            var orbNameLower = orbName.ToLowerInvariant();

            if (orbNameLower.Contains("orbelisk"))
            {
                return new Dictionary<string, string>
                {
                    ["damage"] = (2 * level).ToString(),
                    ["health"] = (5 * level).ToString()
                };
            }

            if (orbNameLower.Contains("doctorb"))
            {
                return new Dictionary<string, string>
                {
                    ["healing"] = (3 + level).ToString()
                };
            }

            if (orbNameLower.Contains("ballwark"))
            {
                return new Dictionary<string, string>
                {
                    ["shield"] = (level * 2).ToString()
                };
            }

            return null;
        }

        /// <summary>
        /// Resolves tokens in localization strings using provided parameters
        /// </summary>
        public string ResolveTokens(string input, Dictionary<string, string> parameters)
        {
            if (string.IsNullOrEmpty(input) || parameters.Count == 0)
                return input;

            var result = input;
            foreach (var kvp in parameters)
            {
                // Support both {key} and {{key}} token formats
                result = result.Replace($"{{{kvp.Key}}}", kvp.Value);
                result = result.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// Resolves tokens in an array of localization strings
        /// </summary>
        public List<string> ResolveTokens(List<string> inputs, Dictionary<string, string> parameters)
        {
            if (inputs == null || parameters.Count == 0)
                return inputs ?? new List<string>();

            return inputs.Select(input => ResolveTokens(input, parameters)).ToList();
        }

        /// <summary>
        /// Consolidates individual orb instances into families with levels
        /// </summary>
        public void ConsolidateOrbsIntoFamilies(Dictionary<string, OrbData> orbs, Dictionary<string, OrbGroupedData> orbFamilies)
        {
            try
            {
                var familyGroups = orbs.Values
                    .GroupBy(orb => StripLevelMarkers(orb.Name ?? orb.Id))
                    .Where(group => group.Count() > 1); // Only group if there are multiple levels

                foreach (var family in familyGroups)
                {
                    var familyName = family.Key;
                    var orbsInFamily = family.OrderBy(orb => 
                        orb.RawData?.TryGetValue("Level", out var level) == true ? 
                        Convert.ToInt32(level) : 1).ToList();

                    var baseOrb = orbsInFamily.First();
                    var familyData = new OrbGroupedData
                    {
                        Id = $"{familyName.ToLowerInvariant().Replace(" ", "_")}_family",
                        Name = familyName,
                        LocKey = baseOrb.LocKey,
                        Description = baseOrb.Description,
                        RarityValue = baseOrb.RarityValue ?? 0,
                        Rarity = baseOrb.Rarity,
                        OrbType = baseOrb.OrbType,
                        CorrelatedSpriteId = baseOrb.CorrelatedSpriteId,
                        SpriteFilePath = baseOrb.SpriteFilePath,
                        CorrelationConfidence = baseOrb.CorrelationConfidence,
                        CorrelationMethod = baseOrb.CorrelationMethod,
                        AlternateSpriteIds = baseOrb.AlternateSpriteIds,
                        Levels = orbsInFamily.Select(orb => new OrbLevelData
                        {
                            Level = orb.RawData?.TryGetValue("Level", out var level) == true ? 
                                Convert.ToInt32(level) : 1,
                            Leaf = orb,
                            LeafId = orb.Id
                        }).ToList()
                    };

                    orbFamilies[familyData.Id] = familyData;

                    Logger.Debug($"🔗 Created orb family '{familyName}' with {orbsInFamily.Count} levels");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error consolidating orbs into families: {ex.Message}");
            }
        }

        private void ExtractParameterKeyValue(object param, Dictionary<string, string> parameters)
        {
            try
            {
                if (param is SerializableStructure paramStructure)
                {
                    string? key = null, value = null;

                    foreach (var field in paramStructure.Type.Fields)
                    {
                        var fieldName = GetFieldName(field);
                        if (paramStructure.TryGetField(field.Name, out var fieldValue))
                        {
                            if (fieldName == "key" || fieldName == "Key")
                            {
                                key = fieldValue.AsString;
                            }
                            else if (fieldName == "value" || fieldName == "Value")
                            {
                                value = fieldValue.AsString;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(key) && value != null)
                    {
                        parameters[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"⚠️ Error extracting parameter key-value: {ex.Message}");
            }
        }

        private static string StripLevelMarkers(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";

            // Remove common level indicators: I, II, III, IV, V, 1, 2, 3, 4, 5
            var patterns = new[] { " I", " II", " III", " IV", " V", " 1", " 2", " 3", " 4", " 5" };
            var result = s;

            foreach (var pattern in patterns)
            {
                if (result.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    result = result.Substring(0, result.Length - pattern.Length);
                    break;
                }
            }

            return result.Trim();
        }

        /// <summary>
        /// Extracts field name from field object (handles dynamic/reflection types)
        /// </summary>
        private string? GetFieldName(dynamic field)
        {
            try
            {
                // Handle dynamic field objects
                return field?.Name?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}

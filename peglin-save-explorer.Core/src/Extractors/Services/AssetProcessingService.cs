using System;
using System.Collections.Generic;
using System.Linq;
using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Extensions;
using peglin_save_explorer.Extractors.Models;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Extractors.Services
{
    /// <summary>
    /// Service responsible for general asset processing and data conversion
    /// </summary>
    public class AssetProcessingService
    {
        /// <summary>
        /// Converts a SerializableStructure to a dictionary and extracts sprite references
        /// </summary>
        public Dictionary<string, object> ConvertStructureToDict(
            SerializableStructure structure,
            AssetCollection collection,
            out IUnityAssetBase? spriteReference)
        {
            var result = new Dictionary<string, object>();
            spriteReference = null;

            foreach (var field in structure.Type.Fields)
            {
                try
                {
                    var fieldName = GetFieldName(field);
                    if (string.IsNullOrEmpty(fieldName)) continue;

                    if (structure.TryGetField(field.Name, out var value))
                    {
                        var convertedValue = ConvertSerializableValue(value, field, collection);

                        // Check if this field might contain a sprite reference
                        if (spriteReference == null && convertedValue is IUnityAssetBase assetRef)
                        {
                            if (SpriteProcessingService.IsSpriteField(fieldName) || SpriteProcessingService.CouldBeSpriteField(fieldName))
                            {
                                spriteReference = assetRef;
                            }
                        }

                        result[fieldName] = convertedValue;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"⚠️ Error processing field in structure: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Converts a SerializableValue to a usable object, properly handling arrays and all data types
        /// </summary>
        public object ConvertSerializableValue(SerializableValue value, dynamic field, AssetCollection? collection = null)
        {
            try
            {
                // Handle string values
                if (!string.IsNullOrEmpty(value.AsString))
                {
                    return value.AsString;
                }

                // Handle complex values
                if (value.CValue != null)
                {
                    if (value.CValue is SerializableStructure subStructure)
                    {
                        return ConvertStructureToDict(subStructure, collection!, out _);
                    }
                    else if (value.CValue is IUnityAssetBase asset)
                    {
                        return asset;
                    }
                    else if (value.CValue is System.Collections.IEnumerable enumerable && !(value.CValue is string))
                    {
                        return enumerable.Cast<object>()
                            .Select(item => item is SerializableValue sv ? ConvertSerializableValue(sv, field, collection) : item)
                            .ToList();
                    }

                    return value.CValue;
                }

                // Handle primitive values - prioritize proper typed accessors
                // For numeric fields, prefer typed accessors even if they're 0
                // Only skip if the field appears to be unset (all values are default)

                // Check if we have any actual numeric data (non-default values)
                bool hasNumericData = value.AsSingle != 0 || value.AsDouble != 0 ||
                                     value.AsInt32 != 0 || value.AsInt64 != 0 ||
                                     value.PValue != 0;

                // If we have numeric data or no string value, use typed accessors
                if (hasNumericData || string.IsNullOrEmpty(value.AsString))
                {
                    // For fields that should be integers (like Level, Count, Types, etc.), prioritize integer accessors
                    var fieldName = GetFieldName(field) ?? "";
                    bool shouldBeInteger = fieldName.Equals("Level", StringComparison.OrdinalIgnoreCase) ||
                                         fieldName.Equals("Count", StringComparison.OrdinalIgnoreCase) ||
                                         fieldName.EndsWith("Level", StringComparison.OrdinalIgnoreCase) ||
                                         fieldName.EndsWith("Count", StringComparison.OrdinalIgnoreCase) ||
                                         fieldName.EndsWith("Type", StringComparison.OrdinalIgnoreCase) ||
                                         fieldName.EndsWith("Types", StringComparison.OrdinalIgnoreCase);

                    if (shouldBeInteger)
                    {
                        return _GetIntValue(value);
                    }
                    else
                    {
                        // Check if the float value seems unreasonable (very small scientific notation)
                        // These are often incorrectly interpreted integer values
                        var floatValue = value.AsSingle;
                        var intValue = value.AsInt32;
                        
                        // If we have a reasonable integer value but the float is extremely small,
                        // it's likely the data should be interpreted as an integer
                        if (intValue != 0 && Math.Abs(floatValue) < 1E-35)
                        {
                            return _GetIntValue(value);
                        }
                        
                        // For other fields (like damage), prioritize float values
                        var returnedFloatValue = _GetFloatValue(value);
                        floatValue = returnedFloatValue is float f ? f : Convert.ToSingle(returnedFloatValue);

                        if ((double)floatValue > 0 && Math.Abs((double)floatValue) > 1E-35)
                        {
                            return floatValue;
                        }

                        // If all typed values are 0 but we detected numeric data, return the most appropriate type
                        return _GetIntValue(value);
                    }
                }

                if (value.AsBoolean) return value.AsBoolean;
                return value.ToString() ?? "";
            }
            catch (Exception ex)
            {
                Logger.Debug($"⚠️ Error converting serializable value: {ex.Message}");
                return value.ToString() ?? "";
            }
        }

        private object _GetIntValue(SerializableValue value)
        {
            if (value.AsInt32 != 0) return value.AsInt32;
            if (value.AsInt64 != 0) return value.AsInt64;

            return 0;
        }

        private object _GetFloatValue(SerializableValue value)
        {
            // Only return float values if they're reasonable (not tiny scientific notation)
            if (value.AsSingle != 0 && Math.Abs(value.AsSingle) > 1E-35) return value.AsSingle;
            if (value.AsDouble != 0 && Math.Abs(value.AsDouble) > 1E-300) return value.AsDouble;
            
            // Don't convert integers to float here - that should be handled explicitly
            // if (value.AsInt32 != 0) return value.AsInt32;
            // if (value.AsInt64 != 0) return value.AsInt64;

            return 0f;
        }

        /// <summary>
        /// Extracts field name from field object (handles dynamic/reflection types)
        /// </summary>
        public string? GetFieldName(dynamic field)
        {
            try
            {
                // Try common field name properties
                if (field?.Name != null) return field.Name.ToString();
                if (field?.FieldName != null) return field.FieldName.ToString();

                return field?.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts components from a GameObject, including localization parameters and orb data aggregation
        /// </summary>
        public void ExtractGameObjectComponents(IGameObject gameObject, Dictionary<long, IMonoBehaviour> componentMap,
            GameObjectData gameObjectData, AssetCollection collection)
        {
            try
            {
                var components = gameObject.FetchComponents();
                if (components == null) return;

                Dictionary<string, string>? localizationParams = null;
                var entityDetectionService = new EntityDetectionService();
                var localizationService = new LocalizationProcessingService();
                var orbComponentsFound = new List<Dictionary<string, object>>();
                var extractedComponentData = new Dictionary<string, object>();

                foreach (var componentPtr in components)
                {
                    try
                    {
                        // Try to resolve the component reference
                        var component = componentPtr.TryGetAsset(collection);
                        if (component != null)
                        {
                            var componentData = new ComponentData
                            {
                                Type = component.ClassName,
                                Name = component.GetBestName()
                            };

                            // Handle MonoBehaviour components (orb data, etc.)
                            if (component is IMonoBehaviour monoBehaviour)
                            {
                                var structure = monoBehaviour.LoadStructure();
                                if (structure != null)
                                {
                                    var properties = ConvertStructureToDict(structure, collection, out _);
                                    componentData.Properties = properties;

                                    // Check if this component is a LocalizationParamsManager
                                    if (entityDetectionService.IsLocalizationParamsManager(properties))
                                    {
                                        Logger.Debug($"✅ Found LocalizationParamsManager in GameObject {gameObject.GetBestName()}");
                                        var extractedParams = localizationService.ExtractLocalizationParams(properties);
                                        if (extractedParams != null && extractedParams.Count > 0)
                                        {
                                            localizationParams = extractedParams;
                                            Logger.Debug($"🔤 Extracted {extractedParams.Count} localization parameters for GameObject: {string.Join(", ", extractedParams.Keys)}");
                                        }
                                    }
                                    // Check if this component contains orb data
                                    else if (entityDetectionService.IsOrbData(properties))
                                    {
                                        Logger.Debug($"🔮 Found orb component in GameObject {gameObject.GetBestName()}: {component.ClassName}");
                                        orbComponentsFound.Add(properties);
                                    }
                                }
                            }
                            else
                            {
                                // Handle other component types if needed
                                componentData.Properties = new Dictionary<string, object>
                                {
                                    ["Type"] = component.ClassName
                                };
                            }

                            gameObjectData.Components.Add(componentData);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"⚠️ Error extracting component: {ex.Message}");
                    }
                }

                // Aggregate orb components if found
                if (orbComponentsFound.Count > 0)
                {
                    Logger.Debug($"🔄 Aggregating {orbComponentsFound.Count} orb components for GameObject {gameObject.GetBestName()}");
                    var aggregatedOrbData = AggregateOrbComponents(orbComponentsFound);
                    if (aggregatedOrbData != null)
                    {
                        // Store the aggregated component data
                        extractedComponentData["OrbComponent"] = aggregatedOrbData;
                        extractedComponentData["HasOrbData"] = true;

                        // Extract key fields for easy access
                        if (aggregatedOrbData.TryGetValue("locNameString", out var locKey))
                            extractedComponentData["LocKey"] = locKey?.ToString();
                        if (aggregatedOrbData.TryGetValue("DamagePerPeg", out var damage))
                            extractedComponentData["DamagePerPeg"] = damage;
                        if (aggregatedOrbData.TryGetValue("CritDamagePerPeg", out var critDamage))
                            extractedComponentData["CritDamagePerPeg"] = critDamage;

                        extractedComponentData["ComponentCount"] = orbComponentsFound.Count;

                        Logger.Debug($"✅ Aggregated orb data for GameObject {gameObject.GetBestName()}: LocKey={extractedComponentData.GetValueOrDefault("LocKey")}");
                    }
                }

                // Store component data and localization parameters in the GameObject's RawData
                if (localizationParams != null && localizationParams.Count > 0)
                {
                    extractedComponentData["LocalizationParams"] = localizationParams;
                    Logger.Debug($"🔤 Added {localizationParams.Count} localization parameters to component data");
                }

                if (extractedComponentData.Count > 0)
                {
                    if (gameObjectData.RawData == null)
                        gameObjectData.RawData = new Dictionary<string, object>();

                    if (gameObjectData.RawData is Dictionary<string, object> rawDataDict)
                    {
                        rawDataDict["ComponentData"] = extractedComponentData;
                        Logger.Debug($"🔤 Stored component data in GameObject RawData with {extractedComponentData.Count} entries");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"⚠️ Error extracting GameObject components: {ex.Message}");
            }
        }

        /// <summary>
        /// Aggregates multiple orb MonoBehaviour components into a single OrbData
        /// </summary>
        public Dictionary<string, object> AggregateOrbComponents(List<Dictionary<string, object>> orbComponents)
        {
            if (orbComponents.Count == 0)
                return new Dictionary<string, object>();

            var aggregated = new Dictionary<string, object>();

            // Merge all component data, with later components overriding earlier ones for conflicts
            foreach (var component in orbComponents)
            {
                foreach (var kvp in component)
                {
                    aggregated[kvp.Key] = kvp.Value;
                }
            }

            return aggregated;
        }

        /// <summary>
        /// Determines component type from GameObject name
        /// </summary>
        public string DetermineComponentTypeFromName(string name)
        {
            var lowerName = name.ToLowerInvariant();

            if (lowerName.Contains("attack") || lowerName.Contains("damage"))
                return "AttackComponent";
            if (lowerName.Contains("ball") || lowerName.Contains("pachinko"))
                return "PachinkoBallComponent";
            if (lowerName.Contains("orb"))
                return "OrbComponent";
            if (lowerName.Contains("upgrade"))
                return "UpgradeComponent";

            return "UnknownComponent";
        }

        /// <summary>
        /// Extracts PathID from a Unity asset reference (handles PPtr structures)
        /// </summary>
        public long ExtractPathIdFromReference(object? reference)
        {
            if (reference == null) return 0;

            if (reference is Dictionary<string, object> dict)
            {
                if (dict.TryGetValue("pathId", out var pathIdObj) &&
                    long.TryParse(pathIdObj?.ToString(), out var pathId))
                {
                    return pathId;
                }
            }

            if (reference is IUnityAssetBase asset)
            {
                // Use reflection to get PathID since it's not on the interface but is on concrete types
                var pathIdProperty = asset.GetType().GetProperty("PathID");
                if (pathIdProperty != null)
                {
                    var pathIdValue = pathIdProperty.GetValue(asset);
                    if (pathIdValue is long pathId)
                    {
                        return pathId;
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// Checks if a field contains a GUID-based asset reference (Unity Addressables)
        /// </summary>
        public bool IsGuidAssetReference(object? value)
        {
            if (value is Dictionary<string, object> dict)
            {
                return dict.ContainsKey("guid") || dict.ContainsKey("m_Guid");
            }
            return false;
        }
    }
}

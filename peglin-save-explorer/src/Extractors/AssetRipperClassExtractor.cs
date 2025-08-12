using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Import.AssetCreation;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using Newtonsoft.Json;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Services;

namespace peglin_save_explorer.Extractors
{
    public class AssetRipperClassExtractor
    {
        private readonly Dictionary<string, ClassInfoData> _classCache = new();

        public class ClassInfoData
        {
            public string ClassName { get; set; } = "";
            public string ClassEnumValue { get; set; } = "";
            public bool StartsUnlocked { get; set; }
            public string UnlockAchievementId { get; set; } = "";
            public int AchievementIndex { get; set; } = -1;
            public string ClassNameLocKey { get; set; } = "";
            public string ClassDescriptionLocKey { get; set; } = "";
            public string ClassUnlockMethodLocKey { get; set; } = "";
            public Dictionary<string, object> RawData { get; set; } = new();

            // Resolved localization strings
            public string DisplayName { get; set; } = "";
            public string Description { get; set; } = "";
            public string UnlockMethod { get; set; } = "";
        }

        public async Task<Dictionary<string, ClassInfoData>> ExtractClassInfoAsync(string bundlePath)
        {
            return await Task.Run(() => ExtractClassInfo(bundlePath));
        }


        private void ResolveLocalizationForClassInfo(ClassInfoData classInfo)
        {
            // Try to extract actual localization from Unity assets first
            bool foundRealLocalization = TryResolveLocalizationFromAssets(classInfo);

            if (!foundRealLocalization)
            {
                // Fallback to hardcoded values based on our analysis of the Peglin dump
                classInfo.DisplayName = GetKnownDisplayName(classInfo.ClassName, classInfo.ClassNameLocKey);
                classInfo.Description = GetKnownDescription(classInfo.ClassName, classInfo.ClassDescriptionLocKey);
                classInfo.UnlockMethod = GetKnownUnlockMethod(classInfo.ClassName, classInfo.ClassUnlockMethodLocKey);
            }

            Logger.Verbose($"[AssetRipper] Resolved localization for {classInfo.ClassName}: '{classInfo.DisplayName}' - {classInfo.Description}");
        }

        private bool TryResolveLocalizationFromAssets(ClassInfoData classInfo)
        {
            try
            {
                var localizationService = LocalizationService.Instance;

                if (!localizationService.EnsureLoaded())
                {
                    Logger.Verbose("[AssetRipper] Localization service failed to load, using fallback values");
                    return false;
                }

                // Try to resolve class name
                if (!string.IsNullOrEmpty(classInfo.ClassNameLocKey))
                {
                    var displayName = localizationService.GetTranslation($"Classes/{classInfo.ClassNameLocKey}");
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        classInfo.DisplayName = displayName;
                        Logger.Verbose($"[AssetRipper] Resolved display name for {classInfo.ClassName}: '{displayName}' from '{classInfo.ClassNameLocKey}'");
                    }
                }

                // Try to resolve class description
                if (!string.IsNullOrEmpty(classInfo.ClassDescriptionLocKey))
                {
                    var description = localizationService.GetTranslation($"Classes/{classInfo.ClassDescriptionLocKey}");
                    if (!string.IsNullOrEmpty(description))
                    {
                        classInfo.Description = description;
                        Logger.Verbose($"[AssetRipper] Resolved description for {classInfo.ClassName}: '{description}' from '{classInfo.ClassDescriptionLocKey}'");
                    }
                }

                // Try to resolve unlock method
                if (!string.IsNullOrEmpty(classInfo.ClassUnlockMethodLocKey))
                {
                    var unlockMethod = localizationService.GetTranslation($"Classes/{classInfo.ClassUnlockMethodLocKey}");
                    if (!string.IsNullOrEmpty(unlockMethod))
                    {
                        classInfo.UnlockMethod = unlockMethod;
                        Logger.Verbose($"[AssetRipper] Resolved unlock method for {classInfo.ClassName}: '{unlockMethod}' from '{classInfo.ClassUnlockMethodLocKey}'");
                    }
                }

                // Return true if we resolved at least one field
                return !string.IsNullOrEmpty(classInfo.DisplayName) ||
                       !string.IsNullOrEmpty(classInfo.Description) ||
                       !string.IsNullOrEmpty(classInfo.UnlockMethod);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[AssetRipper] Error resolving localization from assets: {ex.Message}");
                return false;
            }
        }


        private static string GetKnownDisplayName(string className, string locKey)
        {
            // Use actual class names from the game based on our analysis of the Peglin dump
            // The localization keys map to these names from RunStatisticsDetails.cs
            return (className, locKey) switch
            {
                ("Spinventor", "Classes/spinventor_class_title") => "Spinventor",
                ("Balladin", "Classes/warrior_class_title") => "Balladin",
                ("Roundrel", "Classes/rogue_class_title") => "Roundrel",
                ("Peglin", "Classes/peglin_class_title") => "Peglin",
                ("characterClass", "Classes/peglin_class_title") => "Peglin", // Sometimes extracted as characterClass
                _ => className
            };
        }

        private static string GetKnownDescription(string className, string locKey)
        {
            // Based on in-game descriptions and class characteristics from the Peglin dump
            return (className, locKey) switch
            {
                ("Spinventor", "Classes/spinventor_class_desc") => "A crafty inventor who specializes in spin-based attacks and mechanical contraptions.",
                ("Balladin", "Classes/warrior_class_desc") => "A noble warrior who excels in defensive combat with shields and armor.",
                ("Roundrel", "Classes/rogue_class_desc") => "A sneaky rogue who uses poison and cunning to defeat enemies.",
                ("Peglin", "Classes/peglin_class_desc") => "The default adventurer class, balanced in all aspects of combat.",
                ("characterClass", "Classes/peglin_class_desc") => "The default adventurer class, balanced in all aspects of combat.",
                _ => $"A unique class with special abilities and combat style."
            };
        }

        private static string GetKnownUnlockMethod(string className, string locKey)
        {
            // Based on achievement analysis and in-game unlock requirements
            return (className, locKey) switch
            {
                ("Spinventor", "Classes/spinventor_class_unlock_method") => "Get multiple slime bounces in a single shot",
                ("Balladin", "Classes/warrior_class_unlock_method") => "End a battle with a large amount of shield",
                ("Roundrel", "Classes/rogue_class_unlock_method") => "Apply a large amount of poison to an enemy",
                ("Peglin", _) => "Default class - always available",
                ("characterClass", _) => "Default class - always available",
                _ => $"Complete a specific achievement to unlock this class"
            };
        }

        public Dictionary<string, ClassInfoData> ExtractClassInfoFromBundles(string bundleDirectory)
        {
            var allClassInfo = new Dictionary<string, ClassInfoData>();

            if (!Directory.Exists(bundleDirectory))
            {
                Logger.Verbose($"[AssetRipper] Bundle directory does not exist: {bundleDirectory}");
                return allClassInfo;
            }

            try
            {
                var bundleFiles = Directory.GetFiles(bundleDirectory, "*.bundle", SearchOption.AllDirectories);
                Logger.Verbose($"[AssetRipper] Found {bundleFiles.Length} bundle files in {bundleDirectory}");

                foreach (var bundleFile in bundleFiles)
                {
                    try
                    {
                        // Clear cache for each bundle to avoid conflicts
                        _classCache.Clear();

                        var classInfoFromBundle = ExtractClassInfo(bundleFile);
                        foreach (var kvp in classInfoFromBundle)
                        {
                            allClassInfo[kvp.Key] = kvp.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Verbose($"[AssetRipper] Failed to process bundle {Path.GetFileName(bundleFile)}: {ex.Message}");
                    }
                }

                // Resolve localization for all extracted class info
                foreach (var classInfo in allClassInfo.Values)
                {
                    ResolveLocalizationForClassInfo(classInfo);
                }

                Logger.Verbose($"[AssetRipper] Total ClassInfo entries extracted: {allClassInfo.Count}");
                return allClassInfo;
            }
            catch (Exception ex)
            {
                Logger.Verbose($"[AssetRipper] Error extracting ClassInfo from bundles: {ex.Message}");
                return allClassInfo;
            }
        }

        public Dictionary<string, ClassInfoData> ExtractClassInfo(string bundlePath)
        {
            if (_classCache.Count > 0)
            {
                Logger.Verbose("[AssetRipper] Using cached ClassInfo data...");
                return new Dictionary<string, ClassInfoData>(_classCache);
            }

            try
            {
                Logger.Verbose($"[AssetRipper] Loading ClassInfo from bundle: {bundlePath}");

                // Create a simple assembly manager (we won't need full script compilation)
                var assemblyManager = new BaseManager(s => { });
                var assetFactory = new GameAssetFactory(assemblyManager);

                // Load the bundle
                var gameBundle = GameBundle.FromPaths(new[] { bundlePath }, assetFactory);

                Logger.Verbose($"[AssetRipper] Loaded {gameBundle.FetchAssetCollections().Count()} collections");

                var achievementIndexMap = GetFallbackAchievementMap();

                // Find and extract MonoBehaviours
                foreach (var collection in gameBundle.FetchAssetCollections())
                {
                    foreach (var asset in collection.Assets)
                    {
                        if (asset.Value is IMonoBehaviour monoBehaviour)
                        {
                            ProcessMonoBehaviour(monoBehaviour, achievementIndexMap);
                        }
                    }
                }

                Logger.Verbose($"[AssetRipper] Extracted {_classCache.Count} ClassInfo entries");
                return new Dictionary<string, ClassInfoData>(_classCache);
            }
            catch (Exception ex)
            {
                Logger.Verbose($"[AssetRipper] Error extracting ClassInfo data: {ex.Message}");
                Logger.Error($"ClassInfo extraction failed: {ex}");
                return new Dictionary<string, ClassInfoData>();
            }
        }

        private void ProcessMonoBehaviour(IMonoBehaviour monoBehaviour, Dictionary<string, int> achievementIndexMap)
        {
            try
            {
                // Get the structure data
                var structure = monoBehaviour.LoadStructure();
                if (structure == null) return;

                // Convert to dictionary for easier processing
                var data = ConvertStructureToDict(structure);

                // Check if this looks like ClassInfo data
                if (IsClassInfoData(data))
                {
                    var classInfo = ExtractClassInfoFromData(monoBehaviour.Name, data, achievementIndexMap);
                    if (classInfo != null && !string.IsNullOrEmpty(classInfo.ClassName))
                    {
                        _classCache[classInfo.ClassName] = classInfo;
                        Logger.Verbose($"[AssetRipper] Found ClassInfo: {classInfo.ClassName} - Achievement: {classInfo.UnlockAchievementId} (Index: {classInfo.AchievementIndex})");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Verbose($"[AssetRipper] Error processing MonoBehaviour: {ex.Message}");
            }
        }

        private Dictionary<string, object> ConvertStructureToDict(SerializableStructure structure)
        {
            var result = new Dictionary<string, object>();

            foreach (var field in structure.Type.Fields)
            {
                try
                {
                    if (structure.TryGetField(field.Name, out var value))
                    {
                        result[field.Name] = ConvertSerializableValue(value, field);
                    }
                }
                catch
                {
                    // Skip fields that fail to convert
                }
            }

            return result;
        }

        private object ConvertSerializableValue(SerializableValue value, dynamic field)
        {
            try
            {
                // Try to get string value first (most common for ClassInfo)
                if (!string.IsNullOrEmpty(value.AsString))
                {
                    return value.AsString;
                }

                // Try numeric values
                if (value.PValue != 0)
                {
                    // Could be int, float, or bool
                    return value.AsInt32;
                }

                // Handle complex types
                if (value.CValue != null)
                {
                    if (value.CValue is SerializableStructure subStructure)
                    {
                        return ConvertStructureToDict(subStructure);
                    }
                    else if (value.CValue is IUnityAssetBase asset)
                    {
                        return asset.ToString();
                    }
                    else
                    {
                        return ConvertValue(value.CValue);
                    }
                }
            }
            catch
            {
                // If conversion fails, return a placeholder
            }

            // Default to the field name if we can't extract the value
            return field?.Name ?? "unknown";
        }

        private object ConvertValue(object value)
        {
            if (value == null) return "";

            // Handle primitive types
            if (value.GetType().IsPrimitive || value is string)
            {
                return value;
            }

            // Handle arrays
            if (value.GetType().IsArray)
            {
                return value.ToString() ?? "";
            }

            // Default to string representation
            return value.ToString() ?? "";
        }

        private bool IsClassInfoData(Dictionary<string, object> data)
        {
            // Look for fields that are characteristic of ClassInfo
            return data.ContainsKey("characterClass") ||
                   data.ContainsKey("startsUnlocked") ||
                   data.ContainsKey("unlockAchievementId") ||
                   data.ContainsKey("classNameLocKey");
        }

        private ClassInfoData? ExtractClassInfoFromData(string assetName, Dictionary<string, object> data, Dictionary<string, int> achievementIndexMap)
        {
            try
            {
                var classInfo = new ClassInfoData();

                // Extract characterClass enum value
                if (data.TryGetValue("characterClass", out var characterClass))
                {
                    classInfo.ClassEnumValue = characterClass.ToString() ?? "";
                    classInfo.ClassName = ConvertEnumToClassName(classInfo.ClassEnumValue);
                }

                // Extract startsUnlocked
                if (data.TryGetValue("startsUnlocked", out var startsUnlocked))
                {
                    if (bool.TryParse(startsUnlocked.ToString(), out bool unlocked))
                        classInfo.StartsUnlocked = unlocked;
                }

                // Extract unlockAchievementId
                if (data.TryGetValue("unlockAchievementId", out var unlockAchievementId))
                {
                    var achievementId = unlockAchievementId.ToString() ?? "";
                    classInfo.UnlockAchievementId = achievementId;

                    // Try direct numeric index first
                    if (int.TryParse(achievementId, out int directIndex))
                    {
                        classInfo.AchievementIndex = directIndex;
                    }
                    else if (achievementIndexMap.TryGetValue(achievementId, out int mappedIndex))
                    {
                        classInfo.AchievementIndex = mappedIndex;
                    }
                }

                // Extract localization keys
                if (data.TryGetValue("classNameLocKey", out var nameLocKey))
                    classInfo.ClassNameLocKey = nameLocKey.ToString() ?? "";

                if (data.TryGetValue("classDescriptionLocKey", out var descLocKey))
                    classInfo.ClassDescriptionLocKey = descLocKey.ToString() ?? "";

                if (data.TryGetValue("classUnlockMethodLocKey", out var unlockLocKey))
                    classInfo.ClassUnlockMethodLocKey = unlockLocKey.ToString() ?? "";

                classInfo.RawData = data;

                // Validate that we have essential data
                if (string.IsNullOrEmpty(classInfo.ClassName) ||
                    (string.IsNullOrEmpty(classInfo.UnlockAchievementId) && !classInfo.StartsUnlocked))
                {
                    return null;
                }

                return classInfo;
            }
            catch (Exception ex)
            {
                Logger.Verbose($"[AssetRipper] Error extracting ClassInfo from data: {ex.Message}");
                return null;
            }
        }

        private Dictionary<string, int> GetFallbackAchievementMap()
        {
            // Based on the AchievementId enum we found in the Peglin dump
            return new Dictionary<string, int>
            {
                { "DEFEAT_MOLE_BOSS", 0 },
                { "DEFEAT_SLIME_BOSS", 1 },
                { "DEFEAT_WALL_BOSS", 2 },
                { "DEFEAT_BALLISTA_BOSS", 3 },
                { "DEFEAT_ANY_BOSS_FIRST_TURN", 4 },
                { "DIE_IN_TEXT_SCENARIO", 5 },
                { "DEAL_EXACTLY_ONE_DAMAGE", 6 },
                { "DEAL_X_DAMAGE_IN_ONE_SHOT", 7 },
                { "DEAL_EXACT_DAMAGE_TO_ENEMY", 8 },
                { "CLEAR_PEGBOARD", 9 },
                { "HIT_X_PEGS_IN_ONE_SHOT", 10 },
                { "LEAVE_ENEMY_WITH_ONE_HEALTH", 11 },
                { "BRAMBLE_ENEMY_FOR_X_TURNS", 12 },
                { "DEFEAT_MOLE_TREE", 13 },
                { "DEFEAT_BRICK_BOSS", 14 },
                { "DEFEAT_SAPPER_BOSS", 15 },
                { "REACH_CRUCIBALL_5", 16 },
                { "REACH_CRUCIBALL_10", 17 },
                { "REACH_CRUCIBALL_15", 18 },
                { "MULTIPLE_SLIME_BOUNCES", 19 },
                { "POISON_AMOUNT_ON_ENEMY", 20 },
                { "SHIELD_AMOUNT_AT_END_OF_BATTLE", 21 },
                { "DEFEAT_LESHY_BOSS", 22 },
                { "DEFEAT_PAINTER_BOSS", 23 },
                { "DEFEAT_DRAGON_BOSS", 24 },
                { "ASSEMBLE_ASSEMBALL", 25 },
                { "MULTIPLE_CRITS", 26 },
                { "SPAWN_ALL_MINIONS", 27 },
                { "CLEAN_SHOP", 28 },
                { "MULTIPLE_MULTIBALLS", 29 },
                { "THREE_BRAMBALLS_STUCK_IN_VINES", 30 },
                { "FINISH_RUN_WITH_TWO_EGGS", 31 },
                { "REACH_CRUCIBALL_20", 32 },
                { "REFUSE_INGOT", 33 },
                { "DIE_WHILE_KILLING_MINES_BOSS", 34 },
                { "BRING_EGG_TO_PEGLIN_CHEF", 35 },
                { "KILL_RAINBOW_SLIME", 36 },
                { "WIN_RUN_WITH_ONLY_ONE_RELIC", 37 },
                { "WIN_RUN_WITH_ONLY_LEVEL_ONE_ORBS", 38 },
                { "FULLY_UNLOCK_MONSTER_IN_BESTIARY", 39 },
                { "CARD_DROP_OBTAINED", 40 },
                { "RIGGED_BOMB_WHILE_SWALLOWED", 41 },
                { "DEFEAT_PAINTER_WITHOUT_KILLING_PAINTINGS", 42 },
                { "CONVERT_OR_DESTROY_DULL_PEGS_VS_DEMON_WALL", 43 },
                { "KILL_ALL_RUINS_IN_ONE_ATTACK", 44 },
                { "DEAL_1000_DAMAGE_TO_SAPPER_WITH_SAPPERS", 45 },
                { "COLLECT_ALL_DRAGON_COINS", 46 },
                { "SEND_DEMON_SQUIRREL_TO_HOLE", 47 }
            };
        }

        private ClassInfoData? ProcessClassInfoAsset(IUnityObjectBase asset, Dictionary<string, int> achievementIndexMap)
        {
            try
            {
                if (asset is not IMonoBehaviour monoBehaviour)
                    return null;

                var classInfoData = new ClassInfoData();
                var rawData = new Dictionary<string, object>();

                // Extract data from the MonoBehaviour
                var walker = new AssetWalker();
                walker.Visit(monoBehaviour, (obj, path) =>
                {
                    try
                    {
                        var fieldName = path.Split('.').LastOrDefault() ?? "";
                        if (string.IsNullOrEmpty(fieldName)) return;

                        rawData[fieldName] = obj?.ToString() ?? "";

                        // Map known fields
                        switch (fieldName.ToLowerInvariant())
                        {
                            case "characterclass":
                                if (obj != null)
                                {
                                    classInfoData.ClassEnumValue = obj.ToString() ?? "";
                                    classInfoData.ClassName = ConvertEnumToClassName(classInfoData.ClassEnumValue);
                                }
                                break;

                            case "startsunlocked":
                                if (bool.TryParse(obj?.ToString(), out bool startsUnlocked))
                                    classInfoData.StartsUnlocked = startsUnlocked;
                                break;

                            case "unlockachievementid":
                                if (obj != null)
                                {
                                    var achievementId = obj.ToString() ?? "";
                                    classInfoData.UnlockAchievementId = achievementId;

                                    // Try direct numeric index first
                                    if (int.TryParse(achievementId, out int directIndex))
                                    {
                                        classInfoData.AchievementIndex = directIndex;
                                    }
                                    else if (achievementIndexMap.TryGetValue(achievementId, out int mappedIndex))
                                    {
                                        classInfoData.AchievementIndex = mappedIndex;
                                    }
                                }
                                break;

                            case "classnamelockey":
                                classInfoData.ClassNameLocKey = obj?.ToString() ?? "";
                                break;

                            case "classdescriptionlockey":
                                classInfoData.ClassDescriptionLocKey = obj?.ToString() ?? "";
                                break;

                            case "classunlockmethodlockey":
                                classInfoData.ClassUnlockMethodLocKey = obj?.ToString() ?? "";
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Verbose($"Error processing field {path}: {ex.Message}");
                    }
                });

                classInfoData.RawData = rawData;

                // Validate that we have the essential data
                if (string.IsNullOrEmpty(classInfoData.ClassName) ||
                    (string.IsNullOrEmpty(classInfoData.UnlockAchievementId) && !classInfoData.StartsUnlocked))
                {
                    Logger.Verbose($"Incomplete ClassInfo data: {JsonConvert.SerializeObject(classInfoData, Formatting.Indented)}");
                    return null;
                }

                return classInfoData;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing ClassInfo asset: {ex.Message}");
                return null;
            }
        }

        private string ConvertEnumToClassName(string enumValue)
        {
            // The Class enum values should match the class names
            return enumValue switch
            {
                "0" or "Peglin" => "Peglin",
                "1" or "Balladin" => "Balladin",
                "2" or "Roundrel" => "Roundrel",
                "3" or "Spinventor" => "Spinventor",
                _ => enumValue
            };
        }

        /// <summary>
        /// Helper class to walk through Unity asset data structures
        /// </summary>
        private class AssetWalker
        {
            public void Visit(object obj, Action<object, string> visitor, string path = "")
            {
                if (obj == null) return;

                visitor(obj, path);

                try
                {
                    // Use reflection to walk through the object's properties and fields
                    var type = obj.GetType();

                    // Walk through properties
                    foreach (var prop in type.GetProperties())
                    {
                        try
                        {
                            if (prop.GetIndexParameters().Length > 0) continue; // Skip indexed properties

                            var value = prop.GetValue(obj);
                            if (value != null)
                            {
                                var newPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";

                                if (IsSimpleType(prop.PropertyType))
                                {
                                    visitor(value, newPath);
                                }
                                else if (value != obj) // Avoid circular references
                                {
                                    Visit(value, visitor, newPath);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Verbose($"Error accessing property {prop.Name}: {ex.Message}");
                        }
                    }

                    // Walk through fields
                    foreach (var field in type.GetFields())
                    {
                        try
                        {
                            var value = field.GetValue(obj);
                            if (value != null)
                            {
                                var newPath = string.IsNullOrEmpty(path) ? field.Name : $"{path}.{field.Name}";

                                if (IsSimpleType(field.FieldType))
                                {
                                    visitor(value, newPath);
                                }
                                else if (value != obj) // Avoid circular references
                                {
                                    Visit(value, visitor, newPath);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Verbose($"Error accessing field {field.Name}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Verbose($"Error walking object at path {path}: {ex.Message}");
                }
            }

            private bool IsSimpleType(Type type)
            {
                return type.IsPrimitive ||
                       type == typeof(string) ||
                       type == typeof(decimal) ||
                       type == typeof(DateTime) ||
                       type.IsEnum;
            }
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using System.IO;
using peglin_save_explorer.Core;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Extractors;
using System.Reflection;

namespace peglin_save_explorer.Services
{
    public class ClassManagementService
    {
        private readonly SaveFileManager _saveFileManager;
        private readonly Dictionary<string, int> _classAchievementMap;
        private readonly ConfigurationManager _configManager;

        public ClassManagementService(FileInfo? saveFile = null)
        {
            _configManager = new ConfigurationManager();
            _saveFileManager = new SaveFileManager();
            
            // Load the save file
            if (!_saveFileManager.LoadSaveFile(saveFile))
            {
                throw new InvalidOperationException("Failed to load save file");
            }
            
            // Build class-achievement mapping once per instance
            _classAchievementMap = BuildClassAchievementMap();
        }

        public bool LockClass(string className)
        {
            if (className == "Peglin")
            {
                Logger.Error("Cannot lock the base class (Peglin).");
                return false;
            }

            if (!_classAchievementMap.TryGetValue(className, out var achievementIndex))
            {
                Logger.Error($"Unknown class: {className}");
                return false;
            }

            bool modified = _saveFileManager.ModifySaveData(data => 
            {
                try
                {
                    // Access achievement data directly from binary deserialized objects
                    var persistentData = GetPersistentPlayerSaveData(data);
                    if (persistentData == null)
                    {
                        Logger.Error("Could not find PersistentPlayerSaveData in save file");
                        return false;
                    }

                    // Get achievement data array via reflection
                    var achievementDataProperty = persistentData.GetType().GetField("_achievementData", 
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    
                    if (achievementDataProperty?.GetValue(persistentData) is not bool[] achievements)
                    {
                        Logger.Error("Could not access achievement data array");
                        return false;
                    }

                    if (achievementIndex < 0 || achievementIndex >= achievements.Length)
                    {
                        Logger.Error($"Achievement index {achievementIndex} is out of range");
                        return false;
                    }

                    // Lock the class by setting achievement to false
                    achievements[achievementIndex] = false;
                    Logger.Info($"Locked class {className} by setting achievement index {achievementIndex} to false");
                    
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error locking class {className}: {ex.Message}");
                    return false;
                }
            });
            
            if (modified)
            {
                return _saveFileManager.SaveToFile();
            }
            
            return false;
        }

        public bool UnlockClass(string className)
        {
            if (!_classAchievementMap.TryGetValue(className, out var achievementIndex))
            {
                Logger.Error($"Unknown class: {className}");
                return false;
            }

            bool modified = _saveFileManager.ModifySaveData(data => 
            {
                try
                {
                    // Access achievement data directly from binary deserialized objects
                    var persistentData = GetPersistentPlayerSaveData(data);
                    if (persistentData == null)
                    {
                        Logger.Error("Could not find PersistentPlayerSaveData in save file");
                        return false;
                    }

                    // Get achievement data array via reflection
                    var achievementDataProperty = persistentData.GetType().GetField("_achievementData", 
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    
                    if (achievementDataProperty?.GetValue(persistentData) is not bool[] achievements)
                    {
                        Logger.Error("Could not access achievement data array");
                        return false;
                    }

                    if (achievementIndex < 0 || achievementIndex >= achievements.Length)
                    {
                        Logger.Error($"Achievement index {achievementIndex} is out of range");
                        return false;
                    }

                    // Unlock the class by setting achievement to true
                    achievements[achievementIndex] = true;
                    Logger.Info($"Unlocked class {className} by setting achievement index {achievementIndex} to true");
                    
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error unlocking class {className}: {ex.Message}");
                    return false;
                }
            });
            
            if (modified)
            {
                return _saveFileManager.SaveToFile();
            }
            
            return false;
        }

        public bool SetCruciballLevel(string className, int level)
        {
            if (level < 0 || level > 20)
            {
                Logger.Error("Cruciball level must be between 0 and 20.");
                return false;
            }

            var classIndex = GetClassIndex(className);
            if (classIndex == -1)
            {
                Logger.Error($"Unknown character class: {className}");
                return false;
            }

            bool modified = _saveFileManager.ModifySaveData(data => 
            {
                try
                {
                    var persistentData = GetPersistentPlayerSaveData(data);
                    if (persistentData == null)
                    {
                        Logger.Error("Could not find PersistentPlayerSaveData in save file");
                        return false;
                    }

                    // Get cruciball levels array via reflection
                    var cruciballLevelsProperty = persistentData.GetType().GetField("_cruciballLevels", 
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    
                    if (cruciballLevelsProperty?.GetValue(persistentData) is not int[] cruciballLevels)
                    {
                        Logger.Error("Could not access cruciball levels array");
                        return false;
                    }

                    if (classIndex >= cruciballLevels.Length)
                    {
                        Logger.Error($"Class index {classIndex} is out of range");
                        return false;
                    }

                    cruciballLevels[classIndex] = level;
                    Logger.Info($"Set cruciball level for {className} to {level}");
                    
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error setting cruciball level for {className}: {ex.Message}");
                    return false;
                }
            });
            
            if (modified)
            {
                return _saveFileManager.SaveToFile();
            }
            
            return false;
        }

        public List<ClassStatusInfo> ListClasses()
        {
            var result = new List<ClassStatusInfo>();
            
            // Add unlockable classes
            foreach (var kvp in _classAchievementMap)
            {
                result.Add(new ClassStatusInfo
                {
                    ClassName = kvp.Key,
                    IsUnlocked = IsClassUnlocked(kvp.Key),
                    AchievementId = $"Index_{kvp.Value}",
                    CruciballLevel = GetCurrentCruciballLevel(kvp.Key)
                });
            }

            return result.OrderBy(c => c.ClassName).ToList();
        }

        private bool IsClassUnlocked(string className)
        {
            if (className == "Peglin") return true;

            if (!_classAchievementMap.TryGetValue(className, out var achievementIndex))
            {
                return false;
            }

            try
            {
                // Read current achievement status from save data
                bool isUnlocked = false;
                _saveFileManager.ReadSaveData(data => 
                {
                    var persistentData = GetPersistentPlayerSaveData(data);
                    if (persistentData != null)
                    {
                        var achievementDataProperty = persistentData.GetType().GetField("_achievementData", 
                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                        
                        if (achievementDataProperty?.GetValue(persistentData) is bool[] achievements &&
                            achievementIndex >= 0 && achievementIndex < achievements.Length)
                        {
                            isUnlocked = achievements[achievementIndex];
                        }
                    }
                });
                
                return isUnlocked;
            }
            catch
            {
                return false;
            }
        }

        private int GetCurrentCruciballLevel(string className)
        {
            var classIndex = GetClassIndex(className);
            if (classIndex == -1) return -1;

            try
            {
                int level = -1;
                _saveFileManager.ReadSaveData(data => 
                {
                    var persistentData = GetPersistentPlayerSaveData(data);
                    if (persistentData != null)
                    {
                        var cruciballLevelsProperty = persistentData.GetType().GetField("_cruciballLevels", 
                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                        
                        if (cruciballLevelsProperty?.GetValue(persistentData) is int[] cruciballLevels &&
                            classIndex < cruciballLevels.Length)
                        {
                            level = cruciballLevels[classIndex];
                        }
                    }
                });
                
                return level;
            }
            catch
            {
                return -1;
            }
        }

        private static int GetClassIndex(string className)
        {
            return className switch
            {
                "Peglin" => 0,
                "Balladin" => 1,
                "Roundrel" => 2,
                "Spinventor" => 3,
                _ => -1
            };
        }

        private static object? GetPersistentPlayerSaveData(object saveData)
        {
            try
            {
                // The saveData should be a Dictionary<string, ISerializable> like in SaveDataLoader
                if (saveData is not System.Collections.IDictionary dict)
                {
                    Logger.Debug($"Expected Dictionary structure for save data, got: {saveData?.GetType()}");
                    return null;
                }

                // Look for PersistentPlayerSaveData in the dictionary
                if (!dict.Contains("PersistentPlayerSaveData"))
                {
                    Logger.Debug("Could not find PersistentPlayerSaveData key in save dictionary");
                    return null;
                }

                var persistentPlayerItem = dict["PersistentPlayerSaveData"];
                if (persistentPlayerItem == null)
                {
                    Logger.Debug("PersistentPlayerSaveData is null");
                    return null;
                }

                // Extract the Value field from the OdinSerializer Item wrapper
                var valueField = persistentPlayerItem.GetType().GetField("Value", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (valueField == null)
                {
                    Logger.Debug("Could not find Value field in PersistentPlayerSaveData item");
                    return null;
                }

                var persistentPlayerData = valueField.GetValue(persistentPlayerItem);
                if (persistentPlayerData == null)
                {
                    Logger.Debug("PersistentPlayerSaveData Value is null");
                    return null;
                }

                return persistentPlayerData;
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error navigating to PersistentPlayerSaveData: {ex.Message}");
                return null;
            }
        }

        private Dictionary<string, int> BuildClassAchievementMap()
        {
            try
            {
                Logger.Verbose("Building ClassAchievementMap using AssetRipper from Unity assets...");
                
                var peglinPath = _configManager.GetEffectivePeglinPath();
                
                if (string.IsNullOrEmpty(peglinPath) || !Directory.Exists(peglinPath))
                {
                    Logger.Warning("Peglin path not configured or doesn't exist. Using fallback achievement mappings.");
                    return GetFallbackClassAchievementMap();
                }

                // Try to extract ClassInfo data from Unity assets using AssetRipper
                var extractor = new AssetRipperClassExtractor();
                
                // Get the proper bundle directory path
                var bundlePath = PeglinPathHelper.GetStreamingAssetsBundlePath(peglinPath);
                if (string.IsNullOrEmpty(bundlePath))
                {
                    Logger.Warning($"Could not find StreamingAssets bundle directory in {peglinPath}");
                    return GetFallbackClassAchievementMap();
                }
                
                var classInfoData = extractor.ExtractClassInfoFromBundles(bundlePath);
                
                if (classInfoData.Count == 0)
                {
                    Logger.Warning("Could not extract ClassInfo data from Unity assets. Using fallback achievement mappings.");
                    return GetFallbackClassAchievementMap();
                }

                var result = new Dictionary<string, int>();
                
                // Convert ClassInfo data to achievement mappings
                foreach (var kvp in classInfoData)
                {
                    var className = kvp.Key;
                    var classInfo = kvp.Value;
                    
                    // Only include classes that require achievements to unlock
                    if (!classInfo.StartsUnlocked && classInfo.AchievementIndex >= 0)
                    {
                        result[className] = classInfo.AchievementIndex;
                        Logger.Verbose($"Mapped {className} -> Achievement Index {classInfo.AchievementIndex} ({classInfo.UnlockAchievementId})");
                    }
                    else if (classInfo.StartsUnlocked)
                    {
                        Logger.Verbose($"Skipped {className} - starts unlocked (no achievement required)");
                    }
                    else
                    {
                        Logger.Warning($"Could not determine achievement index for {className}");
                    }
                }

                if (result.Count > 0)
                {
                    Logger.Info($"Built ClassAchievementMap with {result.Count} entries using AssetRipper");
                    return result;
                }
                else
                {
                    Logger.Warning("No valid class-achievement mappings found. Using fallback achievement mappings.");
                    return GetFallbackClassAchievementMap();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error building ClassAchievementMap from AssetRipper: {ex.Message}");
                Logger.Warning("Using fallback achievement mappings.");
                return GetFallbackClassAchievementMap();
            }
        }

        private static Dictionary<string, int> GetFallbackClassAchievementMap()
        {
            // Fallback to hardcoded mappings based on our analysis
            return new Dictionary<string, int>
            {
                { "Balladin", 21 }, // SHIELD_AMOUNT_AT_END_OF_BATTLE
                { "Roundrel", 20 }, // POISON_AMOUNT_ON_ENEMY (was REACH_CRUCIBALL_5 but AssetRipper shows 20)
                { "Spinventor", 19 }, // MULTIPLE_SLIME_BOUNCES
            };
        }

        public class ClassStatusInfo
        {
            public required string ClassName { get; set; }
            public bool IsUnlocked { get; set; }
            public required string AchievementId { get; set; }
            public int CruciballLevel { get; set; } = -1;
        }
    }
}
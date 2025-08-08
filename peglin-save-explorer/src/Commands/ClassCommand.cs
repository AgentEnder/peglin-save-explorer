using System.CommandLine;
using peglin_save_explorer.Services;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Extractors;
using peglin_save_explorer.Core;

namespace peglin_save_explorer.Commands
{
    public class ClassCommand : ICommand
    {
        private Dictionary<string, AssetRipperClassExtractor.ClassInfoData>? _classInfoCache;

        public Command CreateCommand()
        {
            var command = new Command("class", "List classes and their status, or manage character classes - set cruciball levels, lock/unlock classes");

            // Class name argument (optional - only required for modification operations)
            var classNameArgument = new Argument<string?>(
                "className",
                () => null,
                "Character class name for modification operations (e.g., Peglin, Balladin, Roundrel, Spinventor)"
            );
            command.AddArgument(classNameArgument);

            var saveFileOption = new Option<FileInfo?>(
                new[] { "--save-file", "-s" },
                description: "Path to the save file"
            );

            var setCruciballOption = new Option<int?>(
                new[] { "--set-cruciball", "-c" },
                description: "Set the cruciball level (0-20)"
            );

            var lockOption = new Option<bool>(
                new[] { "--lock", "-l" },
                description: "Lock the class (makes it unavailable for play)"
            );

            var unlockOption = new Option<bool>(
                new[] { "--unlock", "-u" },
                description: "Unlock the class (makes it available for play)"
            );

            var listOption = new Option<bool>(
                new[] { "--list" },
                description: "List all classes and their status (default behavior when no actions specified)"
            );

            command.AddOption(saveFileOption);
            command.AddOption(setCruciballOption);
            command.AddOption(lockOption);
            command.AddOption(unlockOption);
            command.AddOption(listOption);

            command.SetHandler(Execute, classNameArgument, saveFileOption, setCruciballOption, 
                lockOption, unlockOption, listOption);

            return command;
        }

        private void Execute(string? className, FileInfo? saveFile, int? setCruciball, 
            bool lockClass, bool unlockClass, bool list)
        {
            try
            {
                // Initialize the service
                var service = new ClassManagementService(saveFile);
                
                // Check if any modification actions are specified
                var actionCount = (setCruciball.HasValue ? 1 : 0) + (lockClass ? 1 : 0) + (unlockClass ? 1 : 0);
                
                // Default to list mode if no actions specified or --list explicitly requested
                if (list || actionCount == 0)
                {
                    ListClasses(service);
                    return;
                }

                // Validate class name for modification operations
                if (string.IsNullOrEmpty(className))
                {
                    Logger.Error("Class name is required for modification operations.");
                    Logger.Info("Valid classes are: Peglin, Balladin, Roundrel, Spinventor");
                    Logger.Info("Use 'class' or 'class --list' to see all classes and their status.");
                    return;
                }
                if (actionCount > 1)
                {
                    Logger.Error("Only one action can be specified at a time (--set-cruciball, --lock, or --unlock).");
                    return;
                }

                // Normalize class name
                var normalizedClassName = NormalizeClassName(className);
                if (normalizedClassName == null)
                {
                    Logger.Error($"Invalid character class '{className}'.");
                    Logger.Info("Valid classes are: Peglin, Balladin, Roundrel, Spinventor");
                    return;
                }

                // Load class info for better display names
                LoadClassInfo();

                // Get display name for better user messages
                var localizedInfo = GetLocalizedClassInfo(normalizedClassName);
                var displayName = localizedInfo?.DisplayName ?? normalizedClassName;

                // Execute the appropriate action
                bool success = false;
                if (setCruciball.HasValue)
                {
                    success = service.SetCruciballLevel(normalizedClassName, setCruciball.Value);
                    if (success)
                    {
                        Logger.Info($"Successfully set {displayName} cruciball level to {setCruciball.Value}");
                    }
                    else
                    {
                        Logger.Error($"Failed to set cruciball level for {displayName}");
                    }
                }
                else if (lockClass)
                {
                    success = service.LockClass(normalizedClassName);
                    if (success)
                    {
                        Logger.Info($"Successfully locked class: {displayName}");
                        // Show unlock hint if available
                        if (!string.IsNullOrEmpty(localizedInfo?.UnlockMethod))
                        {
                            Logger.Info($"To unlock again: {localizedInfo.UnlockMethod}");
                        }
                    }
                    else
                    {
                        Logger.Error($"Failed to lock class: {displayName}");
                    }
                }
                else if (unlockClass)
                {
                    success = service.UnlockClass(normalizedClassName);
                    if (success)
                    {
                        Logger.Info($"Successfully unlocked class: {displayName}");
                        // Show description if available
                        if (!string.IsNullOrEmpty(localizedInfo?.Description))
                        {
                            Logger.Info($"Class info: {localizedInfo.Description}");
                        }
                    }
                    else
                    {
                        Logger.Error($"Failed to unlock class: {displayName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to manage class: {ex.Message}");
            }
        }

        private void ListClasses(ClassManagementService service)
        {
            var classes = service.ListClasses();
            LoadClassInfo(); // Load localized class information

            Logger.Info("Class Status:");
            Logger.Info("=============");

            foreach (var classInfo in classes)
            {
                var status = classInfo.IsUnlocked ? "Unlocked" : "Locked";
                var cruciballInfo = classInfo.CruciballLevel >= 0 
                    ? $" (Cruciball: {classInfo.CruciballLevel})" 
                    : " (Not played)";
                
                // Get localized information if available
                var localizedInfo = GetLocalizedClassInfo(classInfo.ClassName);
                var displayName = localizedInfo?.DisplayName ?? classInfo.ClassName;
                
                Logger.Info($"{displayName}: {status}{cruciballInfo}");
                
                // Show description if available
                if (!string.IsNullOrEmpty(localizedInfo?.Description))
                {
                    Logger.Info($"  Description: {localizedInfo.Description}");
                }
                
                // Show unlock method if class is locked and info is available
                if (!classInfo.IsUnlocked && !string.IsNullOrEmpty(localizedInfo?.UnlockMethod))
                {
                    Logger.Info($"  Unlock: {localizedInfo.UnlockMethod}");
                }
                
                if (!string.IsNullOrEmpty(classInfo.AchievementId))
                {
                    Logger.Verbose($"  Achievement: {classInfo.AchievementId}");
                }
                
                Logger.Info(""); // Add spacing between classes
            }

            Logger.Info("Usage examples:");
            Logger.Info("  peglin-save-explorer class Balladin --set-cruciball 5");
            Logger.Info("  peglin-save-explorer class Spinventor --unlock");
            Logger.Info("  peglin-save-explorer class Roundrel --lock");
        }

        private void LoadClassInfo()
        {
            if (_classInfoCache != null) return; // Already loaded

            try
            {
                var configManager = new ConfigurationManager();
                var peglinPath = configManager.GetEffectivePeglinPath();
                
                if (string.IsNullOrEmpty(peglinPath) || !Directory.Exists(peglinPath))
                {
                    Logger.Verbose("Peglin path not available for class info extraction");
                    _classInfoCache = new Dictionary<string, AssetRipperClassExtractor.ClassInfoData>();
                    return;
                }

                var extractor = new AssetRipperClassExtractor();
                var bundlePath = PeglinPathHelper.GetStreamingAssetsBundlePath(peglinPath);
                
                if (string.IsNullOrEmpty(bundlePath))
                {
                    Logger.Verbose("Could not find StreamingAssets bundle directory");
                    _classInfoCache = new Dictionary<string, AssetRipperClassExtractor.ClassInfoData>();
                    return;
                }

                _classInfoCache = extractor.ExtractClassInfoFromBundles(bundlePath);
                Logger.Verbose($"Loaded {_classInfoCache.Count} class info entries with localization data");
            }
            catch (Exception ex)
            {
                Logger.Verbose($"Error loading class info: {ex.Message}");
                _classInfoCache = new Dictionary<string, AssetRipperClassExtractor.ClassInfoData>();
            }
        }

        private AssetRipperClassExtractor.ClassInfoData? GetLocalizedClassInfo(string className)
        {
            if (_classInfoCache == null) return null;
            
            // Try exact match first
            if (_classInfoCache.TryGetValue(className, out var classInfo))
            {
                return classInfo;
            }
            
            // Try case-insensitive match
            var entry = _classInfoCache.FirstOrDefault(kvp => 
                kvp.Key.Equals(className, StringComparison.OrdinalIgnoreCase));
            
            return entry.Value;
        }


        private static string? NormalizeClassName(string className)
        {
            var validClasses = new[] { "Peglin", "Balladin", "Roundrel", "Spinventor" };
            return validClasses.FirstOrDefault(c => c.Equals(className, StringComparison.OrdinalIgnoreCase));
        }
    }
}
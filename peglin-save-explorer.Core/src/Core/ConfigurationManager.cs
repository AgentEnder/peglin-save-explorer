using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Core
{
    public class ConfigurationManager
    {
        private static readonly string ConfigDirectory = GetConfigDirectory();

        private static string GetConfigDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PeglinSaveExplorer"
                );
            }
            else if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "PeglinSaveExplorer"
                );
            }
            else
            {
                // Linux - use XDG_CONFIG_HOME or fallback to ~/.config
                var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (string.IsNullOrEmpty(xdgConfigHome))
                {
                    xdgConfigHome = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".config"
                    );
                }
                return Path.Combine(xdgConfigHome, "PeglinSaveExplorer");
            }
        }

        private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "settings.json");

        private Configuration _config;

        public Configuration Config => _config;

        public string GetConfigFilePath() => ConfigFilePath;

        public ConfigurationManager()
        {
            _config = LoadConfiguration();

            // Load game data mappings if Peglin path is configured
            if (!string.IsNullOrEmpty(_config.DefaultPeglinInstallPath))
            {
                GameDataMappings.LoadGameDataMappings(_config.DefaultPeglinInstallPath);
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(ConfigDirectory);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to save configuration: {ex.Message}");
            }
        }

        private Configuration LoadConfiguration()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<Configuration>(json);
                    return config ?? new Configuration();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load configuration: {ex.Message}");
            }

            return new Configuration();
        }

        public List<string> DetectPeglinInstallations()
        {
            // Check if we have a recent cache (less than 7 days old) AND it's not empty
            if (_config.CachedPeglinInstallations != null && 
                _config.CachedPeglinInstallations.Count > 0 &&  // Don't use cache if it was empty
                _config.CachedPeglinInstallationsTimestamp != null &&
                (DateTime.Now - _config.CachedPeglinInstallationsTimestamp.Value).TotalDays < 7)
            {
                // Validate that cached paths still exist
                var validCachedPaths = _config.CachedPeglinInstallations
                    .Where(path => Directory.Exists(path) && PeglinPathHelper.IsValidPeglinPath(path))
                    .ToList();

                if (validCachedPaths.Count == _config.CachedPeglinInstallations.Count)
                {
                    return validCachedPaths;
                }
            }

            var installations = new HashSet<string>();

            // First, try known Steam locations (fast)
            var knownPaths = GetKnownSteamPaths();
            
            // Also check Steam library folders from config
            knownPaths.AddRange(GetSteamLibraryPaths());
            foreach (var steamPath in knownPaths)
            {
                var peglinPath = Path.Combine(steamPath, "steamapps", "common", "Peglin");
                if (Directory.Exists(peglinPath))
                {
                    if (PeglinPathHelper.IsValidPeglinPath(peglinPath))
                    {
                        installations.Add(peglinPath);
                    }
                    else if (OperatingSystem.IsMacOS())
                    {
                        // Check for macOS app bundle
                        var macAppPath = Path.Combine(peglinPath, "peglin.app", "Contents", "Resources");
                        if (PeglinPathHelper.IsValidPeglinPath(macAppPath))
                        {
                            installations.Add(macAppPath);
                        }
                    }
                }
            }

            // If we found installations in known locations, we're done
            if (installations.Count > 0)
            {
                _config.CachedPeglinInstallations = installations.ToList();
                _config.CachedPeglinInstallationsTimestamp = DateTime.Now;
                SaveConfiguration();
                return installations.ToList();
            }

            // Otherwise, perform full recursive search with spinner
            var spinner = ConsoleSpinner.Instance;
            spinner.Start("No Peglin found in common locations. Performing deep search...");

            try
            {
                // Get all drives to search
                var drivesToSearch = GetSearchableDrives();

                foreach (var drive in drivesToSearch)
                {
                    spinner.Update($"Searching {drive}...");
                    SearchForSteamApps(drive, installations, spinner, 0, 8);
                }

                // Cache the results
                _config.CachedPeglinInstallations = installations.ToList();
                _config.CachedPeglinInstallationsTimestamp = DateTime.Now;
                SaveConfiguration();
            }
            finally
            {
                spinner.Stop();
            }

            return installations.ToList();
        }

        private List<string> GetKnownSteamPaths()
        {
            var steamPaths = new List<string>();

            if (OperatingSystem.IsWindows())
            {
                // Common Windows Steam paths
                steamPaths.Add(@"C:\Program Files (x86)\Steam");
                steamPaths.Add(@"C:\Program Files\Steam");
                
                // Check for Steam in other common drives
                var drives = new[] { "D", "E", "F", "G" };
                foreach (var drive in drives)
                {
                    steamPaths.Add($@"{drive}:\Steam");
                    steamPaths.Add($@"{drive}:\SteamLibrary");
                    steamPaths.Add($@"{drive}:\Games\Steam");
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home))
                {
                    // Default Steam location on macOS
                    steamPaths.Add(Path.Combine(home, "Library", "Application Support", "Steam"));
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home))
                {
                    // Common Steam locations on Linux
                    steamPaths.Add(Path.Combine(home, ".steam", "steam"));
                    steamPaths.Add(Path.Combine(home, ".local", "share", "Steam"));
                    steamPaths.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".steam", "steam")); // Flatpak
                }
            }

            // Only return paths that actually exist
            return steamPaths.Where(Directory.Exists).ToList();
        }

        private List<string> GetSteamLibraryPaths()
        {
            var libraryPaths = new List<string>();

            try
            {
                string? steamConfigPath = null;

                if (OperatingSystem.IsWindows())
                {
                    // Try common Steam installation paths
                    var possiblePaths = new[]
                    {
                        @"C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf",
                        @"C:\Program Files\Steam\steamapps\libraryfolders.vdf"
                    };
                    steamConfigPath = possiblePaths.FirstOrDefault(File.Exists);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var macPath = Path.Combine(home, "Library", "Application Support", "Steam", "steamapps", "libraryfolders.vdf");
                    if (File.Exists(macPath))
                        steamConfigPath = macPath;
                }
                else if (OperatingSystem.IsLinux())
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var linuxPaths = new[]
                    {
                        Path.Combine(home, ".steam", "steam", "steamapps", "libraryfolders.vdf"),
                        Path.Combine(home, ".local", "share", "Steam", "steamapps", "libraryfolders.vdf")
                    };
                    steamConfigPath = linuxPaths.FirstOrDefault(File.Exists);
                }

                if (!string.IsNullOrEmpty(steamConfigPath) && File.Exists(steamConfigPath))
                {
                    // Simple VDF parsing to find library paths
                    var lines = File.ReadAllLines(steamConfigPath);
                    foreach (var line in lines)
                    {
                        if (line.Contains("\"path\""))
                        {
                            var parts = line.Split('"');
                            if (parts.Length >= 4)
                            {
                                var libraryPath = parts[3];
                                
                                // Handle escaped backslashes on Windows
                                if (OperatingSystem.IsWindows())
                                    libraryPath = libraryPath.Replace("\\\\", "\\");
                                
                                if (Directory.Exists(libraryPath))
                                {
                                    libraryPaths.Add(libraryPath);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors in Steam config parsing
            }

            return libraryPaths;
        }

        private List<string> GetSearchableDrives()
        {
            var drives = new List<string>();

            if (OperatingSystem.IsWindows())
            {
                // Get all available drives on Windows
                drives.AddRange(DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                    .Select(d => d.RootDirectory.FullName));
            }
            else
            {
                // On Unix-like systems, search common mount points
                drives.Add("/");
                
                // Check for additional mount points
                if (Directory.Exists("/mnt")) drives.Add("/mnt");
                if (Directory.Exists("/media")) drives.Add("/media");
                if (Directory.Exists("/Volumes")) drives.Add("/Volumes"); // macOS
                
                // Also check home directory
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home) && Directory.Exists(home))
                {
                    drives.Add(home);
                }
            }

            return drives;
        }

        private void SearchForSteamApps(string path, HashSet<string> installations, ConsoleSpinner spinner, int currentDepth, int maxDepth)
        {
            if (currentDepth >= maxDepth) return;

            try
            {
                // Check if this is a steamapps directory
                if (Path.GetFileName(path).Equals("steamapps", StringComparison.OrdinalIgnoreCase))
                {
                    var peglinPath = Path.Combine(path, "common", "Peglin");
                    if (Directory.Exists(peglinPath))
                    {
                        // Check for Windows/Linux structure
                        if (PeglinPathHelper.IsValidPeglinPath(peglinPath))
                        {
                            installations.Add(peglinPath);
                            spinner.Update($"Found: {peglinPath}");
                        }
                        // Check for macOS app bundle structure
                        else if (OperatingSystem.IsMacOS())
                        {
                            var macAppPath = Path.Combine(peglinPath, "peglin.app", "Contents", "Resources");
                            if (PeglinPathHelper.IsValidPeglinPath(macAppPath))
                            {
                                installations.Add(macAppPath);
                                spinner.Update($"Found: {macAppPath}");
                            }
                        }
                    }
                    return; // No need to search deeper in steamapps
                }

                // Get subdirectories, but skip system/hidden directories
                var subdirectories = Directory.GetDirectories(path)
                    .Where(dir => 
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(dir);
                            // Skip hidden and system directories
                            if ((dirInfo.Attributes & FileAttributes.Hidden) != 0 ||
                                (dirInfo.Attributes & FileAttributes.System) != 0)
                            {
                                return false;
                            }

                            var dirName = dirInfo.Name.ToLower();
                            // Skip common non-game directories
                            if (dirName.StartsWith(".") || 
                                dirName == "windows" || 
                                dirName == "system32" ||
                                dirName == "syswow64" ||
                                dirName == "temp" ||
                                dirName == "tmp" ||
                                dirName == "$recycle.bin" ||
                                dirName == "system volume information")
                            {
                                return false;
                            }

                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    });

                foreach (var subdir in subdirectories)
                {
                    SearchForSteamApps(subdir, installations, spinner, currentDepth + 1, maxDepth);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
            catch (DirectoryNotFoundException)
            {
                // Skip if directory was deleted during search
            }
            catch (Exception)
            {
                // Skip on any other errors
            }
        }

        public List<string> DetectSaveFiles()
        {
            var saveFiles = new List<string>();

            string saveDirectory;
            
            if (OperatingSystem.IsWindows())
            {
                saveDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "AppData", "LocalLow", "Red Nexus Games Inc", "Peglin"
                );
            }
            else if (OperatingSystem.IsMacOS())
            {
                saveDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "Red Nexus Games Inc", "Peglin"
                );
            }
            else if (OperatingSystem.IsLinux())
            {
                // Check XDG_DATA_HOME first, fallback to ~/.local/share
                var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (string.IsNullOrEmpty(xdgDataHome))
                {
                    xdgDataHome = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".local", "share"
                    );
                }
                saveDirectory = Path.Combine(xdgDataHome, "Red Nexus Games Inc", "Peglin");
            }
            else
            {
                // Unsupported platform, return empty list
                return saveFiles;
            }

            if (Directory.Exists(saveDirectory))
            {
                var files = Directory.GetFiles(saveDirectory, "*.data")
                    .Where(f => Path.GetFileName(f).StartsWith("Save_"))
                    .OrderBy(f => f)
                    .ToList();

                saveFiles.AddRange(files);
            }

            return saveFiles;
        }

        public string? GetEffectivePeglinPath(string? overridePath = null, bool promptIfNotFound = true)
        {
            // Use override if provided
            if (!string.IsNullOrWhiteSpace(overridePath) && Directory.Exists(overridePath))
            {
                return overridePath;
            }

            // Use configured default
            if (!string.IsNullOrWhiteSpace(_config.DefaultPeglinInstallPath) &&
                Directory.Exists(_config.DefaultPeglinInstallPath))
            {
                return _config.DefaultPeglinInstallPath;
            }

            // Auto-detect
            var detected = DetectPeglinInstallations();
            if (detected.Count > 0)
            {
                return detected.FirstOrDefault();
            }

            // If no installation found and prompting is enabled, ask the user
            if (promptIfNotFound)
            {
                return PromptForPeglinPath();
            }

            return null;
        }

        private string? PromptForPeglinPath()
        {
            Console.WriteLine();
            Console.WriteLine("No Peglin installation was found automatically.");
            Console.WriteLine("Please enter the path to your Peglin installation directory:");
            if (OperatingSystem.IsMacOS())
            {
                Console.WriteLine("(On macOS, this should be the Resources folder inside the app bundle)");
                Console.WriteLine("Example: /Users/yourname/Library/Application Support/Steam/steamapps/common/Peglin/peglin.app/Contents/Resources");
            }
            else
            {
                Console.WriteLine("(This should be the folder containing 'Peglin_Data')");
            }
            Console.WriteLine();
            Console.Write("Path (or press Enter to skip): ");
            
            var inputPath = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                return null;
            }

            // Validate the path
            if (Directory.Exists(inputPath))
            {
                // Normalize the path (e.g., if user enters .../peglin.app on macOS)
                var normalizedPath = PeglinPathHelper.NormalizePeglinPath(inputPath) ?? inputPath;
                
                if (PeglinPathHelper.IsValidPeglinPath(normalizedPath))
                {
                    // Save this path for future use
                    SetPeglinInstallPath(normalizedPath);
                    Console.WriteLine($"✓ Peglin installation found and saved for future use.");
                    return normalizedPath;
                }
                else
                {
                    Console.WriteLine("Error: This doesn't appear to be a valid Peglin installation directory.");
                    if (OperatingSystem.IsMacOS())
                    {
                        Console.WriteLine("Expected to find: Data/Managed/Assembly-CSharp.dll");
                        Console.WriteLine("Note: On macOS, you should point to the Resources folder inside the app bundle.");
                        Console.WriteLine("Example: .../Peglin/peglin.app/Contents/Resources");
                    }
                    else
                    {
                        Console.WriteLine("Expected to find: Peglin_Data/Managed/Assembly-CSharp.dll");
                    }
                    return null;
                }
            }
            else
            {
                Console.WriteLine($"Error: Directory '{inputPath}' does not exist.");
                return null;
            }
        }

        public string? GetEffectiveSaveFilePath(string? overridePath = null)
        {
            // Use override if provided
            if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            {
                return overridePath;
            }

            // Use configured default
            if (!string.IsNullOrWhiteSpace(_config.DefaultSaveFilePath) &&
                File.Exists(_config.DefaultSaveFilePath))
            {
                return _config.DefaultSaveFilePath;
            }

            // Auto-detect
            var detected = DetectSaveFiles();
            return detected.FirstOrDefault();
        }

        public void SetPeglinInstallPath(string? peglinPath)
        {
            _config.DefaultPeglinInstallPath = peglinPath;
            SaveConfiguration();

            // Reload game data mappings with the new path
            GameDataMappings.LoadGameDataMappings(peglinPath);
        }
    }

    public class Configuration
    {
        public string? DefaultPeglinInstallPath { get; set; }
        public string? DefaultSaveFilePath { get; set; }
        public DateTime LastModified { get; set; } = DateTime.Now;
        public List<string>? CachedPeglinInstallations { get; set; }
        public DateTime? CachedPeglinInstallationsTimestamp { get; set; }
    }
}
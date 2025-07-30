using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace peglin_save_explorer.Core
{
    public class ConfigurationManager
    {
        private static readonly string ConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PeglinSaveExplorer"
        );

        private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "settings.json");

        private Configuration _config;

        public Configuration Config => _config;

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
            var installations = new List<string>();

            // Check common Steam installation paths
            var steamPaths = new[]
            {
                @"C:\Program Files (x86)\Steam\steamapps\common\Peglin",
                @"C:\Program Files\Steam\steamapps\common\Peglin",
                @"D:\Steam\steamapps\common\Peglin",
                @"D:\SteamLibrary\steamapps\common\Peglin",
                @"E:\Steam\steamapps\common\Peglin",
                @"E:\SteamLibrary\steamapps\common\Peglin",
                @"F:\Steam\steamapps\common\Peglin",
                @"F:\SteamLibrary\steamapps\common\Peglin",
                @"G:\Steam\steamapps\common\Peglin",
                @"G:\SteamLibrary\steamapps\common\Peglin"
            };

            foreach (var path in steamPaths)
            {
                if (Directory.Exists(path))
                {
                    var dllPath = Path.Combine(path, "Peglin_Data", "Managed", "Assembly-CSharp.dll");
                    if (File.Exists(dllPath))
                    {
                        installations.Add(path);
                    }
                }
            }

            // Check if user has a custom Steam library location
            try
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var steamConfig = Path.Combine(programFiles, "Steam", "config", "libraryfolders.vdf");

                if (File.Exists(steamConfig))
                {
                    // Simple VDF parsing to find library paths
                    var lines = File.ReadAllLines(steamConfig);
                    foreach (var line in lines)
                    {
                        if (line.Contains("\"path\""))
                        {
                            var parts = line.Split('"');
                            if (parts.Length >= 4)
                            {
                                var libraryPath = parts[3].Replace("\\\\", "\\");
                                var peglinPath = Path.Combine(libraryPath, "steamapps", "common", "Peglin");
                                if (Directory.Exists(peglinPath))
                                {
                                    var dllPath = Path.Combine(peglinPath, "Peglin_Data", "Managed", "Assembly-CSharp.dll");
                                    if (File.Exists(dllPath) && !installations.Contains(peglinPath))
                                    {
                                        installations.Add(peglinPath);
                                    }
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

            return installations;
        }

        public List<string> DetectSaveFiles()
        {
            var saveFiles = new List<string>();

            var saveDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Red Nexus Games Inc", "Peglin"
            );

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

        public string? GetEffectivePeglinPath(string? overridePath = null)
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
            return detected.FirstOrDefault();
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
    }
}
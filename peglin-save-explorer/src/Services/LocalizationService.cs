using peglin_save_explorer.Extractors;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Services
{
    public class LocalizationService
    {
        private static LocalizationService? _instance;
        private static readonly object _lock = new object();
        
        private I2LocalizationParser? _parser;
        private bool _isLoaded = false;
        private string? _lastLoadedFilePath;

        private LocalizationService() { }

        public static LocalizationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LocalizationService();
                        }
                    }
                }
                return _instance;
            }
        }

        public bool EnsureLoaded(string? localizationFilePath = null)
        {
            lock (_lock)
            {
                // If already loaded and same file path, return success
                if (_isLoaded && _parser != null && string.Equals(_lastLoadedFilePath, localizationFilePath))
                {
                    return true;
                }

                // Find localization file if not provided
                var filePath = localizationFilePath ?? FindI2LocalizationFile();
                if (string.IsNullOrEmpty(filePath))
                {
                    Logger.Verbose("[LocalizationService] No I2 localization file found");
                    return false;
                }

                // Load the localization data
                _parser = new I2LocalizationParser();
                _isLoaded = _parser.LoadFromFile(filePath);
                _lastLoadedFilePath = filePath;

                if (_isLoaded)
                {
                    Logger.Verbose($"[LocalizationService] Successfully loaded {_parser.GetTermCount()} localization terms from {Path.GetFileName(filePath)}");
                }
                else
                {
                    Logger.Warning("[LocalizationService] Failed to load localization data");
                }

                return _isLoaded;
            }
        }

        public string? GetTranslation(string key, string language = "English")
        {
            if (!_isLoaded || _parser == null)
            {
                if (!EnsureLoaded())
                {
                    return null;
                }
            }

            return _parser?.GetTranslation(key, language);
        }

        public Dictionary<string, string>? GetAllTranslationsForKey(string key)
        {
            if (!_isLoaded || _parser == null)
            {
                if (!EnsureLoaded())
                {
                    return null;
                }
            }

            return _parser?.GetAllTranslationsForKey(key);
        }

        public List<string> GetAvailableLanguages()
        {
            if (!_isLoaded || _parser == null)
            {
                if (!EnsureLoaded())
                {
                    return new List<string>();
                }
            }

            return _parser?.GetAvailableLanguages() ?? new List<string>();
        }

        public int GetTermCount()
        {
            if (!_isLoaded || _parser == null)
            {
                if (!EnsureLoaded())
                {
                    return 0;
                }
            }

            return _parser?.GetTermCount() ?? 0;
        }

        public List<string> FindKeysContaining(string searchTerm)
        {
            if (!_isLoaded || _parser == null)
            {
                if (!EnsureLoaded())
                {
                    return new List<string>();
                }
            }

            return _parser?.FindKeysContaining(searchTerm) ?? new List<string>();
        }

        /// <summary>
        /// Gets all localization data as a dictionary of language -> key -> translation
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> GetAllLocalizationData()
        {
            if (!_isLoaded || _parser == null)
            {
                if (!EnsureLoaded())
                {
                    return new Dictionary<string, Dictionary<string, string>>();
                }
            }

            return _parser?.GetAllLocalizationData() ?? new Dictionary<string, Dictionary<string, string>>();
        }

        public bool IsLoaded => _isLoaded && _parser != null;

        private static string? FindI2LocalizationFile()
        {
            try
            {
                var saveDataPath = GetPeglinSaveDataPath();
                if (string.IsNullOrEmpty(saveDataPath) || !Directory.Exists(saveDataPath))
                {
                    Logger.Verbose("[LocalizationService] Peglin save data directory not found");
                    return null;
                }

                var locFiles = Directory.GetFiles(saveDataPath, "I2Source_*.loc", SearchOption.TopDirectoryOnly);
                
                if (locFiles.Length == 0)
                {
                    Logger.Verbose($"[LocalizationService] No I2 localization files found in {saveDataPath}");
                    return null;
                }

                var locFile = locFiles[0];
                Logger.Verbose($"[LocalizationService] Found I2 localization file: {Path.GetFileName(locFile)}");
                return locFile;
            }
            catch (Exception ex)
            {
                Logger.Verbose($"[LocalizationService] Error finding I2 localization file: {ex.Message}");
                return null;
            }
        }

        private static string? GetPeglinSaveDataPath()
        {
            try
            {
                string basePath;
                
                if (OperatingSystem.IsWindows())
                {
                    basePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "AppData", "LocalLow", "Red Nexus Games Inc", "Peglin");
                }
                else if (OperatingSystem.IsMacOS())
                {
                    basePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "Library", "Application Support", "Red Nexus Games Inc", "Peglin");
                }
                else if (OperatingSystem.IsLinux())
                {
                    basePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".config", "unity3d", "Red Nexus Games Inc", "Peglin");
                }
                else
                {
                    Logger.Verbose("[LocalizationService] Unsupported operating system for save data detection");
                    return null;
                }

                return Directory.Exists(basePath) ? basePath : null;
            }
            catch (Exception ex)
            {
                Logger.Verbose($"[LocalizationService] Error determining save data path: {ex.Message}");
                return null;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _parser = null;
                _isLoaded = false;
                _lastLoadedFilePath = null;
                Logger.Verbose("[LocalizationService] Reset localization cache");
            }
        }
    }
}
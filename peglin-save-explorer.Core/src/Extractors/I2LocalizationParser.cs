using System.Text;
using System.Reflection;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Core;
using AssetRipper.SourceGenerated.Extensions;

namespace peglin_save_explorer.Extractors
{
    public class I2LocalizationParser
    {
        private readonly Dictionary<string, Dictionary<string, string>> _localizationData = new();
        
        // Note: StringObfuscatorPassword is now extracted via reflection from Peglin assemblies
        // No hardcoded fallback to avoid distributing proprietary game data
        
        private static char[]? _cachedPassword = null;
        
        /// <summary>
        /// Gets the StringObfuscatorPassword via reflection from Peglin assemblies, with fallback to hardcoded value
        /// </summary>
        private static char[] GetStringObfuscatorPassword()
        {
            if (_cachedPassword != null)
            {
                return _cachedPassword;
            }

            try
            {
                Logger.Verbose("[I2Parser] Attempting to extract StringObfuscatorPassword via reflection");
                
                // Try to get the password from loaded assemblies first
                var password = TryGetPasswordFromLoadedAssemblies();
                if (password != null)
                {
                    _cachedPassword = password;
                    Logger.Verbose("[I2Parser] Successfully extracted password from loaded assemblies");
                    return _cachedPassword;
                }

                // Try to load and extract from Peglin installation
                password = TryGetPasswordFromPeglinInstallation();
                if (password != null)
                {
                    _cachedPassword = password;
                    Logger.Verbose("[I2Parser] Successfully extracted password from Peglin installation");
                    return _cachedPassword;
                }
            }
            catch (Exception ex)
            {
                Logger.Verbose($"[I2Parser] Reflection failed: {ex.Message}");
            }

            // No hardcoded fallback available - reflection is required
            Logger.Error("[I2Parser] Failed to extract StringObfuscatorPassword via reflection. Peglin installation or assemblies not accessible.");
            throw new InvalidOperationException("Cannot extract StringObfuscatorPassword from Peglin assemblies. Ensure Peglin is installed and accessible.");
        }

        /// <summary>
        /// Try to get password from currently loaded assemblies
        /// </summary>
        private static char[]? TryGetPasswordFromLoadedAssemblies()
        {
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var stringObfuscatorType = assembly.GetType("I2.Loc.StringObfucator");
                        if (stringObfuscatorType != null)
                        {
                            var passwordField = stringObfuscatorType.GetField(
                                "StringObfuscatorPassword", 
                                BindingFlags.Public | BindingFlags.Static);
                            
                            if (passwordField != null && passwordField.GetValue(null) is char[] password)
                            {
                                return password;
                            }
                        }
                    }
                    catch
                    {
                        // Skip assemblies that can't be processed
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Verbose($"[I2Parser] Error checking loaded assemblies: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// Try to load assembly from Peglin installation and extract password
        /// </summary>
        private static char[]? TryGetPasswordFromPeglinInstallation()
        {
            try
            {
                var configManager = new ConfigurationManager();
                var peglinPath = configManager.GetEffectivePeglinPath();
                
                if (string.IsNullOrEmpty(peglinPath) || !Directory.Exists(peglinPath))
                {
                    Logger.Verbose("[I2Parser] Peglin path not available for password extraction");
                    return null;
                }

                // Look for Assembly-CSharp.dll in the Peglin installation
                var possiblePaths = new[]
                {
                    Path.Combine(peglinPath, "Peglin_Data", "Managed", "Assembly-CSharp.dll"),
                    Path.Combine(peglinPath, "Managed", "Assembly-CSharp.dll"),
                    Path.Combine(peglinPath, "Assembly-CSharp.dll")
                };

                foreach (var assemblyPath in possiblePaths)
                {
                    if (File.Exists(assemblyPath))
                    {
                        try
                        {
                            var assembly = Assembly.LoadFrom(assemblyPath);
                            var stringObfuscatorType = assembly.GetType("I2.Loc.StringObfucator");
                            
                            if (stringObfuscatorType != null)
                            {
                                var passwordField = stringObfuscatorType.GetField(
                                    "StringObfuscatorPassword", 
                                    BindingFlags.Public | BindingFlags.Static);
                                
                                if (passwordField != null && passwordField.GetValue(null) is char[] password)
                                {
                                    Logger.Verbose($"[I2Parser] Extracted password from {Path.GetFileName(assemblyPath)}");
                                    return password;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Verbose($"[I2Parser] Failed to load assembly {assemblyPath}: {ex.Message}");
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Verbose($"[I2Parser] Error accessing Peglin installation: {ex.Message}");
            }
            
            return null;
        }
        
        public class LocalizationTerm
        {
            public required string Key { get; set; }
            public required string Type { get; set; }
            public required string Description { get; set; }
            public required Dictionary<string, string> Languages { get; set; }
        }

        public bool LoadFromFile(string filePath)
        {
            try
            {
                Logger.Verbose($"[I2Parser] Loading localization file: {filePath}");
                
                if (!File.Exists(filePath))
                {
                    Logger.Warning($"[I2Parser] Localization file not found: {filePath}");
                    return false;
                }

                string content = File.ReadAllText(filePath, Encoding.UTF8);
                
                // Check if it starts with the I2 export marker
                if (!content.StartsWith("[i2e]"))
                {
                    Logger.Warning($"[I2Parser] File does not appear to be an I2 localization export");
                    return false;
                }

                // Remove the [i2e] marker and decode the obfuscated content
                string obfuscatedContent = content.Substring(5); // Remove "[i2e]"
                string decodedContent = DecodeObfuscatedString(obfuscatedContent);
                
                if (decodedContent == null)
                {
                    Logger.Warning($"[I2Parser] Failed to decode obfuscated I2 content");
                    return false;
                }

                // Dump the unencrypted plaintext content for debugging
                DumpPlaintextContent(decodedContent);

                return ParseI2Content(decodedContent);
            }
            catch (Exception ex)
            {
                Logger.Error($"[I2Parser] Error loading localization file: {ex.Message}");
                return false;
            }
        }
        
        private bool ParseI2Content(string content)
        {
            try
            {
                // Split content by [/i2csv] markers to get sections
                var sections = content.Split(new[] { "[/i2csv]" }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var section in sections)
                {
                    if (string.IsNullOrWhiteSpace(section)) continue;
                    
                    // Look for category tag in this section
                    var categoryMatch = System.Text.RegularExpressions.Regex.Match(section, @"\[i2category\]([^\[]*)\[/i2category\]");
                    string categoryName = "Default";
                    
                    if (categoryMatch.Success)
                    {
                        categoryName = categoryMatch.Groups[1].Value.Trim();
                        Logger.Verbose($"[I2Parser] Found category: {categoryName}");
                    }
                    
                    // Parse this section's content
                    ParseI2Section(section, categoryName);
                }
                
                var totalTerms = _localizationData.Values.SelectMany(d => d.Keys).Distinct().Count();
                Logger.Verbose($"[I2Parser] Successfully parsed {totalTerms} unique localization terms");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[I2Parser] Error parsing I2 content: {ex.Message}");
                return false;
            }
        }
        
        private void ParseI2Section(string content, string category)
        {
            try
            {
                // Parse I2CSV format: rows separated by [ln], columns by [*]
                var rows = content.Split(new[] { "[ln]" }, StringSplitOptions.RemoveEmptyEntries);
                
                if (rows.Length < 2)
                {
                    return; // Skip sections without data
                }

                // First row should contain headers (Key[*]Type[*]Desc[*]Language1[*]Language2...)
                var headers = rows[0].Split(new[] { "[*]" }, StringSplitOptions.None);
                
                if (headers.Length < 4 || !headers[0].Contains("Key"))
                {
                    return; // Skip non-data sections (like statistics)
                }
                
                // Extract language names (skip Key, Type, Desc columns)
                var languageNames = new List<string>();
                for (int i = 3; i < headers.Length; i++)
                {
                    var langName = headers[i].Trim();
                    if (!string.IsNullOrEmpty(langName) && !langName.Equals("Dev Notes", StringComparison.OrdinalIgnoreCase))
                    {
                        languageNames.Add(langName);
                        if (!_localizationData.ContainsKey(langName))
                        {
                            _localizationData[langName] = new Dictionary<string, string>();
                        }
                    }
                }
                
                // Handle single-language case where no explicit language headers exist
                if (languageNames.Count == 0 && headers.Length >= 4)
                {
                    // Assume single language (English) with translation in column 3
                    languageNames.Add("English");
                    if (!_localizationData.ContainsKey("English"))
                    {
                        _localizationData["English"] = new Dictionary<string, string>();
                    }
                }

                // Parse each data row
                for (int rowIndex = 1; rowIndex < rows.Length; rowIndex++)
                {
                    var columns = rows[rowIndex].Split(new[] { "[*]" }, StringSplitOptions.None);
                    
                    if (columns.Length < 4) continue;
                    
                    var baseKey = columns[0].Trim();
                    if (string.IsNullOrEmpty(baseKey)) continue;
                    
                    // Construct the full key with category prefix (unless it's Default)
                    var fullKey = category.Equals("Default", StringComparison.OrdinalIgnoreCase) 
                        ? baseKey 
                        : $"{category}/{baseKey}";

                    // Debug output for magnet terms to track duplicates
                    if (baseKey.IndexOf("magnet", StringComparison.OrdinalIgnoreCase) >= 0) 
                    {
                        Logger.Verbose($"[I2Parser] Processing magnet term in category '{category}': key='{fullKey}', type='{columns[1].Trim()}', desc='{columns[2].Trim()}'");
                    }
                    
                    // Store translations for each language
                    for (int langIndex = 0; langIndex < languageNames.Count && langIndex + 3 < columns.Length; langIndex++)
                    {
                        var translation = columns[langIndex + 3]; // Skip Key, Type, Desc
                        if (!string.IsNullOrEmpty(translation))
                        {
                            _localizationData[languageNames[langIndex]][fullKey] = DecodeI2String(translation);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[I2Parser] Error parsing section for category '{category}': {ex.Message}");
            }
        }
        
        private static string? DecodeObfuscatedString(string obfuscatedString)
        {
            try
            {
                // Based on StringObfucator.Decode method from Peglin dump
                return XoREncode(FromBase64(obfuscatedString));
            }
            catch (Exception ex)
            {
                Logger.Error($"[I2Parser] Error decoding obfuscated string: {ex.Message}");
                return null;
            }
        }
        
        private static string FromBase64(string base64string)
        {
            byte[] bytes = Convert.FromBase64String(base64string);
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }
        
        private static string XoREncode(string normalString)
        {
            try
            {
                char[] obfuscatorPassword = GetStringObfuscatorPassword();
                char[] charArray = normalString.ToCharArray();
                int length1 = obfuscatorPassword.Length;
                int index = 0;
                for (int length2 = charArray.Length; index < length2; ++index)
                    charArray[index] = (char)((int)charArray[index] ^ (int)obfuscatorPassword[index % length1] ^ (index % 2 == 0 ? (int)(byte)(index * 23) : (int)(byte)(-index * 51)));
                return new string(charArray);
            }
            catch (Exception ex)
            {
                Logger.Error($"[I2Parser] Error in XoR encoding: {ex.Message}");
                return null;
            }
        }
        
        private static string DecodeI2String(string str)
        {
            // Based on LocalizationReader.DecodeString method
            return string.IsNullOrEmpty(str) ? string.Empty : str.Replace("<\\n>", "\r\n");
        }
        
        /// <summary>
        /// Dumps the plaintext content of the localization file for debugging (only once per session)
        /// </summary>
        private static void DumpPlaintextContent(string content)
        {
            try
            {
                // Use the same cache directory as strings.json
                string cacheDir;
                if (OperatingSystem.IsWindows())
                {
                    cacheDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "PeglinSaveExplorer"
                    );
                }
                else if (OperatingSystem.IsMacOS())
                {
                    cacheDir = Path.Combine(
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
                    cacheDir = Path.Combine(xdgConfigHome, "PeglinSaveExplorer");
                }
                
                Directory.CreateDirectory(cacheDir);
                var plaintextPath = Path.Combine(cacheDir, "localization_plaintext.txt");
                
                // Only write if file doesn't exist or is significantly different in size
                if (!File.Exists(plaintextPath) || Math.Abs(File.ReadAllText(plaintextPath).Length - content.Length) > 100)
                {
                    File.WriteAllText(plaintextPath, content);
                    Logger.Info($"[I2Parser] Dumped plaintext localization content to {plaintextPath}");
                }
                else
                {
                    Logger.Verbose($"[I2Parser] Plaintext file already exists and appears current");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[I2Parser] Failed to dump plaintext content: {ex.Message}");
            }
        }

        public string? GetTranslation(string key, string language = "English")
        {
            if (_localizationData.TryGetValue(language, out var languageData))
            {
                if (languageData.TryGetValue(key, out var translation))
                {
                    return translation;
                }
            }
            
            return null;
        }
        
        public Dictionary<string, string>? GetAllTranslationsForKey(string key)
        {
            var result = new Dictionary<string, string>();
            
            foreach (var kvp in _localizationData)
            {
                if (kvp.Value.TryGetValue(key, out var translation) && !string.IsNullOrEmpty(translation))
                {
                    result[kvp.Key] = translation;
                }
            }
            
            return result.Count > 0 ? result : null;
        }
        
        public List<string> GetAvailableLanguages()
        {
            return _localizationData.Keys.ToList();
        }
        
        public int GetTermCount()
        {
            return _localizationData.Values.FirstOrDefault()?.Count ?? 0;
        }
        
        public List<string> FindKeysContaining(string searchTerm)
        {
            var results = new HashSet<string>();
            
            foreach (var languageData in _localizationData.Values)
            {
                foreach (var key in languageData.Keys)
                {
                    if (key.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(key);
                    }
                }
            }
            
            return results.ToList();
        }
        
        /// <summary>
        /// Gets all localization data as a dictionary of language -> key -> translation
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> GetAllLocalizationData()
        {
            return new Dictionary<string, Dictionary<string, string>>(_localizationData);
        }
    }
}
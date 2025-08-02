using System;
using System.CommandLine;
using System.IO;
using peglin_save_explorer.Core;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Commands
{
    public class ConfigCommand : ICommand
    {
        public Command CreateCommand()
        {
            var command = new Command("config", "Manage application configuration");

            var propertyArgument = new Argument<string?>(
                name: "property",
                description: "The configuration property to get or set (e.g., 'peglin-path', 'save-path')",
                getDefaultValue: () => null);
            propertyArgument.Arity = ArgumentArity.ZeroOrOne;

            var valueArgument = new Argument<string?>(
                name: "value",
                description: "The value to set for the property",
                getDefaultValue: () => null);
            valueArgument.Arity = ArgumentArity.ZeroOrOne;

            var clearOption = new Option<bool>(
                new[] { "--clear", "-c" },
                "Clear the specified property value");

            var resetOption = new Option<bool>(
                new[] { "--reset" },
                "Reset all configuration to defaults");

            command.AddArgument(propertyArgument);
            command.AddArgument(valueArgument);
            command.AddOption(clearOption);
            command.AddOption(resetOption);

            command.SetHandler((string? property, string? value, bool clear, bool reset) =>
            {
                Execute(property, value, clear, reset);
            }, propertyArgument, valueArgument, clearOption, resetOption);

            return command;
        }

        private void Execute(string? property, string? value, bool clear, bool reset)
        {
            var configManager = new ConfigurationManager();

            if (reset)
            {
                ResetConfiguration(configManager);
                return;
            }

            if (string.IsNullOrEmpty(property))
            {
                // No property specified - display all configuration
                DisplayConfiguration(configManager);
                return;
            }

            // Normalize property name
            property = property.ToLower().Replace("_", "-");

            if (clear)
            {
                ClearProperty(configManager, property);
            }
            else if (!string.IsNullOrEmpty(value))
            {
                SetProperty(configManager, property, value);
            }
            else
            {
                GetProperty(configManager, property);
            }
        }

        private void DisplayConfiguration(ConfigurationManager configManager)
        {
            var config = configManager.Config;
            
            Console.WriteLine("=== Peglin Save Explorer Configuration ===");
            Console.WriteLine();
            
            // Display config file location
            var configPath = configManager.GetConfigFilePath();
            Console.WriteLine($"Configuration file: {configPath}");
            Console.WriteLine($"File exists: {File.Exists(configPath)}");
            Console.WriteLine();

            // Display configuration values
            Console.WriteLine("Current settings:");
            Console.WriteLine($"  peglin-path: {config.DefaultPeglinInstallPath ?? "(not set)"}");
            Console.WriteLine($"  save-path: {config.DefaultSaveFilePath ?? "(not set)"}");
            
            if (config.CachedPeglinInstallations != null && config.CachedPeglinInstallations.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Cached Peglin installations ({config.CachedPeglinInstallations.Count}):");
                foreach (var installation in config.CachedPeglinInstallations)
                {
                    Console.WriteLine($"  - {installation}");
                }
                if (config.CachedPeglinInstallationsTimestamp.HasValue)
                {
                    var age = DateTime.Now - config.CachedPeglinInstallationsTimestamp.Value;
                    Console.WriteLine($"  Cache age: {age.TotalDays:F1} days");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Available properties:");
            Console.WriteLine("  peglin-path    - Default Peglin installation path");
            Console.WriteLine("  save-path      - Default save file path");
            Console.WriteLine();
            Console.WriteLine("Usage examples:");
            Console.WriteLine("  peglin-save-explorer config peglin-path               # Get current value");
            Console.WriteLine("  peglin-save-explorer config peglin-path /path/to/peglin  # Set value");
            Console.WriteLine("  peglin-save-explorer config peglin-path --clear       # Clear value");
            Console.WriteLine("  peglin-save-explorer config --reset                   # Reset all settings");
        }

        private void GetProperty(ConfigurationManager configManager, string property)
        {
            var config = configManager.Config;
            
            switch (property)
            {
                case "peglin-path":
                case "peglinpath":
                    Console.WriteLine(config.DefaultPeglinInstallPath ?? "(not set)");
                    break;
                    
                case "save-path":
                case "savepath":
                    Console.WriteLine(config.DefaultSaveFilePath ?? "(not set)");
                    break;
                    
                default:
                    Logger.Error($"Unknown configuration property: {property}");
                    Console.WriteLine("Valid properties: peglin-path, save-path");
                    break;
            }
        }

        private void SetProperty(ConfigurationManager configManager, string property, string value)
        {
            var config = configManager.Config;
            
            switch (property)
            {
                case "peglin-path":
                case "peglinpath":
                    // Validate the path
                    if (!Directory.Exists(value))
                    {
                        Logger.Error($"Directory does not exist: {value}");
                        return;
                    }
                    
                    // Normalize the path (e.g., handle .app bundles on macOS)
                    var normalizedPath = PeglinPathHelper.NormalizePeglinPath(value) ?? value;
                    
                    if (!PeglinPathHelper.IsValidPeglinPath(normalizedPath))
                    {
                        Logger.Error("This doesn't appear to be a valid Peglin installation directory.");
                        if (OperatingSystem.IsMacOS())
                        {
                            Console.WriteLine("On macOS, please point to the Resources folder inside the app bundle.");
                            Console.WriteLine("Example: /Users/yourname/Library/Application Support/Steam/steamapps/common/Peglin/peglin.app/Contents/Resources");
                        }
                        else
                        {
                            Console.WriteLine("Expected to find: Peglin_Data/Managed/Assembly-CSharp.dll");
                        }
                        return;
                    }
                    
                    configManager.SetPeglinInstallPath(normalizedPath);
                    Logger.Info($"✓ Peglin installation path set to: {normalizedPath}");
                    break;
                    
                case "save-path":
                case "savepath":
                    if (!File.Exists(value))
                    {
                        Logger.Error($"File does not exist: {value}");
                        return;
                    }
                    
                    if (!value.EndsWith(".data", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Error("Save file must have .data extension");
                        return;
                    }
                    
                    config.DefaultSaveFilePath = value;
                    configManager.SaveConfiguration();
                    Logger.Info($"✓ Default save file path set to: {value}");
                    break;
                    
                default:
                    Logger.Error($"Unknown configuration property: {property}");
                    Console.WriteLine("Valid properties: peglin-path, save-path");
                    break;
            }
        }

        private void ClearProperty(ConfigurationManager configManager, string property)
        {
            var config = configManager.Config;
            
            switch (property)
            {
                case "peglin-path":
                case "peglinpath":
                    config.DefaultPeglinInstallPath = null;
                    // Also clear the cache since the path changed
                    config.CachedPeglinInstallations = null;
                    config.CachedPeglinInstallationsTimestamp = null;
                    configManager.SaveConfiguration();
                    Logger.Info("✓ Peglin installation path cleared");
                    break;
                    
                case "save-path":
                case "savepath":
                    config.DefaultSaveFilePath = null;
                    configManager.SaveConfiguration();
                    Logger.Info("✓ Default save file path cleared");
                    break;
                    
                default:
                    Logger.Error($"Unknown configuration property: {property}");
                    Console.WriteLine("Valid properties: peglin-path, save-path");
                    break;
            }
        }

        private void ResetConfiguration(ConfigurationManager configManager)
        {
            Console.Write("Are you sure you want to reset all configuration? (y/N): ");
            var response = Console.ReadLine()?.Trim().ToLower();
            
            if (response == "y" || response == "yes")
            {
                var config = configManager.Config;
                config.DefaultPeglinInstallPath = null;
                config.DefaultSaveFilePath = null;
                config.CachedPeglinInstallations = null;
                config.CachedPeglinInstallationsTimestamp = null;
                configManager.SaveConfiguration();
                
                Logger.Info("✓ Configuration reset to defaults");
            }
            else
            {
                Console.WriteLine("Reset cancelled");
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using peglin_save_explorer.Commands;

namespace CommandExtractor
{
    public class CommandInfo
    {
        public string Id { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<OptionInfo> Options { get; set; } = new();
        public List<string> Examples { get; set; } = new();
    }

    public class OptionInfo
    {
        public string Type { get; set; } = "";
        public List<string> Flags { get; set; } = new();
        public string Description { get; set; } = "";
        public bool Required { get; set; }
        public string? DefaultValue { get; set; }
    }

    public class CommandsData
    {
        public List<CommandInfo> Commands { get; set; } = new();
        public Dictionary<string, CommandInfo> CommandsByName { get; set; } = new();
        public List<string> CommandIds { get; set; } = new();
    }

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var commands = ExtractCommands();
                
                var commandsData = new CommandsData
                {
                    Commands = commands,
                    CommandsByName = commands.ToDictionary(c => c.Name, c => c),
                    CommandIds = commands.Select(c => c.Name).ToList()
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(commandsData, options);
                Console.WriteLine(json);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        static List<CommandInfo> ExtractCommands()
        {
            var commands = new List<CommandInfo>();
            
            // Get the current assembly's location and use it to find the peglin-save-explorer assembly
            var currentAssemblyPath = Assembly.GetExecutingAssembly().Location;
            var currentDir = Path.GetDirectoryName(currentAssemblyPath)!;
            
            // Navigate from CommandExtractor location to the main project
            // From: docs-site/scripts/CommandExtractor/bin/Debug/net9.0
            // To: peglin-save-explorer/bin/Debug/net9.0
            var repoRoot = currentDir;
            while (!string.IsNullOrEmpty(repoRoot) && !Path.GetFileName(repoRoot).Equals("peglin-save-explorer"))
            {
                repoRoot = Path.GetDirectoryName(repoRoot);
            }
            
            if (string.IsNullOrEmpty(repoRoot) || !Path.GetFileName(repoRoot).Equals("peglin-save-explorer"))
            {
                Console.Error.WriteLine("Could not find repo root");
                return commands;
            }
            
            // Try different runtime target folders
            var baseBinPath = Path.Combine(repoRoot, "peglin-save-explorer", "bin", "Debug", "net9.0");
            string assemblyPath = "";
            
            // Check for runtime-specific build first (e.g., osx-arm64)
            var runtimeDirs = Directory.GetDirectories(baseBinPath).Where(d => !d.EndsWith(".dll") && !d.EndsWith(".exe"));
            foreach (var runtimeDir in runtimeDirs)
            {
                var candidatePath = Path.Combine(runtimeDir, "peglin-save-explorer.dll");
                if (File.Exists(candidatePath))
                {
                    assemblyPath = candidatePath;
                    break;
                }
            }
            
            // Fallback to direct path
            if (string.IsNullOrEmpty(assemblyPath))
            {
                assemblyPath = Path.Combine(baseBinPath, "peglin-save-explorer.dll");
            }
            
            if (!File.Exists(assemblyPath))
            {
                Console.Error.WriteLine($"Assembly not found at: {assemblyPath}");
                Console.Error.WriteLine($"Searched in: {baseBinPath}");
                return commands;
            }
            
            var assembly = Assembly.LoadFrom(assemblyPath);
            Console.Error.WriteLine($"Loaded assembly: {assembly.GetName().Name}");
            
            // Get all command types from the loaded assembly
            Type[] allTypes;
            try
            {
                allTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Some types failed to load, but we can work with the ones that did
                allTypes = ex.Types.Where(t => t != null).ToArray()!;
                Console.Error.WriteLine($"Warning: Some types failed to load, working with {allTypes.Length} types");
            }
            
            var commandTypes = allTypes
                .Where(t => t != null && t.IsClass && !t.IsAbstract && typeof(ICommand).IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToList();
                
            Console.Error.WriteLine($"Found {commandTypes.Count} command types");

            foreach (var commandType in commandTypes)
            {
                try
                {
                    // Skip non-command classes
                    if (commandType.Name == "CommandRegistry")
                        continue;

                    var commandInstance = Activator.CreateInstance(commandType) as ICommand;
                    if (commandInstance == null)
                        continue;

                    var command = commandInstance.CreateCommand();
                    
                    var commandInfo = new CommandInfo
                    {
                        Id = commandType.Name.Replace("Command", "").ToLowerInvariant(),
                        ClassName = commandType.Name,
                        Name = command.Name,
                        Description = command.Description ?? ""
                    };

                    // Extract options using reflection
                    ExtractOptions(command, commandInfo);
                    
                    // Generate a basic example
                    GenerateExample(commandInfo);

                    commands.Add(commandInfo);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Warning: Failed to process {commandType.Name}: {ex.Message}");
                }
            }

            return commands;
        }

        static void ExtractOptions(Command command, CommandInfo commandInfo)
        {
            // Access the options through the public API
            foreach (var symbol in command.Children.OfType<Option>())
            {
                var option = symbol as Option;
                if (option == null) continue;

                var optionInfo = new OptionInfo
                {
                    Type = option.ValueType?.Name ?? "object",
                    Description = option.Description ?? "",
                    Required = option.IsRequired,
                    Flags = option.Aliases.ToList()
                };

                // Try to get default value using reflection
                try
                {
                    var defaultValueProperty = option.GetType().GetProperty("DefaultValue");
                    if (defaultValueProperty != null)
                    {
                        var defaultValue = defaultValueProperty.GetValue(option);
                        if (defaultValue != null)
                        {
                            optionInfo.DefaultValue = defaultValue.ToString();
                        }
                    }
                }
                catch
                {
                    // Ignore if we can't get the default value
                }

                commandInfo.Options.Add(optionInfo);
            }
        }

        static void GenerateExample(CommandInfo commandInfo)
        {
            var requiredFlags = commandInfo.Options
                .Where(opt => opt.Required)
                .Select(opt => $"{opt.Flags.FirstOrDefault()} <value>")
                .Where(flag => !string.IsNullOrEmpty(flag))
                .ToList();

            var exampleCommand = $"peglin-save-explorer {commandInfo.Name}";
            if (requiredFlags.Any())
            {
                exampleCommand += " " + string.Join(" ", requiredFlags);
            }
            
            commandInfo.Examples.Add(exampleCommand);
        }
    }
}
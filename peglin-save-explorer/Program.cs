using System;
using System.Collections.Generic;
using System.IO;
using System.CommandLine;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace peglin_save_explorer
{
    class Program
    {
        private static readonly ConfigurationManager configManager = new ConfigurationManager();
        
        static async Task<int> Main(string[] args)
        {
            // Add widget test command for debugging
            if (args.Length > 0 && args[0] == "widget-test")
            {
                Console.WriteLine("Widget test functionality not implemented yet.");
                return 0;
            }
            
            var rootCommand = new RootCommand("Peglin Save Explorer - Parse and explore Peglin save files using OdinSerializer");

            var fileOption = new Option<FileInfo?>(
                new[] { "--file", "-f" },
                description: "Path to the Peglin save file")
            {
                IsRequired = false // Make it optional since we can use defaults
            };

            // Analysis commands
            var summaryCommand = new Command("summary", "Show player statistics summary")
            {
                fileOption
            };
            summaryCommand.SetHandler((FileInfo? file) => CommandHandlers.ShowSummary(file), fileOption);

            var orbsCommand = new Command("orbs", "Analyze orb usage and performance")
            {
                fileOption
            };
            var topCountOption = new Option<int>(
                new[] { "--top", "-t" },
                description: "Show top N orbs by criteria",
                getDefaultValue: () => 10);
            var sortByOption = new Option<string>(
                new[] { "--sort", "-s" },
                description: "Sort by: damage, usage, efficiency, cruciball",
                getDefaultValue: () => "damage");
            orbsCommand.Add(topCountOption);
            orbsCommand.Add(sortByOption);
            orbsCommand.SetHandler((FileInfo? file, int top, string sortBy) => CommandHandlers.AnalyzeOrbs(file, top, sortBy), fileOption, topCountOption, sortByOption);

            var statsCommand = new Command("stats", "Show detailed player statistics")
            {
                fileOption
            };
            statsCommand.SetHandler((FileInfo? file) => CommandHandlers.ShowDetailedStats(file), fileOption);

            var searchCommand = new Command("search", "Search for specific data in save file")
            {
                fileOption
            };
            var queryOption = new Option<string>(
                new[] { "--query", "-q" },
                description: "Search query (orb name, achievement, etc.)")
            {
                IsRequired = true
            };
            searchCommand.Add(queryOption);
            searchCommand.SetHandler((FileInfo? file, string query) => CommandHandlers.SearchSaveData(file, query), fileOption, queryOption);

            // Interactive mode command
            var interactiveCommand = new Command("interactive", "Start interactive exploration mode")
            {
                fileOption
            };
            interactiveCommand.SetHandler((FileInfo? file) => StartInteractiveMode(file), fileOption);

            // Run history command
            var runHistoryCommand = new Command("runs", "View and manage run history")
            {
                fileOption
            };
            var exportOption = new Option<string>(
                new[] { "--export", "-e" },
                description: "Export run history to file");
            var importOption = new Option<string>(
                new[] { "--import", "-i" },
                description: "Import run history from file");
            var updateSaveOption = new Option<bool>(
                new[] { "--update-save", "-u" },
                description: "Update save file with imported runs (experimental)",
                getDefaultValue: () => false);
            var dumpRawOption = new Option<string>(
                new[] { "--dump-raw", "-d" },
                description: "Dump raw run history data to file for analysis");
            runHistoryCommand.Add(exportOption);
            runHistoryCommand.Add(importOption);
            runHistoryCommand.Add(updateSaveOption);
            runHistoryCommand.Add(dumpRawOption);
            runHistoryCommand.SetHandler((FileInfo? file, string export, string import, bool updateSave, string dumpRaw) => 
                CommandHandlers.HandleRunHistory(file, export, import, updateSave, dumpRaw), 
                fileOption, exportOption, importOption, updateSaveOption, dumpRawOption);

            // Legacy dump command
            var dumpCommand = new Command("dump", "Dump raw save file structure")
            {
                fileOption
            };
            var outputOption = new Option<string>(
                new[] { "--output", "-o" },
                description: "Output file path");
            dumpCommand.Add(outputOption);
            dumpCommand.SetHandler((FileInfo? file, string output) => CommandHandlers.DumpSaveFile(file, output), fileOption, outputOption);

            rootCommand.Add(summaryCommand);
            rootCommand.Add(orbsCommand);
            rootCommand.Add(statsCommand);
            rootCommand.Add(searchCommand);
            rootCommand.Add(runHistoryCommand);
            rootCommand.Add(interactiveCommand);
            rootCommand.Add(dumpCommand);

            // Default handler for backwards compatibility
            rootCommand.SetHandler((FileInfo? file) => CommandHandlers.ShowSummary(file), fileOption);

            return await rootCommand.InvokeAsync(args);
        }

        internal static JObject? LoadSaveData(FileInfo? file)
        {
            // Try to get effective file path
            string? filePath = null;
            
            if (file != null && file.Exists)
            {
                filePath = file.FullName;
            }
            else
            {
                filePath = configManager.GetEffectiveSaveFilePath();
                if (string.IsNullOrEmpty(filePath))
                {
                    Console.WriteLine("Error: No save file specified and no default save file found.");
                    Console.WriteLine("Please specify a save file with -f or configure a default in settings.");
                    return null;
                }
                Console.WriteLine($"Using default save file: {filePath}");
            }
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File '{filePath}' does not exist.");
                return null;
            }

            try
            {
                byte[] saveData = File.ReadAllBytes(filePath);
                var dumper = new SaveFileDumper(configManager);
                var result = dumper.DumpSaveFile(saveData);
                return JObject.Parse(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading save file: {ex.Message}");
                return null;
            }
        }







        static void StartInteractiveMode(FileInfo? file)
        {
            // Pass null saveData so ConsoleSession.Run() handles file loading and sets up fileInfo properly
            var session = new ConsoleSession(null, file);
            session.Run();
        }

    }

}
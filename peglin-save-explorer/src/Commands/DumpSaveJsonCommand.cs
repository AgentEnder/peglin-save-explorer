using System.CommandLine;
using peglin_save_explorer.Core;
using Newtonsoft.Json;
using OdinSerializer;
using ToolBox.Serialization;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Commands
{
    public class DumpSaveJsonCommand : ICommand
    {
        public Command CreateCommand()
        {
            var command = new Command("dump-save-json", "Dump the entire save file structure to JSON for analysis");
            
            var fileOption = new Option<FileInfo?>(
                new[] { "--file", "-f" },
                description: "Path to the save file (defaults to configured save file)"
            );
            
            var outputOption = new Option<string?>(
                new[] { "--output", "-o" },
                description: "Output JSON file path (defaults to save_dump.json)"
            );
            
            command.AddOption(fileOption);
            command.AddOption(outputOption);
            
            command.SetHandler(async (FileInfo? file, string? output) =>
            {
                try
                {
                    var configManager = new ConfigurationManager();
                    string? saveFilePath = null;

                    if (file != null && file.Exists)
                    {
                        saveFilePath = file.FullName;
                    }
                    else
                    {
                        saveFilePath = configManager.GetEffectiveSaveFilePath();
                        if (string.IsNullOrEmpty(saveFilePath))
                        {
                            Logger.Error("No save file specified and no default save file found.");
                            Logger.Info("Please specify a save file with -f or configure a default in settings.");
                            return;
                        }
                        Logger.Info($"Using default save file: {saveFilePath}");
                    }

                    if (!File.Exists(saveFilePath))
                    {
                        Logger.Error($"Save file not found: {saveFilePath}");
                        return;
                    }
                    
                    string outputPath = output ?? "save_dump.json";
                    DumpSaveToJson(saveFilePath, outputPath);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error dumping save: {ex.Message}");
                    Logger.Verbose($"Stack trace: {ex.StackTrace}");
                }
            }, fileOption, outputOption);
            
            return command;
        }
        
        private static void DumpSaveToJson(string saveFilePath, string outputPath)
        {
            try
            {
                Logger.Info($"Input: {saveFilePath}");
                Logger.Info($"Output: {outputPath}");

                // Use the proven SaveFileDumper approach
                byte[] saveData = File.ReadAllBytes(saveFilePath);
                Logger.Info($"Save file size: {saveData.Length} bytes");
                
                var configManager = new ConfigurationManager();
                var dumper = new SaveFileDumper(configManager);
                string jsonResult = dumper.DumpSaveFile(saveData);
                
                // Write to file
                File.WriteAllText(outputPath, jsonResult);
                
                Logger.Info($"Successfully dumped save structure to {outputPath}");
                Logger.Info("You can now analyze the complete save structure in the JSON file.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to dump save: {ex.Message}");
                Logger.Verbose($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
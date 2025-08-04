using System.CommandLine;
using peglin_save_explorer.Core;

namespace peglin_save_explorer.Commands
{
    public class AnalyzeSaveCommand : ICommand
    {
        public Command CreateCommand()
        {
            var command = new Command("analyze-save", "Analyze the structure of a save file without modification");
            
            var fileOption = new Option<FileInfo?>(
                new[] { "--file", "-f" },
                description: "Path to the save file (defaults to configured save file)"
            );
            
            command.AddOption(fileOption);
            
            command.SetHandler(async (FileInfo? file) =>
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
                            Program.WriteToConsole("Error: No save file specified and no default save file found.");
                            Program.WriteToConsole("Please specify a save file with -f or configure a default in settings.");
                            return;
                        }
                        Program.WriteToConsole($"Using default save file: {saveFilePath}");
                    }

                    if (!File.Exists(saveFilePath))
                    {
                        Program.WriteToConsole($"❌ Save file not found: {saveFilePath}");
                        return;
                    }
                    
                    SaveDataLoader.AnalyzeSaveStructure(saveFilePath);
                }
                catch (Exception ex)
                {
                    Program.WriteToConsole($"❌ Error analyzing save: {ex.Message}");
                }
            }, fileOption);
            
            return command;
        }
    }
}
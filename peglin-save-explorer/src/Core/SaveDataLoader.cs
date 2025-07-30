using Newtonsoft.Json.Linq;

namespace peglin_save_explorer.Core
{
    public static class SaveDataLoader
    {
        private static readonly ConfigurationManager configManager = new ConfigurationManager();

        public static JObject? LoadSaveData(FileInfo? file)
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
                    Program.WriteToConsole("Error: No save file specified and no default save file found.");
                    Program.WriteToConsole("Please specify a save file with -f or configure a default in settings.");
                    return null;
                }
                Program.WriteToConsole($"Using default save file: {filePath}");
            }

            if (!File.Exists(filePath))
            {
                Program.WriteToConsole($"Error: File '{filePath}' does not exist.");
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
                Program.WriteToConsole($"Error loading save file: {ex.Message}");
                return null;
            }
        }
    }
}
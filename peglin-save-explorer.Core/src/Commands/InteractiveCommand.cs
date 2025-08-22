using System.CommandLine;
using peglin_save_explorer.UI;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Commands
{
    public class InteractiveCommand : ICommand
    {
        public Command CreateCommand()
        {
            var fileOption = new Option<FileInfo?>(
                new[] { "--file", "-f" },
                description: "Path to the Peglin save file")
            {
                IsRequired = false
            };

            var command = new Command("interactive", "Start interactive exploration mode")
            {
                fileOption
            };

            command.SetHandler((FileInfo? file) => Execute(file), fileOption);
            return command;
        }

        private static void Execute(FileInfo? file)
        {
            // Suppress console output to prevent logs from appearing before widget system starts
            ConsoleUtility.SetConsoleOutputSuppression(true);

            try
            {
                // Pass null saveData so ConsoleSession.Run() handles file loading and sets up fileInfo properly
                var session = new ConsoleSession(null, file);
                session.Run();
            }
            finally
            {
                // Restore console output after widget session ends
                ConsoleUtility.SetConsoleOutputSuppression(false);
            }
        }
    }
}
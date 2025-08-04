using System.CommandLine;
using System.CommandLine.Invocation;
using peglin_save_explorer.Core;

namespace peglin_save_explorer.Commands
{
    public class SetCruciballCommand : ICommand
    {
        public Command CreateCommand()
        {
            var command = new Command("set-cruciball", "Set the cruciball level for a specific character class");

            var classNameArgument = new Argument<string>(
                "class",
                "Character class name (Peglin, Balladin, Roundrel, or Spinventor)"
            );

            var levelArgument = new Argument<int>(
                "level",
                "Cruciball level (0-20)"
            );

            var fileOption = new Option<FileInfo?>(
                new[] { "-f", "--file" },
                "Path to the save file to modify (if not specified, uses default)"
            );

            command.AddArgument(classNameArgument);
            command.AddArgument(levelArgument);
            command.AddOption(fileOption);

            command.SetHandler(Execute, classNameArgument, levelArgument, fileOption);

            return command;
        }

        private void Execute(string className, int level, FileInfo? file)
        {
            // Validate level is in range
            if (level < 0 || level > 20)
            {
                Program.WriteToConsole("Error: Cruciball level must be between 0 and 20.");
                return;
            }

            // Validate class name
            var validClasses = new[] { "Peglin", "Balladin", "Roundrel", "Spinventor" };
            if (!validClasses.Any(c => c.Equals(className, StringComparison.OrdinalIgnoreCase)))
            {
                Program.WriteToConsole($"Error: Invalid character class '{className}'.");
                Program.WriteToConsole("Valid classes are: Peglin, Balladin, Roundrel, Spinventor");
                return;
            }

            // Normalize the class name to match the expected casing
            className = validClasses.First(c => c.Equals(className, StringComparison.OrdinalIgnoreCase));

            Program.WriteToConsole($"Setting cruciball level for {className} to {level}...");

            bool success = SaveDataLoader.UpdateCruciballLevel(className, level, file);

            if (!success)
            {
                Program.WriteToConsole("Failed to update cruciball level.");
            }
        }
    }
}
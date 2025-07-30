using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Commands
{
    public static class CommandRegistry
    {
        public static void RegisterAllCommands(RootCommand rootCommand)
        {
            var commandTypes = GetCommandTypes();
            
            foreach (var commandType in commandTypes)
            {
                try
                {
                    var commandInstance = Activator.CreateInstance(commandType) as ICommand;
                    if (commandInstance != null)
                    {
                        var command = commandInstance.CreateCommand();
                        
                        // Skip verbose flag integration for interactive command
                        if (command.Name != "interactive")
                        {
                            WrapCommandWithLogging(command, rootCommand);
                        }
                        
                        rootCommand.Add(command);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to register command {commandType.Name}: {ex.Message}");
                }
            }
        }

        private static void WrapCommandWithLogging(Command command, RootCommand rootCommand)
        {
            // Get the verbose option from the root command
            var verboseOption = rootCommand.Options.FirstOrDefault(o => o.Name == "verbose");
            if (verboseOption is Option<bool> verboseOpt)
            {
                // Store the original handler
                var originalHandler = command.Handler;
                
                // Create a new handler that sets up logging first
                command.SetHandler(async (InvocationContext context) =>
                {
                    var verbose = context.ParseResult.GetValueForOption(verboseOpt);
                    Logger.SetLogLevel(verbose ? LogLevel.Verbose : LogLevel.Info);
                    
                    // Execute the original handler
                    if (originalHandler != null)
                    {
                        await originalHandler.InvokeAsync(context);
                    }
                });
            }
        }

        private static IEnumerable<Type> GetCommandTypes()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ICommand).IsAssignableFrom(t))
                .OrderBy(t => t.Name);
        }
    }
}
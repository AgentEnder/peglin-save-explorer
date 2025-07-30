using System.CommandLine;
using peglin_save_explorer.Commands;
using peglin_save_explorer.Core;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer
{
    class Program
    {
        internal static bool suppressConsoleOutput = false;
        private static bool isInteractiveMode = false;

        static async Task<int> Main(string[] args)
        {
            // Set up signal handlers to ensure proper terminal cleanup
            Console.CancelKeyPress += OnCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            // Handle legacy test commands
            if (args.Length > 0 && args[0] == "widget-test")
            {
                Console.WriteLine("Widget test functionality not implemented yet.");
                return 0;
            }

            if (args.Length > 0 && args[0] == "test-assetripper")
            {
                Console.WriteLine("AssetRipper extraction test removed. Use 'peglin-save-explorer extract-relics' command instead.");
                return 0;
            }

            var rootCommand = new RootCommand("Peglin Save Explorer - Parse and explore Peglin save files using OdinSerializer");

            // Add global verbose option
            var verboseOption = new Option<bool>(
                new[] { "--verbose", "-v" },
                description: "Show verbose output including debug information",
                getDefaultValue: () => false);
            rootCommand.AddGlobalOption(verboseOption);

            // Register all commands automatically
            CommandRegistry.RegisterAllCommands(rootCommand);

            // Default handler for backwards compatibility (show summary)
            var fileOption = new Option<FileInfo?>(
                new[] { "--file", "-f" },
                description: "Path to the Peglin save file")
            {
                IsRequired = false
            };
            rootCommand.Add(fileOption);
            rootCommand.SetHandler((FileInfo? file, bool verbose) => 
            {
                SetupLogging(verbose);
                ShowDefaultSummary(file);
            }, fileOption, verboseOption);

            return await rootCommand.InvokeAsync(args);
        }

        private static void SetupLogging(bool verbose)
        {
            Logger.SetLogLevel(verbose ? LogLevel.Verbose : LogLevel.Info);
        }

        private static void ShowDefaultSummary(FileInfo? file)
        {
            var summaryCommand = new SummaryCommand();
            var command = summaryCommand.CreateCommand();
            // For the default handler, we'll just call the summary command logic directly
            // This is a bit of a workaround since we can't easily invoke the command directly
            Console.WriteLine("Showing summary (default command). Use --help to see all available commands.");
            var saveData = SaveDataLoader.LoadSaveData(file);
            if (saveData != null)
            {
                // Simple summary output
                Console.WriteLine("Save data loaded successfully. Use 'summary' command for detailed view.");
            }
        }

        internal static void SetConsoleOutputSuppression(bool suppress)
        {
            suppressConsoleOutput = suppress;
            if (suppress)
            {
                isInteractiveMode = true;
            }
            else
            {
                isInteractiveMode = false;
            }
        }

        internal static void WriteToConsole(string message)
        {
            if (!suppressConsoleOutput)
            {
                Console.WriteLine(message);
            }
        }

        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            // Prevent the process from terminating immediately
            e.Cancel = true;

            // Do basic terminal cleanup
            CleanupTerminal();

            // Now allow the process to exit
            Environment.Exit(0);
        }

        private static void OnProcessExit(object? sender, EventArgs e)
        {
            // Do basic terminal cleanup when process exits
            CleanupTerminal();
        }

        private static void CleanupTerminal()
        {
            // Only do terminal cleanup if we're in interactive mode
            // CLI commands should not manipulate terminal state
            if (!isInteractiveMode)
                return;

            try
            {
                // Basic terminal restoration without relying on tracked TerminalManager
                Console.Write("\x1b[?1049l"); // Exit alternate screen
                Console.ResetColor();
                Console.CursorVisible = true;
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }
}
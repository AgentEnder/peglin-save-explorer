using System.CommandLine;
using peglin_save_explorer.Commands;
using peglin_save_explorer.Core;
using peglin_save_explorer.Data;
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

            // Add global clean option
            var cleanOption = new Option<bool>(
                new[] { "--clean", "-c" },
                description: "Clear all caches before executing the command",
                getDefaultValue: () => false);
            rootCommand.AddGlobalOption(cleanOption);

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
            rootCommand.SetHandler((FileInfo? file, bool verbose, bool clean) => 
            {
                SetupLogging(verbose);
                HandleCleanOption(clean);
                ShowDefaultSummary(file);
            }, fileOption, verboseOption, cleanOption);

            return await rootCommand.InvokeAsync(args);
        }

        private static void SetupLogging(bool verbose)
        {
            Logger.SetLogLevel(verbose ? LogLevel.Verbose : LogLevel.Info);
        }

        private static void HandleCleanOption(bool clean)
        {
            if (clean)
            {
                Logger.Info("Clean option enabled - clearing all caches");
                CacheManager.ClearAllCaches();
            }
        }

        private static void ShowDefaultSummary(FileInfo? file)
        {
            Console.WriteLine("Showing summary (default command). Use --help to see all available commands.");
            var saveData = SaveDataLoader.LoadSaveData(file);
            if (saveData != null)
            {
                Console.WriteLine("Save data loaded successfully. Use 'summary' command for detailed view.");
            }
        }

        internal static void SetConsoleOutputSuppression(bool suppress)
        {
            suppressConsoleOutput = suppress;
            ConsoleUtility.SetConsoleOutputSuppression(suppress);
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
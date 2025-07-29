using System;

namespace peglin_save_explorer
{
    public static class DisplayHelper
    {
        // Terminal-safe ASCII symbols to replace emojis
        public const string FILE_ICON = "*";
        public const string SAVE_ICON = "+";
        public const string ORBS_ICON = "o";
        public const string DAMAGE_ICON = "#";
        public const string HITS_ICON = ">";
        public const string EFFICIENCY_ICON = "~";
        public const string TOP_ICON = "^";
        public const string ERROR_ICON = "!";
        public const string SUCCESS_ICON = "+";
        public const string TIP_ICON = "?";
        public const string SEARCH_ICON = "/";
        public const string STATS_ICON = "=";
        public const string GAME_ICON = "@";
        public const string COMBAT_ICON = "#";
        public const string TARGET_ICON = ">";
        public const string MONEY_ICON = "$";
        public const string STAR_ICON = "*";
        public const string FOLDER_ICON = "+";
        public const string TRASH_ICON = "x";
        public const string BOMB_ICON = "!";

        public static void PrintSectionHeader(string title)
        {
            Console.WriteLine();
            Console.WriteLine($"== {title} ==");
            Console.WriteLine();
        }

        public static void PrintSubHeader(string title)
        {
            Console.WriteLine();
            Console.WriteLine($"-- {title} --");
        }

        public static void PrintError(string message)
        {
            Console.WriteLine($"{ERROR_ICON} {message}");
        }

        public static void PrintSuccess(string message)
        {
            Console.WriteLine($"{SUCCESS_ICON} {message}");
        }

        public static void PrintInfo(string message)
        {
            Console.WriteLine($"{TIP_ICON} {message}");
        }

        public static void PrintFileInfo(string fileName)
        {
            Console.WriteLine($"{FILE_ICON} Peglin Save File: {fileName}");
        }

        public static void PrintSaveInfo(string message)
        {
            Console.WriteLine($"{SAVE_ICON} {message}");
        }

        public static void PrintOrbAnalysis(int topCount, string sortBy)
        {
            Console.WriteLine($"{ORBS_ICON} ORB ANALYSIS (Top {topCount} by {sortBy})");
        }

        public static void PrintOrbStats(long damage, long fired, long efficiency, int cruciball, long discarded, long removed)
        {
            Console.WriteLine($"    {DAMAGE_ICON} Damage: {damage:N0}");
            Console.WriteLine($"    {HITS_ICON} Times Fired: {fired:N0}");
            Console.WriteLine($"    {EFFICIENCY_ICON} Efficiency: {efficiency:N0} dmg/shot");
            Console.WriteLine($"    {TOP_ICON} Highest Cruciball: {cruciball}");
            Console.WriteLine($"    {TRASH_ICON} Discarded: {discarded:N0} | Removed: {removed:N0}");
        }

        public static void PrintSearchHeader(string query)
        {
            Console.WriteLine($"{SEARCH_ICON} SEARCHING FOR: '{query}'");
        }

        public static void PrintInteractiveHeader(string fileName)
        {
            Console.WriteLine($"{ORBS_ICON} PEGLIN SAVE EXPLORER - INTERACTIVE MODE");
            Console.WriteLine();
            Console.WriteLine($"{FILE_ICON} File: {fileName}");
            Console.WriteLine($"{SAVE_ICON} Loaded: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        public static void PrintPrompt(string message)
        {
            Console.Write($"\n{TARGET_ICON} {message}: ");
        }

        public static void ClearConsole()
        {
            // Only clear if we're in an interactive console (not piped)
            if (!Console.IsOutputRedirected)
            {
                try
                {
                    Console.Clear();
                }
                catch (System.IO.IOException)
                {
                    // Handle case where console doesn't support clearing
                    Console.WriteLine("\n" + new string('=', 60) + "\n");
                }
            }
            else
            {
                // Add visual separator for piped output
                Console.WriteLine("\n" + new string('=', 60) + "\n");
            }
        }

        public static void PrintNavigationTip()
        {
            Console.WriteLine($"\n{TIP_ICON} Press 'Enter' to return to main menu...");
        }

        public static void PrintContinuePrompt()
        {
            Console.WriteLine($"\n{TIP_ICON} Press any key to continue...");
        }
    }
}
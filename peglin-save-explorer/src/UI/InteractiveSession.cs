using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.UI
{
    public class InteractiveSession
    {
        private readonly JObject saveData;
        private readonly string fileName;
        private readonly JObject data;
        private readonly JObject statsData;
        private readonly JArray orbData;

        public InteractiveSession(JObject saveData, string fileName)
        {
            this.saveData = saveData;
            this.fileName = fileName;
            this.data = saveData["data"] as JObject;
            this.statsData = data?["PermanentStats"]?["Value"] as JObject;
            this.orbData = statsData?["historicOrbPlayData"] as JArray;
        }

        public void Run()
        {
            ClearConsole();
            ShowWelcome();

            while (true)
            {
                ShowMainMenu();
                var input = Console.ReadLine()?.Trim().ToLower();

                switch (input)
                {
                    case "1":
                    case "summary":
                    case "s":
                        ShowInteractiveSummary();
                        break;
                    case "2":
                    case "orbs":
                    case "o":
                        ShowOrbsExplorer();
                        break;
                    case "3":
                    case "stats":
                    case "st":
                        ShowStatsExplorer();
                        break;
                    case "4":
                    case "search":
                    case "f":
                        ShowSearchInterface();
                        break;
                    case "5":
                    case "browse":
                    case "b":
                        ShowDataBrowser();
                        break;
                    case "q":
                    case "quit":
                    case "exit":
                        Console.WriteLine($"\n{DisplayHelper.SUCCESS_ICON} Thanks for using Peglin Save Explorer!");
                        return;
                    case "c":
                    case "clear":
                        ClearConsole();
                        ShowWelcome();
                        break;
                    case "h":
                    case "help":
                        ShowHelp();
                        break;
                    default:
                        DisplayHelper.PrintError("Invalid option. Press 'h' for help or 'q' to quit.");
                        PressAnyKey();
                        break;
                }
            }
        }

        private void ShowWelcome()
        {
            Console.WriteLine($"{DisplayHelper.ORBS_ICON} PEGLIN SAVE EXPLORER - INTERACTIVE MODE");
            Console.WriteLine("══════════════════════════════════════════");
            Console.WriteLine($"{DisplayHelper.FILE_ICON} File: {fileName}");
            Console.WriteLine($"{DisplayHelper.SAVE_ICON} Loaded: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
        }

        private void ShowMainMenu()
        {
            Console.WriteLine("┌─ MAIN MENU ─────────────────────────────────┐");
            Console.WriteLine("│ 1. Summary        - Player overview         │");
            Console.WriteLine("│ 2. Orbs          - Orb analysis & insights  │");
            Console.WriteLine("│ 3. Stats         - Detailed statistics      │");
            Console.WriteLine("│ 4. Search        - Find specific data       │");
            Console.WriteLine("│ 5. Browse        - Raw data browser         │");
            Console.WriteLine("│                                              │");
            Console.WriteLine("│ h. Help          c. Clear       q. Quit     │");
            Console.WriteLine("└──────────────────────────────────────────────┘");
            DisplayHelper.PrintPrompt("Select option");
        }

        private void ShowInteractiveSummary()
        {
            ClearConsole();
            ShowHeader("PLAYER SUMMARY");

            if (statsData != null)
            {
                var totalRuns = (int)statsData["totalRuns"];
                var totalWins = (int)statsData["totalWins"];
                var winRate = totalRuns > 0 ? (double)totalWins / totalRuns * 100 : 0;

                DisplayHelper.PrintSubHeader("CORE STATISTICS");
                Console.WriteLine("─────────────────────────────────────────────");
                Console.WriteLine($"Total Runs: {totalRuns:N0}         Total Wins: {totalWins:N0}");
                Console.WriteLine($"Win Rate: {winRate:F1}%           Custom Runs: {statsData["totalCustomRuns"]:N0}");
                Console.WriteLine($"Damage Dealt: {(long)statsData["totalDamageDealt"]:N0}");
                Console.WriteLine($"Pegs Hit: {(long)statsData["totalPegsHit"]:N0}     Coins Earned: {(long)statsData["totalCoinsEarned"]:N0}");
                Console.WriteLine();

                // Class performance with interactive details
                var runsPerClass = statsData["runsPerClass"] as JArray;
                var winsPerClass = statsData["winsPerClass"] as JArray;
                if (runsPerClass != null && winsPerClass != null)
                {
                    var classNames = new[] { "Peglin", "Multiball Master", "Matryoshka Master", "Roundrel" };
                    DisplayHelper.PrintSubHeader("CLASS PERFORMANCE");
                    Console.WriteLine("─────────────────────────────────────────────");
                    for (int i = 0; i < Math.Min(classNames.Length, runsPerClass.Count); i++)
                    {
                        var runs = (int)runsPerClass[i];
                        var wins = (int)winsPerClass[i];
                        var rate = runs > 0 ? (double)wins / runs * 100 : 0;
                        var bar = CreateProgressBar(rate, 30);
                        Console.WriteLine($"{classNames[i],-18}: {wins,3}/{runs,-3} {bar} {rate:F1}%");
                    }
                }
            }

            DisplayHelper.PrintNavigationTip();
            Console.ReadLine();
        }

        private void ShowOrbsExplorer()
        {
            if (orbData == null)
            {
                DisplayHelper.PrintError("No orb data available");
                PressAnyKey();
                return;
            }

            var orbs = orbData.Cast<JObject>().ToList();
            var currentPage = 0;
            var pageSize = 10;
            var sortMode = "damage";

            while (true)
            {
                ClearConsole();
                ShowHeader($"ORBS EXPLORER ({orbs.Count} orbs, sorted by {sortMode})");

                // Sort orbs based on current mode
                var sortedOrbs = sortMode switch
                {
                    "usage" => orbs.OrderByDescending(o => (int)o["timesFired"]).ToList(),
                    "efficiency" => orbs.OrderByDescending(o => 
                    {
                        var fired = (int)o["timesFired"];
                        var damage = (long)o["damageDealt"];
                        return fired > 0 ? damage / fired : 0;
                    }).ToList(),
                    "cruciball" => orbs.OrderByDescending(o => (int)o["highestCruciballBeat"]).ToList(),
                    _ => orbs.OrderByDescending(o => (long)o["damageDealt"]).ToList(),
                };

                var totalPages = (int)Math.Ceiling((double)sortedOrbs.Count / pageSize);
                var pageOrbs = sortedOrbs.Skip(currentPage * pageSize).Take(pageSize).ToList();

                // Display current page
                for (int i = 0; i < pageOrbs.Count; i++)
                {
                    var orb = pageOrbs[i];
                    var globalIndex = currentPage * pageSize + i + 1;
                    var name = orb["name"]?.ToString() ?? "Unknown";
                    var damage = (long)orb["damageDealt"];
                    var fired = (int)orb["timesFired"];
                    var efficiency = fired > 0 ? damage / fired : 0;
                    var cruciball = (int)orb["highestCruciballBeat"];

                    Console.WriteLine($"{globalIndex,3}. {name}");
                    Console.WriteLine($"     {DisplayHelper.DAMAGE_ICON} {damage:N0} dmg  {DisplayHelper.HITS_ICON} {fired:N0} shots  {DisplayHelper.EFFICIENCY_ICON} {efficiency:N0}/shot  {DisplayHelper.TOP_ICON} C{cruciball}");
                    Console.WriteLine();
                }

                // Navigation info
                Console.WriteLine("─────────────────────────────────────────────");
                Console.WriteLine($"Page {currentPage + 1}/{totalPages}");
                Console.WriteLine();
                Console.WriteLine("Navigation: [n]ext [p]rev [d]amage [u]sage [e]fficiency [c]ruciball [q]uit");
                Console.Write("Command: ");

                var input = Console.ReadLine()?.Trim().ToLower();
                switch (input)
                {
                    case "n":
                    case "next":
                        if (currentPage < totalPages - 1) currentPage++;
                        break;
                    case "p":
                    case "prev":
                        if (currentPage > 0) currentPage--;
                        break;
                    case "d":
                    case "damage":
                        sortMode = "damage";
                        currentPage = 0;
                        break;
                    case "u":
                    case "usage":
                        sortMode = "usage";
                        currentPage = 0;
                        break;
                    case "e":
                    case "efficiency":
                        sortMode = "efficiency";
                        currentPage = 0;
                        break;
                    case "c":
                    case "cruciball":
                        sortMode = "cruciball";
                        currentPage = 0;
                        break;
                    case "q":
                    case "quit":
                    case "":
                        return;
                }
            }
        }

        private void ShowStatsExplorer()
        {
            ClearConsole();
            ShowHeader("DETAILED STATISTICS");

            if (statsData == null)
            {
                DisplayHelper.PrintError("No statistics data available");
                PressAnyKey();
                return;
            }

            DisplayHelper.PrintSubHeader("GAMEPLAY STATISTICS");
            Console.WriteLine("─────────────────────────────────────────────");
            Console.WriteLine($"Total Runs: {statsData["totalRuns"]:N0}           Total Wins: {statsData["totalWins"]:N0}");
            Console.WriteLine($"Custom Runs: {statsData["totalCustomRuns"]:N0}         Custom Wins: {statsData["totalCustomRunWins"]:N0}");
            Console.WriteLine();

            DisplayHelper.PrintSubHeader("COMBAT STATISTICS");
            Console.WriteLine("─────────────────────────────────────────────");
            Console.WriteLine($"Total Damage: {(long)statsData["totalDamageDealt"]:N0}");
            Console.WriteLine($"Best Single Run: {(long)statsData["mostDamageDealt"]:N0}");
            Console.WriteLine($"Damage Negated: {(long)statsData["totalDamageNegated"]:N0}");
            Console.WriteLine($"Shots Taken: {(long)statsData["totalShotsTaken"]:N0}      Crit Shots: {(long)statsData["totalCritShotsTaken"]:N0}");
            Console.WriteLine();

            DisplayHelper.PrintSubHeader("PEG STATISTICS");
            Console.WriteLine("─────────────────────────────────────────────");
            Console.WriteLine($"Total Pegs Hit: {(long)statsData["totalPegsHit"]:N0}");
            Console.WriteLine($"Refresh Pegs: {(long)statsData["totalPegsHitRefresh"]:N0}    Crit Pegs: {(long)statsData["totalPegsHitCrit"]:N0}");
            Console.WriteLine($"Best Turn: {(long)statsData["mostPegsHitInOneTurn"]:N0} pegs");
            Console.WriteLine($"Pegs Refreshed: {(long)statsData["totalPegsRefreshed"]:N0}");
            Console.WriteLine();

            Console.WriteLine($"{DisplayHelper.MONEY_ICON} ECONOMY & {DisplayHelper.BOMB_ICON} BOMBS");
            Console.WriteLine("─────────────────────────────────────────────");
            Console.WriteLine($"Coins Earned: {(long)statsData["totalCoinsEarned"]:N0}     Best Run: {(long)statsData["mostCoinsEarned"]:N0}");
            Console.WriteLine($"Bombs Thrown: {(long)statsData["totalBombsThrown"]:N0}     Rigged: {(long)statsData["totalBombsThrownRigged"]:N0}");
            Console.WriteLine($"Bombs Created: {(long)statsData["totalBombsCreated"]:N0}");

            PressAnyKey();
        }

        private void ShowSearchInterface()
        {
            ClearConsole();
            ShowHeader("SEARCH INTERFACE");

            Console.WriteLine($"{DisplayHelper.SEARCH_ICON} Enter search terms to find data in your save file.");
            Console.WriteLine("Examples: 'matryoshka', 'achievement', 'cruciball', 'damage'");
            Console.WriteLine("Type 'back' to return to main menu.\n");

            while (true)
            {
                Console.Write("Search: ");
                var query = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(query)) continue;
                if (query.ToLower() == "back") return;

                var results = SearchInJToken(saveData, query.ToLower(), "");
                
                Console.WriteLine($"\n{DisplayHelper.STATS_ICON} Found {results.Count} results for '{query}':");
                Console.WriteLine("─────────────────────────────────────────────");

                var displayCount = Math.Min(15, results.Count);
                for (int i = 0; i < displayCount; i++)
                {
                    var result = results[i];
                    Console.WriteLine($"{i + 1,2}. {result.Path}");
                    Console.WriteLine($"    → {result.Value}");
                }

                if (results.Count > displayCount)
                {
                    Console.WriteLine($"    ... and {results.Count - displayCount} more results");
                }
                
                Console.WriteLine();
            }
        }

        private void ShowDataBrowser()
        {
            ClearConsole();
            ShowHeader("RAW DATA BROWSER");
            
            Console.WriteLine($"{DisplayHelper.FOLDER_ICON} Navigate through your save data structure:");
            Console.WriteLine("Available sections:");
            
            if (data != null)
            {
                var sections = data.Properties().ToList();
                for (int i = 0; i < sections.Count; i++)
                {
                    var section = sections[i];
                    Console.WriteLine($"{i + 1,2}. {section.Name}");
                }
            }

            Console.WriteLine("\nEnter section number to explore, or 'back' to return:");
            Console.Write("Selection: ");
            
            var input = Console.ReadLine()?.Trim();
            if (input?.ToLower() == "back") return;
            
            if (int.TryParse(input, out int sectionIndex) && sectionIndex > 0 && sectionIndex <= data?.Properties().Count())
            {
                var section = data.Properties().ElementAt(sectionIndex - 1);
                ShowDataSection(section.Name, section.Value);
            }
            else
            {
                Console.WriteLine("Invalid selection.");
                PressAnyKey();
            }
        }

        private void ShowDataSection(string sectionName, JToken sectionData)
        {
            ClearConsole();
            ShowHeader($"DATA SECTION: {sectionName}");
            
            var jsonString = sectionData.ToString(Newtonsoft.Json.Formatting.Indented);
            var lines = jsonString.Split('\n');
            
            // Show first 30 lines
            var displayLines = Math.Min(30, lines.Length);
            for (int i = 0; i < displayLines; i++)
            {
                Console.WriteLine($"{i + 1,3}: {lines[i]}");
            }
            
            if (lines.Length > displayLines)
            {
                Console.WriteLine($"\n... ({lines.Length - displayLines} more lines)");
            }
            
            PressAnyKey();
        }

        private void ShowHeader(string title)
        {
            Console.WriteLine($"{DisplayHelper.ORBS_ICON} {title}");
            Console.WriteLine("".PadRight(title.Length + 3, '═'));
            Console.WriteLine();
        }

        private void ShowHelp()
        {
            ClearConsole();
            ShowHeader("HELP & SHORTCUTS");
            
            Console.WriteLine($"{DisplayHelper.TIP_ICON} NAVIGATION SHORTCUTS:");
            Console.WriteLine("  • Numbers (1-5) or first letter of menu items");
            Console.WriteLine("  • 'q' or 'quit' to exit anywhere");
            Console.WriteLine("  • 'c' or 'clear' to clear screen");
            Console.WriteLine("  • 'h' or 'help' for this help screen");
            Console.WriteLine();
            
            Console.WriteLine($"{DisplayHelper.ORBS_ICON} ORBS EXPLORER:");
            Console.WriteLine("  • Navigate with 'n'ext/'p'rev");
            Console.WriteLine("  • Sort by: 'd'amage, 'u'sage, 'e'fficiency, 'c'ruciball");
            Console.WriteLine();
            
            Console.WriteLine($"{DisplayHelper.SEARCH_ICON} SEARCH TIPS:");
            Console.WriteLine("  • Search is case-insensitive");
            Console.WriteLine("  • Searches both keys and values");
            Console.WriteLine("  • Try: orb names, 'damage', 'cruciball', 'achievement'");
            
            PressAnyKey();
        }

        private void PressAnyKey()
        {
            DisplayHelper.PrintContinuePrompt();
            Console.ReadKey(true);
        }

        private void ClearConsole()
        {
            try
            {
                if (!Console.IsOutputRedirected && !Console.IsInputRedirected)
                {
                    DisplayHelper.ClearConsole();
                }
                else
                {
                    // When output is redirected, just add some spacing
                    Console.WriteLine("\n".PadRight(50, '='));
                }
            }
            catch
            {
                // Fallback if console operations fail
                Console.WriteLine("\n".PadRight(50, '='));
            }
        }

        private string CreateProgressBar(double percentage, int width)
        {
            var filled = (int)Math.Round(percentage / 100.0 * width);
            var empty = width - filled;
            return "[" + new string('█', filled) + new string('░', empty) + "]";
        }

        private List<(string Path, string Value)> SearchInJToken(JToken token, string query, string currentPath)
        {
            var results = new List<(string, string)>();

            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    var newPath = string.IsNullOrEmpty(currentPath) ? property.Name : $"{currentPath}.{property.Name}";
                    
                    if (property.Name.ToLower().Contains(query))
                    {
                        results.Add((newPath, property.Value?.ToString() ?? "null"));
                    }
                    
                    results.AddRange(SearchInJToken(property.Value, query, newPath));
                }
            }
            else if (token is JArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    var newPath = $"{currentPath}[{i}]";
                    results.AddRange(SearchInJToken(array[i], query, newPath));
                }
            }
            else if (token is JValue value)
            {
                var stringValue = value.ToString();
                if (stringValue.ToLower().Contains(query))
                {
                    results.Add((currentPath, stringValue));
                }
            }

            return results;
        }
    }
}
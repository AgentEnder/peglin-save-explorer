using System;
using System.Collections.Generic;
using System.Linq;

namespace peglin_save_explorer.Utils
{
    public static class ConsoleAutocomplete
    {
        /// <summary>
        /// Prompts the user to select from a list of options with autocomplete functionality.
        /// </summary>
        /// <param name="prompt">The prompt message to display</param>
        /// <param name="options">Array of valid options</param>
        /// <param name="caseSensitive">Whether matching should be case sensitive (default: false)</param>
        /// <param name="allowPartialMatch">Whether to allow partial matches if no exact match is found (default: true)</param>
        /// <returns>The selected option, or null if no valid selection was made</returns>
        public static string? PromptWithAutocomplete(string prompt, string[] options, bool caseSensitive = false, bool allowPartialMatch = true)
        {
            Logger.Info(prompt);
            Logger.Info("(Start typing, then use Tab/↓ for next suggestion, ↑ for previous suggestion)");
            foreach (var option in options)
            {
                Logger.Info($"  • {option}");
            }
            Console.Write("\nEnter selection: ");

            var userInput = ""; // What the user actually typed
            var displayInput = ""; // What's currently displayed (may be filled by Tab/Arrow)
            var currentSuggestionIndex = -1;
            var lastFilteredSuggestions = new List<string>();
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var isNavigating = false; // Track if we're in navigation mode

            ConsoleKeyInfo keyInfo;
            do
            {
                keyInfo = Console.ReadKey(true);

                if (keyInfo.Key == ConsoleKey.Tab || keyInfo.Key == ConsoleKey.DownArrow)
                {
                    // Get filtered suggestions based on user's actual typed input, not display input
                    var filteredSuggestions = GetFilteredSuggestions(options, userInput, comparison);
                    
                    if (filteredSuggestions.Any())
                    {
                        // If we're starting a new tab cycle or the suggestions changed, reset the index
                        if (!lastFilteredSuggestions.SequenceEqual(filteredSuggestions))
                        {
                            currentSuggestionIndex = -1;
                            lastFilteredSuggestions = filteredSuggestions;
                        }
                        
                        // Cycle to next suggestion
                        currentSuggestionIndex = (currentSuggestionIndex + 1) % filteredSuggestions.Count;
                        var suggestion = filteredSuggestions[currentSuggestionIndex];
                        
                        // Clear current input and replace with suggestion
                        ClearCurrentLine();
                        Console.Write("Enter selection: ");
                        displayInput = suggestion;
                        Console.Write(displayInput);
                        isNavigating = true;
                        
                        // Don't show suggestions when we've just selected one via Tab/Arrow
                    }
                }
                else if (keyInfo.Key == ConsoleKey.UpArrow)
                {
                    // Get filtered suggestions based on user's actual typed input, not display input
                    var filteredSuggestions = GetFilteredSuggestions(options, userInput, comparison);
                    
                    if (filteredSuggestions.Any())
                    {
                        // If we're starting a new cycle or the suggestions changed, reset the index
                        if (!lastFilteredSuggestions.SequenceEqual(filteredSuggestions))
                        {
                            currentSuggestionIndex = filteredSuggestions.Count; // Start from end for up arrow
                            lastFilteredSuggestions = filteredSuggestions;
                        }
                        
                        // Cycle to previous suggestion
                        currentSuggestionIndex = (currentSuggestionIndex - 1 + filteredSuggestions.Count) % filteredSuggestions.Count;
                        var suggestion = filteredSuggestions[currentSuggestionIndex];
                        
                        // Clear current input and replace with suggestion
                        ClearCurrentLine();
                        Console.Write("Enter selection: ");
                        displayInput = suggestion;
                        Console.Write(displayInput);
                        isNavigating = true;
                        
                        // Don't show suggestions when we've just selected one via Arrow
                    }
                }
                else if (keyInfo.Key == ConsoleKey.Backspace && displayInput.Length > 0)
                {
                    if (isNavigating)
                    {
                        // If we were navigating, go back to user input and then backspace
                        displayInput = userInput;
                        isNavigating = false;
                    }
                    
                    if (displayInput.Length > 0)
                    {
                        displayInput = displayInput.Substring(0, displayInput.Length - 1);
                        userInput = displayInput; // Update user input to match
                    }
                    
                    currentSuggestionIndex = -1;
                    lastFilteredSuggestions.Clear();
                    
                    // Redraw the line
                    ClearCurrentLine();
                    Console.Write("Enter selection: ");
                    Console.Write(displayInput);
                    
                    // Show suggestions
                    ShowSuggestions(options, displayInput, comparison);
                }
                else if (keyInfo.Key == ConsoleKey.Enter)
                {
                    break;
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    // User is typing - update both user input and display input
                    if (isNavigating)
                    {
                        // If we were navigating, start fresh from user input
                        displayInput = userInput + keyInfo.KeyChar;
                        isNavigating = false;
                    }
                    else
                    {
                        displayInput += keyInfo.KeyChar;
                    }
                    
                    userInput = displayInput; // Keep user input in sync when typing
                    currentSuggestionIndex = -1;
                    lastFilteredSuggestions.Clear();
                    
                    // Redraw the line
                    ClearCurrentLine();
                    Console.Write("Enter selection: ");
                    Console.Write(displayInput);
                    
                    // Show suggestions
                    ShowSuggestions(options, displayInput, comparison);
                }
            } while (keyInfo.Key != ConsoleKey.Enter);

            Console.WriteLine(); // Move to next line

            // Find the best match
            var selectedOption = options.FirstOrDefault(o => o.Equals(displayInput.Trim(), comparison));
            
            if (selectedOption != null)
            {
                Logger.Info($"Selected: {selectedOption}");
                return selectedOption;
            }

            // Try partial match if exact match not found and partial matching is allowed
            if (allowPartialMatch)
            {
                var partialMatch = options.FirstOrDefault(o => o.StartsWith(displayInput.Trim(), comparison));
                if (partialMatch != null)
                {
                    Logger.Info($"Selected: {partialMatch} (partial match)");
                    return partialMatch;
                }
            }

            Logger.Error($"Invalid selection '{displayInput}'. Valid options are: {string.Join(", ", options)}");
            return null;
        }

        /// <summary>
        /// Prompts the user to enter an integer within a specified range with validation.
        /// </summary>
        /// <param name="prompt">The prompt message to display</param>
        /// <param name="minValue">Minimum allowed value (inclusive)</param>
        /// <param name="maxValue">Maximum allowed value (inclusive)</param>
        /// <returns>The entered integer, or null if invalid input was provided</returns>
        public static int? PromptForInteger(string prompt, int minValue, int maxValue)
        {
            Logger.Info($"{prompt} ({minValue}-{maxValue}): ");
            var input = Console.ReadLine();
            if (int.TryParse(input, out int value) && value >= minValue && value <= maxValue)
            {
                Logger.Info($"Selected: {value}");
                return value;
            }

            Logger.Error($"Invalid input. Please enter a number between {minValue} and {maxValue}.");
            return null;
        }

        /// <summary>
        /// Prompts the user with a simple yes/no question.
        /// </summary>
        /// <param name="prompt">The question to ask</param>
        /// <param name="defaultValue">Default value if user just presses Enter (optional)</param>
        /// <returns>True for yes, false for no, or null if no valid input was provided</returns>
        public static bool? PromptYesNo(string prompt, bool? defaultValue = null)
        {
            var defaultText = defaultValue.HasValue ? $" (default: {(defaultValue.Value ? "yes" : "no")})" : "";
            Logger.Info($"{prompt} (y/n){defaultText}: ");
            
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();
            
            if (string.IsNullOrEmpty(input) && defaultValue.HasValue)
            {
                Logger.Info($"Using default: {(defaultValue.Value ? "yes" : "no")}");
                return defaultValue.Value;
            }
            
            if (input == "y" || input == "yes")
            {
                Logger.Info("Selected: yes");
                return true;
            }
            
            if (input == "n" || input == "no")
            {
                Logger.Info("Selected: no");
                return false;
            }

            Logger.Error("Please enter 'y' for yes or 'n' for no.");
            return null;
        }

        private static List<string> GetFilteredSuggestions(string[] options, string input, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(input))
                return options.ToList();

            return options
                .Where(o => o.StartsWith(input, comparison))
                .ToList();
        }

        private static void ShowSuggestions(string[] options, string input, StringComparison comparison)
        {
            // Don't show suggestions automatically while typing to avoid line shifting
            // Users can use Tab/Arrow keys to see and navigate suggestions
            return;
        }

        private static void ClearCurrentLine()
        {
            var currentTop = Console.CursorTop;
            Console.SetCursorPosition(0, currentTop);
            Console.Write(new string(' ', Console.WindowWidth - 1));
            Console.SetCursorPosition(0, currentTop);
        }
    }
}

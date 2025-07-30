using System;
using System.Collections.Generic;
using System.Linq;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.UI
{
    public class AutocompleteWidget : ConsoleWidget
    {
        private List<AutocompleteMenuItem> allItems;
        private List<AutocompleteMenuItem> filteredItems;
        private int selectedIndex;
        private string filterText;
        private bool caseSensitive;
        private int scrollOffset;
        private int maxDisplayItems;
        private string prompt;
        private bool isCompleted;
        private AutocompleteMenuItem? selectedItem;

        public AutocompleteWidget(List<AutocompleteMenuItem> items, string prompt = "Select an option:", bool caseSensitive = false)
        {
            this.allItems = items ?? new List<AutocompleteMenuItem>();
            this.filteredItems = new List<AutocompleteMenuItem>(allItems);
            this.prompt = prompt;
            this.caseSensitive = caseSensitive;
            this.filterText = "";
            this.selectedIndex = 0;
            this.scrollOffset = 0;
            this.isCompleted = false;
            this.selectedItem = null;

            RefreshFilteredItems();
        }

        public override void SetTerminal(TerminalManager terminal)
        {
            base.SetTerminal(terminal);

            // Calculate display area based on terminal size and widget position
            // Reserve space for: prompt(1) + instructions(1) + empty(1) + filter(1) + status(1) = 5 lines
            var availableHeight = terminal.Height - Y - 5; // Account for Y position and widget overhead
            this.maxDisplayItems = Math.Max(5, Math.Min(20, availableHeight)); // Conservative max of 20
            this.Height = 5 + maxDisplayItems; // 5 overhead lines + item lines
        }

        public override void OnResize()
        {
            if (Terminal != null)
            {
                // Recalculate display area based on new terminal size and widget position
                // Reserve space for: prompt(1) + instructions(1) + empty(1) + filter(1) + status(1) = 5 lines
                var availableHeight = Terminal.Height - Y - 5; // Account for Y position and widget overhead
                this.maxDisplayItems = Math.Max(5, Math.Min(20, availableHeight)); // Conservative max of 20
                this.Height = 5 + maxDisplayItems; // 5 overhead lines + item lines

                // Adjust scroll offset if necessary
                if (filteredItems.Count > 0)
                {
                    var maxScrollOffset = Math.Max(0, filteredItems.Count - maxDisplayItems);
                    if (scrollOffset > maxScrollOffset)
                    {
                        scrollOffset = maxScrollOffset;
                    }

                    // Ensure selected item is still visible
                    UpdateScrollOffset();
                }
            }
        }

        public AutocompleteMenuItem? GetSelectedItem()
        {
            return selectedItem;
        }

        public bool IsCompleted()
        {
            return isCompleted;
        }

        public override void Update()
        {
            // No continuous updates needed for autocomplete
        }

        public override void Render()
        {
            if (Terminal == null) return;

            // Clear the entire widget area first to prevent background color artifacts
            var widgetWidth = Math.Min(Terminal.Width - X, 80); // Match the max width used below
            var clearLine = new FormattedString(new string(' ', widgetWidth), TextFormat.Default);

            // Calculate actual lines needed: 4 header lines + actual items displayed + 1 status line
            var actualItemsDisplayed = Math.Min(maxDisplayItems, filteredItems.Count);
            var totalLines = 4 + actualItemsDisplayed + 1; // 4 for header lines, actual items, +1 for status

            // Clear all lines that will be used by this widget, plus a few extra to clear previous artifacts
            var linesToClear = Math.Max(totalLines, Height); // Use Height property to ensure we clear the widget's full allocated space
            for (int i = 0; i < linesToClear && (Y + i) < Terminal.Height; i++)
            {
                Terminal.WriteAt(X, Y + i, clearLine);
            }

            int currentY = Y;

            // Render prompt and instructions with proper formatting
            Terminal.WriteAt(X, currentY++, new FormattedString(prompt, TextFormat.Highlighted));
            Terminal.WriteAt(X, currentY++, new FormattedString("Type to filter, use ↑↓ to navigate, Enter to select, Esc to cancel", TextFormat.Default));
            currentY++; // Empty line

            // Render filter text
            Terminal.WriteAt(X, currentY++, new FormattedString($"Filter: {filterText}_", TextFormat.Default));

            if (filteredItems.Count == 0)
            {
                Terminal.WriteAt(X, currentY++, new FormattedString("No matching options found.", TextFormat.Default));

                // Fill remaining item lines with empty space
                for (int i = 0; i < maxDisplayItems; i++)
                {
                    currentY++;
                }
            }
            else
            {
                // Calculate viewport
                var displayCount = Math.Min(maxDisplayItems, filteredItems.Count);
                var endIndex = Math.Min(scrollOffset + displayCount, filteredItems.Count);

                // Render items in viewport
                for (int i = scrollOffset; i < endIndex; i++)
                {
                    var item = filteredItems[i];
                    var isSelected = i == selectedIndex;

                    if (isSelected && HasFocus)
                    {
                        var selectedText = new FormattedString($"> {item.DisplayText}", TextFormat.Selected);
                        Terminal.WriteAt(X, currentY, selectedText);
                        // Fill the entire line width to ensure background covers the full line
                        var lineWidth = Math.Min(Terminal.Width - X, 80); // Reasonable max width
                        var padding = Math.Max(0, lineWidth - selectedText.Length);
                        if (padding > 0)
                        {
                            var paddingText = new FormattedString(new string(' ', padding), TextFormat.Selected);
                            Terminal.WriteAt(X + selectedText.Length, currentY, paddingText);
                        }
                        currentY++;
                    }
                    else
                    {
                        var normalText = new FormattedString($"  {item.DisplayText}", TextFormat.Default);
                        Terminal.WriteAt(X, currentY, normalText);
                        currentY++;
                    }
                }

                // Fill remaining lines (advance currentY for spacing)
                var displayedItems = endIndex - scrollOffset;
                for (int i = displayedItems; i < maxDisplayItems; i++)
                {
                    currentY++;
                }
            }

            // Render status
            string statusText;
            if (filteredItems.Count > maxDisplayItems)
            {
                statusText = $"Showing {selectedIndex + 1} of {filteredItems.Count} items (↑↓ to scroll)";
            }
            else
            {
                statusText = $"Showing {filteredItems.Count} items";
            }

            // Add focus indicator with highlighting
            if (HasFocus)
            {
                statusText += " [FOCUSED]";
                var focusedText = new FormattedString(statusText, TextFormat.Highlighted);
                Terminal.WriteAt(X, currentY, focusedText);
            }
            else
            {
                var normalText = new FormattedString(statusText, TextFormat.Default);
                Terminal.WriteAt(X, currentY, normalText);
            }
        }

        public override bool HandleInput(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.UpArrow:
                    if (filteredItems.Count > 0)
                    {
                        selectedIndex = selectedIndex > 0 ? selectedIndex - 1 : filteredItems.Count - 1;
                        UpdateScrollOffset();
                    }
                    return true;

                case ConsoleKey.DownArrow:
                    if (filteredItems.Count > 0)
                    {
                        selectedIndex = selectedIndex < filteredItems.Count - 1 ? selectedIndex + 1 : 0;
                        UpdateScrollOffset();
                    }
                    return true;

                case ConsoleKey.Enter:
                    if (filteredItems.Count > 0 && selectedIndex < filteredItems.Count)
                    {
                        selectedItem = filteredItems[selectedIndex];
                        isCompleted = true;
                    }
                    return true;

                case ConsoleKey.Escape:
                    selectedItem = null;
                    isCompleted = true;
                    return true;

                case ConsoleKey.Backspace:
                    if (filterText.Length > 0)
                    {
                        filterText = filterText.Substring(0, filterText.Length - 1);
                        RefreshFilteredItems();
                        scrollOffset = 0;
                        UpdateScrollOffset();
                    }
                    return true;

                default:
                    if (char.IsLetterOrDigit(keyInfo.KeyChar) || char.IsPunctuation(keyInfo.KeyChar) || keyInfo.KeyChar == ' ')
                    {
                        filterText += keyInfo.KeyChar;
                        RefreshFilteredItems();
                        scrollOffset = 0;
                        UpdateScrollOffset();
                    }
                    return true;
            }
        }

        private void RefreshFilteredItems()
        {
            if (string.IsNullOrEmpty(filterText))
            {
                filteredItems = new List<AutocompleteMenuItem>(allItems);
            }
            else
            {
                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                filteredItems = allItems
                    .Where(item => item.DisplayText.Contains(filterText, comparison) ||
                                  item.Value.Contains(filterText, comparison))
                    .ToList();
            }

            // Ensure selected index is valid
            if (selectedIndex >= filteredItems.Count)
            {
                selectedIndex = Math.Max(0, filteredItems.Count - 1);
            }
        }

        private void UpdateScrollOffset()
        {
            if (filteredItems.Count <= maxDisplayItems)
            {
                scrollOffset = 0;
                return;
            }

            // Adjust scroll offset to keep selected item visible
            if (selectedIndex < scrollOffset)
            {
                scrollOffset = selectedIndex;
            }
            else if (selectedIndex >= scrollOffset + maxDisplayItems)
            {
                scrollOffset = selectedIndex - maxDisplayItems + 1;
            }
        }
    }

    public class AutocompleteMenuItem
    {
        public string DisplayText { get; set; } = "";
        public string Value { get; set; } = "";
        public object? Data { get; set; }

        public AutocompleteMenuItem(string displayText, string value, object? data = null)
        {
            DisplayText = displayText;
            Value = value;
            Data = data;
        }
    }

}
using System;
using System.Collections.Generic;
using System.Linq;

namespace peglin_save_explorer
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

            // Calculate display area based on terminal size
            this.maxDisplayItems = Math.Max(5, Math.Min(15, terminal.Height - 15)); // Leave room for header and other UI
            this.Height = 3 + maxDisplayItems + 1; // prompt + instructions + empty + items + status
        }

        public override void OnResize()
        {
            if (Terminal != null)
            {
                // Recalculate display area based on new terminal size
                this.maxDisplayItems = Math.Max(5, Math.Min(15, Terminal.Height - 15)); // Leave room for header and other UI
                this.Height = 3 + maxDisplayItems + 1; // prompt + instructions + empty + items + status

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
                        // Explicitly clear the remainder of the line to ensure no background artifacts
                        var lineWidth = Math.Min(Terminal.Width - X, 80); // Reasonable max width
                        var padding = Math.Max(0, lineWidth - normalText.Length);
                        if (padding > 0)
                        {
                            var paddingText = new FormattedString(new string(' ', padding), TextFormat.Default);
                            Terminal.WriteAt(X + normalText.Length, currentY, paddingText);
                        }
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
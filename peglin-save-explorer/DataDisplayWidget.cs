using System;
using System.Collections.Generic;
using System.Linq;

namespace peglin_save_explorer
{
    public class DataDisplayWidget : ConsoleWidget
    {
        private string title;
        private List<DataItem> items;
        private bool showBorder;
        private int scrollOffset;
        private int maxDisplayItems;

        public DataDisplayWidget(string title, bool showBorder = true)
        {
            this.title = title ?? "";
            this.items = new List<DataItem>();
            this.showBorder = showBorder;
            this.scrollOffset = 0;
        }

        public override void SetTerminal(TerminalManager terminal)
        {
            base.SetTerminal(terminal);

            // Calculate display area based on terminal size - more aggressive for small terminals
            this.maxDisplayItems = Math.Max(3, Math.Min(20, terminal.Height - 6));
            this.Height = maxDisplayItems + (showBorder ? 5 : 3); // border + title + content + status
        }

        public override void OnResize()
        {
            if (Terminal != null)
            {
                // Recalculate display area based on new terminal size
                this.maxDisplayItems = Math.Max(3, Math.Min(20, Terminal.Height - 6));
                this.Height = maxDisplayItems + (showBorder ? 5 : 3); // border + title + content + status

                // Adjust scroll offset if necessary
                var totalPages = (int)Math.Ceiling((double)items.Count / maxDisplayItems);
                var maxScrollOffset = Math.Max(0, totalPages - 1) * maxDisplayItems;
                if (scrollOffset > maxScrollOffset)
                {
                    scrollOffset = maxScrollOffset;
                }
            }
        }

        public void AddItem(string key, object value, bool isSection = false)
        {
            items.Add(new DataItem(key, value?.ToString() ?? "", isSection));
        }

        public void AddSection(string sectionName)
        {
            items.Add(new DataItem(sectionName, "", true));
        }

        public void AddEmptyLine()
        {
            items.Add(new DataItem("", "", false));
        }

        public void ClearItems()
        {
            items.Clear();
            scrollOffset = 0;
        }

        public override void Update()
        {
            // No continuous updates needed
        }

        public override void Render()
        {
            if (Terminal == null) return;

            int currentY = Y;

            // Render title with border
            if (showBorder && !string.IsNullOrEmpty(title))
            {
                var titleLength = Math.Min(title.Length, 58);
                var padding = Math.Max(0, (62 - titleLength) / 2);
                var paddedTitle = title.PadLeft(padding + titleLength).PadRight(62);

                Terminal.WriteAt(X, currentY++, new FormattedString("╔══════════════════════════════════════════════════════════════╗", TextFormat.Default));
                Terminal.WriteAt(X, currentY++, new FormattedString($"║{paddedTitle}║", TextFormat.Default));
                Terminal.WriteAt(X, currentY++, new FormattedString("╚══════════════════════════════════════════════════════════════╝", TextFormat.Default));
            }
            else if (!string.IsNullOrEmpty(title))
            {
                Terminal.WriteAt(X, currentY++, new FormattedString(title, TextFormat.Highlighted));
            }

            currentY++; // Empty line after title

            // Calculate viewport
            var displayCount = Math.Min(maxDisplayItems, items.Count);
            var endIndex = Math.Min(scrollOffset + displayCount, items.Count);

            // Render data items
            if (items.Count == 0)
            {
                Terminal.WriteAt(X, currentY++, new FormattedString("No data available.", TextFormat.Default));
            }
            else
            {
                for (int i = scrollOffset; i < endIndex; i++)
                {
                    var item = items[i];

                    if (item.IsSection)
                    {
                        // Render section header
                        Terminal.WriteAt(X, currentY++, new FormattedString(item.Key, TextFormat.Highlighted));
                    }
                    else if (string.IsNullOrEmpty(item.Key) && string.IsNullOrEmpty(item.Value))
                    {
                        // Empty line
                        currentY++;
                    }
                    else if (string.IsNullOrEmpty(item.Value))
                    {
                        // Key only (like a simple text line)
                        Terminal.WriteAt(X, currentY++, new FormattedString(item.Key, TextFormat.Default));
                    }
                    else
                    {
                        // Key-value pair
                        var keyPart = new FormattedString($"{item.Key}: ", TextFormat.Default);
                        var valuePart = new FormattedString(item.Value, TextFormat.Default);

                        Terminal.WriteAt(X, currentY, keyPart);
                        Terminal.WriteAt(X + keyPart.Length, currentY, valuePart);
                        currentY++;
                    }
                }
            }

            // Fill remaining lines
            var displayedItems = endIndex - scrollOffset;
            for (int i = displayedItems; i < maxDisplayItems; i++)
            {
                currentY++;
            }

            // Render status
            if (items.Count > 0)
            {
                string statusText;
                if (items.Count > maxDisplayItems)
                {
                    statusText = $"Showing items {scrollOffset + 1}-{endIndex} of {items.Count} (↑↓ to scroll, Enter/Esc to close)";
                }
                else
                {
                    statusText = $"Showing {items.Count} items (Enter/Esc to close)";
                }

                if (HasFocus)
                {
                    statusText += " [FOCUSED]";
                    Terminal.WriteAt(X, currentY, new FormattedString(statusText, TextFormat.Highlighted));
                }
                else
                {
                    Terminal.WriteAt(X, currentY, new FormattedString(statusText, TextFormat.Default));
                }
            }
        }

        public override bool HandleInput(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.UpArrow:
                    if (scrollOffset > 0)
                    {
                        scrollOffset--;
                    }
                    return true;

                case ConsoleKey.DownArrow:
                    if (scrollOffset + maxDisplayItems < items.Count)
                    {
                        scrollOffset++;
                    }
                    return true;

                case ConsoleKey.PageUp:
                    scrollOffset = Math.Max(0, scrollOffset - maxDisplayItems);
                    return true;

                case ConsoleKey.PageDown:
                    scrollOffset = Math.Min(Math.Max(0, items.Count - maxDisplayItems), scrollOffset + maxDisplayItems);
                    return true;

                case ConsoleKey.Home:
                    scrollOffset = 0;
                    return true;

                case ConsoleKey.End:
                    scrollOffset = Math.Max(0, items.Count - maxDisplayItems);
                    return true;

                case ConsoleKey.Enter:
                case ConsoleKey.Escape:
                    // Signal completion
                    return false;

                default:
                    return false;
            }
        }

        private class DataItem
        {
            public string Key { get; set; }
            public string Value { get; set; }
            public bool IsSection { get; set; }

            public DataItem(string key, string value, bool isSection)
            {
                Key = key;
                Value = value;
                IsSection = isSection;
            }
        }
    }
}
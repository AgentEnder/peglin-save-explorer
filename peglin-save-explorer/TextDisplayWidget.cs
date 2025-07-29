using System;
using System.Collections.Generic;

namespace peglin_save_explorer
{
    public class TextDisplayWidget : ConsoleWidget
    {
        private List<string> lines;
        private string title;
        private bool showBorder;
        private int scrollOffset;
        private int maxDisplayLines;
        private bool showScrollIndicator;

        public TextDisplayWidget(string title, List<string> lines, bool showBorder = true)
        {
            this.title = title ?? "";
            this.lines = lines ?? new List<string>();
            this.showBorder = showBorder;
            this.scrollOffset = 0;
            this.showScrollIndicator = true;
        }

        public TextDisplayWidget(string title, string content, bool showBorder = true)
        {
            this.title = title ?? "";
            this.lines = new List<string>();
            this.showBorder = showBorder;
            this.scrollOffset = 0;
            this.showScrollIndicator = true;

            if (!string.IsNullOrEmpty(content))
            {
                this.lines.AddRange(content.Split('\n'));
            }
        }

        public override void SetTerminal(TerminalManager terminal)
        {
            base.SetTerminal(terminal);

            // Calculate display area based on terminal size - more aggressive for small terminals
            this.maxDisplayLines = Math.Max(3, Math.Min(25, terminal.Height - 6)); // Leave room for border and footer
            this.Height = maxDisplayLines + (showBorder ? 4 : 2); // border + title + content + status
        }

        public override void OnResize()
        {
            if (Terminal != null)
            {
                // Recalculate display area based on new terminal size
                this.maxDisplayLines = Math.Max(3, Math.Min(25, Terminal.Height - 6)); // Leave room for border and footer
                this.Height = maxDisplayLines + (showBorder ? 4 : 2); // border + title + content + status

                // Adjust scroll offset if necessary
                var maxScrollOffset = Math.Max(0, lines.Count - maxDisplayLines);
                if (scrollOffset > maxScrollOffset)
                {
                    scrollOffset = maxScrollOffset;
                }
            }
        }

        public void AddLine(string line)
        {
            lines.Add(line ?? "");
        }

        public void AddLines(IEnumerable<string> newLines)
        {
            lines.AddRange(newLines ?? new List<string>());
        }

        public void ClearLines()
        {
            lines.Clear();
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
                var titleLength = Math.Min(title.Length, 58); // Fit within border
                var padding = Math.Max(0, (62 - titleLength) / 2); // Center title in border
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
            var displayCount = Math.Min(maxDisplayLines, lines.Count);
            var endIndex = Math.Min(scrollOffset + displayCount, lines.Count);

            // Render content lines
            if (lines.Count == 0)
            {
                Terminal.WriteAt(X, currentY++, new FormattedString("No data available.", TextFormat.Default));
            }
            else
            {
                for (int i = scrollOffset; i < endIndex; i++)
                {
                    var line = lines[i];
                    Terminal.WriteAt(X, currentY++, new FormattedString(line, TextFormat.Default));
                }
            }

            // Fill remaining lines
            var displayedLines = endIndex - scrollOffset;
            for (int i = displayedLines; i < maxDisplayLines; i++)
            {
                currentY++;
            }

            // Render scroll indicator and status
            if (showScrollIndicator && lines.Count > 0)
            {
                string statusText;
                if (lines.Count > maxDisplayLines)
                {
                    statusText = $"Showing lines {scrollOffset + 1}-{endIndex} of {lines.Count} (↑↓ to scroll)";
                }
                else
                {
                    statusText = $"Showing {lines.Count} lines";
                }

                Terminal.WriteAt(X, currentY, new FormattedString(statusText, TextFormat.Default));
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
                    if (scrollOffset + maxDisplayLines < lines.Count)
                    {
                        scrollOffset++;
                    }
                    return true;

                case ConsoleKey.PageUp:
                    scrollOffset = Math.Max(0, scrollOffset - maxDisplayLines);
                    return true;

                case ConsoleKey.PageDown:
                    scrollOffset = Math.Min(Math.Max(0, lines.Count - maxDisplayLines), scrollOffset + maxDisplayLines);
                    return true;

                case ConsoleKey.Home:
                    scrollOffset = 0;
                    return true;

                case ConsoleKey.End:
                    scrollOffset = Math.Max(0, lines.Count - maxDisplayLines);
                    return true;

                case ConsoleKey.Enter:
                case ConsoleKey.Escape:
                    // Signal completion by returning false (let parent handle)
                    return false;

                default:
                    return false;
            }
        }
    }
}
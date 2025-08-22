using System;
using System.Text;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.UI
{
    public class TerminalManager
    {
        private FormattedChar[,] frontBuffer;
        private FormattedChar[,] backBuffer;
        private int width;
        private int height;
        private bool inAltScreen = false;

        public int Width => width;
        public int Height => height;

        public TerminalManager()
        {
            try
            {
                InitializeBuffers();
                EnterAltScreen();
            }
            catch (Exception ex)
            {
                // If terminal initialization fails, throw with more context
                throw new InvalidOperationException($"Failed to initialize terminal manager: {ex.Message}", ex);
            }
        }

        private void InitializeBuffers()
        {
            try
            {
                width = Console.WindowWidth;
                height = Console.WindowHeight;
            }
            catch (IOException)
            {
                // Fallback for environments without proper console (like Git Bash)
                width = 80;
                height = 25;
            }

            frontBuffer = new FormattedChar[height, width];
            backBuffer = new FormattedChar[height, width];

            // Initialize with spaces and default formatting
            var defaultChar = new FormattedChar(' ', TextFormat.Default);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    frontBuffer[y, x] = defaultChar;
                    backBuffer[y, x] = defaultChar;
                }
            }
        }

        public void EnterAltScreen()
        {
            if (!inAltScreen)
            {
                Console.Write("\x1b[?1049h"); // Enter alt screen
                Console.Write("\x1b[2J");     // Clear screen
                Console.Write("\x1b[H");      // Move cursor to home
                Console.CursorVisible = false;
                inAltScreen = true;
            }
        }

        public void ExitAltScreen()
        {
            if (inAltScreen)
            {
                Console.Write("\x1b[?1049l"); // Exit alt screen
                Console.CursorVisible = true;
                inAltScreen = false;
            }
        }

        public void SetCursorVisible(bool visible)
        {
            try
            {
                Console.CursorVisible = visible;
                // Force a flush to ensure the change takes effect immediately
                Console.Out.Flush();
            }
            catch
            {
                // Ignore errors if cursor visibility can't be set in this environment
            }
        }

        public void WriteAt(int x, int y, string text, ConsoleColor? fgColor = null, ConsoleColor? bgColor = null)
        {
            if (y < 0 || y >= height) return;

            var format = new TextFormat(fgColor ?? ConsoleColor.Gray, bgColor);

            for (int i = 0; i < text.Length && x + i < width; i++)
            {
                if (x + i >= 0)
                {
                    backBuffer[y, x + i] = new FormattedChar(text[i], format);
                }
            }
        }

        public void WriteAt(int x, int y, char ch, ConsoleColor? fgColor = null, ConsoleColor? bgColor = null)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                var format = new TextFormat(fgColor ?? ConsoleColor.Gray, bgColor);
                backBuffer[y, x] = new FormattedChar(ch, format);
            }
        }

        public void WriteAt(int x, int y, FormattedString formattedText)
        {
            if (y < 0 || y >= height) return;

            for (int i = 0; i < formattedText.Length && x + i < width; i++)
            {
                if (x + i >= 0)
                {
                    backBuffer[y, x + i] = formattedText[i];
                }
            }
        }

        public void WriteAt(int x, int y, FormattedChar formattedChar)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                backBuffer[y, x] = formattedChar;
            }
        }

        public void ClearBackBuffer()
        {
            var clearChar = new FormattedChar(' ', TextFormat.Default);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    backBuffer[y, x] = clearChar;
                }
            }
        }

        public void Present()
        {
            var output = new StringBuilder();
            var currentFgColor = ConsoleColor.Gray;
            var currentBgColor = (ConsoleColor?)null;

            for (int y = 0; y < height; y++)
            {
                bool lineHasChanges = false;
                int firstChangeX = width;
                int lastChangeX = -1;

                // Find the range of changes in this line
                for (int x = 0; x < width; x++)
                {
                    if (!backBuffer[y, x].Character.Equals(frontBuffer[y, x].Character) ||
                        !backBuffer[y, x].Format.Equals(frontBuffer[y, x].Format))
                    {
                        lineHasChanges = true;
                        firstChangeX = Math.Min(firstChangeX, x);
                        lastChangeX = Math.Max(lastChangeX, x);
                    }
                }

                // If there are changes in this line, update only the changed region
                if (lineHasChanges)
                {
                    output.Append($"\x1b[{y + 1};{firstChangeX + 1}H"); // Move cursor

                    for (int x = firstChangeX; x <= lastChangeX; x++)
                    {
                        var newFormat = backBuffer[y, x].Format;

                        // Handle foreground color changes
                        if (newFormat.ForegroundColor != currentFgColor)
                        {
                            output.Append($"\x1b[3{(int)newFormat.ForegroundColor}m");
                            currentFgColor = newFormat.ForegroundColor;
                        }

                        // Handle background color changes
                        if (newFormat.BackgroundColor != currentBgColor)
                        {
                            if (newFormat.BackgroundColor.HasValue)
                            {
                                output.Append($"\x1b[4{(int)newFormat.BackgroundColor.Value}m");
                            }
                            else
                            {
                                // Reset to default background
                                output.Append("\x1b[49m");
                            }
                            currentBgColor = newFormat.BackgroundColor;
                        }

                        output.Append(backBuffer[y, x].Character);
                        frontBuffer[y, x] = backBuffer[y, x];
                    }
                }
            }

            Console.Write(output.ToString());

            // Ensure cursor stays hidden after presenting
            if (inAltScreen)
            {
                Console.CursorVisible = false;
            }
        }

        public void Dispose()
        {
            ExitAltScreen();
            // Reset console to known good state
            try
            {
                Console.ResetColor();
                Console.CursorVisible = true;
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        // Check if console size changed and reinitialize if needed
        public bool CheckResize()
        {
            try
            {
                if (Console.WindowWidth != width || Console.WindowHeight != height)
                {
                    // Clear the screen before reinitializing buffers
                    Console.Write("\x1b[2J");     // Clear entire screen
                    Console.Write("\x1b[H");      // Move cursor to home

                    InitializeBuffers();
                    return true;
                }
            }
            catch (IOException)
            {
                // Can't check resize in environments without proper console
                return false;
            }
            return false;
        }
    }
}
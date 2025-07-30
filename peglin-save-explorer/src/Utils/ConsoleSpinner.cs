using System;
using System.Threading;

namespace peglin_save_explorer.Utils
{
    public class ConsoleSpinner
    {
        private static readonly string[] SpinnerChars = { "|", "/", "-", "\\" };
        private int _currentSpinnerIndex = 0;
        private string _currentMessage = "";
        private bool _isRunning = false;
        private Timer? _timer;
        private readonly object _lock = new object();
        private int _lastMessageLines = 0;

        public void Start(string message = "Processing...")
        {
            lock (_lock)
            {
                if (_isRunning) return;

                // Add a blank line for cleaner output
                Console.WriteLine();

                _currentMessage = message;
                _isRunning = true;
                _currentSpinnerIndex = 0;
                _lastMessageLines = 0;

                _timer = new Timer(Spin, null, 0, 100); // Update every 100ms
            }
        }

        public void Update(string message)
        {
            lock (_lock)
            {
                // Truncate long messages to prevent wrapping
                var maxWidth = Math.Max(80, Console.BufferWidth - 10); // Reserve space for spinner
                if (message.Length > maxWidth)
                {
                    message = message.Substring(0, maxWidth - 3) + "...";
                }
                _currentMessage = message;
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning) return;

                _isRunning = false;
                _timer?.Dispose();
                _timer = null;

                // Clear all lines that might have been used
                ClearLines(_lastMessageLines + 1); // +1 for safety
                _lastMessageLines = 0;

                // Add a blank line for clean separation from subsequent output
                Console.WriteLine();
            }
        }

        private void Spin(object? state)
        {
            lock (_lock)
            {
                if (!_isRunning) return;

                var spinner = SpinnerChars[_currentSpinnerIndex];
                _currentSpinnerIndex = (_currentSpinnerIndex + 1) % SpinnerChars.Length;

                var fullMessage = $"{spinner} {_currentMessage}";

                // Calculate how many lines this message will take
                var consoleWidth = Math.Max(1, Console.BufferWidth);
                var newLines = (fullMessage.Length / consoleWidth) + 1;

                // Clear previous lines if we had any
                if (_lastMessageLines > 0)
                {
                    ClearLines(_lastMessageLines);
                }

                // Write the new message
                Console.Write($"\r{fullMessage}");
                _lastMessageLines = newLines;
            }
        }

        private void ClearLines(int lineCount)
        {
            try
            {
                for (int i = 0; i < lineCount; i++)
                {
                    Console.Write("\r" + new string(' ', Math.Max(1, Console.BufferWidth - 1)));
                    if (i < lineCount - 1)
                    {
                        Console.Write("\x1b[1A"); // Move cursor up one line
                    }
                }
                Console.Write("\r");
            }
            catch
            {
                // Ignore errors during cleanup - terminal might not support ANSI codes
                Console.Write("\r" + new string(' ', Math.Max(1, Console.BufferWidth - 1)) + "\r");
            }
        }
    }
}

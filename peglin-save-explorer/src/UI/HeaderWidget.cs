using System;

namespace peglin_save_explorer.UI
{
    public class HeaderWidget : ConsoleWidget
    {
        private string fileName;
        private DateTime loadTime;

        public HeaderWidget(string fileName, DateTime loadTime)
        {
            this.fileName = fileName;
            this.loadTime = loadTime;
            this.Height = 6; // Header box + filename + loaded time + separator
            this.Width = Console.WindowWidth;
        }

        public override void OnResize()
        {
            if (Terminal != null)
            {
                this.Width = Terminal.Width;
            }
        }

        public override void Update()
        {
            // Static header doesn't need updates
        }

        public override void Render()
        {
            if (Terminal == null) return;

            Terminal.WriteAt(X, Y + 0, "╔══════════════════════════════════════════════════════════════╗");
            Terminal.WriteAt(X, Y + 1, "║                  Peglin Save Explorer                        ║");
            Terminal.WriteAt(X, Y + 2, "╚══════════════════════════════════════════════════════════════╝");
            Terminal.WriteAt(X, Y + 4, $"File: {fileName}");
            Terminal.WriteAt(X, Y + 5, $"Loaded: {loadTime:yyyy-MM-dd HH:mm:ss}");
            Terminal.WriteAt(X, Y + 7, "──────────────────────────────────────────────────────────────");
        }

        public override bool HandleInput(ConsoleKeyInfo keyInfo)
        {
            // Header doesn't handle input
            return false;
        }
    }
}
using System;
using System.Collections.Generic;

namespace peglin_save_explorer
{
    public abstract class ConsoleWidget
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool HasFocus { get; set; } = false;
        protected TerminalManager? Terminal { get; private set; }

        public virtual void SetTerminal(TerminalManager terminal)
        {
            this.Terminal = terminal;
        }

        public abstract void Render();
        public abstract bool HandleInput(ConsoleKeyInfo keyInfo);
        public abstract void Update();

        /// <summary>
        /// Called when the terminal is resized. Widgets should recalculate their dimensions.
        /// </summary>
        public virtual void OnResize()
        {
            // Default implementation does nothing - widgets can override if they need to handle resize
        }
    }

    public class WidgetManager : IDisposable
    {
        private List<ConsoleWidget> widgets = new List<ConsoleWidget>();
        private int focusedWidgetIndex = -1;
        private bool shouldExit = false;
        private TerminalManager terminal;

        public WidgetManager()
        {
            terminal = new TerminalManager();
        }

        public TerminalManager Terminal => terminal;

        public void AddWidget(ConsoleWidget widget)
        {
            widget.SetTerminal(terminal);
            widgets.Add(widget);

            // Only interactive widgets can receive focus (not HeaderWidget)
            if (widget is not HeaderWidget)
            {
                // Clear focus from all widgets first
                foreach (var w in widgets)
                {
                    w.HasFocus = false;
                }

                // Set focus to the new interactive widget
                focusedWidgetIndex = widgets.Count - 1;
                widget.HasFocus = true;
            }
            else if (focusedWidgetIndex == -1)
            {
                // If this is a header widget and we have no focus yet, 
                // keep focusedWidgetIndex as -1 to wait for an interactive widget
                focusedWidgetIndex = -1;
            }
        }

        public void RemoveWidget(ConsoleWidget widget)
        {
            var index = widgets.IndexOf(widget);
            if (index >= 0)
            {
                widgets.RemoveAt(index);
                if (focusedWidgetIndex == index)
                {
                    focusedWidgetIndex = widgets.Count > 0 ? 0 : -1;
                    if (focusedWidgetIndex >= 0)
                    {
                        widgets[focusedWidgetIndex].HasFocus = true;
                    }
                }
                else if (focusedWidgetIndex > index)
                {
                    focusedWidgetIndex--;
                }
            }
        }

        public void SetFocus(ConsoleWidget widget)
        {
            // Clear focus from all widgets
            foreach (var w in widgets)
            {
                w.HasFocus = false;
            }

            // Set focus to the specified widget
            var index = widgets.IndexOf(widget);
            if (index >= 0)
            {
                focusedWidgetIndex = index;
                widget.HasFocus = true;
            }
        }

        public void Run()
        {
            // Verify input system is working before starting main loop
            if (!TestInputSystem())
            {
                Console.WriteLine("ERROR: Input system is not working properly.");
                Console.WriteLine("This can happen in environments where Console.KeyAvailable is not supported.");
                Console.WriteLine("The application cannot function without proper input handling.");
                Console.WriteLine("Please try running in a different terminal or environment.");
                return;
            }


            while (!shouldExit)
            {
                // Check for console resize
                if (terminal.CheckResize())
                {
                    // Handle resize - notify all widgets to recalculate their dimensions
                    foreach (var widget in widgets)
                    {
                        widget.OnResize();
                    }
                }

                // Clear back buffer
                terminal.ClearBackBuffer();

                // Update all widgets
                foreach (var widget in widgets)
                {
                    if (widget.IsVisible)
                    {
                        widget.Update();
                    }
                }

                // Check if any AutocompleteWidget is completed
                foreach (var widget in widgets)
                {
                    if (widget is AutocompleteWidget autocomplete && autocomplete.IsCompleted())
                    {
                        shouldExit = true;
                        break;
                    }
                }

                if (shouldExit) break;

                // Render all widgets to back buffer
                foreach (var widget in widgets)
                {
                    if (widget.IsVisible)
                    {
                        widget.Render();
                    }
                }

                // Present the back buffer (only updates changed areas)
                terminal.Present();

                // Handle input with improved approach for alt screen mode
                try
                {
                    // Check for available input
                    if (Console.KeyAvailable)
                    {
                        var keyInfo = Console.ReadKey(true);

                        // Handle global ESC key to exit
                        if (keyInfo.Key == ConsoleKey.Escape)
                        {
                            shouldExit = true;
                            break;
                        }

                        // Pass input to focused widget
                        if (focusedWidgetIndex >= 0 && focusedWidgetIndex < widgets.Count)
                        {
                            var focusedWidget = widgets[focusedWidgetIndex];
                            var handled = focusedWidget.HandleInput(keyInfo);
                            if (!handled)
                            {
                                // Handle global navigation between widgets if needed
                                // For now, just ignore unhandled input
                            }
                        }
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Input not available in this environment
                    // Log error and exit since the app can't function
                    terminal.WriteAt(0, terminal.Height - 1, $"INPUT ERROR: {ex.Message}");
                    terminal.Present();
                    System.Threading.Thread.Sleep(2000); // Give user time to see error
                    shouldExit = true;
                    break;
                }
                catch (Exception ex)
                {
                    // Unexpected input error
                    terminal.WriteAt(0, terminal.Height - 1, $"UNEXPECTED INPUT ERROR: {ex.Message}");
                    terminal.Present();
                    System.Threading.Thread.Sleep(2000);
                    shouldExit = true;
                    break;
                }

                // Small delay to prevent excessive CPU usage
                System.Threading.Thread.Sleep(16); // ~60 FPS
            }
        }

        public void Exit()
        {
            shouldExit = true;
        }

        private bool TestInputSystem()
        {
            try
            {
                // Test if we can check for available input
                var available = Console.KeyAvailable;

                // Test if we can get basic console properties
                var width = Console.WindowWidth;
                var height = Console.WindowHeight;

                // Test if we're in a redirected environment
                if (Console.IsInputRedirected)
                {
                    Console.WriteLine("WARNING: Input is redirected. Interactive features may not work properly.");
                    return false;
                }

                return true;
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("ERROR: Console input operations are not supported in this environment.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Unexpected error testing input system: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            // Ensure we exit alt screen mode before disposing
            if (terminal != null)
            {
                terminal.ExitAltScreen();
                terminal.Dispose();
            }
        }
    }
}
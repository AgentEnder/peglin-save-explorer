namespace peglin_save_explorer.Utils
{
    public static class ConsoleUtility
    {
        public static bool SuppressConsoleOutput { get; set; } = false;
        
        public static void SetConsoleOutputSuppression(bool suppress)
        {
            SuppressConsoleOutput = suppress;
        }

        public static void WriteToConsole(string message)
        {
            if (!SuppressConsoleOutput)
            {
                Console.WriteLine(message);
            }
        }
    }
}
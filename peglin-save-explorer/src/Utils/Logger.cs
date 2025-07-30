using System;

namespace peglin_save_explorer.Utils
{
    public enum LogLevel
    {
        Error = 0,
        Warning = 1,
        Info = 2,
        Debug = 3,
        Verbose = 4
    }

    public static class Logger
    {
        private static LogLevel _currentLevel = LogLevel.Info;

        public static void SetLogLevel(LogLevel level)
        {
            _currentLevel = level;
            
            // Also set the log level for OdinSerializer stubs if available
            try
            {
                var stubsAssembly = System.Reflection.Assembly.GetAssembly(typeof(OdinSerializer.SerializationUtility));
                if (stubsAssembly != null)
                {
                    var simpleLoggerType = stubsAssembly.GetType("StandaloneLogging.SimpleLogger");
                    if (simpleLoggerType != null)
                    {
                        var setLogLevelMethod = simpleLoggerType.GetMethod("SetLogLevel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (setLogLevelMethod != null)
                        {
                            // Convert our LogLevel to SimpleLogger's LogLevel (they have the same values)
                            var simpleLogLevel = (int)level;
                            setLogLevelMethod.Invoke(null, new object[] { simpleLogLevel });
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors - SimpleLogger might not be available in all builds
            }
        }

        public static void Error(string message)
        {
            Log(LogLevel.Error, message);
        }

        public static void Warning(string message)
        {
            Log(LogLevel.Warning, message);
        }

        public static void Info(string message)
        {
            Log(LogLevel.Info, message);
        }

        public static void Debug(string message)
        {
            Log(LogLevel.Debug, message);
        }

        public static void Verbose(string message)
        {
            Log(LogLevel.Verbose, message);
        }

        private static void Log(LogLevel level, string message)
        {
            if (level <= _currentLevel)
            {
                var prefix = level switch
                {
                    LogLevel.Error => "[ERROR] ",
                    LogLevel.Warning => "[WARN] ",
                    LogLevel.Info => "",
                    LogLevel.Debug => "[DEBUG] ",
                    LogLevel.Verbose => "[VERBOSE] ",
                    _ => ""
                };

                Console.WriteLine($"{prefix}{message}");
            }
        }
    }
}
namespace IngameCoding
{
    namespace Debug
    {
        internal static class Debug
        {
            internal static void Log(string msg) => System.Diagnostics.Debug.WriteLine(msg);
            internal static void LogError(Errors.Exception error) => System.Diagnostics.Debug.WriteLine(error);
            internal static void LogWarning(string msg) => System.Diagnostics.Debug.WriteLine(msg);
        }
    }

    namespace Terminal
    {
        internal static class Output
        {
            internal static void LogDebug(string msg)
            {
                LogColor(msg, ConsoleColor.DarkGray);
            }

            internal static void Log(string msg)
            {
                Console.WriteLine(msg);
            }

            internal static void LogError(string msg)
            {
                LogColor(msg, ConsoleColor.Red);
            }

            internal static void LogError(Errors.Exception error)
            {
                LogColor(error.MessageAll, ConsoleColor.Red);
            }

            internal static void LogError(System.Exception error)
            {
                LogColor(error.ToString(), ConsoleColor.Red);
            }

            internal static void LogWarning(string msg)
            {
                LogColor(msg, ConsoleColor.DarkYellow);
            }

            static void LogColor(string message, ConsoleColor color)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }
    }
}

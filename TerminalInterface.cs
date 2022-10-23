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
                LogColor(msg, System.ConsoleColor.DarkGray);
            }

            internal static void Log(string msg)
            {
                Console.WriteLine(msg);
            }

            internal static void LogError(string msg)
            {
                LogColor(msg, System.ConsoleColor.Red);
            }

            internal static void LogError(Errors.Exception error)
            {
                LogColor(error.MessageAll, System.ConsoleColor.Red);
            }

            internal static void LogError(System.Exception error)
            {
                LogColor(error.ToString(), System.ConsoleColor.Red);
            }

            internal static void LogWarning(string msg)
            {
                LogColor(msg, System.ConsoleColor.DarkYellow);
            }

            static void LogColor(string message, System.ConsoleColor color)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }
    }
}

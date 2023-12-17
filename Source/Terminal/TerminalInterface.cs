using System;

namespace LanguageCore
{
    public delegate void PrintCallback(string message, LogType logType);

    public static class Output
    {
        public static void LogDebug(string message) => LogColor(message, ConsoleColor.DarkGray);
        public static void Log(string message) => Console.WriteLine(message);
        public static void LogError(string message) => LogColor(message, ConsoleColor.Red);
        public static void LogWarning(string message) => LogColor(message, ConsoleColor.DarkYellow);

        static void LogColor(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public static void Write(string data) => Console.Out.Write(data);
        public static void WriteError(string data) => Console.Error.Write(data);
    }

    public enum LogType
    {
        /// <summary>
        /// Used by:
        /// <list type="bullet">
        /// <item>Tokenizer</item>
        /// <item>Parser</item>
        /// <item>Compiler</item>
        /// <item>Interpreter</item>
        /// </list>
        /// </summary>
        System,
        /// <summary>
        /// Used by:
        /// <list type="bullet">
        /// <item>The code</item>
        /// </list>
        /// </summary>
        Normal,
        /// <summary>
        /// Used by:
        /// <list type="bullet">
        /// <item>Tokenizer</item>
        /// <item>Parser</item>
        /// <item>Compiler</item>
        /// <item>Interpreter</item>
        /// <item>The code</item>
        /// </list>
        /// </summary>
        Warning,
        /// <summary>
        /// Used by:
        /// <list type="bullet">
        /// <item>Compiler</item>
        /// <item>Interpreter</item>
        /// <item>The code</item>
        /// </list>
        /// </summary>
        Error,
        /// <summary>
        /// Used by:
        /// <list type="bullet">
        /// <item>Tokenizer</item>
        /// <item>Parser</item>
        /// <item>Compiler</item>
        /// <item>Interpreter</item>
        /// </list>
        /// </summary>
        Debug,
    }
}

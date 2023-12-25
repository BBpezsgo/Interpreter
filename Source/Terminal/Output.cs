using System;
using TheProgram;

namespace LanguageCore
{
    public delegate void PrintCallback(string message, LogType logType);

    public static class Output
    {
        static ProgramArguments arguments;

        public static bool LogDebugs => arguments.LogDebugs;
        public static bool LogInfos => arguments.LogInfo;
        public static bool LogSystems => arguments.LogSystem;
        public static bool LogWarnings => arguments.LogWarnings;

        public static void SetProgramArguments(ProgramArguments arguments) => Output.arguments = arguments;

        public static void Log(string message, LogType logType)
        {
            switch (logType)
            {
                case LogType.System:
                    if (LogSystems) Console.WriteLine(message);
                    break;
                case LogType.Normal:
                    Output.LogInfo(message);
                    break;
                case LogType.Warning:
                    Output.LogWarning(message);
                    break;
                case LogType.Error:
                    Output.LogError(message);
                    break;
                case LogType.Debug:
                    Output.LogDebug(message);
                    break;
            }
        }

        public static void LogInfo(string message)
        { if (LogInfos) LogColor(message, ConsoleColor.Blue); }

        public static void LogError(string message)
        { LogColor(message, ConsoleColor.Red); }

        public static void LogWarning(string message)
        { if (LogWarnings) LogColor(message, ConsoleColor.DarkYellow); }

        public static void LogDebug(string message)
        { if (LogDebugs) LogColor(message, ConsoleColor.DarkGray); }

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

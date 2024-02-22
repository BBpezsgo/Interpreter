using System;
using System.Drawing;
using TheProgram;

namespace LanguageCore
{
    public delegate void PrintCallback(string message, LogType logType);

    public static class Output
    {
        static ProgramArguments arguments;

        public static bool LogDebugs => (arguments.LogFlags & LogType.Debug) != 0;
        public static bool LogInfos => (arguments.LogFlags & LogType.Normal) != 0;
        public static bool LogWarnings => (arguments.LogFlags & LogType.Warning) != 0;

        public static void SetProgramArguments(ProgramArguments arguments) => Output.arguments = arguments;

        public static void Log(string message, LogType logType)
        {
            switch (logType)
            {
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

        public static void LogInfo(Information information)
        {
            if (!LogInfos) return;

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(information.ToString());
            string? arrows = information.GetArrows();
            if (arrows != null)
            { Console.WriteLine(arrows); }
            Console.ResetColor();
        }

        public static void LogInfo(Hint hint)
        {
            if (!LogInfos) return;

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(hint.ToString());
            string? arrows = hint.GetArrows();
            if (arrows != null)
            { Console.WriteLine(arrows); }
            Console.ResetColor();
        }

        public static void LogError(string message)
        { LogColor(message, ConsoleColor.Red); }

        public static void LogError(LanguageException exception)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(exception.ToString());
            string? arrows = exception.GetArrows();
            if (arrows != null)
            { Console.WriteLine(arrows); }
            Console.ResetColor();
        }

        public static void LogError(Error error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error.ToString());
            string? arrows = error.GetArrows();
            if (arrows != null)
            { Console.WriteLine(arrows); }
            Console.ResetColor();
        }

        public static void LogError(Exception exception)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(exception.ToString());
            Console.ResetColor();
        }

        public static void LogWarning(string message)
        { if (LogWarnings) LogColor(message, ConsoleColor.DarkYellow); }

        public static void LogWarning(Warning warning)
        {
            if (LogWarnings)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine(warning.ToString());
                string? arrows = warning.GetArrows();
                if (arrows != null)
                { Console.WriteLine(arrows); }
                Console.ResetColor();
            }
        }

        public static void LogDebug(string message)
        { if (LogDebugs) LogColor(message, ConsoleColor.DarkGray); }

        static void LogColor(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public static void WriteLine() => Console.Out.WriteLine();
        public static void WriteLine(string data) => Console.Out.WriteLine(data);
        public static void Write(string data) => Console.Out.Write(data);
        public static void WriteError(string data) => Console.Error.Write(data);
    }

    [Flags]
    public enum LogType
    {
        Normal = 0b_00010,
        Warning = 0b_00100,
        Error = 0b_01000,
        Debug = 0b_10000,
    }
}

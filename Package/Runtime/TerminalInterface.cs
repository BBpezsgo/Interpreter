using System;
using System.Threading.Tasks;

namespace IngameCoding.Output
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

    internal static class Output
    {
        internal static void Debug(string msg)
        {
            LogColor(msg, ConsoleColor.DarkGray);
        }

        internal static void Value(string msg, object v)
        {
            Console.Write(msg);
            Console.Write(' ');
            ValuePart(v);
            Console.WriteLine();
        }

        static void ValuePart(int v)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(v);
            Console.ResetColor();
        }

        static void ValuePart(bool v)
        {
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.Write(v);
            Console.ResetColor();
        }

        static void ValuePart(string v)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"\"{v}\"");
            Console.ResetColor();
        }

        static void ValuePart(object v)
        {
            if (v is int vInt) ValuePart(vInt);
            else if (v is bool vBool) ValuePart(vBool);
            else if (v is string vString) ValuePart(vString);
            else Console.Write(v);
        }

        internal static void Log(string msg)
        {
            Console.WriteLine(msg);
        }

        internal static void Error(string error)
        {
            LogColor(error, ConsoleColor.Red);
        }

        internal static void Error(Errors.Exception error)
        {
            LogColor(error.MessageAll, ConsoleColor.Red);
        }

        internal static void Error(Errors.Error error)
        {
            LogColor(error.MessageAll, ConsoleColor.Red);
        }

        internal static void Error(Exception error)
        {
            LogColor(error.ToString(), ConsoleColor.Red);
        }

        internal static void Warning(string warning)
        {
            LogColor(warning, ConsoleColor.DarkYellow);
        }

        internal static void Warning(Errors.Warning warning)
        {
            LogColor(warning.MessageAll, ConsoleColor.DarkYellow);
        }

        static void LogColor(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        internal static async Task Write(string data)
        {
            await Console.Out.WriteAsync(data);
        }

        internal static async Task WriteError(string data)
        {
            await Console.Error.WriteAsync(data);
        }
    }

    public static class File
    {
        static readonly string Path = "C:\\Users\\bazsi\\.vscode\\extensions\\bbc\\out.log";

        public static void Log(string msg)
        {
            System.IO.File.AppendAllText(Path, msg + "\n");
        }
    }
}

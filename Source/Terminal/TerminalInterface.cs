using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProgrammingLanguage.Output
{
    public delegate void PrintCallback(string message, LogType logType);

    internal static class Debug
    {
        internal static void Log(string msg) => System.Diagnostics.Debug.WriteLine(msg);
        internal static void LogError(Errors.Exception error) => System.Diagnostics.Debug.WriteLine(error);
        internal static void LogError(System.Exception error) => System.Diagnostics.Debug.WriteLine(error);
        internal static void LogWarning(string msg) => System.Diagnostics.Debug.WriteLine(msg);
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
            LogColor(error.ToString(), ConsoleColor.Red);
        }

        internal static void Error(Errors.Error error)
        {
            LogColor(error.ToString(), ConsoleColor.Red);
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
            LogColor(warning.ToString(), ConsoleColor.DarkYellow);
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
        static readonly string Path = "C:\\Users\\bazsi\\Documents\\GitHub\\InterpreterVSCodeExtension\\out.log";

        public static void Write(string msg) => System.IO.File.AppendAllText(Path, msg);
        public static void WriteLine(string msg) => Write(msg + "\r\n");
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

namespace ProgrammingLanguage.TerminalInterface
{
    internal class TerminalInterface
    {
        string Input = string.Empty;
        int CurrentLineWidth = 0;
        bool ShouldClose = false;

        bool TryGetSuggestion(out string suggestion)
        {
            suggestion = null;
            if (Input.Length == 0) return false;

            List<string> suggestions = new();

            if (Input.StartsWith("run "))
            {

            }
            else
            {
                suggestions.Add("run");
            }

            foreach (string item in suggestions)
            {
                if (!item.StartsWith(Input)) continue;

                suggestion = item;
                return true;
            }

            return false;
        }

        void Tick()
        {
            char inp = Console.ReadKey().KeyChar;
            if (inp == '\r')
            {
                if (!string.IsNullOrEmpty(Input))
                {
                    Console.Write(" > ");
                    Console.WriteLine(Input);
                    ProcessInput(Input);
                    Console.Write(" > ");
                }
                Input = string.Empty;
                return;
            }

            string suggestion;

            ClearLastLine(CurrentLineWidth + 3);

            CurrentLineWidth = 0;

            if (inp == '\b')
            {
                if (Input.Length > 0) Input = Input[..^1];
            }
            else if (inp == '\t')
            {
                TryGetSuggestion(out suggestion);

                if (suggestion != null)
                {
                    Input = suggestion + " ";
                }
            }
            else if (char.IsControl(inp))
            { }
            else
            {
                Input += inp;
            }

            TryGetSuggestion(out suggestion);

            Console.Write(" > ");
            CurrentLineWidth += 3;

            Console.Write(Input);
            CurrentLineWidth += Input.Length;

            if (suggestion != null)
            {
                int cur = Console.CursorLeft;
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                Console.Write(suggestion[Input.Length..]);
                CurrentLineWidth += suggestion[Input.Length..].Length;

                Console.ResetColor();
                Console.CursorLeft = cur;
            }
        }

        void ProcessInput(string input)
        {
            Console.WriteLine($"Processing input \"{input}\"");
        }

        static void ClearLastLine() => ClearLastLine(Console.BufferWidth);
        static void ClearLastLine(int width)
        {
            Console.Write('\r');
            Console.Write(new string(' ', width));
            Console.Write('\r');
        }

        public static void Start()
        {
            TerminalInterface terminalInterface = new();
            Console.Write(" > ");
            while (!terminalInterface.ShouldClose) terminalInterface.Tick();
        }
    }
}
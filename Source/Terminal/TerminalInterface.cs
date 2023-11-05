using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace LanguageCore
{
    public delegate void PrintCallback(string message, LogType logType);

    internal static class Debug
    {
        public static void Log(string message) => System.Diagnostics.Debug.WriteLine(message);
        public static void LogError(string message) => System.Diagnostics.Debug.WriteLine(message);
        public static void LogError(object? message) => System.Diagnostics.Debug.WriteLine(message);
        public static void LogWarning(string message) => System.Diagnostics.Debug.WriteLine(message);
    }

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

        public static async Task Write(string data) => await Console.Out.WriteAsync(data);
        public static async Task WriteError(string data) => await Console.Error.WriteAsync(data);
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

namespace LanguageCore.TerminalInterface
{
    public class TerminalInterface
    {
        string Input = string.Empty;
        int CurrentLineWidth = 0;
        bool ShouldClose = false;

        bool TryGetSuggestion([NotNullWhen(true)] out string? suggestion)
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

            string? suggestion;

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
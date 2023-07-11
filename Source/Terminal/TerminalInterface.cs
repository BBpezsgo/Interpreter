using System;
using System.Threading.Tasks;

namespace ProgrammingLanguage.Output
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
        internal static TerminalInterface Instance;
        string currentInput = "";
        int currentLineWidth = 0;
        readonly string[] suggestions = new string[]
        {
            "hello",
            "bruh"
        };

        internal TerminalInterface()
        {
            Instance = this;
            while (true)
            {
                char inp = Console.ReadKey().KeyChar;
                if (inp == '\r')
                {
                    Console.WriteLine(currentInput);
                    ProcessInput(currentInput);
                    currentInput = "";
                }
                else
                {
                    ClearLastLine(currentLineWidth + 3);

                    currentLineWidth = currentInput.Length;
                    if (inp == '\b')
                    {
                        if (currentInput.Length > 0) currentInput = currentInput[..^1];
                    }
                    else if (inp == '\t')
                    {
                        if (currentInput.Length > 0) foreach (var item in suggestions)
                            {
                                if (item.StartsWith(currentInput))
                                {
                                    currentInput = item;
                                    currentLineWidth = currentInput.Length;
                                    break;
                                }
                            }
                    }
                    else
                    {
                        currentInput += inp;
                    }

                    if (currentInput.Length > 0) foreach (var item in suggestions)
                        {
                            if (item.StartsWith(currentInput))
                            {
                                currentLineWidth += item[currentInput.Length..].Length;
                                break;
                            }
                        }

                    Console.Write(currentInput);
                    if (currentInput.Length > 0) foreach (var item in suggestions)
                        {
                            if (item.StartsWith(currentInput))
                            {
                                Console.ForegroundColor = ConsoleColor.DarkCyan;
                                Console.Write(item[currentInput.Length..]);
                                Console.ResetColor();
                                break;
                            }
                        }
                }
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
            for (int i = width - 1; i >= 0; i--)
            { Console.Write(' '); }
            Console.Write('\r');
        }
    }
}
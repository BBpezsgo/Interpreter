using System;
using System.Linq;

namespace TheProgram
{
    using IngameCoding.BBCode;
    using IngameCoding.BBCode.Parser;
    using IngameCoding.Bytecode;
    using IngameCoding;
    using IngameCoding.Output.Terminal;
    using IngameCoding.BBCode.Compiler;

    internal static class ArgumentParser
    {
        static void ParseArgs(string[] args, out bool ThrowErrors, out bool LogDebugs, out bool LogSystem, out ParserSettings parserSettings, out Compiler.CompilerSettings compilerSettings, out BytecodeInterpreterSettings bytecodeInterpreterSettings)
        {
            ThrowErrors = false;
            LogDebugs = true;
            LogSystem = true;
            compilerSettings = Compiler.CompilerSettings.Default;
            parserSettings = ParserSettings.Default;
            bytecodeInterpreterSettings = BytecodeInterpreterSettings.Default;

            if (args.Length > 1)
            {
                int i = 0;
                while (i < args.Length - 1)
                {
                    if (args[i] == "-debug")
                    {
                        goto ArgParseDone;
                    }

                    if (args[i] == "-code-editor")
                    {
                        goto ArgParseDone;
                    }

                    if (args[i] == "-throw-errors")
                    {
                        ThrowErrors = true;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-hide-debug")
                    {
                        LogDebugs = false;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-hide-system")
                    {
                        LogSystem = false;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-dont-optimize")
                    {
                        compilerSettings.DontOptimize = true;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-c-generate-comments")
                    {
                        i++;
                        if (i >= args.Length - 1 || !bool.TryParse(args[i], out compilerSettings.GenerateComments))
                        { throw new ArgumentException("Expected bool value after argument '-c-generate-comments'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-c-remove-unused-functions")
                    {
                        i++;
                        if (i >= args.Length - 1 || !byte.TryParse(args[i], out compilerSettings.RemoveUnusedFunctionsMaxIterations))
                        { throw new ArgumentException("Expected byte value after argument '-c-remove-unused-functions'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-bc-clock")
                    {
                        i++;
                        if (i >= args.Length - 1 || !int.TryParse(args[i], out bytecodeInterpreterSettings.ClockCyclesPerUpdate))
                        { throw new ArgumentException("Expected int value after argument '-bc-clock'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-bc-instruction-limit")
                    {
                        i++;
                        if (i >= args.Length - 1 || !int.TryParse(args[i], out bytecodeInterpreterSettings.InstructionLimit))
                        { throw new ArgumentException("Expected int value after argument '-bc-instruction-limit'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-bc-stack-size")
                    {
                        i++;
                        if (i >= args.Length - 1 || !int.TryParse(args[i], out bytecodeInterpreterSettings.StackMaxSize))
                        { throw new ArgumentException("Expected int value after argument '-bc-stack-size'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-c-print-instructions")
                    {
                        i++;
                        if (i >= args.Length - 1 || !bool.TryParse(args[i], out compilerSettings.PrintInstructions))
                        { throw new ArgumentException("Expected bool value after argument '-c-print-instructions'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-p-print-info")
                    {
                        i++;
                        if (i >= args.Length - 1 || !bool.TryParse(args[i], out parserSettings.PrintInfo))
                        { throw new ArgumentException("Expected bool value after argument '-p-print-info'"); }
                        goto ArgParseDone;
                    }

                    throw new ArgumentException($"Unknown argument '{args[i]}'");

                ArgParseDone:
                    i++;
                }
            }
        }

        public struct Settings
        {
            public System.IO.FileInfo File;

            public ParserSettings parserSettings;
            public Compiler.CompilerSettings compilerSettings;
            public BytecodeInterpreterSettings bytecodeInterpreterSettings;
            public bool ThrowErrors;
            public bool HandleErrors => !ThrowErrors;
            public bool LogDebugs;
            public bool LogSystem;
            public bool Debug;
            public bool CodeEditor;
        }

        public static Settings? Parse(params string[] args)
        {
            if (args.Length == 0)
            {
                Output.LogError("Wrong number of arguments was passed!");
                return null;
            }

            ParserSettings parserSettings;
            Compiler.CompilerSettings compilerSettings;
            BytecodeInterpreterSettings bytecodeInterpreterSettings;
            bool ThrowErrors;
            bool LogDebugs;
            bool LogSystem;

            try
            {
                ParseArgs(args, out ThrowErrors, out LogDebugs, out LogSystem, out parserSettings, out compilerSettings, out bytecodeInterpreterSettings);
            }
            catch (ArgumentException error)
            {
                Output.LogError(error.Message);
                return null;
            }

            return new Settings()
            {
                parserSettings = parserSettings,
                compilerSettings = compilerSettings,
                bytecodeInterpreterSettings = bytecodeInterpreterSettings,
                ThrowErrors = ThrowErrors,
                LogDebugs = LogDebugs,
                LogSystem = LogSystem,
                File = new System.IO.FileInfo(args.Last()),
                Debug = args.Contains("-debug"),
                CodeEditor = args.Contains("-code-editor")
            };
        }
    }
}

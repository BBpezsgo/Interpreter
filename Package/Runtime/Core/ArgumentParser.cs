using System;
using System.Linq;

namespace TheProgram
{
    using IngameCoding.BBCode.Compiler;
    using IngameCoding.BBCode.Parser;
    using IngameCoding.Bytecode;
    using IngameCoding.Output;

    using System.Collections.Generic;

    internal static class ArgumentParser
    {
        public enum RunType
        {
            Normal,
            Debugger,
            Tester,
        }

        internal class ArgumentNormalizer
        {
            internal readonly List<string> Result;

            NormalizerState State;
            string CurrentArg;

            enum NormalizerState
            {
                None,
                String,
            }

            public ArgumentNormalizer()
            {
                Result = new();
                State = NormalizerState.None;
                CurrentArg = "";
            }

            void FinishArgument()
            {
                var currentArg = CurrentArg.Trim();

                State = NormalizerState.None;
                CurrentArg = "";

                if (string.IsNullOrEmpty(currentArg)) return;
                if (currentArg == "") return;
                if (currentArg.Length == 0) return;

                Result.Add(currentArg);
            }

            public void NormalizeArgs(params string[] args) => NormalizeArgs(string.Join(' ', args));
            public void NormalizeArgs(string args)
            {
                Result.Clear();
                State = NormalizerState.None;
                CurrentArg = "";

                for (int i = 0; i < args.Length; i++)
                {
                    char c = args[i];

                    switch (c)
                    {
                        case '"':
                            if (State == NormalizerState.String)
                            {
                                FinishArgument();
                                break;
                            }
                            State = NormalizerState.String;
                            break;
                        case '\t':
                        case ' ':
                            if (State == NormalizerState.String)
                            {
                                CurrentArg += c;
                                break;
                            }
                            FinishArgument();
                            break;
                        default:
                            CurrentArg += c;
                            break;
                    }
                }

                FinishArgument();
            }
        }

        /// <summary>
        /// Parses the passed arguments
        /// </summary>
        /// <param name="args">The passed arguments</param>
        /// <exception cref="ArgumentException"></exception>
        static void ParseArgs(string[] args, out bool ThrowErrors, out bool LogDebugs, out bool LogSystem, out RunType RunType, out ParserSettings parserSettings, out Compiler.CompilerSettings compilerSettings, out BytecodeInterpreterSettings bytecodeInterpreterSettings)
        {
            ThrowErrors = false;
            LogDebugs = true;
            LogSystem = true;
            RunType = RunType.Normal;
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
                        if (RunType != RunType.Normal) throw new ArgumentException(
                            $"The \"RunType\" is already defined ({RunType}), but you tried to set it to {RunType.Debugger}");
                        RunType = RunType.Debugger;
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

                    if (args[i] == "-test")
                    {
                        if (RunType != RunType.Normal) throw new ArgumentException(
                            $"The \"RunType\" is already defined ({RunType}), but you tried to set it to {RunType.Tester}");
                        RunType = RunType.Tester;
                        goto ArgParseDone;
                    }

                    throw new ArgumentException($"Unknown argument '{args[i]}'");

                ArgParseDone:
                    i++;
                }
            }
        }

        /// <summary>
        /// Argument parser result
        /// </summary>
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
            public RunType RunType;
        }

        /// <summary>
        /// Normalizes, parses and compiles the passed arguments
        /// </summary>
        /// <param name="args">The passed arguments</param>
        /// <returns><seealso cref="Settings"/>, or <c>null</c> on error</returns>
        public static Settings? Parse(params string[] args)
        {
            if (args.Length == 0)
            {
                Output.Error("No arguments passed!");
                return null;
            }

            ParserSettings parserSettings;
            Compiler.CompilerSettings compilerSettings;
            BytecodeInterpreterSettings bytecodeInterpreterSettings;
            bool ThrowErrors;
            bool LogDebugs;
            bool LogSystem;
            RunType RunType;

            ArgumentNormalizer normalizer = new();
            normalizer.NormalizeArgs(args);
            string[] normalizedArgs = normalizer.Result.ToArray();

            try
            {
                ParseArgs(normalizedArgs, out ThrowErrors, out LogDebugs, out LogSystem, out RunType, out parserSettings, out compilerSettings, out bytecodeInterpreterSettings);
            }
            catch (ArgumentException error)
            {
                Output.Error(error.Message);
                PrintArgs(normalizedArgs);
                return null;
            }

            if (!System.IO.File.Exists(normalizedArgs.Last()))
            {
                Output.Error($"File '{normalizedArgs.Last()}' not found!");
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
                RunType = RunType,
                File = new System.IO.FileInfo(normalizedArgs.Last()),
            };
        }

        public static void PrintArgs(params string[] args)
        {
            Console.Write("[ ");
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) Console.Write(", ");
                Console.Write($"'{args[i]}'");
            }
            Console.WriteLine(" ]");
        }
    }
}

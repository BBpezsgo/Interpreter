using System;
using System.Linq;

namespace TheProgram
{
    using ProgrammingLanguage.BBCode.Compiler;
    using ProgrammingLanguage.BBCode.Parser;
    using ProgrammingLanguage.Bytecode;
    using ProgrammingLanguage.Output;

    using System.Collections.Generic;
    using System.IO.Compression;

    internal static class ArgumentParser
    {
        public enum RunType
        {
            Normal,
            Debugger,
            Tester,
            Compile,
            Decompile,
            ConsoleGUI,
        }

        public enum FileType
        {
            Binary,
            Readable,
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
        static Settings ParseArgs(string[] args)
        {
            Settings result = Settings.Default();

            if (args.Length > 1)
            {
                int i = 0;
                while (i < args.Length - 1)
                {
                    if (args[i] == "-heap")
                    {
                        i++;
                        if (i >= args.Length - 1 || !int.TryParse(args[i], out result.bytecodeInterpreterSettings.HeapSize))
                        { throw new ArgumentException("Expected number value after argument '-heap'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-bf-no-cache")
                    {
                        if (result.RunType != RunType.Normal) throw new ArgumentException(
                            $"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {RunType.Debugger}");
                        result.compilerSettings.BuiltinFunctionCache = false;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-debug")
                    {
                        if (result.RunType != RunType.Normal) throw new ArgumentException(
                            $"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {RunType.Debugger}");
                        result.RunType = RunType.Debugger;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-console-gui")
                    {
                        if (result.RunType != RunType.Normal) throw new ArgumentException(
                            $"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {RunType.ConsoleGUI}");
                        result.RunType = RunType.ConsoleGUI;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-decompile")
                    {
                        if (result.RunType != RunType.Normal) throw new ArgumentException(
                            $"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {RunType.Decompile}");
                        result.RunType = RunType.Decompile;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-compile")
                    {
                        if (result.RunType != RunType.Normal) throw new ArgumentException(
                            $"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {RunType.Compile}");
                        result.RunType = RunType.Compile;

                        i++;
                        if (i >= args.Length - 1)
                        { throw new ArgumentException("Expected string value after argument '-compile'"); }

                        result.CompileOutput = args[i];

                        goto ArgParseDone;
                    }

                    if (args[i] == "-basepath")
                    {
                        i++;
                        if (i >= args.Length - 1)
                        { throw new ArgumentException("Expected string value after argument '-basepath'"); }

                        result.BasePath = args[i];

                        goto ArgParseDone;
                    }

                    if (args[i] == "-compression")
                    {
                        if (result.RunType != RunType.Normal && result.RunType != RunType.Compile)
                        { Output.Warning($"\"-compression\" argument is valid only in Compile mode"); }

                        i++;
                        if (i >= args.Length - 1)
                        { throw new ArgumentException("Expected string value after argument '-compression'"); }

                        result.CompressionLevel = args[i].ToLower() switch
                        {
                            "no" => CompressionLevel.NoCompression,
                            "none" => CompressionLevel.NoCompression,
                            "fast" => CompressionLevel.Fastest,
                            "fastest" => CompressionLevel.Fastest,
                            "optimal" => CompressionLevel.Optimal,
                            "smallest" => CompressionLevel.SmallestSize,
                            _ => throw new ArgumentException($"Unknown compression level '{args[i]}'"),
                        };

                        goto ArgParseDone;
                    }

                    if (args[i] == "-code-editor")
                    {
                        goto ArgParseDone;
                    }

                    if (args[i] == "-throw-errors")
                    {
                        result.ThrowErrors = true;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-hide-debug")
                    {
                        result.LogDebugs = false;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-hide-system")
                    {
                        result.LogSystem = false;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-dont-optimize")
                    {
                        result.compilerSettings.DontOptimize = true;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-c-generate-comments")
                    {
                        i++;
                        if (i >= args.Length - 1 || !bool.TryParse(args[i], out result.compilerSettings.GenerateComments))
                        { throw new ArgumentException("Expected bool value after argument '-c-generate-comments'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-no-debug-info")
                    {
                        result.compilerSettings.GenerateDebugInstructions = false;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-c-remove-unused-functions")
                    {
                        i++;
                        if (i >= args.Length - 1 || !byte.TryParse(args[i], out result.compilerSettings.RemoveUnusedFunctionsMaxIterations))
                        { throw new ArgumentException("Expected byte value after argument '-c-remove-unused-functions'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-bc-clock")
                    {
                        i++;
                        if (i >= args.Length - 1 || !int.TryParse(args[i], out result.bytecodeInterpreterSettings.ClockCyclesPerUpdate))
                        { throw new ArgumentException("Expected int value after argument '-bc-clock'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-bc-instruction-limit")
                    {
                        i++;
                        if (i >= args.Length - 1 || !int.TryParse(args[i], out result.bytecodeInterpreterSettings.InstructionLimit))
                        { throw new ArgumentException("Expected int value after argument '-bc-instruction-limit'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-bc-stack-size")
                    {
                        i++;
                        if (i >= args.Length - 1 || !int.TryParse(args[i], out result.bytecodeInterpreterSettings.StackMaxSize))
                        { throw new ArgumentException("Expected int value after argument '-bc-stack-size'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-c-print-instructions")
                    {
                        i++;
                        if (i >= args.Length - 1 || !bool.TryParse(args[i], out result.compilerSettings.PrintInstructions))
                        { throw new ArgumentException("Expected bool value after argument '-c-print-instructions'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-p-print-info")
                    {
                        i++;
                        if (i >= args.Length - 1 || !bool.TryParse(args[i], out result.parserSettings.PrintInfo))
                        { throw new ArgumentException("Expected bool value after argument '-p-print-info'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-test")
                    {
                        if (result.RunType != RunType.Normal) throw new ArgumentException(
                            $"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {RunType.Tester}");
                        result.RunType = RunType.Tester;
                        goto ArgParseDone;
                    }

                    if (args[i] == "-pipe")
                    {
                        i++;
                        if (i >= args.Length - 1)
                        { throw new ArgumentException("Expected string value after argument '-pipe'"); }

                        result.PipeName = args[i];

                        goto ArgParseDone;
                    }

                    if (args[i] == "-port")
                    {
                        i++;
                        if (i >= args.Length - 1 || !int.TryParse(args[i], out result.Port))
                        { throw new ArgumentException("Expected int value after argument '-pipe'"); }
                        goto ArgParseDone;
                    }

                    if (args[i] == "-test-id")
                    {
                        i++;
                        if (i >= args.Length - 1)
                        { throw new ArgumentException("Expected string value after argument '-test-id'"); }
                        result.TestID = args[i];
                        goto ArgParseDone;
                    }

                    if (args[i] == "-compiler-type")
                    {
                        i++;
                        if (i >= args.Length - 1)
                        { throw new ArgumentException("Expected string value after argument '-compiler-type'"); }
                        switch (args[i])
                        {
                            case "binary":
                                result.CompileToFileType = FileType.Binary;
                                break;
                            case "readable":
                                result.CompileToFileType = FileType.Readable;
                                break;
                            default:
                                { throw new ArgumentException($"Unknown compiler type '{args[i]}'. Possible values: 'binary', 'readable'"); }
                        }
                        goto ArgParseDone;
                    }

                    throw new ArgumentException($"Unknown argument '{args[i]}'");

                ArgParseDone:
                    i++;
                }
            }

            if (!System.IO.File.Exists(args.Last()))
            {
                throw new ArgumentException($"File '{args.Last()}' not found!");
            }

            result.File = new System.IO.FileInfo(args.Last());

            return result;
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
            public string PipeName;
            public int Port;
            public bool LogDebugs;
            public bool LogSystem;
            public RunType RunType;
            public string CompileOutput;
            public CompressionLevel CompressionLevel;
            public string BasePath;
            public string TestID;
            public FileType CompileToFileType;

            public static Settings Default() => new()
            {
                ThrowErrors = false,
                LogDebugs = true,
                LogSystem = true,
                BasePath = "",
                RunType = RunType.Normal,
                compilerSettings = Compiler.CompilerSettings.Default,
                parserSettings = ParserSettings.Default,
                bytecodeInterpreterSettings = BytecodeInterpreterSettings.Default,
                CompileOutput = null,
                CompressionLevel = CompressionLevel.Optimal,
                PipeName = null,
                Port = -1,
                TestID = null,
                CompileToFileType = FileType.Binary,
            };
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

            ArgumentNormalizer normalizer = new();
            normalizer.NormalizeArgs(args);
            string[] normalizedArgs = normalizer.Result.ToArray();

            Settings settings;

            try
            {
                settings = ParseArgs(normalizedArgs);
            }
            catch (ArgumentException error)
            {
                Output.Error(error.Message);
                PrintArgs(normalizedArgs);
                return null;
            }

            return settings;
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

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;

namespace TheProgram
{
    using LanguageCore;
    using LanguageCore.Runtime;

    public static class ArgumentParser
    {
        public enum RunType
        {
            Normal,
            Debugger,
            Compile,
            Decompile,
            Brainfuck,
            IL,
            ASM,
        }

        public enum FileType
        {
            Binary,
            Readable,
        }

        class ArgumentNormalizer
        {
            public readonly List<string> Result;

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
                CurrentArg = string.Empty;
            }

            void FinishArgument()
            {
                string currentArg = CurrentArg.Trim();

                State = NormalizerState.None;
                CurrentArg = string.Empty;

                if (string.IsNullOrEmpty(currentArg)) return;

                Result.Add(currentArg);
            }

            public void NormalizeArgs(params string[] args) => NormalizeArgs(string.Join(' ', args));
            public void NormalizeArgs(string args)
            {
                Result.Clear();
                State = NormalizerState.None;
                CurrentArg = string.Empty;

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

        static bool ExpectArg(string[] args, ref int i, [NotNullWhen(true)] out string? result, params string[] name)
        {
            result = null;

            if (i >= args.Length - 1)
            { return false; }

            for (int j = 0; j < name.Length; j++)
            {
                if (args[i] == name[j])
                {
                    result = args[i];
                    i++;
                    return true;
                }
            }

            return false;
        }

        static bool ExpectParam(string[] args, ref int i, out int param)
        {
            param = default;

            if (i >= args.Length - 1)
            { return false; }

            if (!int.TryParse(args[i], out int @int))
            { return false; }

            i++;
            param = @int;
            return true;
        }

        static bool ExpectParam(string[] args, ref int i, [NotNullWhen(true)] out string? param)
        {
            param = default;

            if (i >= args.Length - 1)
            { return false; }

            param = args[i];
            i++;
            return true;
        }

        static Settings ParseArgs(string[] args)
        {
            Settings result = Settings.Default;
#pragma warning disable IDE0059 // Unnecessary assignment of a value
            string? arg = null;
#pragma warning restore IDE0059 // Unnecessary assignment of a value

            if (args.Length > 1)
            {
                int i = 0;
                while (i < args.Length - 1)
                {
                    if (ExpectArg(args, ref i, out _, "--no-nullcheck", "-nn"))
                    {
                        result.compilerSettings.CheckNullPointers = false;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--no-pause", "-np"))
                    {
                        result.DoNotPause = true;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out arg, "--heap-size", "-hs"))
                    {
                        if (!ExpectParam(args, ref i, out result.bytecodeInterpreterSettings.HeapSize))
                        { throw new ArgumentException($"Expected number value after argument \"{arg}\""); }

                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--brainfuck", "-bf"))
                    {
                        if (result.RunType != RunType.Normal)
                        { throw new ArgumentException($"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {RunType.Brainfuck}"); }

                        result.RunType = RunType.Brainfuck;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--asm"))
                    {
                        if (result.RunType != RunType.Normal)
                        { throw new ArgumentException($"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {RunType.ASM}"); }

                        result.RunType = RunType.ASM;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--il"))
                    {
                        if (result.RunType != RunType.Normal)
                        { throw new ArgumentException($"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {RunType.IL}"); }

                        if (!ExpectParam(args, ref i, out result.CompileOutput))
                        { throw new ArgumentException("Expected string value after argument '-il'"); }

                        result.RunType = RunType.IL;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--debug"))
                    {
                        if (result.RunType != RunType.Normal)
                        { throw new ArgumentException($"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {RunType.Debugger}"); }

                        result.RunType = RunType.Debugger;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--console-gui", "-cg"))
                    {
                        result.ConsoleGUI = true;
                        continue;
                    }

                    /*
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
                    */

                    if (ExpectArg(args, ref i, out arg, "--basepath", "-bp"))
                    {
                        if (!ExpectParam(args, ref i, out result.compilerSettings.BasePath))
                        { throw new ArgumentException($"Expected string value after argument \"{arg}\""); }

                        continue;
                    }

                    /*
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
                    */

                    if (ExpectArg(args, ref i, out _, "--throw-errors", "-te"))
                    {
                        result.ThrowErrors = true;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--hide-debug", "-hd"))
                    {
                        result.LogDebugs = false;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--hide-system", "-hs"))
                    {
                        result.LogSystem = false;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--hide-warning", "--hide-warnings", "-hw"))
                    {
                        result.LogWarnings = false;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--hide-info", "-hi"))
                    {
                        result.LogInfo = false;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--dont-optimize", "-do"))
                    {
                        result.compilerSettings.DontOptimize = true;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--no-debug-info", "-ndi"))
                    {
                        result.compilerSettings.GenerateDebugInstructions = false;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--remove-unused-functions", "-ruf"))
                    {
                        if (!ExpectParam(args, ref i, out result.compilerSettings.RemoveUnusedFunctionsMaxIterations))
                        { throw new ArgumentException("Expected byte value after argument '-c-remove-unused-functions'"); }

                        continue;
                    }

                    if (ExpectArg(args, ref i, out arg, "--stack-size", "-ss"))
                    {
                        if (!ExpectParam(args, ref i, out result.bytecodeInterpreterSettings.StackMaxSize))
                        { throw new ArgumentException($"Expected int value after argument \"{arg}\""); }

                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--print-instructions", "-pi"))
                    {
                        result.compilerSettings.PrintInstructions = true;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out arg, "--pipe", "-pp"))
                    {
                        if (!ExpectParam(args, ref i, out result.PipeName))
                        { throw new ArgumentException($"Expected string value after argument \"{arg}\""); }

                        continue;
                    }

                    if (ExpectArg(args, ref i, out arg, "--port", "-pr"))
                    {
                        if (!ExpectParam(args, ref i, out result.Port))
                        { throw new ArgumentException($"Expected int value after argument \"{arg}\""); }

                        continue;
                    }

                    if (ExpectArg(args, ref i, out arg, "--test-id", "-ti"))
                    {
                        if (!ExpectParam(args, ref i, out result.TestID))
                        { throw new ArgumentException($"Expected string value after argument \"{arg}\""); }

                        continue;
                    }

                    if (ExpectArg(args, ref i, out arg, "--compiler-type", "-ct"))
                    {
                        if (!ExpectParam(args, ref i, out string? compileType))
                        { throw new ArgumentException($"Expected string value after argument \"{arg}\""); }

                        result.CompileToFileType = compileType switch
                        {
                            "binary" => FileType.Binary,
                            "readable" => FileType.Readable,
                            _ => throw new ArgumentException($"Unknown compiler type \"{compileType}\". Possible values: \"binary\", \"readable\""),
                        };
                        continue;
                    }

                    throw new ArgumentException($"Unknown argument \"{args[i]}\"");
                }
            }

            string file = args[^1];
            if (!System.IO.File.Exists(file))
            { throw new ArgumentException($"File \"{file}\" not found"); }
            result.File = new System.IO.FileInfo(file);

            return result;
        }

        /// <summary>
        /// Argument parser result
        /// </summary>
        public struct Settings
        {
            public System.IO.FileInfo File;

            public LanguageCore.Compiler.CompilerSettings compilerSettings;
            public BytecodeInterpreterSettings bytecodeInterpreterSettings;
            public bool ThrowErrors;
            public readonly bool HandleErrors => !ThrowErrors;
            public string? PipeName;
            public int Port;

            public bool LogDebugs;
            public bool LogSystem;
            public bool LogWarnings;
            public bool LogInfo;

            public RunType RunType;
            public string? CompileOutput;
            public CompressionLevel CompressionLevel;
            public string? TestID;
            public FileType CompileToFileType;
            public bool IsTest;

            public bool ConsoleGUI;

            public bool DoNotPause;

            public static Settings Default => new()
            {
                ThrowErrors = false,
                LogDebugs = true,
                LogSystem = true,
                LogWarnings = true,
                LogInfo = true,
                RunType = RunType.Normal,
                compilerSettings = LanguageCore.Compiler.CompilerSettings.Default,
                bytecodeInterpreterSettings = BytecodeInterpreterSettings.Default,
                CompileOutput = null,
                CompressionLevel = CompressionLevel.Optimal,
                PipeName = null,
                Port = -1,
                TestID = null,
                CompileToFileType = FileType.Binary,
                IsTest = false,
                ConsoleGUI = false,
                DoNotPause = false,
            };
        }

        public static bool Parse(out Settings settings, params string[] args)
        {
            settings = Settings.Default;
            Settings? _settings = ArgumentParser.Parse(args);
            if (_settings.HasValue)
            {
                settings = _settings.Value;
                return true;
            }
            else
            {
                return false;
            }
        }
        public static Settings? Parse(params string[] args)
        {
            if (args.Length == 0)
            {
                Output.LogError("No arguments passed!");
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
                Output.LogError(error.Message);
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

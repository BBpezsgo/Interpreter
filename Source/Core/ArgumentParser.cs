using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TheProgram
{
    using LanguageCore;
    using LanguageCore.Runtime;

    public struct ProgramArguments
    {
        [MemberNotNullWhen(false, nameof(File))]
        public readonly bool IsEmpty => File is null;

        public System.IO.FileInfo? File;

        public LanguageCore.Compiler.CompilerSettings CompilerSettings;
        public LanguageCore.Compiler.GeneratorSettings GeneratorSettings;
        public BytecodeInterpreterSettings BytecodeInterpreterSettings;

        public bool ThrowErrors;

        public LogType LogFlags;

        public ProgramRunType RunType;

        public bool ConsoleGUI;

        public bool DoNotPause;

        public static ProgramArguments Default => new()
        {
            ThrowErrors = false,
            LogFlags = LogType.System | LogType.Debug | LogType.Normal | LogType.Warning | LogType.Error,
            RunType = ProgramRunType.Normal,
            CompilerSettings = LanguageCore.Compiler.CompilerSettings.Default,
            GeneratorSettings = LanguageCore.Compiler.GeneratorSettings.Default,
            BytecodeInterpreterSettings = BytecodeInterpreterSettings.Default,
            ConsoleGUI = false,
            DoNotPause = false,
            File = null,
        };
    }

    public enum ProgramRunType
    {
        Normal,
        Brainfuck,
        IL,
        ASM,
    }

    public static class ArgumentParser
    {
        sealed class ArgumentNormalizer
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

        static ProgramArguments ParseArgs(string[] args)
        {
            ProgramArguments result = ProgramArguments.Default;
#pragma warning disable IDE0018 // Inline variable declaration
            string? arg;
#pragma warning restore IDE0018

            if (args.Length > 1)
            {
                int i = 0;
                while (i < args.Length - 1)
                {
                    if (ExpectArg(args, ref i, out _, "--no-nullcheck", "-nn"))
                    {
                        result.GeneratorSettings.CheckNullPointers = false;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--no-pause", "-np"))
                    {
                        result.DoNotPause = true;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out arg, "--heap-size", "-hs"))
                    {
                        if (!ExpectParam(args, ref i, out result.BytecodeInterpreterSettings.HeapSize))
                        { throw new ArgumentException($"Expected number value after argument \"{arg}\""); }

                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--brainfuck", "-bf"))
                    {
                        if (result.RunType != ProgramRunType.Normal)
                        { throw new ArgumentException($"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {ProgramRunType.Brainfuck}"); }

                        result.RunType = ProgramRunType.Brainfuck;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--asm"))
                    {
                        if (result.RunType != ProgramRunType.Normal)
                        { throw new ArgumentException($"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {ProgramRunType.ASM}"); }

                        result.RunType = ProgramRunType.ASM;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--il"))
                    {
                        if (result.RunType != ProgramRunType.Normal)
                        { throw new ArgumentException($"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {ProgramRunType.IL}"); }

                        result.RunType = ProgramRunType.IL;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--console-gui", "-cg"))
                    {
                        result.ConsoleGUI = true;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out arg, "--basepath", "-bp"))
                    {
                        if (!ExpectParam(args, ref i, out result.CompilerSettings.BasePath))
                        { throw new ArgumentException($"Expected string value after argument \"{arg}\""); }

                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--throw-errors", "-te"))
                    {
                        result.ThrowErrors = true;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--hide-debug", "-hd"))
                    {
                        result.LogFlags &= ~LogType.Debug;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--hide-system", "-hs"))
                    {
                        result.LogFlags &= ~LogType.System;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--hide-warning", "--hide-warnings", "-hw"))
                    {
                        result.LogFlags &= ~LogType.Warning;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--hide-info", "-hi"))
                    {
                        result.LogFlags &= ~LogType.Normal;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--dont-optimize", "-do"))
                    {
                        result.GeneratorSettings.DontOptimize = true;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--no-debug-info", "-ndi"))
                    {
                        result.GeneratorSettings.GenerateDebugInstructions = false;
                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--remove-unused-functions", "-ruf"))
                    {
                        if (!ExpectParam(args, ref i, out result.GeneratorSettings.RemoveUnusedFunctionsMaxIterations))
                        { throw new ArgumentException("Expected byte value after argument '-c-remove-unused-functions'"); }

                        continue;
                    }

                    if (ExpectArg(args, ref i, out arg, "--stack-size", "-ss"))
                    {
                        if (!ExpectParam(args, ref i, out result.BytecodeInterpreterSettings.StackMaxSize))
                        { throw new ArgumentException($"Expected int value after argument \"{arg}\""); }

                        continue;
                    }

                    if (ExpectArg(args, ref i, out _, "--print-instructions", "-pi"))
                    {
                        result.GeneratorSettings.PrintInstructions = true;
                        continue;
                    }
           
                    throw new ArgumentException($"Unknown argument \"{args[i]}\"");
                }
            }

            if (args.Length > 0)
            {
                string file = args[^1];
                if (!System.IO.File.Exists(file))
                { throw new ArgumentException($"File \"{file}\" not found"); }
                result.File = new System.IO.FileInfo(file);
            }
            else
            {
                result.File = null;
            }

            return result;
        }

        public static bool Parse(out ProgramArguments settings, params string[] args)
        {
            settings = ProgramArguments.Default;
            ProgramArguments? _settings = ArgumentParser.Parse(args);
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
        public static ProgramArguments? Parse(params string[] args)
        {
            if (args.Length == 0)
            { return ProgramArguments.Default; }

            ArgumentNormalizer normalizer = new();
            normalizer.NormalizeArgs(args);
            string[] normalizedArgs = normalizer.Result.ToArray();

            ProgramArguments settings;

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

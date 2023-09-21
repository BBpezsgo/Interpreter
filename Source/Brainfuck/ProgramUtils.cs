﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ProgrammingLanguage.Brainfuck;
using ProgrammingLanguage.Brainfuck.Compiler;
using ProgrammingLanguage.Core;

#nullable enable

namespace TheProgram.Brainfuck
{
    public enum RunKind : int
    {
        Default,
        UI,
        SpeedTest,
    }

    [Flags]
    public enum PrintFlags : int
    {
        None = 0,
        PrintResultLabel = 1,
        PrintExecutionTime = 2,
        PrintMemory = 4,
    }

    internal static class ProgramUtils
    {
        public static void Run(ArgumentParser.Settings args, RunKind runKind, PrintFlags runFlags)
        {
            CompileOptions flags =
                CompileOptions.None;

            void PrintCallback(string message, ProgrammingLanguage.Output.LogType level)
            {
                switch (level)
                {
                    case ProgrammingLanguage.Output.LogType.System:
                        if (!args.LogSystem) break;
                        ProgrammingLanguage.Output.Output.Log(message);
                        break;
                    case ProgrammingLanguage.Output.LogType.Normal:
                        ProgrammingLanguage.Output.Output.Log(message);
                        break;
                    case ProgrammingLanguage.Output.LogType.Warning:
                        if (!args.LogWarnings) break;
                        ProgrammingLanguage.Output.Output.Warning(message);
                        break;
                    case ProgrammingLanguage.Output.LogType.Error:
                        ProgrammingLanguage.Output.Output.Error(message);
                        break;
                    case ProgrammingLanguage.Output.LogType.Debug:
                        if (!args.LogDebugs) break;
                            ProgrammingLanguage.Output.Output.Debug(message);
                        break;
                    default:
                        ProgrammingLanguage.Output.Output.Log(message);
                        break;
                }
            }

            CodeGenerator.Result? _code = ProgramUtils.CompilePlus(args.File, flags, args.compilerSettings, PrintCallback);
            if (!_code.HasValue)
            { return; }

            CodeGenerator.Result code = _code.Value;

            ProgrammingLanguage.Brainfuck.Interpreter interpreter = new(code.Code)
            {
                DebugInfo = code.DebugInfo.ToArray(),
                OriginalCode = code.Tokens,
            };

            switch (runKind)
            {
                case RunKind.UI:
                    {
                        Console.WriteLine();
                        Console.Write("Press any key to start the interpreter");
                        Console.ReadKey();

                        interpreter.RunWithUI(true, 5);
                        break;
                    }
                case RunKind.SpeedTest:
                    {
                        if (runFlags.HasFlag(PrintFlags.PrintResultLabel))
                        {
                            Console.WriteLine();
                            Console.WriteLine($" === RESULT ===");
                            Console.WriteLine();
                        }

                        SpeedTest(code.Code, 3);
                        break;
                    }
                case RunKind.Default:
                    {
                        if (runFlags.HasFlag(PrintFlags.PrintResultLabel))
                        {
                            Console.WriteLine();
                            Console.WriteLine($" === RESULT ===");
                            Console.WriteLine();
                        }

                        if (runFlags.HasFlag(PrintFlags.PrintExecutionTime))
                        {
                            Stopwatch sw = Stopwatch.StartNew();
                            interpreter.Run();
                            sw.Stop();

                            Console.WriteLine();
                            Console.WriteLine();
                            Console.WriteLine($"Execution time: {sw.ElapsedMilliseconds} ms");
                        }
                        else
                        {
                            interpreter.Run();
                        }

                        if (runFlags.HasFlag(PrintFlags.PrintMemory))
                        {
                            Console.ResetColor();
                            Console.WriteLine();
                            Console.WriteLine();
                            Console.WriteLine($" === MEMORY ===");
                            Console.WriteLine();
                            Console.ResetColor();

                            int zerosToShow = 10;
                            int finalIndex = 0;

                            for (int i = 0; i < interpreter.Memory.Length; i++)
                            { if (interpreter.Memory[i] != 0) finalIndex = i; }

                            finalIndex = Math.Max(finalIndex, interpreter.MemoryPointer);
                            finalIndex = Math.Min(interpreter.Memory.Length, finalIndex + zerosToShow);

                            for (int i = 0; i < finalIndex; i++)
                            {
                                var cell = interpreter.Memory[i];
                                if (i == interpreter.MemoryPointer)
                                { Console.ForegroundColor = ConsoleColor.Red; }
                                else if (cell == 0)
                                { Console.ForegroundColor = ConsoleColor.DarkGray; }
                                Console.Write($" {cell} ");
                                Console.ResetColor();
                            }

                            if (interpreter.Memory.Length - finalIndex > 0)
                            {
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.Write($" ... ");
                                Console.ResetColor();
                            }

                            Console.WriteLine();
                        }

                        break;
                    }
            }
        }

        public static void SpeedTest(string code, int iterations)
        {
            ProgrammingLanguage.Brainfuck.Interpreter interpreter = new(code, _ => { });

            int line = Console.GetCursorPosition().Top;
            Console.ResetColor();
            Stopwatch sw = new();
            for (int i = 0; i < iterations; i++)
            {
                Console.SetCursorPosition(0, line);
                Console.Write($"Running iteration {i} / {iterations} ...         ");
                interpreter.Reset();

                sw.Start();
                interpreter.Run();
                sw.Stop();
            }

            Console.SetCursorPosition(0, line);
            Console.WriteLine($"Execution time: {sw.ElapsedMilliseconds} ms                 ");
        }

        [Flags]
        public enum CompileOptions : int
        {
            None = 0b_0000,

            PrintCompiled = 0b_0001,
            PrintCompiledMinimized = 0b_0010,
            WriteToFile = 0b_0100,

            PrintFinal = 0b_1000,
        }

        public static CodeGenerator.Result? CompilePlus(FileInfo file, CompileOptions options, ProgrammingLanguage.BBCode.Compiler.Compiler.CompilerSettings compilerSettings, ProgrammingLanguage.Output.PrintCallback printCallback)
                => CompilePlus(file, (int)options, compilerSettings, printCallback);
        public static CodeGenerator.Result? CompilePlus(FileInfo file, int options, ProgrammingLanguage.BBCode.Compiler.Compiler.CompilerSettings _compilerSettings, ProgrammingLanguage.Output.PrintCallback printCallback)
        {
            CodeGenerator.Settings compilerSettings = CodeGenerator.Settings.Default;

            string code = File.ReadAllText(file.FullName);

            compilerSettings.ClearGlobalVariablesBeforeExit = true;

            bool throwErrors = true;

            CodeGenerator.Result compilerResult;

            try
            {
                compilerResult = EasyCompiler.Compile(file, _compilerSettings, compilerSettings, printCallback).CodeGeneratorResult;
                printCallback?.Invoke($"Optimized {compilerResult.Optimizations} statements", ProgrammingLanguage.Output.LogType.Debug);
            }
            catch (ProgrammingLanguage.Errors.Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(exception.ToString());

                string? arrows = GetArrows(exception.Position, code);
                if (arrows != null)
                { Console.WriteLine(arrows); }

                Console.ResetColor();

                if (throwErrors) throw;
                else return null;
            }
            catch (Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(exception.ToString());

                Console.ResetColor();

                if (throwErrors) throw;
                else return null;
            }

            if ((options & (int)CompileOptions.PrintCompiled) != 0)
            {
                Console.WriteLine();
                Console.WriteLine($" === COMPILED ===");
                PrintCode(Simplifier.Simplify(compilerResult.Code));
                Console.WriteLine();
                Console.ResetColor();
            }

            compilerResult.Code = Minifier.Minify(compilerResult.Code);

            if ((options & (int)CompileOptions.PrintCompiledMinimized) != 0)
            {
                Console.WriteLine();
                Console.WriteLine($" === MINIFIED ===");
                PrintCode(Simplifier.Simplify(compilerResult.Code));
                Console.WriteLine();
                Console.ResetColor();
            }

            compilerResult.Code = Minifier.Minify(Utils.RemoveNoncodes(compilerResult.Code));

            if ((options & (int)CompileOptions.PrintFinal) != 0)
            {
                Console.WriteLine();
                Console.WriteLine($" === FINAL ===");
                PrintCode(compilerResult.Code);
                Console.WriteLine();
                Console.ResetColor();
            }

            if ((options & (int)CompileOptions.WriteToFile) != 0)
            {
                string compiledFilePath = Path.Combine(Path.GetDirectoryName(file.FullName) ?? TestConstants.TestFilesPath, Path.GetFileNameWithoutExtension(file.FullName) + ".bf");
                File.WriteAllText(compiledFilePath, compilerResult.Code);
            }

            return compilerResult;
        }

        public static string? Compile(string code, CompileOptions options)
            => Compile(code, (int)options);
        public static string Compile(string code, int options)
        {
            string compiled = Minifier.Minify(Utils.RemoveNoncodes(code));

            if ((options & (int)CompileOptions.PrintFinal) != 0)
            {
                Console.WriteLine();
                Console.WriteLine($" === FINAL ===");
                PrintCode(compiled);
                Console.WriteLine();
                Console.ResetColor();
            }

            return compiled;
        }

        public static string? CompileFile(string file, CompileOptions options, ProgrammingLanguage.BBCode.Compiler.Compiler.CompilerSettings compilerSettings, ProgrammingLanguage.Output.PrintCallback printCallback)
            => CompileFile(file, (int)options, compilerSettings, printCallback);
        public static string? CompileFile(string file, int options, ProgrammingLanguage.BBCode.Compiler.Compiler.CompilerSettings compilerSettings, ProgrammingLanguage.Output.PrintCallback printCallback)
        {
            if (!File.Exists(file))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File \"{file}\" does not exists");
                Console.ResetColor();
                return null;
            }

            string extension = Path.GetExtension(file)[1..];
            if (extension == "bfpp")
            {
                return CompilePlus(new FileInfo(file), options, compilerSettings, printCallback)?.Code;
            }

            if (extension == "bf")
            {
                return Compile(File.ReadAllText(file), options);
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Unknown extension \"{extension}\"");
            Console.ResetColor();
            return null;
        }

        public static string? GetArrows(Position position, string text)
        {
            if (position.AbsolutePosition == new Range<int>(0, 0)) return null;
            if (position.Start.Line != position.End.Line)
            { return null; }
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (position.Start.Line - 1 >= lines.Length)
            { return null; }
            string line = lines[position.Start.Line - 1];

            string result = "";

            result += line.Replace('\t', ' ');
            result += "\r\n";
            result += new string(' ', Math.Max(0, position.Start.Character - 1));
            result += new string('^', Math.Max(1, position.End.Character - position.Start.Character));
            return result;
        }

        public static void PrintCode(string code)
        {
            bool expectNumber = false;
            for (int i = 0; i < code.Length; i++)
            {
                switch (code[i])
                {
                    case '>':
                    case '<':
                        if (Console.ForegroundColor != ConsoleColor.Red) Console.ForegroundColor = ConsoleColor.Red;
                        expectNumber = true;
                        break;
                    case '+':
                    case '-':
                        if (Console.ForegroundColor != ConsoleColor.Blue) Console.ForegroundColor = ConsoleColor.Blue;
                        expectNumber = true;
                        break;
                    case '[':
                    case ']':
                        if (Console.ForegroundColor != ConsoleColor.Green) Console.ForegroundColor = ConsoleColor.Green;
                        expectNumber = false;
                        break;
                    case '.':
                    case ',':
                        if (Console.ForegroundColor != ConsoleColor.Magenta) Console.ForegroundColor = ConsoleColor.Magenta;
                        expectNumber = false;
                        break;
                    default:
                        if (expectNumber && (new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' }).Contains(code[i]))
                        {
                            if (Console.ForegroundColor != ConsoleColor.Yellow) Console.ForegroundColor = ConsoleColor.Yellow;
                        }
                        else if (Utils.CodeCharacters.Contains(code[i]))
                        {
                            expectNumber = false;
                            if (Console.ForegroundColor != ConsoleColor.Magenta) Console.ForegroundColor = ConsoleColor.Magenta;
                        }
                        else
                        {
                            expectNumber = false;
                            if (Console.ForegroundColor != ConsoleColor.DarkGray) Console.ForegroundColor = ConsoleColor.DarkGray;
                        }
                        break;
                }
                Console.Write(code[i]);
            }
        }

        public static void PrintCodeChar(char code)
        {
            switch (code)
            {
                case '>':
                case '<':
                    if (Console.ForegroundColor != ConsoleColor.Red) Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case '+':
                case '-':
                    if (Console.ForegroundColor != ConsoleColor.Blue) Console.ForegroundColor = ConsoleColor.Blue;
                    break;
                case '[':
                case ']':
                    if (Console.ForegroundColor != ConsoleColor.Green) Console.ForegroundColor = ConsoleColor.Green;
                    break;
                case '.':
                case ',':
                    if (Console.ForegroundColor != ConsoleColor.Magenta) Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                default:
                    if (Utils.CodeCharacters.Contains(code))
                    { if (Console.ForegroundColor != ConsoleColor.Magenta) Console.ForegroundColor = ConsoleColor.Magenta; }
                    else
                    { if (Console.ForegroundColor != ConsoleColor.DarkGray) Console.ForegroundColor = ConsoleColor.DarkGray; }
                    break;
            }
            Console.Write(code);
        }
    }
}

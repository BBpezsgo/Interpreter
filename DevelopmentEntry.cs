#define ENABLE_DEBUG
#define RELEASE_TEST_

#pragma warning disable CS0162 // Unreachable code detected

using System;
using System.Diagnostics;

namespace TheProgram
{
    internal static class DevelopmentEntry
    {
#if (!DEBUG || !ENABLE_DEBUG) && !RELEASE_TEST
        internal static bool Start() => false;
#else
        internal static bool Start()
        {
            string[] args = Array.Empty<string>();

#if DEBUG && ENABLE_DEBUG

            //string path = TestConstants.ExampleFilesPath + "hello-world.bbc";
            string path = TestConstants.TestFilesPath + "test34.bbc";

            if (args.Length == 0) args = new string[]
            {
                // "-throw-errors",
                "-basepath \"../CodeFiles/\"",
                // "-c-print-instructions true",
                // "-c-remove-unused-functions 5",
                // "C:\\Users\\bazsi\\.vscode\\extensions\\bbc\\TestFiles\\a.bbc",
                // "-hide-debug",
                "-hide-system",
                // "-c-generate-comments false",
                // "-no-debug-info",
                // "-dont-optimize",
                // "-test",
                // "-decompile",
                // "-compile",
                // "-debug",
                // "-console-gui",
                // "\".\\output.bin\"",
                // "-compression", "no",
                "-brainfuck",
                "-heap 2048",
                "-bc-instruction-limit " + int.MaxValue.ToString(),
                $"\"{path}\""
                // $"\"{TestConstants.TestFilesPath}tester.bbct\""
            };
#endif
#if RELEASE_TEST
            if (args.Length == 0) args = new string[]
            {
                "\"D:\\Program Files\\BBCodeProject\\BBCode\\TestFiles\\helloworld.bbc\""
            };
#endif

            var settings = ArgumentParser.Parse(args);
            if (!settings.HasValue) return true;

            switch (settings.Value.RunType)
            {
                case ArgumentParser.RunType.ConsoleGUI:
                    ConsoleGUI.ConsoleGUI gui = new()
                    {
                        FilledElement = new ConsoleGUI.InterpreterElement(path, settings.Value.compilerSettings, settings.Value.parserSettings, settings.Value.bytecodeInterpreterSettings, settings.Value.HandleErrors, settings.Value.BasePath)
                    };
                    while (!gui.Destroyed)
                    { gui.Tick(); }
                    return true;
                case ArgumentParser.RunType.Debugger:
                    throw new NotImplementedException();
                case ArgumentParser.RunType.Tester:
                    ProgrammingLanguage.Tester.Tester.RunTestFile(settings.Value);
                    break;
                case ArgumentParser.RunType.Normal:
                    ProgrammingLanguage.Core.EasyInterpreter.Run(settings.Value);
                    break;
                case ArgumentParser.RunType.Compile:
                    throw new NotImplementedException();
                case ArgumentParser.RunType.Decompile:
                    throw new NotImplementedException();
                case ArgumentParser.RunType.Brainfuck:
                    {
                        ProgrammingLanguage.Brainfuck.Compiler.CodeGenerator.Result? _code = Brainfuck.ProgramUtils.CompilePlus(settings.Value.File, Brainfuck.ProgramUtils.CompileOptions.None); //, Brainfuck.ProgramUtils.CompileOptions.PrintCompiledMinimized);
                        if (!_code.HasValue)
                        { break; }
                        var code = _code.Value;

                        ProgrammingLanguage.Brainfuck.Interpreter interpreter = new(code.Code)
                        {
                            DebugInfo = code.DebugInfo.ToArray(),
                            OriginalCode = code.Tokens,
                        };

                        int runMode = 2;

                        if (runMode == 0)
                        {
                            Console.WriteLine();
                            Console.Write("Press any key to start the interpreter");
                            Console.ReadKey();

                            interpreter.RunWithUI(true, 5);
                        }
                        else if (runMode == 1)
                        {
                            Console.WriteLine();
                            Console.WriteLine($" === RESULT ===");
                            Console.WriteLine();

                            Brainfuck.ProgramUtils.SpeedTest(code.Code, 3);
                        }
                        else
                        {
                            Console.WriteLine();
                            Console.WriteLine($" === RESULT ===");
                            Console.WriteLine();

                            Stopwatch sw = Stopwatch.StartNew();
                            interpreter.Run();
                            sw.Stop();

                            Console.WriteLine();
                            Console.WriteLine();
                            Console.WriteLine($"Execution time: {sw.ElapsedMilliseconds} ms");

                            Console.ResetColor();
                            Console.WriteLine();
                            Console.WriteLine();
                            Console.WriteLine($" === MEMORY ===");
                            Console.WriteLine();
                            Console.ResetColor();

                            {
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
                        }
                    }
                    break;
            }

            return true;
        }
#endif
    }
}

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LanguageCore.Brainfuck
{
    using Generator;

    public enum BrainfuckRunKind : int
    {
        Default,
        UI,
        SpeedTest,
    }

    [Flags]
    public enum BrainfuckPrintFlags : int
    {
        None = 0,
        PrintResultLabel = 1,
        PrintExecutionTime = 2,
        PrintMemory = 4,
    }

    public static class BrainfuckRunner
    {
        static BrainfuckGeneratorSettings CompilerSettings => BrainfuckGeneratorSettings.Default;

        public static void Run(TheProgram.ProgramArguments args, BrainfuckRunKind runKind, BrainfuckPrintFlags runFlags, EasyBrainfuckCompilerFlags flags = EasyBrainfuckCompilerFlags.None)
        {
            EasyBrainfuckCompilerResult? _code = EasyBrainfuckCompiler.Compile(args.File!, flags, Output.Log);
            if (!_code.HasValue)
            { return; }

            EasyBrainfuckCompilerResult generated = _code.Value;

            InterpreterCompact interpreter = new(generated.GeneratorResult.Code)
            {
                DebugInfo = generated.GeneratorResult.DebugInfo,
                OriginalCode = generated.Tokens,
            };

            switch (runKind)
            {
                case BrainfuckRunKind.UI:
                {
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    { throw new PlatformNotSupportedException($"Console rendering is only supported on Windows"); }

                    Console.WriteLine();
                    Console.Write("Press any key to start the interpreter");
                    Console.ReadKey();

                    interpreter.RunWithUI(true, 2);
                    break;
                }
                case BrainfuckRunKind.SpeedTest:
                {
                    if (runFlags.HasFlag(BrainfuckPrintFlags.PrintResultLabel))
                    {
                        Console.WriteLine();
                        Console.WriteLine($" === RESULT ===");
                        Console.WriteLine();
                    }

                    SpeedTest(generated.GeneratorResult.Code, 3);
                    break;
                }
                case BrainfuckRunKind.Default:
                {
                    if (runFlags.HasFlag(BrainfuckPrintFlags.PrintResultLabel))
                    {
                        Console.WriteLine();
                        Console.WriteLine($" === RESULT ===");
                        Console.WriteLine();
                    }

                    if (runFlags.HasFlag(BrainfuckPrintFlags.PrintExecutionTime))
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

                    if (runFlags.HasFlag(BrainfuckPrintFlags.PrintMemory))
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

                        int heapStart = CompilerSettings.HeapStart;
                        int heapEnd = heapStart + CompilerSettings.HeapSize * BasicHeapCodeHelper.BLOCK_SIZE;

                        for (int i = 0; i < finalIndex; i++)
                        {
                            byte cell = interpreter.Memory[i];

                            ConsoleColor fg = ConsoleColor.White;
                            ConsoleColor bg = ConsoleColor.Black;

                            if (cell == 0)
                            { fg = ConsoleColor.DarkGray; }

                            if (i == heapStart)
                            {
                                bg = ConsoleColor.DarkBlue;
                            }

                            if (i > heapStart + 2)
                            {
                                int j = (i - heapStart) / BasicHeapCodeHelper.BLOCK_SIZE;
                                int k = (i - heapStart) % BasicHeapCodeHelper.BLOCK_SIZE;
                                if (k == BasicHeapCodeHelper.OFFSET_DATA)
                                { bg = ConsoleColor.DarkGreen; }
                            }

                            if (i == interpreter.MemoryPointer)
                            {
                                bg = ConsoleColor.DarkRed;
                                fg = ConsoleColor.Gray;
                            }

                            Console.ForegroundColor = fg;
                            Console.BackgroundColor = bg;

                            Console.Write($" {cell} ");
                            Console.ResetColor();
                        }

                        if (interpreter.Memory.Length - finalIndex > 0)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write($" ... ");
                            Console.ResetColor();
                            Console.WriteLine();
                            break;
                        }

                        Console.WriteLine();
                    }

                    break;
                }
            }
        }

        public static void SpeedTest(string code, int iterations)
        {
            Interpreter interpreter = new(code, _ => { });

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
    }
}

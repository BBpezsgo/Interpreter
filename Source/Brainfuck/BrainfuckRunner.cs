using System;

namespace LanguageCore.Brainfuck
{
    [Flags]
    public enum BrainfuckPrintFlags : int
    {
        None = 0b_0000,
        PrintResultLabel = 0b_0001,
        PrintExecutionTime = 0b_0010,
        PrintMemory = 0b_0100,
    }

    /*
    public static class BrainfuckRunner
    {
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
    */
}

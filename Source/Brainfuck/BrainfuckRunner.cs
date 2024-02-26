namespace LanguageCore.Brainfuck;

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

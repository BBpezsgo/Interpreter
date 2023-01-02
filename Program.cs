#define ENABLE_DEBUG

using IngameCoding.Core;

using System;

namespace TheProgram
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG && ENABLE_DEBUG

            /*
            Range<SinglePosition> range = new(new SinglePosition(3, 9), new SinglePosition(4, 1));
            SinglePosition p0 = new(3, 10);

            Console.WriteLine($"{range.Contains(p0)}");

            return;

            for (int line = 0; line < 20; line++)
            {
                for (int chr = 0; chr < 20; chr++)
                {
                    SinglePosition point = new(line, chr);
                    Console.WriteLine($"{point} {range.Contains(point)}");
                }
            }

            return;
            */

#if false
            var file = "test-net.bbc";
            IngameCoding.Core.EasyInterpreter.Run(ArgumentParser.Parse(
                // "-throw-errors",
                // "-c-print-instructions",
                // "true",
                "C:\\Users\\bazsi\\.vscode\\extensions\\bbc\\TestFiles\\a.bbc" //$"\"{TestConstants.TestFilesPath}{file}\""
            ).Value);
#else
            var settings = ArgumentParser.Parse(args).Value;
            if (settings.Debug)
            {
                DebugTest.Run(settings);
            }
            else if (settings.CodeEditor)
            {
                CodeEditor.Run(settings);
            }
#endif

#else
            var settings = ArgumentParser.Parse(args);
            if (!settings.HasValue) goto ExitProgram;

            if (settings.Value.Debug)
            {
                DebugTest.Run(settings.Value);
                return;
            }
            else
            {
                IngameCoding.Core.EasyInterpreter.Run(settings.Value);
            }

        ExitProgram:
            Console.WriteLine("\n\r\n\rPress any key to exit");
            Console.ReadKey();
#endif
        }
    }
}

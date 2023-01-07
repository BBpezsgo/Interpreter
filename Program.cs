#define ENABLE_DEBUG

using System;

namespace TheProgram
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG && ENABLE_DEBUG
            var file = "tester.test.bbc";
            args = new string[]
            {
                // "-throw-errors",
                // "-c-print-instructions", "true",
                // "C:\\Users\\bazsi\\.vscode\\extensions\\bbc\\TestFiles\\a.bbc",
                "-hide-debug",
                "-test",
                $"\"{TestConstants.TestFilesPath}{file}\""
            };
#endif

            var settings = ArgumentParser.Parse(args);
            if (!settings.HasValue) goto ExitProgram;

            switch (settings.Value.RunType)
            {
                case ArgumentParser.RunType.Debugger:
                    DebugTest.Run(settings.Value);
                    return;
                case ArgumentParser.RunType.Tester:
                    IngameCoding.Tester.Tester.RunTestFile(settings.Value);
                    break;
                default:
                    if (settings.Value.CodeEditor)
                    {
                        CodeEditor.Run(settings.Value);
                    }
                    else
                    {
                        IngameCoding.Core.EasyInterpreter.Run(settings.Value);
                    }
                    break;
            }

        ExitProgram:
#if DEBUG
            ;
#else
            Console.WriteLine("\n\r\n\rPress any key to exit");
            Console.ReadKey();
#endif
        }
    }
}

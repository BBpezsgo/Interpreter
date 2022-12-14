#define ENABLE_DEBUG_
#define RELEASE_TEST_

using System;

namespace TheProgram
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG && ENABLE_DEBUG
            var file = "compile-test.bbc";
            if (args.Length == 0) args = new string[]
            {
                // "-throw-errors",
                // "-c-print-instructions", "true",
                // "C:\\Users\\bazsi\\.vscode\\extensions\\bbc\\TestFiles\\a.bbc",
                // "-hide-debug",
                // "-test",
                "-decompile",
                // "-compile",
                // "\".\\output.bin\"",
                $"\"{TestConstants.TestFilesPath}{file}\""
            };
#endif
#if RELEASE_TEST
            if (args.Length == 0) args = new string[]
            {
                "\"D:\\Program Files\\BBCodeProject\\BBCode\\TestFiles\\helloworld.bbc\""
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
                case ArgumentParser.RunType.Normal:
                    IngameCoding.Core.EasyInterpreter.Run(settings.Value);
                    break;
                case ArgumentParser.RunType.Compile:
                    IngameCoding.CompileIntoFile.Compile(settings.Value);
                    break;
                case ArgumentParser.RunType.Decompile:
                    IngameCoding.Core.EasyInterpreter.RunBinary(settings.Value);
                    break;
            }

        ExitProgram:
#if DEBUG && ENABLE_DEBUG
            ;
#else
            Console.WriteLine("\n\r\n\rPress any key to exit");
            Console.ReadKey();
#endif
        }
    }
}

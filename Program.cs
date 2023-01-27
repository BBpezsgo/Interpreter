#define ENABLE_DEBUG
#define RELEASE_TEST_

using System;

namespace TheProgram
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // TODO: valami hiba van amit nem volt kedvem debuggolni, szoval hajrá :D
            // fájl: test-matrix.bbc

#if DEBUG && ENABLE_DEBUG
            var file = "test-namespaces.bbc";
            if (args.Length == 0) args = new string[]
            {
                 "-throw-errors",
                // "-c-print-instructions true",
                // "C:\\Users\\bazsi\\.vscode\\extensions\\bbc\\TestFiles\\a.bbc",
                // "-hide-debug",
                // "-c-generate-comments false",
                // "-no-debug-info",
                // "-dont-optimize",
                // "-test",
                // "-decompile",
                // "-compile",
                // "\".\\output.bin\"",
                // "-compression", "no",
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

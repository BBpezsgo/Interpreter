#define ENABLE_DEBUG
#define RELEASE_TEST_

#pragma warning disable CS0162 // Unreachable code detected

using System;

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
            var file = "donught.bbc";

            if (args.Length == 0) args = new string[]
            {
                // "-throw-errors",
                "-basepath \"../CodeFiles/\"",
                // "-c-print-instructions true",
                // "-c-remove-unused-functions 5",
                // "C:\\Users\\bazsi\\.vscode\\extensions\\bbc\\TestFiles\\a.bbc",
                "-hide-debug",
                "-hide-system",
                "-c-generate-comments false",
                // "-no-debug-info",
                // "-dont-optimize",
                // "-test",
                // "-decompile",
                // "-compile",
                // "-debug",
                // "-console-gui",
                // "\".\\output.bin\"",
                // "-compression", "no",
                "-heap 2048",
                "-bc-instruction-limit " + int.MaxValue.ToString(),
                $"\"{TestConstants.TestFilesPath}{file}\""
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
                        FilledElement = new ConsoleGUI.InterpreterElement($"{TestConstants.TestFilesPath}{file}", settings.Value.compilerSettings, settings.Value.parserSettings, settings.Value.bytecodeInterpreterSettings, settings.Value.HandleErrors)
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
            }

            return true;
        }
#endif
    }
}

﻿#define ENABLE_DEBUG
#define RELEASE_TEST_

#pragma warning disable CS0162 // Unreachable code detected

namespace TheProgram
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // TODO: valami hiba van amit nem volt kedvem debuggolni, szoval hajrá :D
            // fájl: test-matrix.bbc

#if DEBUG && ENABLE_DEBUG
            var file = "test-pointers.bbc";

            if (false)
            {
                ConsoleGUI.ConsoleGUI consoleGUI = new()
                {
                    FilledElement = new ConsoleGUI.InterpreterElement($"{TestConstants.TestFilesPath}{file}")
                };
                consoleGUI.Start();

                return;
            }

            if (args.Length == 0) args = new string[]
            {
                "-throw-errors",
                "-basepath \"../CodeFiles/\"",
                // "-c-print-instructions true",
                // "-c-remove-unused-functions 5",
                // "C:\\Users\\bazsi\\.vscode\\extensions\\bbc\\TestFiles\\a.bbc",
                "-hide-debug",
                "-hide-system",
                // "-c-generate-comments false",
                // "-no-debug-info",
                // "-dont-optimize",
                // "-test",
                // "-decompile",
                // "-compile",
                "-debug",
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

            ConsoleGUI.ConsoleGUI a = new()
            {
                FilledElement = new ConsoleGUI.InterpreterElement($"{TestConstants.TestFilesPath}{file}", settings.Value.compilerSettings, settings.Value.parserSettings, settings.Value.bytecodeInterpreterSettings, settings.Value.HandleErrors)
            };
            return;

            /*
            IngameCoding.CompileIntoFile.Compile(ArgumentParser.Parse(new string[]
            {
                "-throw-errors",
                "-basepath \"../CodeFiles/\"",
                "-dont-optimize",
                "-compile",
                "\".\\output.txt\"",
                "-compiler-type", "readable",
                $"\"{TestConstants.TestFilesPath}{file}\""
            }).Value);
            IngameCoding.Core.EasyInterpreter.RunCompiledFile(ArgumentParser.Parse(new string[]
            {
                "-throw-errors",
                "-basepath \"../CodeFiles/\"",
                "-dont-optimize",
                "-decompile",
                "-compiler-type", "readable",
                "\".\\output.txt\"",
            }).Value);
            IngameCoding.CompileIntoFile.Compile(ArgumentParser.Parse(new string[]
            {
                "-throw-errors",
                "-basepath \"../CodeFiles/\"",
                "-dont-optimize",
                "-compile",
                "\".\\output.bin\"",
                "-compression", "no",
                "-compiler-type", "binary",
                $"\"{TestConstants.TestFilesPath}{file}\""
            }).Value);
            IngameCoding.Core.EasyInterpreter.RunCompiledFile(ArgumentParser.Parse(new string[]
            {
                "-throw-errors",
                "-basepath \"../CodeFiles/\"",
                "-dont-optimize",
                "-decompile",
                "-compiler-type", "binary",
                "\".\\output.bin\"",
            }).Value);
            goto ExitProgram;
            */

            switch (settings.Value.RunType)
            {
                case ArgumentParser.RunType.Debugger:
                    Debugger _ = new(settings.Value);
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
                    IngameCoding.Core.EasyInterpreter.RunCompiledFile(settings.Value);
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

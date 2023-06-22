﻿#define ENABLE_DEBUG
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
            var file = "test16.bbc";

            if (false)
            {
                ConsoleGUI.ConsoleGUI consoleGUI = new()
                {
                    FilledElement = new ConsoleGUI.InterpreterElement($"{TestConstants.TestFilesPath}{file}")
                };
                consoleGUI.Start();

                return true;
            }

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
                 "-console-gui",
                // "\".\\output.bin\"",
                // "-compression", "no",
                "-heap 64",
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
            return true;
            */

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
                    // unused = new Debugger(settings.Value);
                    throw new NotImplementedException();
                    // return true;
                case ArgumentParser.RunType.Tester:
                    IngameCoding.Tester.Tester.RunTestFile(settings.Value);
                    break;
                case ArgumentParser.RunType.Normal:
                    IngameCoding.Core.EasyInterpreter.Run(settings.Value);
                    break;
                case ArgumentParser.RunType.Compile:
                    // IngameCoding.CompileIntoFile.Compile(settings.Value);
                    throw new NotImplementedException();
                case ArgumentParser.RunType.Decompile:
                    // IngameCoding.Core.EasyInterpreter.RunCompiledFile(settings.Value);
                    throw new NotImplementedException();
            }

            return true;
        }
#endif
    }
}

#define ENABLE_DEBUG
#define RELEASE_TEST_

using System;
using System.Diagnostics;
using System.IO;

namespace TheProgram
{
    public static class DevelopmentEntry
    {
#if (!DEBUG || !ENABLE_DEBUG) && !RELEASE_TEST
        public static bool Start(string[] args) => false;
#else
        public static bool Start(string[] args)
        {
            string[] generatedArgs = Array.Empty<string>();

#if DEBUG && ENABLE_DEBUG

            //string path = TestConstants.ExampleFilesPath + "hello-world.bbc";
            string path = TestConstants.TestFilesPath + "test-win32.bbc";

            generatedArgs = new string[]
            {
                // "--throw-errors",
                "--basepath \"../CodeFiles/\"",
                // "--hide-debug",
                "--hide-system",
                // "--dont-optimize",
                "--console-gui",
                // "--brainfuck",
                // "--no-nullcheck",
                "--heap-size 2048",
                $"\"{path}\""
            };
#endif
#if RELEASE_TEST
            generatedArgs = new string[]
            {
                "\"D:\\Program Files\\BBCodeProject\\BBCode\\TestFiles\\helloworld.bbc\""
            };
#endif

            string[] concatenatedArgs = new string[args.Length + generatedArgs.Length];
            args.CopyTo(concatenatedArgs, 0);
            generatedArgs.CopyTo(concatenatedArgs, args.Length);

            if (!ArgumentParser.Parse(out ArgumentParser.Settings settings, concatenatedArgs)) return true;

            switch (settings.RunType)
            {
                case ArgumentParser.RunType.Debugger:
                    _ = new Debugger(settings);
                    break;
                case ArgumentParser.RunType.Normal:
                    if (settings.ConsoleGUI)
                    {
                        ConsoleGUI.ConsoleGUI gui = new()
                        {
                            FilledElement = new ConsoleGUI.InterpreterElement(path, settings.compilerSettings, settings.bytecodeInterpreterSettings, settings.HandleErrors, settings.BasePath)
                        };
                        while (!gui.Destroyed)
                        { gui.Tick(); }
                    }
                    else
                    {
                        LanguageCore.Runtime.EasyInterpreter.Run(settings);
                    }
                    break;
                case ArgumentParser.RunType.Compile:
                    LanguageCore.BBCode.EasyCompiler.Result yeah = LanguageCore.BBCode.EasyCompiler.Compile(new FileInfo(path), new System.Collections.Generic.Dictionary<string, LanguageCore.Runtime.ExternalFunctionBase>(), LanguageCore.Tokenizing.TokenizerSettings.Default, settings.compilerSettings, null, settings.BasePath);
                    LanguageCore.Runtime.Instruction[] yeahCode = yeah.CodeGeneratorResult.Code;
                    File.WriteAllBytes(settings.CompileOutput ?? string.Empty, DataUtilities.Serializer.SerializerStatic.Serialize(yeahCode));
                    break;
                case ArgumentParser.RunType.Brainfuck:
                    {
                        Brainfuck.PrintFlags printFlags = Brainfuck.PrintFlags.PrintMemory;

                        Brainfuck.ProgramUtils.CompileOptions compileOptions;
                        if (settings.compilerSettings.PrintInstructions)
                        { compileOptions = Brainfuck.ProgramUtils.CompileOptions.PrintCompiledMinimized; }
                        else
                        { compileOptions = Brainfuck.ProgramUtils.CompileOptions.None; }

                        if (settings.ConsoleGUI)
                        { Brainfuck.ProgramUtils.Run(settings, Brainfuck.RunKind.UI, printFlags, compileOptions); }
                        else
                        { Brainfuck.ProgramUtils.Run(settings, Brainfuck.RunKind.Default, printFlags, compileOptions); }
                        break;
                    }
                case ArgumentParser.RunType.IL:
                    {
                        LanguageCore.Tokenizing.Token[] tokens = LanguageCore.Tokenizing.Tokenizer.Tokenize(File.ReadAllText(settings.File.FullName), settings.File.FullName);

                        LanguageCore.Parser.ParserResult ast = LanguageCore.Parser.Parser.Parse(tokens);

                        LanguageCore.BBCode.Compiler.Compiler.Result compiled = LanguageCore.BBCode.Compiler.Compiler.Compile(ast, new System.Collections.Generic.Dictionary<string, LanguageCore.Runtime.ExternalFunctionBase>(), settings.File, null, settings.BasePath);

                        LanguageCore.IL.Compiler.CodeGenerator.Result code = LanguageCore.IL.Compiler.CodeGenerator.Generate(compiled, settings.compilerSettings, default, null);

                        System.Reflection.Assembly assembly = code.Assembly;

                        break;
                    }
                case ArgumentParser.RunType.ASM:
                    {
                        LanguageCore.Tokenizing.Token[] tokens = LanguageCore.Tokenizing.Tokenizer.Tokenize(File.ReadAllText(settings.File.FullName), settings.File.FullName);

                        LanguageCore.Parser.ParserResult ast = LanguageCore.Parser.Parser.Parse(tokens);

                        LanguageCore.BBCode.Compiler.Compiler.Result compiled = LanguageCore.BBCode.Compiler.Compiler.Compile(ast, new System.Collections.Generic.Dictionary<string, LanguageCore.Runtime.ExternalFunctionBase>(), settings.File, null, settings.BasePath);

                        LanguageCore.ASM.Compiler.CodeGenerator.Result code = LanguageCore.ASM.Compiler.CodeGenerator.Generate(compiled, settings.compilerSettings, default, null);

                        const string OutputFile = @"C:\Users\bazsi\Desktop\ehs";

                        LanguageCore.ASM.Assembler.Assemble(code.AssemblyCode, OutputFile);

                        if (File.Exists(OutputFile + ".exe"))
                        {
                            Process? process = Process.Start(new ProcessStartInfo(OutputFile + ".exe"));
                            process?.WaitForExit();
                            Console.WriteLine();
                            Console.WriteLine($"Exit code: {process?.ExitCode}");
                        }

                        /*
                        const string nasm = @"C:\Program Files\mingw64\bin\nasm.exe";
                        const string masm = @"C:\Program Files\mingw64\bin\nasm.exe";
                        const string ld = @"C:\Program Files\mingw64\bin\x86_64-w64-mingw32-gcc.exe";
                        const string link = @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Tools\MSVC\14.37.32822\bin\Hostx64\x64\link.exe";
                        const string vcvarsall = @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvarsall.bat";

                        Process assembling = Process.Start(new ProcessStartInfo(nasm, $"-fwin64 {OutputFileAsm} -o {OutputFileObject}"));
                        assembling.WaitForExit();
                        System.Threading.Thread.Sleep(1000);
                        Process linking = Process.Start(new ProcessStartInfo(ld, $"-e WinMain -o {OutputFileExe} {OutputFileObject}"));
                        // Process linking = Process.Start(new ProcessStartInfo(link, $"/subsystem:console /nodefaultlib /entry:start {OutputFileObject}"));
                        linking.WaitForExit();
                        if (System.IO.File.Exists(OutputFileExe))
                        {
                            System.Threading.Thread.Sleep(1000);
                            Process executing = Process.Start(new ProcessStartInfo(OutputFileExe));
                            executing.WaitForExit();
                            Console.WriteLine(executing.ExitCode);
                        }
                        */

                        break;
                    }
                default: throw new NotImplementedException();
            }

            if (settings.PauseAtEnd)
            {
                System.Console.WriteLine();
                System.Console.WriteLine();
                System.Console.WriteLine("Press any key to exit");
                System.Console.ReadKey();
            }

            return true;
        }
#endif
    }
}

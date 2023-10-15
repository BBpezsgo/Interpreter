#define ENABLE_DEBUG
#define RELEASE_TEST_

using System;
using System.Diagnostics;

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

            //string path = TestConstants.ExampleFilesPath + "hello-world.bbc";
            string path = TestConstants.TestFilesPath + "test26.bbc";

            if (args.Length == 0) args = new string[]
            {
                // "-throw-errors",
                "-basepath \"../CodeFiles/\"",
                // "-c-print-instructions true",
                // "-c-remove-unused-functions 5",
                // "-hide-debug",
                "-hide-system",
                //"-c-generate-comments false",
                // "-no-debug-info",
                // "-dont-optimize",
                // "-console-gui",
                "-brainfuck",
                "-heap 2048",
                "-bc-instruction-limit " + int.MaxValue.ToString(),
                $"\"{path}\""
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
                        FilledElement = new ConsoleGUI.InterpreterElement(path, settings.Value.compilerSettings, settings.Value.parserSettings, settings.Value.bytecodeInterpreterSettings, settings.Value.HandleErrors, settings.Value.BasePath)
                    };
                    while (!gui.Destroyed)
                    { gui.Tick(); }
                    return true;
                case ArgumentParser.RunType.Debugger:
                    _ = new Debugger(settings.Value);
                    break;
                case ArgumentParser.RunType.Normal:
                    LanguageCore.Runtime.EasyInterpreter.Run(settings.Value);
                    break;
                case ArgumentParser.RunType.Compile:
                    LanguageCore.BBCode.EasyCompiler.Result yeah = LanguageCore.BBCode.EasyCompiler.Compile(new System.IO.FileInfo(path), new System.Collections.Generic.Dictionary<string, LanguageCore.Runtime.ExternalFunctionBase>(), LanguageCore.Tokenizing.TokenizerSettings.Default, settings.Value.parserSettings, settings.Value.compilerSettings, null, settings.Value.BasePath);
                    LanguageCore.Runtime.Instruction[] yeahCode = yeah.CodeGeneratorResult.Code;
                    System.IO.File.WriteAllBytes(settings.Value.CompileOutput, DataUtilities.Serializer.SerializerStatic.Serialize(yeahCode));
                    break;
                case ArgumentParser.RunType.Decompile:
                    throw new NotImplementedException();
                case ArgumentParser.RunType.Brainfuck:
                    Brainfuck.ProgramUtils.Run(settings.Value, Brainfuck.RunKind.UI, Brainfuck.PrintFlags.PrintMemory, Brainfuck.ProgramUtils.CompileOptions.PrintCompiledMinimized);
                    break;
                case ArgumentParser.RunType.IL:
                    {
                        LanguageCore.Tokenizing.Tokenizer tokenizer = new(LanguageCore.Tokenizing.TokenizerSettings.Default, null); ;
                        LanguageCore.Tokenizing.Token[] tokens = tokenizer.Parse(System.IO.File.ReadAllText(settings.Value.File.FullName));

                        LanguageCore.Parser.ParserResult ast = LanguageCore.Parser.Parser.Parse(tokens);

                        LanguageCore.BBCode.Compiler.Compiler.Result compiled = LanguageCore.BBCode.Compiler.Compiler.Compile(ast, new System.Collections.Generic.Dictionary<string, LanguageCore.Runtime.ExternalFunctionBase>(), settings.Value.File, LanguageCore.Parser.ParserSettings.Default, null, settings.Value.BasePath);

                        LanguageCore.IL.Compiler.CodeGenerator.Result code = LanguageCore.IL.Compiler.CodeGenerator.Generate(compiled, settings.Value.compilerSettings, default, null);

                        System.Reflection.Assembly assembly = code.Assembly;

                        break;
                    }
                case ArgumentParser.RunType.ASM:
                    {
                        LanguageCore.Tokenizing.Tokenizer tokenizer = new(LanguageCore.Tokenizing.TokenizerSettings.Default, null); ;
                        LanguageCore.Tokenizing.Token[] tokens = tokenizer.Parse(System.IO.File.ReadAllText(settings.Value.File.FullName));

                        LanguageCore.Parser.ParserResult ast = LanguageCore.Parser.Parser.Parse(tokens);

                        LanguageCore.BBCode.Compiler.Compiler.Result compiled = LanguageCore.BBCode.Compiler.Compiler.Compile(ast, new System.Collections.Generic.Dictionary<string, LanguageCore.Runtime.ExternalFunctionBase>(), settings.Value.File, LanguageCore.Parser.ParserSettings.Default, null, settings.Value.BasePath);

                        LanguageCore.ASM.Compiler.CodeGenerator.Result code = LanguageCore.ASM.Compiler.CodeGenerator.Generate(compiled, settings.Value.compilerSettings, default, null);

                        const string OutputFile = @"C:\Users\bazsi\Desktop\ehs";

                        LanguageCore.ASM.Assembler.Assemble(code.AssemblyCode, OutputFile);

                        if (File.Exists(OutputFile + ".exe"))
                        {
                            Process process = Process.Start(new ProcessStartInfo(OutputFile + ".exe"));
                            process.WaitForExit();
                            Console.WriteLine();
                            Console.WriteLine($"Exit code: {process.ExitCode}");
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
            }

            return true;
        }
#endif
    }
}

using System;
using System.Diagnostics;
using System.IO;
using LanguageCore.BBCode.Generator;
using LanguageCore.Parser;

namespace TheProgram
{
    public static class Entry
    {
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="NotImplementedException"/>
        public static void Run(ArgumentParser.Settings arguments)
        {
            switch (arguments.RunType)
            {
                case ArgumentParser.RunType.Debugger:
#if AOT
                    throw new NotSupportedException($"The compiler compiled in AOT mode so System.Text.Json isn't available");
#else
                    _ = new Debugger(arguments);
                    break;
#endif
                case ArgumentParser.RunType.Normal:
                    if (arguments.ConsoleGUI)
                    {
                        ConsoleGUI.ConsoleGUI gui = new()
                        {
                            FilledElement = new ConsoleGUI.InterpreterElement(arguments.File.FullName, arguments.compilerSettings, arguments.bytecodeInterpreterSettings, arguments.HandleErrors)
                        };
                        while (!gui.Destroyed)
                        { gui.Tick(); }
                    }
                    else
                    {
                        LanguageCore.Runtime.EasyInterpreter.Run(arguments);
                    }
                    break;
                case ArgumentParser.RunType.Compile:
                    {
                        LanguageCore.Compiler.CompilerResult compiled = LanguageCore.Compiler.Compiler.Compile(Parser.ParseFile(arguments.File.FullName), null, arguments.File, arguments.compilerSettings.BasePath);
                        BBCodeGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, arguments.compilerSettings);
                        File.WriteAllBytes(arguments.CompileOutput ?? string.Empty, DataUtilities.Serializer.SerializerStatic.Serialize(generatedCode.Code));
                        break;
                    }
                case ArgumentParser.RunType.Brainfuck:
                    {
                        Brainfuck.PrintFlags printFlags = Brainfuck.PrintFlags.PrintMemory;

                        Brainfuck.BrainfuckRunner.CompileOptions compileOptions;
                        if (arguments.compilerSettings.PrintInstructions)
                        { compileOptions = Brainfuck.BrainfuckRunner.CompileOptions.PrintCompiledMinimized; }
                        else
                        { compileOptions = Brainfuck.BrainfuckRunner.CompileOptions.None; }

                        if (arguments.ConsoleGUI)
                        { Brainfuck.BrainfuckRunner.Run(arguments, Brainfuck.RunKind.UI, printFlags, compileOptions); }
                        else
                        { Brainfuck.BrainfuckRunner.Run(arguments, Brainfuck.RunKind.Default, printFlags, compileOptions); }
                        break;
                    }
                case ArgumentParser.RunType.IL:
                    {
#if AOT
                        throw new NotSupportedException($"The compiler compiled in AOT mode so IL generation isn't available");
#else
                        LanguageCore.Tokenizing.Token[] tokens = LanguageCore.Tokenizing.StringTokenizer.Tokenize(File.ReadAllText(arguments.File.FullName));

                        LanguageCore.Parser.ParserResult ast = LanguageCore.Parser.Parser.Parse(tokens);

                        LanguageCore.Compiler.CompilerResult compiled = LanguageCore.Compiler.Compiler.Compile(ast, null, arguments.File, null);

                        LanguageCore.IL.Generator.ILGeneratorResult code = LanguageCore.IL.Generator.CodeGeneratorForIL.Generate(compiled, arguments.compilerSettings, default, null);

                        System.Reflection.Assembly assembly = code.Assembly;
                        break;
#endif
                    }
                case ArgumentParser.RunType.ASM:
                    {
                        LanguageCore.Tokenizing.Token[] tokens = LanguageCore.Tokenizing.StringTokenizer.Tokenize(File.ReadAllText(arguments.File.FullName));

                        LanguageCore.Parser.ParserResult ast = LanguageCore.Parser.Parser.Parse(tokens);

                        LanguageCore.Compiler.CompilerResult compiled = LanguageCore.Compiler.Compiler.Compile(ast, null, arguments.File, null);

                        LanguageCore.ASM.Generator.CodeGeneratorForAsm.Result code = LanguageCore.ASM.Generator.CodeGeneratorForAsm.Generate(compiled, arguments.compilerSettings, default, null);

                        string? fileDirectoryPath = arguments.File.DirectoryName;
                        string fileNameNoExt = Path.GetFileNameWithoutExtension(arguments.File.Name);

                        fileDirectoryPath ??= ".\\";

                        string outputFile = Path.Combine(fileDirectoryPath, fileNameNoExt);

                        LanguageCore.ASM.Assembler.Assemble(code.AssemblyCode, outputFile);

                        if (File.Exists(outputFile + ".exe"))
                        {
                            Process? process = Process.Start(new ProcessStartInfo(outputFile + ".exe"));
                            if (process == null)
                            { throw new Exception($"Failed to start process \"{outputFile + ".exe"}\""); }
                            process.WaitForExit();
                            Console.WriteLine();
                            Console.WriteLine($"Exit code: {process.ExitCode}");

                            if (LanguageCore.ProcessRuntimeException.TryGetFromExitCode(process.ExitCode, out var runtimeException))
                            { throw runtimeException; }
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
                default: throw new NotImplementedException($"Mode \"{arguments.RunType}\" isn't implemented for some reason");
            }
        }
    }
}

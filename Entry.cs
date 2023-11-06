using System;
using System.Diagnostics;
using System.IO;

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
                            FilledElement = new ConsoleGUI.InterpreterElement(arguments.File.FullName, arguments.compilerSettings, arguments.bytecodeInterpreterSettings, arguments.HandleErrors, arguments.BasePath)
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
                    LanguageCore.BBCode.EasyCompiler.Result yeah = LanguageCore.BBCode.EasyCompiler.Compile(arguments.File, new System.Collections.Generic.Dictionary<string, LanguageCore.Runtime.ExternalFunctionBase>(), LanguageCore.Tokenizing.TokenizerSettings.Default, arguments.compilerSettings, null, arguments.BasePath);
                    LanguageCore.Runtime.Instruction[] yeahCode = yeah.CodeGeneratorResult.Code;
                    File.WriteAllBytes(arguments.CompileOutput ?? string.Empty, DataUtilities.Serializer.SerializerStatic.Serialize(yeahCode));
                    break;
                case ArgumentParser.RunType.Brainfuck:
                    {
                        Brainfuck.PrintFlags printFlags = Brainfuck.PrintFlags.PrintMemory;

                        Brainfuck.ProgramUtils.CompileOptions compileOptions;
                        if (arguments.compilerSettings.PrintInstructions)
                        { compileOptions = Brainfuck.ProgramUtils.CompileOptions.PrintCompiledMinimized; }
                        else
                        { compileOptions = Brainfuck.ProgramUtils.CompileOptions.None; }

                        if (arguments.ConsoleGUI)
                        { Brainfuck.ProgramUtils.Run(arguments, Brainfuck.RunKind.UI, printFlags, compileOptions); }
                        else
                        { Brainfuck.ProgramUtils.Run(arguments, Brainfuck.RunKind.Default, printFlags, compileOptions); }
                        break;
                    }
                case ArgumentParser.RunType.IL:
                    {
#if AOT
                        throw new NotSupportedException($"The compiler compiled in AOT mode so IL generation isn't available");
#else
                        LanguageCore.Tokenizing.Token[] tokens = LanguageCore.Tokenizing.Tokenizer.Tokenize(File.ReadAllText(arguments.File.FullName), arguments.File.FullName);

                        LanguageCore.Parser.ParserResult ast = LanguageCore.Parser.Parser.Parse(tokens);

                        LanguageCore.BBCode.Compiler.Compiler.Result compiled = LanguageCore.BBCode.Compiler.Compiler.Compile(ast, new System.Collections.Generic.Dictionary<string, LanguageCore.Runtime.ExternalFunctionBase>(), arguments.File, null, arguments.BasePath);

                        LanguageCore.IL.Compiler.CodeGenerator.Result code = LanguageCore.IL.Compiler.CodeGenerator.Generate(compiled, arguments.compilerSettings, default, null);

                        System.Reflection.Assembly assembly = code.Assembly;
                        break;
#endif
                    }
                case ArgumentParser.RunType.ASM:
                    {
                        LanguageCore.Tokenizing.Token[] tokens = LanguageCore.Tokenizing.Tokenizer.Tokenize(File.ReadAllText(arguments.File.FullName), arguments.File.FullName);

                        LanguageCore.Parser.ParserResult ast = LanguageCore.Parser.Parser.Parse(tokens);

                        LanguageCore.BBCode.Compiler.Compiler.Result compiled = LanguageCore.BBCode.Compiler.Compiler.Compile(ast, new System.Collections.Generic.Dictionary<string, LanguageCore.Runtime.ExternalFunctionBase>(), arguments.File, null, arguments.BasePath);

                        LanguageCore.ASM.Compiler.CodeGenerator.Result code = LanguageCore.ASM.Compiler.CodeGenerator.Generate(compiled, arguments.compilerSettings, default, null);

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
                default: throw new NotImplementedException($"Mode \"{arguments.RunType}\" isn't implemented for some reason");
            }
        }
    }
}

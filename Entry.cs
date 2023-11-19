using System;
using System.Diagnostics;
using System.IO;

namespace TheProgram
{
    using LanguageCore.ASM.Generator;
    using LanguageCore.BBCode.Generator;
    using LanguageCore.Brainfuck;
    using LanguageCore.Compiler;
    using LanguageCore.IL.Generator;
    using LanguageCore.Parser;

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
                        CompilerResult compiled = Compiler.Compile(Parser.ParseFile(arguments.File.FullName), null, arguments.File, arguments.compilerSettings.BasePath);
                        BBCodeGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, arguments.compilerSettings);
                        File.WriteAllBytes(arguments.CompileOutput ?? string.Empty, DataUtilities.Serializer.SerializerStatic.Serialize(generatedCode.Code));
                        break;
                    }
                case ArgumentParser.RunType.Brainfuck:
                    {
                        BrainfuckPrintFlags printFlags = BrainfuckPrintFlags.PrintMemory;

                        EasyBrainfuckCompilerFlags compileOptions;
                        if (arguments.compilerSettings.PrintInstructions)
                        { compileOptions = EasyBrainfuckCompilerFlags.PrintCompiledMinimized; }
                        else
                        { compileOptions = EasyBrainfuckCompilerFlags.None; }

                        if (arguments.ConsoleGUI)
                        { BrainfuckRunner.Run(arguments, BrainfuckRunKind.UI, printFlags, compileOptions); }
                        else
                        { BrainfuckRunner.Run(arguments, BrainfuckRunKind.Default, printFlags, compileOptions); }
                        break;
                    }
                case ArgumentParser.RunType.IL:
                    {
#if AOT
                        throw new NotSupportedException($"The compiler compiled in AOT mode so IL generation isn't available");
#else
                        CompilerResult compiled = Compiler.Compile(Parser.ParseFile(arguments.File.FullName), null, arguments.File, null);

                        ILGeneratorResult code = CodeGeneratorForIL.Generate(compiled, arguments.compilerSettings, default, null);

                        System.Reflection.Assembly assembly = code.Assembly;
                        break;
#endif
                    }
                case ArgumentParser.RunType.ASM:
                    {
                        CompilerResult compiled = Compiler.Compile(Parser.ParseFile(arguments.File.FullName), null, arguments.File, null);

                        AsmGeneratorResult code = CodeGeneratorForAsm.Generate(compiled, default, null);

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

                            if (LanguageCore.ProcessRuntimeException.TryGetFromExitCode(process.ExitCode, out LanguageCore.ProcessRuntimeException? runtimeException))
                            { throw runtimeException; }
                        }

                        break;
                    }
                default: throw new NotImplementedException($"Mode \"{arguments.RunType}\" isn't implemented for some reason");
            }
        }
    }
}

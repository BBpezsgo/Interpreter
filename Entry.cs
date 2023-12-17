using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace TheProgram
{
    using LanguageCore;
    using LanguageCore.ASM.Generator;
    using LanguageCore.BBCode.Generator;
    using LanguageCore.Brainfuck;
    using LanguageCore.Compiler;
#if !AOT
    using LanguageCore.IL.Generator;
#endif
    using LanguageCore.Parser;

    public static class Entry
    {
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="NotImplementedException"/>
        public static void Run(ArgumentParser.Settings arguments)
        {
            if (arguments.IsEmpty)
            {
                new Interactive().Run();
                return;
            }

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
                {
                    if (arguments.ConsoleGUI)
                    {
                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        { throw new PlatformNotSupportedException($"Console rendering is only supported on Windows"); }

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
                }
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

                    ILGeneratorResult generated = CodeGeneratorForIL.Generate(compiled, arguments.compilerSettings, default, null);

                    generated.Invoke();
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

                    outputFile += ".exe";

                    if (File.Exists(outputFile))
                    {
                        Process? process = Process.Start(new ProcessStartInfo(outputFile));
                        if (process == null)
                        { throw new InternalException($"Failed to start process \"{outputFile}\""); }
                        process.WaitForExit();
                        Console.WriteLine();
                        Console.WriteLine($"Exit code: {process.ExitCode}");

                        if (ProcessRuntimeException.TryGetFromExitCode(process.ExitCode, out LanguageCore.ProcessRuntimeException? runtimeException))
                        { throw runtimeException; }
                    }

                    break;
                }
                default: throw new NotImplementedException($"Mode \"{arguments.RunType}\" isn't implemented for some reason");
            }
        }
    }
}

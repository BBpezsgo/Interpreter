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
        public static void Run(ProgramArguments arguments)
        {
            if (arguments.IsEmpty)
            {
                new Interactive().Run();
                return;
            }

            switch (arguments.RunType)
            {
                case ProgramRunType.Normal:
                {
                    if (arguments.ConsoleGUI)
                    {
                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        { throw new PlatformNotSupportedException($"Console rendering is only supported on Windows"); }

                        ConsoleGUI.ConsoleGUI gui = new()
                        {
                            FilledElement = new ConsoleGUI.InterpreterElement(arguments.File.FullName, arguments.CompilerSettings, arguments.BytecodeInterpreterSettings, arguments.HandleErrors)
                        };
                        while (!gui.Destroyed)
                        { gui.Tick(); }
                    }
                    else
                    {
                        Output.LogDebug($"Executing file \"{arguments.File.FullName}\" ...");
                        LanguageCore.Runtime.Interpreter interpreter = new();

                        interpreter.OnStdOut += (sender, data) => Output.Write(data);
                        interpreter.OnStdError += (sender, data) => Output.WriteError(data);

                        interpreter.OnOutput += (_, message, logType) => Output.Log(message, logType);

                        interpreter.OnNeedInput += (sender) =>
                        {
                            ConsoleKeyInfo input = Console.ReadKey(true);
                            sender.OnInput(input.KeyChar);
                        };

#if DEBUG
                        interpreter.OnExecuted += (sender) =>
                        {
                            if (sender.BytecodeInterpreter == null) return;

                            Console.WriteLine();
                            Console.WriteLine($" ===== HEAP ===== ");
                            Console.WriteLine();

                            sender.BytecodeInterpreter.Memory.Heap.DebugPrint();

                            if (sender.BytecodeInterpreter.Memory.Stack.Count > 0)
                            {
                                Console.WriteLine();
                                Console.WriteLine($" ===== STACK ===== ");
                                Console.WriteLine();

                                for (int i = 0; i < sender.BytecodeInterpreter.Memory.Stack.Count; i++)
                                {
                                    sender.BytecodeInterpreter.Memory.Stack[i].DebugPrint();
                                    Console.WriteLine();
                                }
                            }
                        };
#endif

                        ExternalFunctionCollection externalFunctions = new();
                        interpreter.GenerateExternalFunctions(externalFunctions);

#if AOT
                        Output.LogDebug($"Skipping loading DLL-s because the compiler compiled in AOT mode");
#else
                        string dllsFolderPath = Path.Combine(arguments.File.Directory!.FullName, arguments.CompilerSettings.BasePath?.Replace('/', '\\') ?? string.Empty);
                        if (Directory.Exists(dllsFolderPath))
                        {
                            DirectoryInfo dllsFolder = new(dllsFolderPath);
                            Output.LogDebug($"Load DLLs from \"{dllsFolder.FullName}\" ...");
                            FileInfo[] dlls = dllsFolder.GetFiles("*.dll");
                            foreach (FileInfo dll in dlls)
                            { interpreter.LoadDLL(externalFunctions, dll.FullName); }
                        }
                        else
                        {
                            Output.LogWarning($"Folder \"{dllsFolderPath}\" doesn't exists!");
                        }
#endif

                        BBCodeGeneratorResult generatedCode;

                        if (arguments.HandleErrors)
                        {
                            try
                            {
                                CompilerResult compiled = Compiler.Compile(Parser.ParseFile(arguments.File.FullName), externalFunctions, arguments.File, arguments.CompilerSettings.BasePath, Output.Log);
                                generatedCode = CodeGeneratorForMain.Generate(compiled, arguments.CompilerSettings, Output.Log);

                                generatedCode.ThrowErrors();
                                generatedCode.Print(Output.Log);
                            }
                            catch (Exception ex)
                            {
                                Output.LogError(ex.ToString());
                                return;
                            }
                        }
                        else
                        {
                            CompilerResult compiled = Compiler.Compile(Parser.ParseFile(arguments.File.FullName), externalFunctions, arguments.File, arguments.CompilerSettings.BasePath, Output.Log);
                            generatedCode = CodeGeneratorForMain.Generate(compiled, arguments.CompilerSettings, Output.Log);

                            generatedCode.ThrowErrors();
                            generatedCode.Print(Output.Log);
                        }

                        interpreter.CompilerResult = generatedCode;
                        interpreter.Initialize(generatedCode.Code, arguments.BytecodeInterpreterSettings, externalFunctions);

                        while (interpreter.IsExecutingCode)
                        {
                            interpreter.Update();
                        }
                    }
                    break;
                }
                case ProgramRunType.Brainfuck:
                {
                    BrainfuckPrintFlags printFlags = BrainfuckPrintFlags.PrintMemory;

                    EasyBrainfuckCompilerFlags compileOptions;
                    if (arguments.CompilerSettings.PrintInstructions)
                    { compileOptions = EasyBrainfuckCompilerFlags.PrintCompiledMinimized; }
                    else
                    { compileOptions = EasyBrainfuckCompilerFlags.None; }

                    if (arguments.ConsoleGUI)
                    { BrainfuckRunner.Run(arguments, BrainfuckRunKind.UI, printFlags, compileOptions); }
                    else
                    { BrainfuckRunner.Run(arguments, BrainfuckRunKind.Default, printFlags, compileOptions); }
                    break;
                }
                case ProgramRunType.IL:
                {
#if AOT
                    throw new NotSupportedException($"The compiler compiled in AOT mode so IL generation isn't available");
#else
                    CompilerResult compiled = Compiler.Compile(Parser.ParseFile(arguments.File.FullName), null, arguments.File, null);

                    ILGeneratorResult generated = CodeGeneratorForIL.Generate(compiled, arguments.CompilerSettings, default, null);

                    generated.Invoke();
                    break;
#endif
                }
                case ProgramRunType.ASM:
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

                        if (ProcessRuntimeException.TryGetFromExitCode(process.ExitCode, out ProcessRuntimeException? runtimeException))
                        { throw runtimeException; }
                    }

                    break;
                }
                default: throw new NotImplementedException($"Mode \"{arguments.RunType}\" isn't implemented for some reason");
            }
        }
    }
}

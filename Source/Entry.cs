using System.IO;
using System.Runtime.InteropServices;

namespace LanguageCore;

using ASM.Generator;
using BBCode.Generator;
using Brainfuck;
using Brainfuck.Generator;
using Compiler;
using Runtime;
using Tokenizing;

public static class Entry
{
    /// <exception cref="NotSupportedException"/>
    /// <exception cref="NotImplementedException"/>
    public static void Run(ProgramArguments arguments)
    {
        Output.SetProgramArguments(arguments);
        ConsoleProgress.SetProgramArguments(arguments);

        if (arguments.IsEmpty)
        {
            new Interactive.Interactive().Run();
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
                        FilledElement = new ConsoleGUI.InterpreterElement(arguments.File.FullName, arguments.CompilerSettings, arguments.BytecodeInterpreterSettings, !arguments.ThrowErrors)
                    };
                    while (!gui.Destroyed)
                    { gui.Tick(); }
                }
                else
                {
                    Output.LogDebug($"Executing file \"{arguments.File.FullName}\" ...");
                    Runtime.Interpreter interpreter = new();

                    interpreter.OnStdOut += (sender, data) => Output.Write(char.ToString(data));
                    interpreter.OnStdError += (sender, data) => Output.WriteError(char.ToString(data));

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

                    Dictionary<string, ExternalFunctionBase> externalFunctions = new();
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
                        { externalFunctions.LoadAssembly(dll.FullName); }
                    }
                    else
                    {
                        Output.LogWarning($"Folder \"{dllsFolderPath}\" doesn't exists!");
                    }
#endif

                    BBCodeGeneratorResult generatedCode;
                    AnalysisCollection analysisCollection = new();

                    if (!arguments.ThrowErrors)
                    {
                        try
                        {
                            CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.File, externalFunctions, arguments.CompilerSettings, Output.Log, analysisCollection);
                            generatedCode = CodeGeneratorForMain.Generate(compiled, arguments.GeneratorSettings, Output.Log, analysisCollection);
                            analysisCollection.Throw();
                            analysisCollection.Print();
                        }
                        catch (LanguageException ex)
                        {
                            analysisCollection.Print();
                            Output.LogError(ex);
                            return;
                        }
                        catch (Exception ex)
                        {
                            analysisCollection.Print();
                            Output.LogError(ex);
                            return;
                        }
                    }
                    else
                    {
                        CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.File, externalFunctions, arguments.CompilerSettings, Output.Log, analysisCollection);
                        generatedCode = CodeGeneratorForMain.Generate(compiled, arguments.GeneratorSettings, Output.Log, analysisCollection);
                        analysisCollection.Throw();
                        analysisCollection.Print();
                    }

                    if (arguments.GeneratorSettings.PrintInstructions)
                    {
                        for (int i = 0; i < generatedCode.Code.Length; i++)
                        {
                            Instruction instruction = generatedCode.Code[i];

                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.Write(instruction.Opcode);
                            Console.ResetColor();
                            Console.Write(' ');

                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.Write(instruction.AddressingMode);
                            Console.ResetColor();
                            Console.Write(' ');

                            instruction.Parameter.DebugPrint();

                            Console.WriteLine();
                        }
                    }

                    interpreter.CompilerResult = generatedCode;
                    interpreter.Initialize(generatedCode.Code, arguments.BytecodeInterpreterSettings, externalFunctions);

                    while (interpreter.IsExecutingCode)
                    {
                        interpreter.Update();
                    }
                    Console.ResetColor();
                }
                break;
            }
            case ProgramRunType.Brainfuck:
            {
                Output.LogDebug($"Executing file \"{arguments.File.FullName}\" ...");

                BrainfuckGeneratorResult generated;
                Token[] tokens;
                BrainfuckGeneratorSettings generatorSettings = arguments.BrainfuckGeneratorSettings;

                AnalysisCollection analysisCollection = new();
                if (arguments.ThrowErrors)
                {
                    tokens = StreamTokenizer.Tokenize(arguments.File!.FullName);
                    CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.File, null, arguments.CompilerSettings, Output.Log, analysisCollection);
                    generated = CodeGeneratorForBrainfuck.Generate(compiled, generatorSettings, Output.Log, analysisCollection);
                    analysisCollection.Throw();
                    analysisCollection.Print();
                    Output.LogDebug($"Optimized {generated.Optimizations} statements");
                }
                else
                {
                    try
                    {
                        tokens = StreamTokenizer.Tokenize(arguments.File!.FullName);
                        CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.File!, null, arguments.CompilerSettings, Output.Log, analysisCollection);
                        generated = CodeGeneratorForBrainfuck.Generate(compiled, generatorSettings, Output.Log, analysisCollection);
                        analysisCollection.Throw();
                        analysisCollection.Print();
                        Output.LogDebug($"Optimized {generated.Optimizations} statements");
                    }
                    catch (LanguageException exception)
                    {
                        analysisCollection.Print();
                        Output.LogError(exception);
                        return;
                    }
                    catch (Exception exception)
                    {
                        analysisCollection.Print();
                        Output.LogError(exception);
                        return;
                    }
                }

                bool pauseBeforeRun = false;

                if (arguments.InstructionPrintFlags.HasFlag(InstructionPrintFlags.Commented))
                {
                    Output.WriteLine();
                    Output.WriteLine($" === COMPILED ===");
                    BrainfuckCode.PrintCode(Simplifier.Simplify(generated.Code));
                    Output.WriteLine();

                    pauseBeforeRun = true;
                }

                generated.Code = BrainfuckCode.RemoveNoncodes(generated.Code, true);

                Output.LogDebug($"Minify code ...");
                int prevCodeLength = generated.Code.Length;
                generated.Code = Minifier.Minify(generated.Code);
                Output.LogDebug($"Minification: {prevCodeLength} -> {generated.Code.Length} ({((float)generated.Code.Length - prevCodeLength) / (float)generated.Code.Length * 100f:#}%)");

                if (arguments.InstructionPrintFlags.HasFlag(InstructionPrintFlags.Final))
                {
                    Output.WriteLine();
                    Output.WriteLine($" === FINAL ===");
                    Output.WriteLine();
                    BrainfuckCode.PrintCode(generated.Code);
                    Output.WriteLine();

                    pauseBeforeRun = true;
                }

                if (arguments.InstructionPrintFlags.HasFlag(InstructionPrintFlags.Simplified))
                {
                    Output.WriteLine();
                    Output.WriteLine($" === SIMPLIFIED ===");
                    Output.WriteLine();
                    BrainfuckCode.PrintCode(Simplifier.Simplify(generated.Code));
                    Output.WriteLine();

                    pauseBeforeRun = true;
                }

                /*
                Output.WriteLine();
                Output.WriteLine($" === COMPACTED ===");
                Output.WriteLine();
                BrainfuckCode.PrintCode(string.Join(null, CompactCode.Generate(generated.Code, false, null)));
                Output.WriteLine();
                */

                if (arguments.OutputFile is not null)
                {
                    Output.WriteLine($"Writing to \"{arguments.OutputFile}\" ...");
                    // string compiledFilePath = Path.Combine(Path.GetDirectoryName(arguments.File!.FullName) ?? throw new InternalException($"Failed to get directory name of file \"{arguments.File!.FullName}\""), Path.GetFileNameWithoutExtension(arguments.File!.FullName) + ".bf");
                    File.WriteAllText(arguments.OutputFile, generated.Code);
                }

                InterpreterCompact interpreter = new()
                {
                    DebugInfo = generated.DebugInfo,
                };
                interpreter.LoadCode(generated.Code, true, interpreter.DebugInfo);

                if (pauseBeforeRun)
                {
                    Output.WriteLine();
                    Output.Write("Press any key to start executing");
                    Console.ReadKey();
                }

                if (arguments.ConsoleGUI)
                {
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    { throw new PlatformNotSupportedException($"Console rendering is only supported on Windows"); }

                    interpreter.RunWithUI(true, -100);

                    Console.ReadKey();
                }
                else
                {
                    Output.WriteLine();
                    Output.WriteLine($" === OUTPUT ===");
                    Output.WriteLine();

                    if (false)
                    {
                        // Stopwatch sw = Stopwatch.StartNew();
                        // interpreter.Run();
                        // Console.ResetColor();
                        // sw.Stop();
                        // 
                        // Output.WriteLine();
                        // Output.WriteLine();
                        // Output.WriteLine($"Execution time: {sw.ElapsedMilliseconds} ms");
                    }
                    else
                    {
                        interpreter.Run();
                    }

                    {
                        Output.WriteLine();
                        Output.WriteLine();
                        Output.WriteLine($" === MEMORY ===");
                        Output.WriteLine();

                        const int zerosToShow = 10;
                        int finalIndex = 0;

                        for (int i = 0; i < interpreter.Memory.Length; i++)
                        { if (interpreter.Memory[i] != 0) finalIndex = i; }

                        finalIndex = Math.Max(finalIndex, interpreter.MemoryPointer);
                        finalIndex = Math.Min(interpreter.Memory.Length, finalIndex + zerosToShow);

                        int heapStart = BrainfuckGeneratorSettings.Default.HeapStart;
                        int heapEnd = heapStart + (BrainfuckGeneratorSettings.Default.HeapSize * HeapCodeHelper.BLOCK_SIZE);

                        for (int i = 0; i < finalIndex; i++)
                        {
                            if (i % 15 == 0 && i > 0)
                            { Console.WriteLine(); }

                            byte cell = interpreter.Memory[i];

                            ConsoleColor fg = ConsoleColor.White;
                            ConsoleColor bg = ConsoleColor.Black;

                            if (cell == 0)
                            { fg = ConsoleColor.DarkGray; }

                            if (i == heapStart)
                            { bg = ConsoleColor.DarkBlue; }

                            if (i > heapStart + 2)
                            {
                                int j = (i - heapStart) / HeapCodeHelper.BLOCK_SIZE;
                                int k = (i - heapStart) % HeapCodeHelper.BLOCK_SIZE;
                                if (k == HeapCodeHelper.OFFSET_DATA)
                                {
                                    bg = ConsoleColor.DarkGreen;
                                    if (cell == 0)
                                    { fg = ConsoleColor.Green; }
                                    else
                                    { fg = ConsoleColor.White; }
                                }
                                else
                                {
                                    bg = ConsoleColor.DarkGray;
                                    if (cell == 0)
                                    { fg = ConsoleColor.Gray; }
                                    else
                                    { fg = ConsoleColor.White; }
                                }
                            }

                            if (i == interpreter.MemoryPointer)
                            {
                                bg = ConsoleColor.DarkRed;
                                fg = ConsoleColor.Gray;
                            }

                            Console.ForegroundColor = fg;
                            Console.BackgroundColor = bg;

                            Console.Write($"{cell,3} ");
                            Console.ResetColor();
                        }

                        if (interpreter.Memory.Length - finalIndex > 0)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write($" ... ");
                            Console.ResetColor();
                            Console.WriteLine();
                        }

                        // byte[] heap = interpreter.GetHeap(BrainfuckGeneratorSettings.Default);
                        // Output.WriteLine();
                        // Output.WriteLine();
                        // Output.WriteLine($" === HEAP ===");
                        // Output.WriteLine();
                        // for (int i = 0; i < heap.Length; i++)
                        // {
                        //     Console.WriteLine($"{i}: {heap[i]}");
                        // }

                        Console.WriteLine();

                        static void PrintLegend(ConsoleColor background, ConsoleColor foreground, string colorLabel, string label)
                        {
                            Console.Write(' ');
                            Console.ForegroundColor = foreground;
                            Console.BackgroundColor = background;
                            Console.Write(colorLabel);
                            Console.ResetColor();
                            Console.Write(':');
                            Console.Write(' ');
                            Console.Write(label);
                            Console.WriteLine();
                        }

                        PrintLegend(ConsoleColor.Red, ConsoleColor.White, "Red", "Memory Pointer");
                        PrintLegend(ConsoleColor.DarkBlue, ConsoleColor.White, "Blue", "HEAP Start");
                        PrintLegend(ConsoleColor.DarkGray, ConsoleColor.White, "Gray", "HEAP Internal");
                        PrintLegend(ConsoleColor.DarkGreen, ConsoleColor.White, "Green", "HEAP Data");
                        Console.WriteLine();
                    }
                }
                break;
            }
            case ProgramRunType.ASM:
            {
                bool is16Bits = false;

                AnalysisCollection analysisCollection = new();

                CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.File, null, arguments.CompilerSettings, Output.Log, analysisCollection);

                AsmGeneratorResult code = CodeGeneratorForAsm.Generate(compiled, new AsmGeneratorSettings()
                {
                    Is16Bits = is16Bits,
                }, Output.Log, analysisCollection);

                analysisCollection.Throw();
                analysisCollection.Print();

                string? fileDirectoryPath = arguments.File.DirectoryName;
                string fileNameNoExt = Path.GetFileNameWithoutExtension(arguments.File.Name);

                fileDirectoryPath ??= ".\\";

                string outputFile = Path.Combine(fileDirectoryPath, fileNameNoExt);

                if (is16Bits)
                {
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    { throw new PlatformNotSupportedException($"Console rendering is only supported on Windows"); }

                    ASM.Assembler.AssembleRaw(code.AssemblyCode, outputFile, true);

                    outputFile += ".bin";
                    if (File.Exists(outputFile))
                    {
                        Intel.I8086 i8086 = new(outputFile);
                        while (!i8086.IsHalted)
                        { i8086.Clock(); }
                    }
                }
                else
                {
                    ASM.Assembler.Assemble(code.AssemblyCode, outputFile, true);

                    outputFile += ".exe";
                    if (File.Exists(outputFile))
                    {
                        Process? process = Process.Start(new ProcessStartInfo(outputFile)) ?? throw new InternalException($"Failed to start process \"{outputFile}\"");
                        process.WaitForExit();
                        Console.WriteLine();
                        Console.WriteLine($"Exit code: {process.ExitCode}");

                        if (ProcessRuntimeException.TryGetFromExitCode(process.ExitCode, out ProcessRuntimeException? runtimeException))
                        { throw runtimeException; }
                    }
                }

                break;
            }
            default: throw new NotImplementedException($"Mode \"{arguments.RunType}\" isn't implemented for some reason");
        }
    }
}

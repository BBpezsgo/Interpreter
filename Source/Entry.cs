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
    using LanguageCore.Brainfuck.Generator;
    using LanguageCore.Compiler;
#if !AOT
    using LanguageCore.IL.Generator;
#endif
    using LanguageCore.Parser;
    using LanguageCore.Runtime;
    using LanguageCore.Tokenizing;

    public static class Entry
    {
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="NotImplementedException"/>
        public static void Run(ProgramArguments arguments)
        {
            if (arguments.IsEmpty)
            {
                new LanguageCore.Interactive.Interactive().Run();
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
                        LanguageCore.Runtime.Interpreter interpreter = new();

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
                                CompilerResult compiled = Compiler.CompileFile(arguments.File, externalFunctions, arguments.CompilerSettings, Output.Log, analysisCollection);
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
                            CompilerResult compiled = Compiler.CompileFile(arguments.File, externalFunctions, arguments.CompilerSettings, Output.Log, analysisCollection);
                            generatedCode = CodeGeneratorForMain.Generate(compiled, arguments.GeneratorSettings, Output.Log, analysisCollection);
                            analysisCollection.Throw();
                            analysisCollection.Print();
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
                    BrainfuckPrintFlags printFlags = BrainfuckPrintFlags.PrintMemory;

                    BrainfuckCompilerFlags compileOptions =
                        arguments.GeneratorSettings.PrintInstructions
                        ? BrainfuckCompilerFlags.PrintFinal | BrainfuckCompilerFlags.PrintCompiled
                        : BrainfuckCompilerFlags.None;

                    BrainfuckGeneratorResult generated;
                    Token[] tokens;

                    AnalysisCollection analysisCollection = new();
                    if (arguments.ThrowErrors)
                    {
                        tokens = StreamTokenizer.Tokenize(arguments.File!.FullName);
                        CompilerResult compiled = Compiler.CompileFile(arguments.File, null, arguments.CompilerSettings, Output.Log, analysisCollection);
                        generated = CodeGeneratorForBrainfuck.Generate(compiled, BrainfuckGeneratorSettings.Default, Output.Log, analysisCollection);
                        analysisCollection.Throw();
                        analysisCollection.Print();
                        Output.LogDebug($"Optimized {generated.Optimizations} statements");
                    }
                    else
                    {
                        try
                        {
                            tokens = StreamTokenizer.Tokenize(arguments.File!.FullName);
                            CompilerResult compiled = Compiler.CompileFile(arguments.File!, null, arguments.CompilerSettings, Output.Log, analysisCollection);
                            generated = CodeGeneratorForBrainfuck.Generate(compiled, BrainfuckGeneratorSettings.Default, Output.Log, analysisCollection);
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

                    if ((compileOptions & BrainfuckCompilerFlags.PrintCompiled) != 0)
                    {
                        Output.WriteLine();
                        Output.WriteLine($" === COMPILED ===");
                        BrainfuckCode.PrintCode(Simplifier.Simplify(generated.Code));
                        Output.WriteLine();
                    }

                    generated.Code = Minifier.Minify(generated.Code);

                    if ((compileOptions & BrainfuckCompilerFlags.PrintCompiledMinimized) != 0)
                    {
                        Output.WriteLine();
                        Output.WriteLine($" === MINIFIED ===");
                        BrainfuckCode.PrintCode(Simplifier.Simplify(generated.Code));
                        Output.WriteLine();
                    }

                    generated.Code = Minifier.Minify(BrainfuckCode.RemoveNoncodes(generated.Code));

                    if ((compileOptions & BrainfuckCompilerFlags.PrintFinal) != 0)
                    {
                        Output.WriteLine();
                        Output.WriteLine($" === FINAL ===");
                        BrainfuckCode.PrintCode(generated.Code);
                        Output.WriteLine();
                    }

                    if ((compileOptions & BrainfuckCompilerFlags.WriteToFile) != 0)
                    {
                        string compiledFilePath = Path.Combine(Path.GetDirectoryName(arguments.File!.FullName) ?? throw new InternalException($"Failed to get directory name of file \"{arguments.File!.FullName}\""), Path.GetFileNameWithoutExtension(arguments.File!.FullName) + ".bf");
                        File.WriteAllText(compiledFilePath, generated.Code);
                    }

                    InterpreterCompact interpreter = new(generated.Code)
                    {
                        DebugInfo = generated.DebugInfo,
                        OriginalCode = tokens,
                    };

                    if (arguments.ConsoleGUI)
                    {
                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        { throw new PlatformNotSupportedException($"Console rendering is only supported on Windows"); }

                        Output.WriteLine();
                        Output.Write("Press any key to start the interpreter");
                        Console.ReadKey();

                        interpreter.RunWithUI(true, 10);

                        Console.ReadKey();
                    }
                    else
                    {
                        if ((printFlags & BrainfuckPrintFlags.PrintResultLabel) != 0)
                        {
                            Output.WriteLine();
                            Output.WriteLine($" === RESULT ===");
                            Output.WriteLine();
                        }

                        if ((printFlags & BrainfuckPrintFlags.PrintExecutionTime) != 0)
                        {
                            Stopwatch sw = Stopwatch.StartNew();
                            interpreter.Run();
                            Console.ResetColor();
                            sw.Stop();

                            Output.WriteLine();
                            Output.WriteLine();
                            Output.WriteLine($"Execution time: {sw.ElapsedMilliseconds} ms");
                        }
                        else
                        {
                            interpreter.Run();
                        }

                        if ((printFlags & BrainfuckPrintFlags.PrintMemory) != 0)
                        {
                            Output.WriteLine();
                            Output.WriteLine();
                            Output.WriteLine($" === MEMORY ===");
                            Output.WriteLine();

                            int zerosToShow = 10;
                            int finalIndex = 0;

                            for (int i = 0; i < interpreter.Memory.Length; i++)
                            { if (interpreter.Memory[i] != 0) finalIndex = i; }

                            finalIndex = Math.Max(finalIndex, interpreter.MemoryPointer);
                            finalIndex = Math.Min(interpreter.Memory.Length, finalIndex + zerosToShow);

                            int heapStart = BrainfuckGeneratorSettings.Default.HeapStart;
                            int heapEnd = heapStart + BrainfuckGeneratorSettings.Default.HeapSize * BasicHeapCodeHelper.BLOCK_SIZE;

                            for (int i = 0; i < finalIndex; i++)
                            {
                                if (i % 16 == 0 && i > 0)
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
                                    int j = (i - heapStart) / BasicHeapCodeHelper.BLOCK_SIZE;
                                    int k = (i - heapStart) % BasicHeapCodeHelper.BLOCK_SIZE;
                                    if (k == BasicHeapCodeHelper.OFFSET_DATA)
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

                                Console.Write($" {cell} ");
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
                        }
                    }
                    break;
                }
                case ProgramRunType.IL:
                {
#if AOT
                    throw new NotSupportedException($"The compiler compiled in AOT mode so IL generation isn't available");
#else
                    AnalysisCollection analysisCollection = new();

                    CompilerResult compiled = Compiler.CompileFile(arguments.File, null, arguments.CompilerSettings, Output.Log, analysisCollection);

                    ILGeneratorResult generated = CodeGeneratorForIL.Generate(compiled, arguments.CompilerSettings, default, Output.Log, analysisCollection);

                    analysisCollection.Throw();
                    analysisCollection.Print();

                    generated.Invoke();
                    break;
#endif
                }
                case ProgramRunType.ASM:
                {
                    AnalysisCollection analysisCollection = new();

                    CompilerResult compiled = Compiler.CompileFile(arguments.File, null, arguments.CompilerSettings, Output.Log, analysisCollection);

                    AsmGeneratorResult code = CodeGeneratorForAsm.Generate(compiled, default, Output.Log, analysisCollection);

                    analysisCollection.Throw();
                    analysisCollection.Print();

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

using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using CommandLine;
using LanguageCore.BBLang.Generator;
using LanguageCore.Brainfuck;
using LanguageCore.Brainfuck.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;

namespace LanguageCore;

public static class Entry
{
    /// <exception cref="NotSupportedException"/>
    /// <exception cref="NotImplementedException"/>
    public static int Run(params string[] arguments)
    {
        CommandLine.Parser parser = new(with => with.HelpWriter = null);
        ParserResult<CommandLineOptions> parserResult = parser.ParseArguments<CommandLineOptions>(arguments);

        switch (parserResult.Tag)
        {
            case ParserResultType.Parsed:
            {
                if (parserResult.Value.ThrowErrors)
                {
                    return Entry.Run(parserResult.Value);
                }

                try
                {
                    return Entry.Run(parserResult.Value);
                }
                catch (Exception exception)
                {
                    Output.LogError($"Unhandled exception: {exception}");
                    return 1;
                }
            }
            case ParserResultType.NotParsed:
            {
                Program.DisplayHelp(parserResult, parserResult.Errors);
                return 1;
            }
            default:
            {
                return 1;
            }
        }
    }

    /// <exception cref="NotSupportedException"/>
    /// <exception cref="NotImplementedException"/>
    public static int Run(CommandLineOptions arguments)
    {
        Output.LogDebugs = arguments.Verbose;
        Output.LogInfos = true;
        Output.LogWarnings = true;
        ConsoleProgress.IsEnabled = arguments.Verbose;

        CompilerSettings compilerSettings = new(CompilerSettings.Default)
        {
            BasePath = arguments.BasePath,
        };
        MainGeneratorSettings mainGeneratorSettings = new(MainGeneratorSettings.Default)
        {
            CheckNullPointers = !arguments.NoNullcheck ?? MainGeneratorSettings.Default.CheckNullPointers,
            DontOptimize = arguments.DontOptimize ?? MainGeneratorSettings.Default.DontOptimize,
            PrintInstructions = arguments.PrintInstructions ?? MainGeneratorSettings.Default.PrintInstructions,
            GenerateDebugInstructions = !arguments.NoDebugInfo ?? MainGeneratorSettings.Default.GenerateDebugInstructions,
            StackSize = arguments.StackSize ?? MainGeneratorSettings.Default.StackSize,
        };
        BrainfuckGeneratorSettings brainfuckGeneratorSettings = new(BrainfuckGeneratorSettings.Default)
        {
            DontOptimize = arguments.DontOptimize ?? BrainfuckGeneratorSettings.Default.DontOptimize,
            GenerateDebugInformation = !arguments.NoDebugInfo ?? BrainfuckGeneratorSettings.Default.GenerateDebugInformation,
            GenerateComments = !arguments.NoDebugInfo ?? BrainfuckGeneratorSettings.Default.GenerateComments,
            GenerateSmallComments = !arguments.NoDebugInfo ?? BrainfuckGeneratorSettings.Default.GenerateSmallComments,
            StackSize = arguments.StackSize ?? BrainfuckGeneratorSettings.Default.StackSize,
            HeapSize = arguments.HeapSize ?? BrainfuckGeneratorSettings.Default.HeapSize,
        };
        BytecodeInterpreterSettings bytecodeInterpreterSettings = new(BytecodeInterpreterSettings.Default)
        {
            StackSize = arguments.StackSize ?? BytecodeInterpreterSettings.Default.StackSize,
            HeapSize = arguments.HeapSize ?? BytecodeInterpreterSettings.Default.HeapSize,
        };

        if (arguments.Source is null)
        {
            new Interactive.Interactive().Run();
            return 0;
        }

        string[] additionalImports = new string[]
        {
            "../StandardLibrary/Primitives.bbc"
        };

        switch (arguments.Format)
        {
            case "bytecode":
            {
                Output.LogDebug($"Executing \"{arguments.Source}\" ...");

                List<IExternalFunction> externalFunctions = BytecodeProcessorEx.GetExternalFunctions();

                BBLangGeneratorResult generatedCode;
                AnalysisCollection analysisCollection = new();

                if (arguments.ThrowErrors)
                {
                    CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.Source, externalFunctions, compilerSettings, PreprocessorVariables.Normal, Output.Log, analysisCollection, null, null, additionalImports);
                    generatedCode = CodeGeneratorForMain.Generate(compiled, mainGeneratorSettings, Output.Log, analysisCollection);
                    analysisCollection.Throw();
                    analysisCollection.Print();
                }
                else
                {
                    try
                    {
                        CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.Source, externalFunctions, compilerSettings, PreprocessorVariables.Normal, Output.Log, analysisCollection, null, null, additionalImports);
                        generatedCode = CodeGeneratorForMain.Generate(compiled, mainGeneratorSettings, Output.Log, analysisCollection);
                        analysisCollection.Throw();
                        analysisCollection.Print();
                    }
                    catch (LanguageException ex)
                    {
                        analysisCollection.Print();
                        Output.LogError(ex);
                        return 1;
                    }
                    catch (Exception ex)
                    {
                        analysisCollection.Print();
                        Output.LogError(ex);
                        return 1;
                    }
                }

                if (mainGeneratorSettings.PrintInstructions)
                {
                    for (int i = 0; i < generatedCode.Code.Length; i++)
                    {
                        Instruction instruction = generatedCode.Code[i];

                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write(instruction.Opcode);
                        Console.ResetColor();
                        Console.Write(' ');

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(instruction.Operand1.Value);

                        Console.WriteLine();
                    }
                }

                Console.ResetColor();

                if (arguments.Output is not null)
                {
                    Console.WriteLine($"Writing to \"{arguments.Output}\" ...");
                    File.WriteAllText(arguments.Output, null);
                    using FileStream stream = File.OpenWrite(arguments.Output);
                    using StreamWriter writer = new(stream);
                    foreach (Instruction instruction in generatedCode.Code)
                    {
                        writer.WriteLine(instruction.ToString());
                    }
                }

                static void PrintStuff(Runtime.BytecodeProcessorEx interpreter)
                {
#if DEBUG
                    Console.WriteLine();
                    Console.WriteLine($" ===== HEAP ===== ");
                    Console.WriteLine();

                    if (interpreter.Processor.Memory.GetData(0, BitWidth._32).I32 != 0)
                    {
                        int endlessSafe = interpreter.Processor.Memory.Length;
                        int i = 0;
                        int blockIndex = 0;
                        while (i + 1 < 127)
                        {
                            (int blockSize, bool blockIsUsed) = HeapImplementation.GetHeader(interpreter.Processor.Memory[i]);

                            Console.Write($"BLOCK {i}: ");

                            Console.Write($"SIZE: {blockSize} ");

                            if (blockIsUsed)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.Write("USED");
                                Console.ResetColor();
                                Console.Write(" :");
                                Console.WriteLine();

                                for (int j = i + 1; j < (blockSize + i + 1); j++)
                                {
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.Write(interpreter.Processor.Memory.GetData(j, BitWidth._8).U8);
                                    Console.Write(" ");
                                }
                                Console.WriteLine();
                                Console.WriteLine();
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.Write("FREE");
                                Console.ResetColor();
                                Console.WriteLine();
                            }

                            i += blockSize + 1;
                            blockIndex++;

                            if (endlessSafe-- < 0) throw new EndlessLoopException();
                        }
                    }
                    else
                    { Console.WriteLine("Empty"); }

                    Console.WriteLine();
                    Console.WriteLine($" ===== STACK ===== ");
                    Console.WriteLine();

                    IEnumerable<byte> stack;
#pragma warning disable CS0162 // Unreachable code detected
                    if (BytecodeProcessor.StackDirection > 0)
                    {
                        stack = new ArraySegment<byte>(interpreter.Processor.Memory)[interpreter.Processor.StackStart..interpreter.Processor.Registers.StackPointer];
                    }
                    else
                    {
                        stack = new ArraySegment<byte>(interpreter.Processor.Memory)[interpreter.Processor.Registers.StackPointer..(interpreter.Processor.StackStart + 1)].Reverse();
                    }
#pragma warning restore CS0162 // Unreachable code detected

                    foreach (byte item in stack)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(item);
                        Console.WriteLine();
                    }

                    Console.ResetColor();
#endif
                }

                BytecodeProcessorEx interpreter = new(
                    bytecodeInterpreterSettings,
                    generatedCode.Code,
                    null,
                    generatedCode.DebugInfo);

                if (arguments.Debug)
                {
                    // if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    // { throw new PlatformNotSupportedException("Console rendering is only supported on Windows"); }

                    // if (pauseBeforeRun)
                    // {
                    //     Console.WriteLine();
                    //     Console.Write("Press any key to start executing");
                    //     Console.ReadKey();
                    // }

                    Console.ResetColor();
                    Console.Clear();

                    ConsoleGUI.ConsoleGUI gui = new()
                    {
                        FilledElement = new ConsoleGUI.InterpreterElement(interpreter)
                    };

                    while (!gui.IsDisposed)
                    {
                        gui.Tick();
                        Thread.Sleep(10);
                    }

                    Console.Clear();
                    Console.ResetColor();
                    PrintStuff(interpreter);
                }
                else
                {
                    interpreter.IO.OnStdOut += (data) => Console.Out.Write(char.ToString(data));

                    interpreter.IO.OnNeedInput += () =>
                    {
                        ConsoleKeyInfo input = Console.ReadKey(true);
                        interpreter.IO.SendKey(input.KeyChar);
                    };

                    try
                    {
                        while (!interpreter.Processor.IsDone)
                        { interpreter.Tick(); }
                    }
                    catch (UserException error)
                    {
                        Output.LogError($"User Exception: {error}");
                        if (arguments.ThrowErrors) throw;
                    }
                    catch (RuntimeException error)
                    {
                        Output.LogError($"Runtime Exception: {error}");
                        if (arguments.ThrowErrors) throw;
                    }
                    catch (Exception error)
                    {
                        Output.LogError($"Internal Exception: {new RuntimeException(error.Message, error, interpreter.Processor.GetContext(), interpreter.DebugInformation)}");
                        if (arguments.ThrowErrors) throw;
                    }
                    finally
                    {
                        Console.ResetColor();
                        PrintStuff(interpreter);
                    }
                }

                break;
            }
            case "brainfuck":
            {
                Output.LogDebug($"Executing \"{arguments.Source}\" ...");

                BrainfuckGeneratorResult generated;
                ImmutableArray<Token> tokens;

                AnalysisCollection analysisCollection = new();
                if (arguments.ThrowErrors)
                {
                    tokens = AnyTokenizer.Tokenize(arguments.Source, PreprocessorVariables.Brainfuck).Tokens;
                    CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.Source, null, compilerSettings, PreprocessorVariables.Brainfuck, Output.Log, analysisCollection, null, null, additionalImports);
                    generated = CodeGeneratorForBrainfuck.Generate(compiled, brainfuckGeneratorSettings, Output.Log, analysisCollection);
                    analysisCollection.Throw();
                    analysisCollection.Print();
                    Output.LogDebug($"Optimized {generated.Optimizations} statements");
                    Output.LogDebug($"Precomputed {generated.Precomputations} statements");
                    Output.LogDebug($"Evaluated {generated.FunctionEvaluations} functions");
                }
                else
                {
                    try
                    {
                        tokens = AnyTokenizer.Tokenize(arguments.Source, PreprocessorVariables.Brainfuck).Tokens;
                        CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.Source, null, compilerSettings, PreprocessorVariables.Brainfuck, Output.Log, analysisCollection, null, null, additionalImports);
                        generated = CodeGeneratorForBrainfuck.Generate(compiled, brainfuckGeneratorSettings, Output.Log, analysisCollection);
                        analysisCollection.Throw();
                        analysisCollection.Print();
                        Output.LogDebug($"Optimized {generated.Optimizations} statements");
                        Output.LogDebug($"Precomputed {generated.Precomputations} statements");
                        Output.LogDebug($"Evaluated {generated.FunctionEvaluations} functions");
                    }
                    catch (LanguageException exception)
                    {
                        analysisCollection.Print();
                        Output.LogError(exception);
                        return 1;
                    }
                    catch (Exception exception)
                    {
                        analysisCollection.Print();
                        Output.LogError(exception);
                        return 1;
                    }
                }

                bool pauseBeforeRun = false;

                // if (arguments.PrintFlags.HasFlag(PrintFlags.Commented))
                // {
                //     Console.WriteLine();
                //     Console.WriteLine($" === COMPILED ===");
                //     BrainfuckCode.PrintCode(Simplifier.Simplify(generated.Code));
                //     Console.WriteLine();
                // 
                //     pauseBeforeRun = true;
                // }

                generated.Code = BrainfuckCode.RemoveNoncodes(generated.Code, true, generated.DebugInfo);

                Output.LogDebug($"Minify code ...");
                int prevCodeLength = generated.Code.Length;
                generated.Code = Minifier.Minify(generated.Code, generated.DebugInfo);
                Output.LogDebug($"Minification: {prevCodeLength} -> {generated.Code.Length} ({((float)generated.Code.Length - prevCodeLength) / (float)generated.Code.Length * 100f:#}%)");

                if (mainGeneratorSettings.PrintInstructions)
                {
                    Console.WriteLine();
                    Console.WriteLine($" === FINAL ===");
                    Console.WriteLine();
                    BrainfuckCode.PrintCode(generated.Code);
                    Console.WriteLine();

                    pauseBeforeRun = true;
                }

                /*
                if (arguments.PrintFlags.HasFlag(PrintFlags.Simplified))
                {
                    Console.WriteLine();
                    Console.WriteLine($" === SIMPLIFIED ===");
                    Console.WriteLine();
                    BrainfuckCode.PrintCode(Simplifier.Simplify(generated.Code));
                    Console.WriteLine();

                    pauseBeforeRun = true;
                }
                */

                /*
                Output.WriteLine();
                Output.WriteLine($" === COMPACTED ===");
                Output.WriteLine();
                BrainfuckCode.PrintCode(string.Join(null, CompactCode.Generate(generated.Code, false, null)));
                Output.WriteLine();
                */

                if (arguments.Output is not null)
                {
                    Console.WriteLine($"Writing to \"{arguments.Output}\" ...");
                    File.WriteAllText(arguments.Output, generated.Code);
                }

                InterpreterCompact interpreter = new()
                {
                    DebugInfo = generated.DebugInfo,
                };
                interpreter.LoadCode(generated.Code, true, interpreter.DebugInfo);

                if (pauseBeforeRun)
                {
                    Console.WriteLine();
                    Console.Write("Press any key to start executing");
                    Console.ReadKey();
                }

                if (arguments.Debug)
                {
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    { throw new PlatformNotSupportedException($"Console rendering is only supported on Windows"); }

                    throw new NotImplementedException();
                    // interpreter.RunWithUI(true, -100);
                    // Console.ReadKey();
                }
                else
                {
                    interpreter.Run();

                    if (arguments.PrintMemory ?? false)
                    {
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine($" === MEMORY ===");
                        Console.WriteLine();

                        const int zerosToShow = 10;
                        int finalIndex = 0;

                        for (int i = 0; i < interpreter.Memory.Length; i++)
                        { if (interpreter.Memory[i] != 0) finalIndex = i; }

                        finalIndex = Math.Max(finalIndex, interpreter.MemoryPointer);
                        finalIndex = Math.Min(interpreter.Memory.Length, finalIndex + zerosToShow);

                        int heapStart = brainfuckGeneratorSettings.HeapStart;
                        int heapEnd = heapStart + (brainfuckGeneratorSettings.HeapSize * HeapCodeHelper.BlockSize);

                        for (int i = 0; i < finalIndex; i++)
                        {
                            if (i % 16 == 0)
                            {
                                if (i > 0)
                                { Console.WriteLine(); }
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.Write($"{i}: ");
                                Console.ResetColor();
                            }

                            byte cell = interpreter.Memory[i];

                            ConsoleColor fg = ConsoleColor.White;
                            ConsoleColor bg = ConsoleColor.Black;

                            if (cell == 0)
                            { fg = ConsoleColor.DarkGray; }

                            if (i == heapStart)
                            { bg = ConsoleColor.DarkBlue; }

                            if (i > heapStart + 2)
                            {
                                int j = (i - heapStart) / HeapCodeHelper.BlockSize;
                                int k = (i - heapStart) % HeapCodeHelper.BlockSize;
                                if (k == HeapCodeHelper.DataOffset)
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

                        if (interpreter.Memory.Length > finalIndex)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write($" ... ");
                            Console.ResetColor();
                            Console.WriteLine();
                        }

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
            case "assembly":
            {
                Output.LogDebug($"Executing \"{arguments.Source}\" ...");

                List<IExternalFunction> externalFunctions = Runtime.BytecodeProcessorEx.GetExternalFunctions();

                BBLangGeneratorResult generatedCode;
                AnalysisCollection analysisCollection = new();

                BitWidth bits = BitWidth._64;

                mainGeneratorSettings.PointerSize = (int)bits;

                if (arguments.ThrowErrors)
                {
                    CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.Source, externalFunctions, compilerSettings, PreprocessorVariables.Normal, Output.Log, analysisCollection, null, null, additionalImports);
                    generatedCode = CodeGeneratorForMain.Generate(compiled, mainGeneratorSettings, Output.Log, analysisCollection);
                    analysisCollection.Throw();
                    analysisCollection.Print();
                }
                else
                {
                    try
                    {
                        CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.Source, externalFunctions, compilerSettings, PreprocessorVariables.Normal, Output.Log, analysisCollection, null, null, additionalImports);
                        generatedCode = CodeGeneratorForMain.Generate(compiled, mainGeneratorSettings, Output.Log, analysisCollection);
                        analysisCollection.Throw();
                        analysisCollection.Print();
                    }
                    catch (LanguageException ex)
                    {
                        analysisCollection.Print();
                        Output.LogError(ex);
                        return 1;
                    }
                    catch (Exception ex)
                    {
                        analysisCollection.Print();
                        Output.LogError(ex);
                        return 1;
                    }
                }

                string asm = ASM.Generator.ConverterForAsm.Convert(generatedCode.Code.AsSpan(), generatedCode.DebugInfo, bits);
                string outputFile = arguments.Source.LocalPath + "_executable";

                Output.LogDebug("Assembling and linking ...");

                ASM.Assembler.Assemble(asm, outputFile);

                Output.LogInfo($"Output: \"{outputFile}\"");

                // if (File.Exists(outputFile))
                // {
                //     Process? process = Process.Start(new ProcessStartInfo(outputFile)) ?? throw new InternalException($"Failed to start process \"{outputFile}\"");
                //     process.WaitForExit();
                //     Console.WriteLine();
                //     Console.WriteLine($"Exit code: {process.ExitCode}");
                // 
                //     if (ProcessRuntimeException.TryGetFromExitCode(process.ExitCode, out ProcessRuntimeException? runtimeException))
                //     { throw runtimeException; }
                // }

                break;
            }
            /*
            case ProgramRunType.ASM:
            {
                const bool is16Bits = true;

                AnalysisCollection analysisCollection = new();

                CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.File, null, arguments.CompilerSettings, Enumerable.Empty<string>(), Output.Log, analysisCollection, null, null, additionalImports);

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
            */

            default: throw new NotImplementedException($"Unknown format \"{arguments.Format}\"");
        }

        return 0;
    }
}

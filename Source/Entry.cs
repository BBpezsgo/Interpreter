using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace LanguageCore;

using BBLang.Generator;
using Brainfuck;
using Brainfuck.Generator;
using Compiler;
using Runtime;
using Tokenizing;

[ExcludeFromCodeCoverage]
public static class Entry
{
    /// <exception cref="NotSupportedException"/>
    /// <exception cref="NotImplementedException"/>
    public static void Run(params string[] arguments)
    {
        bool pauseAtEnd = true;

        if (ArgumentParser.Parse(out ProgramArguments arguments_, arguments))
        {
            if (arguments_.ThrowErrors)
            {
                Entry.Run(arguments_);
            }
            else
            {
                try
                { Entry.Run(arguments_); }
                catch (Exception exception)
                { Output.LogError($"Unhandled exception: {exception}"); }
            }

            if (arguments_.DoNotPause) pauseAtEnd = false;
        }

        if (pauseAtEnd)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }

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
                Output.LogDebug($"Executing \"{arguments.File}\" ...");

                Dictionary<int, ExternalFunctionBase> externalFunctions = Runtime.Interpreter.GetExternalFunctions();

                BBLangGeneratorResult generatedCode;
                AnalysisCollection analysisCollection = new();

                if (arguments.ThrowErrors)
                {
                    CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.File, externalFunctions, arguments.CompilerSettings, PreprocessorVariables.Normal, Output.Log, analysisCollection, null, null);
                    generatedCode = CodeGeneratorForMain.Generate(compiled, arguments.MainGeneratorSettings, Output.Log, analysisCollection);
                    analysisCollection.Throw();
                    analysisCollection.Print();
                }
                else
                {
                    try
                    {
                        CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.File, externalFunctions, arguments.CompilerSettings, PreprocessorVariables.Normal, Output.Log, analysisCollection, null, null);
                        generatedCode = CodeGeneratorForMain.Generate(compiled, arguments.MainGeneratorSettings, Output.Log, analysisCollection);
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

                if (arguments.MainGeneratorSettings.PrintInstructions)
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

                Runtime.Interpreter interpreter;

                static void PrintStuff(Runtime.Interpreter interpreter)
                {
#if DEBUG
                    Console.WriteLine();
                    Console.WriteLine($" ===== HEAP ===== ");
                    Console.WriteLine();

                    if (interpreter.BytecodeInterpreter.GetData(0, BitWidth._32).I32 != 0)
                    {
                        int endlessSafe = interpreter.BytecodeInterpreter.Memory.Length;
                        int i = 0;
                        int blockIndex = 0;
                        while (i + 1 < 127)
                        {
                            (int blockSize, bool blockIsUsed) = HeapImplementation.GetHeader(interpreter.BytecodeInterpreter.Memory[i]);

                            Console.Write($"BLOCK {blockIndex} ({i}): ");

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
                                    Console.Write(interpreter.BytecodeInterpreter.GetData(j, BitWidth._32).I32);
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

                    foreach (RuntimeValue item in interpreter.BytecodeInterpreter.GetStack())
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(item.I32);
                        Console.WriteLine();
                    }

                    Console.ResetColor();
#endif
                }

                if (arguments.ConsoleGUI)
                {
                    InterpreterDebuggabble _interpreter = new(false, arguments.BytecodeInterpreterSettings, generatedCode.Code, generatedCode.DebugInfo);
                    interpreter = _interpreter;

                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    { throw new PlatformNotSupportedException("Console rendering is only supported on Windows"); }

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
                        FilledElement = new ConsoleGUI.InterpreterElement(_interpreter)
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
                    interpreter = new(arguments.ThrowErrors, arguments.BytecodeInterpreterSettings, generatedCode.Code, generatedCode.DebugInfo);

                    interpreter.OnStdOut += (sender, data) => Console.Out.Write(char.ToString(data));
                    interpreter.OnStdError += (sender, data) => Console.Error.Write(char.ToString(data));

                    interpreter.OnOutput += (_, message, logType) => Output.Log(message, logType);

                    interpreter.OnNeedInput += (sender) =>
                    {
                        ConsoleKeyInfo input = Console.ReadKey(true);
                        sender.OnInput(input.KeyChar);
                    };

                    try
                    {
                        while (!interpreter.BytecodeInterpreter.IsDone)
                        { interpreter.Update(); }
                    }
                    finally
                    {
                        Console.ResetColor();
                        PrintStuff(interpreter);
                    }
                }

                break;
            }
            case ProgramRunType.Brainfuck:
            {
                Output.LogDebug($"Executing \"{arguments.File}\" ...");

                BrainfuckGeneratorResult generated;
                ImmutableArray<Token> tokens;
                BrainfuckGeneratorSettings generatorSettings = arguments.BrainfuckGeneratorSettings;

                AnalysisCollection analysisCollection = new();
                if (arguments.ThrowErrors)
                {
                    tokens = AnyTokenizer.Tokenize(arguments.File, PreprocessorVariables.Brainfuck).Tokens;
                    CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.File, null, arguments.CompilerSettings, PreprocessorVariables.Brainfuck, Output.Log, analysisCollection, null, null);
                    generated = CodeGeneratorForBrainfuck.Generate(compiled, generatorSettings, Output.Log, analysisCollection);
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
                        tokens = AnyTokenizer.Tokenize(arguments.File, PreprocessorVariables.Brainfuck).Tokens;
                        CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.File, null, arguments.CompilerSettings, PreprocessorVariables.Brainfuck, Output.Log, analysisCollection, null, null);
                        generated = CodeGeneratorForBrainfuck.Generate(compiled, generatorSettings, Output.Log, analysisCollection);
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

                if (arguments.PrintFlags.HasFlag(PrintFlags.Commented))
                {
                    Console.WriteLine();
                    Console.WriteLine($" === COMPILED ===");
                    BrainfuckCode.PrintCode(Simplifier.Simplify(generated.Code));
                    Console.WriteLine();

                    pauseBeforeRun = true;
                }

                generated.Code = BrainfuckCode.RemoveNoncodes(generated.Code, true, generated.DebugInfo);

                Output.LogDebug($"Minify code ...");
                int prevCodeLength = generated.Code.Length;
                generated.Code = Minifier.Minify(generated.Code, generated.DebugInfo);
                Output.LogDebug($"Minification: {prevCodeLength} -> {generated.Code.Length} ({((float)generated.Code.Length - prevCodeLength) / (float)generated.Code.Length * 100f:#}%)");

                if (arguments.PrintFlags.HasFlag(PrintFlags.Final))
                {
                    Console.WriteLine();
                    Console.WriteLine($" === FINAL ===");
                    Console.WriteLine();
                    BrainfuckCode.PrintCode(generated.Code);
                    Console.WriteLine();

                    pauseBeforeRun = true;
                }

                if (arguments.PrintFlags.HasFlag(PrintFlags.Simplified))
                {
                    Console.WriteLine();
                    Console.WriteLine($" === SIMPLIFIED ===");
                    Console.WriteLine();
                    BrainfuckCode.PrintCode(Simplifier.Simplify(generated.Code));
                    Console.WriteLine();

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
                    Console.WriteLine($"Writing to \"{arguments.OutputFile}\" ...");
                    File.WriteAllText(arguments.OutputFile, generated.Code);
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

                if (arguments.ConsoleGUI)
                {
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    { throw new PlatformNotSupportedException($"Console rendering is only supported on Windows"); }

                    interpreter.RunWithUI(true, -100);

                    Console.ReadKey();
                }
                else
                {
                    interpreter.Run();

                    if (arguments.PrintFlags.HasFlag(PrintFlags.Heap))
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

                        int heapStart = arguments.BrainfuckGeneratorSettings.HeapStart;
                        int heapEnd = heapStart + (arguments.BrainfuckGeneratorSettings.HeapSize * HeapCodeHelper.BlockSize);

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
            /*
            case ProgramRunType.ASM:
            {
                const bool is16Bits = true;

                AnalysisCollection analysisCollection = new();

                CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.File, null, arguments.CompilerSettings, Enumerable.Empty<string>(), Output.Log, analysisCollection, null, null);

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

            default: throw new NotImplementedException($"Mode \"{arguments.RunType}\" isn't implemented for some reason");
        }
    }
}

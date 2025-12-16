using System.IO;
using System.Runtime.InteropServices;
using CommandLine;
using LanguageCore.BBLang.Generator;
using LanguageCore.Brainfuck;
using LanguageCore.Brainfuck.Generator;
using LanguageCore.Compiler;
using LanguageCore.Native.Generator;
using LanguageCore.Runtime;
using LanguageCore.TUI;

namespace LanguageCore;

public static class Entry
{
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
                    return Run(parserResult.Value);
                }

                try
                {
                    return Run(parserResult.Value);
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

    public static int Run(CommandLineOptions arguments)
    {
        Output.LogDebugs = arguments.Verbose;
        Output.LogInfos = true;
        Output.LogWarnings = true;
        ConsoleProgress.IsEnabled = arguments.Verbose;

        if (arguments.Source is null)
        {
            Interactive.Run();
            return 0;
        }

        ImmutableArray<string> additionalImports = ImmutableArray.Create<string>(
            "Primitives"
        );

        switch (arguments.Format)
        {
            case "bytecode":
            {
                Output.LogDebug($"Executing \"{arguments.Source}\" ...");

                List<IExternalFunction> externalFunctions = new();
                IO io = arguments.Debug ? new VirtualIO() : StandardIO.Instance;
                io.Register(externalFunctions);
                BytecodeProcessor.AddStaticExternalFunctions(externalFunctions);

                BBLangGeneratorResult generatedCode;
                DiagnosticsCollection diagnostics = new();

                List<ExternalConstant> externalConstants = new()
                {
                    new ExternalConstant("heap_size", arguments.HeapSize ?? BytecodeInterpreterSettings.Default.HeapSize )
                };

                CompilerSettings compilerSettings = new(CodeGeneratorForMain.DefaultCompilerSettings)
                {
                    Optimizations = arguments.DontOptimize ? OptimizationSettings.None : OptimizationSettings.All,
                    ExternalFunctions = externalFunctions.ToImmutableArray(),
                    ExternalConstants = externalConstants.ToImmutableArray(),
                    PreprocessorVariables = PreprocessorVariables.Normal,
                    AdditionalImports = additionalImports,
                    SourceProviders = ImmutableArray.Create<ISourceProvider>(
                        new FileSourceProvider()
                        {
                            ExtraDirectories = new string?[]
                            {
                                arguments.BasePath
                            },
                        }
                    ),
                };
                MainGeneratorSettings mainGeneratorSettings = new(MainGeneratorSettings.Default)
                {
                    CheckNullPointers = !arguments.NoNullcheck,
                    Optimizations = arguments.DontOptimize ? GeneratorOptimizationSettings.None : GeneratorOptimizationSettings.All,
                    StackSize = arguments.StackSize ?? MainGeneratorSettings.Default.StackSize,
                    ILGeneratorSettings = new IL.Generator.ILGeneratorSettings()
                    {
                        AllowCrash = true,
                        // AllowHeap = true,
                        AllowPointers = true,
                    },
                };
                BytecodeInterpreterSettings bytecodeInterpreterSettings = new(BytecodeInterpreterSettings.Default)
                {
                    StackSize = arguments.StackSize ?? BytecodeInterpreterSettings.Default.StackSize,
                    HeapSize = arguments.HeapSize ?? BytecodeInterpreterSettings.Default.HeapSize,
                };

                try
                {
                    CompilerResult compiled = StatementCompiler.CompileFile(arguments.Source, compilerSettings, diagnostics);
                    generatedCode = CodeGeneratorForMain.Generate(compiled, mainGeneratorSettings, Output.Log, diagnostics);

                    if (arguments.IntermediateOutput is not null)
                    {
                        using StreamWriter f = new(arguments.IntermediateOutput);
                        f.WriteLine(compiled.Stringify());
                        if (!generatedCode.ILGeneratorBuilders.IsDefault)
                        {
                            foreach (string builder in generatedCode.ILGeneratorBuilders)
                            {
                                f.WriteLine(builder);
                            }
                        }
                    }
                    diagnostics.Print();
                    if (diagnostics.HasErrors) return 1;
                }
                catch (LanguageException ex)
                {
                    diagnostics.Print();
                    Output.LogError(ex);
                    return 1;
                }
                catch (Exception ex)
                {
                    diagnostics.Print();
                    Output.LogError(ex);
                    return 1;
                }

                Output.LogDebug($"Optimized {generatedCode.Statistics.Optimizations} statements");
                Output.LogDebug($"Precomputed {generatedCode.Statistics.Precomputations} statements");
                Output.LogDebug($"Evaluated {generatedCode.Statistics.FunctionEvaluations} functions");
                Output.LogDebug($"Inlined {generatedCode.Statistics.InlinedFunctions} functions");
                Output.LogDebug($"Optimized {generatedCode.Statistics.InstructionLevelOptimizations} instructions");

                if (arguments.PrintInstructions)
                {
                    for (int i = 0; i < generatedCode.Code.Length; i++)
                    {
                        int indent = 0;
                        FunctionInformation f = default;
                        if (generatedCode.DebugInfo is not null)
                        {
                            foreach (FunctionInformation item in generatedCode.DebugInfo.FunctionInformation)
                            {
                                if (item.Instructions.Contains(i))
                                {
                                    indent++;
                                    f = item;
                                }

                                if (item.Instructions.Start == i)
                                {
                                    ConsoleWriter t = default;
                                    InterpreterRenderer.WriteFunction(ref t, item.Function, item.TypeArguments);
                                    Console.WriteLine();
                                    Console.WriteLine('{');
                                    break;
                                }
                            }

                            if (generatedCode.DebugInfo.CodeComments.TryGetValue(i, out List<string>? comments))
                            {
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                foreach (string comment in comments)
                                {
                                    Console.Write(new string(' ', indent * 2));
                                    Console.WriteLine(comment);
                                }
                            }
                        }

                        Instruction instruction = generatedCode.Code[i];

                        Console.Write(new string(' ', indent * 2));

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write($"{i,4}: ");

                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write(instruction.Opcode);
                        Console.ResetColor();
                        Console.Write(' ');

                        int pcount = instruction.Opcode.ParameterCount();

                        if (pcount >= 1)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write(instruction.Operand1.ToString());
                        }

                        if (pcount >= 2)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write(' ');
                            Console.Write(instruction.Operand2.ToString());
                        }

                        Console.WriteLine();

                        if (f.IsValid && i == f.Instructions.End - 1)
                        {
                            Console.WriteLine('}');
                        }
                    }
                }

                Console.ResetColor();

                if (arguments.Output is not null)
                {
                    Console.WriteLine($"Writing to \"{arguments.Output}\" ...");
                    File.WriteAllBytes(arguments.Output, ReadOnlySpan<byte>.Empty);
                    using FileStream stream = File.OpenWrite(arguments.Output);
                    using StreamWriter writer = new(stream);
                    generatedCode.CodeEmitter.WriteTo(writer, false);
                }

                void PrintStuff(BytecodeProcessor interpreter)
                {
#if DEBUG
                    if (arguments.PrintMemory)
                    {
                        Console.WriteLine();
                        Console.WriteLine($" ===== HEAP ===== ");
                        Console.WriteLine();

                        if (interpreter.Memory.AsSpan().Get<int>(0) != 0)
                        {
                            int endlessSafe = interpreter.Memory.Length;
                            int i = 0;
                            while (i + BytecodeHeapImplementation.HeaderSize < 127)
                            {
                                (int size, bool status) = BytecodeHeapImplementation.GetHeader(interpreter.Memory, i);

                                Console.Write($"BLOCK {i}: ");

                                Console.Write($"SIZE: {size} ");

                                if (status)
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.Write("USED");
                                    Console.ResetColor();
                                    Console.Write(" :");
                                    Console.WriteLine();

                                    for (int j = 0; j < size; j++)
                                    {
                                        int address = i + BytecodeHeapImplementation.HeaderSize + j;
                                        if (address >= interpreter.Memory.Length) break;
                                        Console.ForegroundColor = ConsoleColor.Green;
                                        Console.Write(interpreter.Memory.AsSpan().Get<byte>(address));
                                        Console.Write(" ");
                                    }
                                    Console.ResetColor();
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

                                i += size + BytecodeHeapImplementation.HeaderSize;

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
                        if (ProcessorState.StackDirection > 0)
                        {
                            stack = new ArraySegment<byte>(interpreter.Memory)[interpreter.StackStart..interpreter.Registers.StackPointer];
                        }
                        else
                        {
                            stack = new ArraySegment<byte>(interpreter.Memory)[interpreter.Registers.StackPointer..(interpreter.StackStart + 1)].Reverse();
                        }
#pragma warning restore CS0162 // Unreachable code detected

                        int n = 0;
                        foreach (byte item in stack)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write(item);
                            Console.WriteLine();
                            if (n++ > 200)
                            {
                                Console.ForegroundColor = ConsoleColor.Gray;
                                Console.WriteLine("...");
                                break;
                            }
                        }

                        Console.ResetColor();
                    }
#endif
                }

                BytecodeProcessor interpreter = new(
                    bytecodeInterpreterSettings,
                    generatedCode.Code,
                    null,
                    generatedCode.DebugInfo,
                    externalFunctions,
                    generatedCode.GeneratedUnmanagedFunctions
                );

                GC.Collect();

                if (arguments.Debug)
                {
                    Console.ResetColor();
                    Console.Clear();

                    new InterpreterRenderer(interpreter).Run();

                    Console.Clear();
                    Console.ResetColor();
                    PrintStuff(interpreter);
                }
                else
                {
                    if (Debugger.IsAttached)
                    {
                        interpreter.RunUntilCompletion();
                    }
                    else
                    {
                        try
                        {
                            interpreter.RunUntilCompletion();
                        }
                        catch (RuntimeException error)
                        {
                            Output.LogError(error.ToString(true));
                            return 1;
                        }
                        finally
                        {
                            Console.ResetColor();
                            PrintStuff(interpreter);
                        }
                    }
                }

                return interpreter.Memory.AsSpan().Get<int>(interpreter.Registers.StackPointer);
            }
            case "il":
            {
                Output.LogDebug($"Executing \"{arguments.Source}\" ...");

                List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions(VoidIO.Instance);

                DiagnosticsCollection diagnostics = new();

                CompilerSettings compilerSettings = new(CodeGeneratorForMain.DefaultCompilerSettings)
                {
                    Optimizations = arguments.DontOptimize ? OptimizationSettings.None : OptimizationSettings.All,
                    ExternalFunctions = externalFunctions.ToImmutableArray(),
                    PreprocessorVariables = PreprocessorVariables.Normal,
                    AdditionalImports = additionalImports,
                    SourceProviders = ImmutableArray.Create<ISourceProvider>(
                        new FileSourceProvider()
                        {
                            ExtraDirectories = new string?[]
                            {
                                arguments.BasePath
                            },
                        }
                    ),
                };

                if (externalFunctions.TryGet("stdout", out IExternalFunction? stdoutFunction, out _))
                {
                    externalFunctions.AddExternalFunction(ExternalFunctionSync.Create<char>(stdoutFunction.Id, "stdout", Console.Write));
                }

                if (externalFunctions.TryGet("stdin", out IExternalFunction? stdinFunction, out _))
                {
                    externalFunctions.AddExternalFunction(ExternalFunctionSync.Create<char>(stdinFunction.Id, "stdin", static () => (char)Console.Read()));
                }

                CompilerResult compiled = StatementCompiler.CompileFile(arguments.Source, new(compilerSettings)
                {
                    ExternalFunctions = externalFunctions.ToImmutableArray(),
                    PreprocessorVariables = PreprocessorVariables.IL,
                }, diagnostics);
                Func<int> res = IL.Generator.CodeGeneratorForIL.Generate(compiled, diagnostics, new()
                {
                    AllowCrash = true,
                    AllowHeap = true,
                    AllowPointers = true,
                });
                diagnostics.Print();
                if (diagnostics.HasErrors) return 1;

                res.Invoke();
                return 0;
            }
            case "brainfuck":
            {
                Output.LogDebug($"Executing \"{arguments.Source}\" ...");

                BrainfuckGeneratorResult generated;

                CompilerSettings compilerSettings = new(CodeGeneratorForBrainfuck.DefaultCompilerSettings)
                {
                    Optimizations = arguments.DontOptimize ? OptimizationSettings.None : OptimizationSettings.All,
                    PreprocessorVariables = PreprocessorVariables.Brainfuck,
                    AdditionalImports = additionalImports,
                    SourceProviders = ImmutableArray.Create<ISourceProvider>(
                        new FileSourceProvider()
                        {
                            ExtraDirectories = new string?[]
                            {
                                arguments.BasePath
                            },
                        }
                    ),
                };
                BrainfuckGeneratorSettings brainfuckGeneratorSettings = new(BrainfuckGeneratorSettings.Default)
                {
                    DontOptimize = arguments.DontOptimize,
                    GenerateDebugInformation = !arguments.NoDebugInfo,
                    GenerateComments = !arguments.NoDebugInfo,
                    GenerateSmallComments = !arguments.NoDebugInfo,
                    StackSize = arguments.StackSize ?? BrainfuckGeneratorSettings.Default.StackSize,
                    HeapSize = arguments.HeapSize ?? BrainfuckGeneratorSettings.Default.HeapSize,
                };

                DiagnosticsCollection diagnostics = new();
                try
                {
                    CompilerResult compiled = StatementCompiler.CompileFile(arguments.Source, compilerSettings, diagnostics);
                    generated = CodeGeneratorForBrainfuck.Generate(compiled, brainfuckGeneratorSettings, Output.Log, diagnostics);
                    diagnostics.Print();
                    if (diagnostics.HasErrors) return 1;
                    Output.LogDebug($"Optimized {generated.Statistics.Optimizations} statements");
                    Output.LogDebug($"Precomputed {generated.Statistics.Precomputations} statements");
                    Output.LogDebug($"Evaluated {generated.Statistics.FunctionEvaluations} functions");
                }
                catch (LanguageException exception)
                {
                    diagnostics.Print();
                    Output.LogError(exception);
                    return 1;
                }
                catch (Exception exception)
                {
                    diagnostics.Print();
                    Output.LogError(exception);
                    return 1;
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

                if (arguments.PrintInstructions)
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

                InterpreterCompact interpreter = new();
                interpreter.LoadCode(generated.Code, true, generated.DebugInfo);
                interpreter.DebugInfo = new CompiledDebugInformation(generated.DebugInfo);

                if (pauseBeforeRun)
                {
                    Console.WriteLine();
                    Console.Write("Press any key to start executing");
                    Console.Read();
                }

                if (arguments.Debug)
                {
                    new BrainfuckRenderer(interpreter).Run();
                    Console.ReadKey();
                }
                else
                {
                    interpreter.Run();

                    if (arguments.PrintMemory)
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
                                Console.Write($"{i,3}: ");
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
            case "assembly-old":
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                { throw new PlatformNotSupportedException($"This is only supported on Linux"); }

                List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions(VoidIO.Instance);

                CompilerSettings compilerSettings = new(CodeGeneratorForMain.DefaultCompilerSettings)
                {
                    Optimizations = arguments.DontOptimize ? OptimizationSettings.None : OptimizationSettings.All,
                    ExternalFunctions = externalFunctions.ToImmutableArray(),
                    PreprocessorVariables = PreprocessorVariables.Normal,
                    AdditionalImports = additionalImports,
                    SourceProviders = ImmutableArray.Create<ISourceProvider>(
                        new FileSourceProvider()
                        {
                            ExtraDirectories = new string?[]
                            {
                                arguments.BasePath
                            },
                        }
                    ),
                    PointerSize = nint.Size,
                };
                MainGeneratorSettings mainGeneratorSettings = new(MainGeneratorSettings.Default)
                {
                    CheckNullPointers = !arguments.NoNullcheck,
                    Optimizations = arguments.DontOptimize ? GeneratorOptimizationSettings.None : GeneratorOptimizationSettings.All,
                    StackSize = arguments.StackSize ?? MainGeneratorSettings.Default.StackSize,
                    PointerSize = nint.Size,
                };

                DiagnosticsCollection diagnostics = new();

                CompilerResult compiled = StatementCompiler.CompileFile(arguments.Source, compilerSettings, diagnostics);
                diagnostics.Print();
                if (diagnostics.HasErrors) return 1;

                diagnostics.Clear();
                BBLangGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, mainGeneratorSettings, null, diagnostics);
                diagnostics.Print();
                if (diagnostics.HasErrors) return 1;

                string asm = Assembly.Generator.ConverterForAsm.Convert(generatedCode.Code.AsSpan(), generatedCode.DebugInfo, (BitWidth)nint.Size);

                Output.LogDebug("Assembling and linking ...");

                diagnostics.Clear();
                byte[] code = Assembler.Assemble(asm, diagnostics);
                diagnostics.Print();
                if (diagnostics.HasErrors) return 1;

                using NativeFunction f = NativeFunction.Allocate(code);

                return f.AsDelegate<CodeGeneratorForNative.JitFn>()();
            }
            case "assembly":
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                { throw new PlatformNotSupportedException($"This is only supported on Linux"); }

                Output.LogDebug($"Executing \"{arguments.Source}\" ...");

                List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions(VoidIO.Instance);

                CompilerSettings compilerSettings = new(CodeGeneratorForMain.DefaultCompilerSettings)
                {
                    Optimizations = arguments.DontOptimize ? OptimizationSettings.None : OptimizationSettings.All,
                    ExternalFunctions = externalFunctions.ToImmutableArray(),
                    PreprocessorVariables = PreprocessorVariables.Normal,
                    AdditionalImports = additionalImports,
                    SourceProviders = ImmutableArray.Create<ISourceProvider>(
                        new FileSourceProvider()
                        {
                            ExtraDirectories = new string?[]
                            {
                                arguments.BasePath
                            },
                        }
                    ),
                };

                DiagnosticsCollection diagnostics = new();

                try
                {
                    CompilerResult compiled = StatementCompiler.CompileFile(arguments.Source, compilerSettings, diagnostics);
                    using NativeFunction f = CodeGeneratorForNative.Generate(compiled, diagnostics);
                    diagnostics.Print();
                    if (diagnostics.HasErrors) return 1;
                    return f.AsDelegate<CodeGeneratorForNative.JitFn>()();
                }
                catch (LanguageException ex)
                {
                    diagnostics.Print();
                    Output.LogError(ex);
                    return 1;
                }
                catch (Exception ex)
                {
                    diagnostics.Print();
                    Output.LogError(ex);
                    return 1;
                }
            }
            /*
            case ProgramRunType.ASM:
            {
                const bool is16Bits = true;

                AnalysisCollection analysisCollection = new();

                CompilerResult compiled = Compiler.Compiler.CompileFile(arguments.File, null, arguments.CompilerSettings, Enumerable.Empty<string>(), analysisCollection, null, null, additionalImports);

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

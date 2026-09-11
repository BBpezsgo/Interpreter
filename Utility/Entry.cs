using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using CommandLine;
using LanguageCore.BBLang.Generator;
using LanguageCore.Brainfuck;
using LanguageCore.Brainfuck.Generator;
using LanguageCore.Compiler;
using LanguageCore.Native.Generator;
using LanguageCore.Runtime;
using LanguageCore.TUI;
using LanguageCore.Workspaces;
using StreamJsonRpc;

namespace LanguageCore;

public static class Entry
{
    public static int Run(params string[] arguments)
    {
        CommandLine.Parser argsParser = new(static with => with.HelpWriter = null);
        ParserResult<CommandLineOptions> parserResult = argsParser.ParseArguments<CommandLineOptions>(arguments);
        argsParser.Dispose();

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
                    ConsoleLogger.Default.LogError($"Unhandled exception: {exception}");
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

    static int RunIpc(CommandLineOptions arguments)
    {
        if (arguments.Source is null) return 1;

#pragma warning disable CA2000 // Dispose objects before losing scope
        using JsonRpc rpc = JsonRpc.Attach(
            Console.OpenStandardOutput(),
            Console.OpenStandardInput()
        );
#pragma warning restore CA2000 // Dispose objects before losing scope
        JsonRpcLogger log = new(rpc);

        List<IExternalFunction> externalFunctions = new();
        using JsonRpcIO io = new(rpc);
        io.Register(externalFunctions);
        BytecodeProcessor.AddStaticExternalFunctions(externalFunctions);

        DiagnosticsCollection diagnostics = new();

        Configuration config = Configuration.Parse(ConfigurationManager.Search(arguments.Uri is null ? new Uri(Environment.CurrentDirectory, UriKind.Absolute) : new Uri(arguments.Uri, UriKind.Absolute)), diagnostics);
        diagnostics.WriteErrorsTo(Console.Error);
        diagnostics.Clear();

        List<ISourceProvider> sourceProviders = new();
        string entryFile;
        const string DataSourcePrefix = "data:";
        if (arguments.Source.StartsWith(DataSourcePrefix))
        {
            entryFile = "memory:///script";
            sourceProviders.Add(new MemorySourceProvider(new Dictionary<string, string>()
            {
                { "/script", Encoding.UTF8.GetString(System.Buffers.Text.Base64.DecodeFromChars(arguments.Source.AsSpan(DataSourcePrefix.Length))) }
            }));
        }
        else
        {
            entryFile = arguments.Source;
        }
        sourceProviders.Add(new FileSourceProvider()
        {
            ExtraDirectories = config.ExtraDirectories,
        });

        CompilerSettings compilerSettings = new(CodeGeneratorForMain.DefaultCompilerSettings)
        {
            ExternalFunctions = externalFunctions.ToImmutableArray(),
            ExternalConstants = [
                new ExternalConstant("heap_size", BytecodeInterpreterSettings.Default.HeapSize),
                ..config.ExternalConstants,
            ],
            AdditionalImports = config.AdditionalImports,
            PreprocessorVariables = PreprocessorVariables.Normal,
            SourceProviders = sourceProviders.ToImmutableArray(),
        };
        MainGeneratorSettings mainGeneratorSettings = new(MainGeneratorSettings.Default)
        {
            Optimizations = GeneratorOptimizationSettings.All,
            ILGeneratorSettings = new IL.Generator.ILGeneratorSettings()
            {
                AllowCrash = true,
                // AllowHeap = true,
                AllowPointers = true,
                AllowUnsafePointers = true,
            },
        };
        BytecodeInterpreterSettings bytecodeInterpreterSettings = new(BytecodeInterpreterSettings.Default)
        {

        };

        BBLangGeneratorResult generatedCode;
        try
        {
            CompilerResult compiled = StatementCompiler.CompileFile(entryFile, compilerSettings, diagnostics);
            generatedCode = CodeGeneratorForMain.Generate(compiled, mainGeneratorSettings, null, diagnostics);

            diagnostics.WriteErrorsTo(Console.Error);
            if (diagnostics.HasErrors) return 1;
        }
        catch (LanguageExceptionAt ex)
        {
            diagnostics.WriteErrorsTo(Console.Error);
            Console.Error.WriteLine(ex);
            return 1;
        }
        catch (Exception ex)
        {
            diagnostics.WriteErrorsTo(Console.Error);
            Console.Error.WriteLine(ex);
            return 1;
        }

        BytecodeProcessor interpreter = new(
            bytecodeInterpreterSettings,
            generatedCode.Code,
            null,
            generatedCode.DebugInfo,
            externalFunctions,
            generatedCode.GeneratedUnmanagedFunctions
        );

        try
        {
            interpreter.RunUntilCompletion();
        }
        catch (RuntimeException error)
        {
            Console.Error.WriteLine(error.ToString());
            return 1;
        }
        finally
        {
            io.Flush();
        }

        rpc.NotifyAsync("result", interpreter.Memory.AsSpan().Get<int>(interpreter.Registers.StackPointer));

        return 0;
    }

    public static int Run(CommandLineOptions arguments)
    {
        if (arguments.Ipc)
        {
            return RunIpc(arguments);
        }

        ConsoleLogger logger = new()
        {
            LogDebugs = arguments.Verbose,
            LogInfos = true,
            LogWarnings = true,
            EnableProgress = arguments.Verbose,
        };

        if (arguments.Source is null)
        {
            Interactive.Run();
            return 0;
        }

        ImmutableArray<string> additionalImports = ImmutableArray.Create(
            "Primitives"
        );

        switch (arguments.Format)
        {
            case "bytecode":
            {
                logger.LogDebug($"Executing \"{arguments.Source}\" ...");

                List<IExternalFunction> externalFunctions = new();
                using IO io = arguments.Debug ? new VirtualIO() : new StreamedStandardIO();
                io.Register(externalFunctions);
                BytecodeProcessor.AddStaticExternalFunctions(externalFunctions);

                BytecodeProcessor interpreter = default!;
                BBLangGeneratorResult generatedCode = default!;
                DiagnosticsCollection diagnostics = new();

                externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(), "meow", () =>
                {
                    Debugger.Break();

                    if (HeapUtils.AnalyzeMemorySync(interpreter, generatedCode.ExposedFunctions!, out ImmutableArray<HeapUtils.HeapBlock> blocks, out string? error))
                    {
                        Debugger.Break();
                    }
                    else
                    {
                        Debugger.Break();
                    }

                    if (HeapUtils.AnalyzeMemoryTask.Create(interpreter, generatedCode.ExposedFunctions!, out HeapUtils.AnalyzeMemoryTask? task, out error))
                    {
                        Debugger.Break();

                        while (!task.Tick(100))
                        {

                        }

                        Debugger.Break();
                    }
                    else
                    {
                        Debugger.Break();
                    }
                }));

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
                    //ILGeneratorSettings = new IL.Generator.ILGeneratorSettings()
                    //{
                    //    AllowCrash = true,
                    //    // AllowHeap = true,
                    //    AllowPointers = true,
                    //},
                };
                BytecodeInterpreterSettings bytecodeInterpreterSettings = new(BytecodeInterpreterSettings.Default)
                {
                    StackSize = arguments.StackSize ?? BytecodeInterpreterSettings.Default.StackSize,
                    HeapSize = arguments.HeapSize ?? BytecodeInterpreterSettings.Default.HeapSize,
                };

                try
                {
                    CompilerResult compiled = StatementCompiler.CompileFile(arguments.Source, compilerSettings, diagnostics, logger);
                    generatedCode = CodeGeneratorForMain.Generate(compiled, mainGeneratorSettings, logger, diagnostics);

                    if (arguments.IntermediateOutput is not null)
                    {
                        using StreamWriter f = new(arguments.IntermediateOutput);
                        Stringifier.BuilderBase res = new Stringifier.BuilderStream(f)
                        {
                            Minimize = false,
                        };

                        List<CompiledFunction> functions = compiled.Functions.ToList();

                        foreach (CompiledStruct @struct in compiled.Structs)
                        {
                            res.NewLine();
                            res.NewLine();
                            List<CompiledFunction> methods = new();
                            for (int i = 0; i < functions.Count; i++)
                            {
                                if (functions[i].Function.Parameters.Length > 0
                                    && functions[i].Function.Parameters[0].Identifier == StatementKeywords.This
                                    && functions[i].Function.Parameters[0].Definition.IsThis)
                                {
                                    if (functions[i].Function.Parameters[0].Type.Is(out IReferenceType? referenceType)
                                        && referenceType.To.Is(out StructType? structType)
                                        && structType.Struct == @struct)
                                    {
                                        methods.Add(functions[i]);
                                        functions.RemoveAt(i--);
                                    }
                                }
                            }
                            Stringifier.Stringify(@struct, methods, res);
                        }

                        foreach (CompiledEnum @enum in compiled.Enums)
                        {
                            res.NewLine();
                            res.NewLine();
                            Stringifier.Stringify(@enum, res);
                        }

                        foreach (CompiledAlias alias in compiled.Aliases)
                        {
                            res.NewLine();
                            res.NewLine();
                            Stringifier.Stringify(alias, res);
                        }

                        foreach (CompiledFunction function in functions)
                        {
                            res.NewLine();
                            res.NewLine();
                            Stringifier.Stringify(function.Function, function.Body, res);
                        }

                        res.NewLine();
                        res.NewLine();
                        res.Append("/* Top level statements */");
                        res.NewLine();

                        foreach (CompiledVariableConstant statement in compiled.CompiledConstants)
                        {
                            res.NewLine();
                            Stringifier.Stringify(statement, res);
                            res.Append(';');
                        }

                        foreach (CompiledStatement statement in compiled.Statements)
                        {
                            if (statement is CompiledEmptyStatement) continue;
                            res.NewLine();
                            Stringifier.Stringify(statement, res);
                            if (Stringifier.NeedsSemicolon(statement)) res.Append(';');
                        }

                        f.WriteLine();

                        if (!generatedCode.ILGeneratorBuilders.IsDefault)
                        {
                            f.WriteLine();
                            f.WriteLine("/* MSIL */");
                            f.WriteLine();

                            foreach (string builder in generatedCode.ILGeneratorBuilders)
                            {
                                f.WriteLine(builder);
                            }
                        }
                    }
                    diagnostics.Print(logger);
                    if (diagnostics.HasErrors) return 1;
                }
                catch (LanguageExceptionAt ex)
                {
                    diagnostics.Print(logger);
                    logger.LogError(ex);
                    return 1;
                }
                catch (Exception ex)
                {
                    diagnostics.Print(logger);
                    logger.LogError(ex);
                    return 1;
                }

                logger.LogDebug($"Optimized {generatedCode.Statistics.Optimizations} statements");
                logger.LogDebug($"Precomputed {generatedCode.Statistics.Precomputations} statements");
                logger.LogDebug($"Evaluated {generatedCode.Statistics.FunctionEvaluations} functions");
                logger.LogDebug($"Inlined {generatedCode.Statistics.InlinedFunctions} functions");
                logger.LogDebug($"Optimized {generatedCode.Statistics.InstructionLevelOptimizations} instructions");

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
                    string output = Path.GetFullPath(arguments.Output, Environment.CurrentDirectory);
                    Console.WriteLine($"Writing to \"{output}\" ...");
                    File.WriteAllBytes(output, ReadOnlySpan<byte>.Empty);
                    using FileStream stream = File.OpenWrite(output);
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

                interpreter = new(
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
                        if (!string.IsNullOrEmpty(arguments.ProfilerExport))
                        {
                            ProcessorState state = interpreter.GetState();
                            Profiling.Profiler profiler = Path.GetExtension(arguments.ProfilerExport).Equals(".cpuprofile") ? new Profiling.GoogleProfiler(interpreter.DebugInformation) : new Profiling.PerfProfiler(interpreter.DebugInformation);
                            ulong tick = 0;
                            try
                            {
                                while (!state.IsDone)
                                {
                                    state.Tick();
                                    state.ThrowIfCrashed(interpreter.DebugInformation);
                                    profiler.Sample(in state, ++tick);
                                }
                            }
                            catch (RuntimeException ex)
                            {
                                ex.DebugInformation = interpreter.DebugInformation;
                                ex.Context ??= state.GetContext();
                                throw;
                            }
                            profiler.WriteTo(arguments.ProfilerExport);
                            if (profiler is Profiling.GoogleProfiler googleProfiler)
                            {
                                googleProfiler.WriteHeapProfileTo(Path.ChangeExtension(arguments.ProfilerExport, "heapprofile"));
                            }
                            interpreter.Registers = state.Registers;
                        }
                        else
                        {
                            interpreter.RunUntilCompletion();
                        }
                    }
                    else
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(arguments.ProfilerExport))
                            {
                                ProcessorState state = interpreter.GetState();
                                Profiling.Profiler profiler = Path.GetExtension(arguments.ProfilerExport).Equals(".cpuprofile") ? new Profiling.GoogleProfiler(interpreter.DebugInformation) : new Profiling.PerfProfiler(interpreter.DebugInformation);
                                ulong tick = 0;
                                try
                                {
                                    while (!state.IsDone)
                                    {
                                        state.Tick();
                                        state.ThrowIfCrashed(interpreter.DebugInformation);
                                        profiler.Sample(in state, ++tick);
                                    }
                                }
                                catch (RuntimeException ex)
                                {
                                    ex.DebugInformation = interpreter.DebugInformation;
                                    ex.Context ??= state.GetContext();
                                    throw;
                                }
                                profiler.WriteTo(arguments.ProfilerExport);
                                if (profiler is Profiling.GoogleProfiler googleProfiler)
                                {
                                    googleProfiler.WriteHeapProfileTo(Path.ChangeExtension(arguments.ProfilerExport, "heapprofile"));
                                }
                                interpreter.Registers = state.Registers;
                            }
                            else
                            {
                                interpreter.RunUntilCompletion();
                            }
                        }
                        catch (RuntimeException error)
                        {
                            logger.LogError(error.ToString(true, true));
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
#if TRIMMED
                return 1;
#else
                logger.LogDebug($"Executing \"{arguments.Source}\" ...");

                List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions(StandardIO.Instance);

                DiagnosticsCollection diagnostics = new();

                CompilerSettings compilerSettings = new(IL.Generator.CodeGeneratorForIL.DefaultCompilerSettings)
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

                CompilerResult compiled = StatementCompiler.CompileFile(arguments.Source, new(compilerSettings)
                {
                    ExternalFunctions = externalFunctions.ToImmutableArray(),
                    PreprocessorVariables = PreprocessorVariables.IL,
                }, diagnostics, logger);
                IL.Generator.ILGeneratorResult res = IL.Generator.CodeGeneratorForIL.Generate(compiled, diagnostics, new()
                {
                    AllowCrash = true,
                    AllowHeap = true,
                    AllowPointers = true,
                    AllowUnsafePointers = true,
                });

                if (arguments.Output is not null)
                {
                    string output = Path.GetFullPath(arguments.Output, Environment.CurrentDirectory);
                    Console.WriteLine($"Writing to \"{output}\" ...");
                    Stringifier.Builder builder = new();

                    ImmutableArray<DynamicMethod> methods = res.Methods;
                    foreach (Type type in res.Module.GetTypes())
                    {
                        builder.NewLine();
                        builder.NewLine();
                        Stringifier.StringifyDefinition(type, builder);
                    }
                    foreach (MethodInfo method in res.Module.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Where(v => !methods.Any(w => v.Equals(w))))
                    {
                        builder.NewLine();
                        builder.NewLine();
                        Stringifier.StringifyDefinition(method, builder);
                    }
                    foreach (FieldInfo field in res.Module.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        builder.NewLine();
                        builder.NewLine();
                        Stringifier.StringifyDefinition(field, builder);
                    }
                    foreach (DynamicMethod method in methods)
                    {
                        builder.NewLine();
                        builder.NewLine();
                        Stringifier.StringifyDefinition(method, builder);
                    }
                    builder.NewLine();
                    builder.NewLine();
                    Stringifier.StringifyDefinition(res.EntryPoint, builder);

                    File.WriteAllText(output, builder.ToString());
                }

                diagnostics.Print(logger);
                if (diagnostics.HasErrors) return 1;

                res.EntryPointDelegate.Invoke();
                return 0;
#endif
            }
            case "brainfuck":
            {
                logger.LogDebug($"Executing \"{arguments.Source}\" ...");

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
                    CompilerResult compiled = StatementCompiler.CompileFile(arguments.Source, compilerSettings, diagnostics, logger);
                    generated = CodeGeneratorForBrainfuck.Generate(compiled, brainfuckGeneratorSettings, logger, diagnostics);
                    diagnostics.Print(logger);
                    if (diagnostics.HasErrors) return 1;
                    logger.LogDebug($"Optimized {generated.Statistics.Optimizations} statements");
                    logger.LogDebug($"Precomputed {generated.Statistics.Precomputations} statements");
                    logger.LogDebug($"Evaluated {generated.Statistics.FunctionEvaluations} functions");
                }
                catch (LanguageExceptionAt exception)
                {
                    diagnostics.Print(logger);
                    logger.LogError(exception);
                    return 1;
                }
                catch (Exception exception)
                {
                    diagnostics.Print(logger);
                    logger.LogError(exception);
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

                IDisposableProgress<float> p1 = logger.Progress(LogType.Debug);
                generated.Code = BrainfuckCode.RemoveNoncodes(generated.Code, generated.DebugInfo, p1);
                p1.Dispose();

                logger.LogDebug($"Minify code ...");
                int prevCodeLength = generated.Code.Length;
                IDisposableProgress<string> p2 = logger.Label(LogType.Debug);
                generated.Code = Minifier.Minify(generated.Code, generated.DebugInfo, p2);
                p2.Dispose();
                logger.LogDebug($"Minification: {prevCodeLength} -> {generated.Code.Length} ({((float)generated.Code.Length - prevCodeLength) / (float)generated.Code.Length * 100f:#}%)");

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
                    string output = Path.GetFullPath(arguments.Output, Environment.CurrentDirectory);
                    Console.WriteLine($"Writing to \"{output}\" ...");
                    File.WriteAllText(output, generated.Code);
                }

                InterpreterCompact interpreter = new();
                interpreter.LoadCode(generated.Code, generated.DebugInfo, logger);
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
                    RuntimeInfo = new()
                    {
                        PointerSize = nint.Size,
                    },
                };
                MainGeneratorSettings mainGeneratorSettings = new(MainGeneratorSettings.Default)
                {
                    CheckNullPointers = !arguments.NoNullcheck,
                    Optimizations = arguments.DontOptimize ? GeneratorOptimizationSettings.None : GeneratorOptimizationSettings.All,
                    StackSize = arguments.StackSize ?? MainGeneratorSettings.Default.StackSize,
                    PointerSize = nint.Size,
                };

                DiagnosticsCollection diagnostics = new();

                CompilerResult compiled = StatementCompiler.CompileFile(arguments.Source, compilerSettings, diagnostics, logger);
                diagnostics.Print(logger);
                if (diagnostics.HasErrors) return 1;

                diagnostics.Clear();
                BBLangGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, mainGeneratorSettings, logger, diagnostics);
                diagnostics.Print(logger);
                if (diagnostics.HasErrors) return 1;

                string asm = Assembly.Generator.ConverterForAsm.Convert(generatedCode.Code.AsSpan(), generatedCode.DebugInfo, (BitWidth)nint.Size);

                logger.LogDebug("Assembling and linking ...");

                diagnostics.Clear();
                byte[] code = Assembler.Assemble(asm, diagnostics);
                diagnostics.Print(logger);
                if (diagnostics.HasErrors) return 1;

                using NativeFunction f = NativeFunction.Allocate(code);

                return f.AsDelegate<CodeGeneratorForNative.JitFn>()();
            }
            case "assembly":
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                { throw new PlatformNotSupportedException($"This is only supported on Linux"); }

                logger.LogDebug($"Executing \"{arguments.Source}\" ...");

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
                    CompilerResult compiled = StatementCompiler.CompileFile(arguments.Source, compilerSettings, diagnostics, logger);
                    using NativeFunction f = CodeGeneratorForNative.Generate(compiled, diagnostics);
                    diagnostics.Print(logger);
                    if (diagnostics.HasErrors) return 1;
                    return f.AsDelegate<CodeGeneratorForNative.JitFn>()();
                }
                catch (LanguageExceptionAt ex)
                {
                    diagnostics.Print(logger);
                    logger.LogError(ex);
                    return 1;
                }
                catch (Exception ex)
                {
                    diagnostics.Print(logger);
                    logger.LogError(ex);
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

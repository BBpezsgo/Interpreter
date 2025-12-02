using System.Collections.Immutable;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.Tests;

static class InterpreterRunner
{
    public readonly struct MainResult : IResult
    {
        public readonly string StdOutput { get; }
        public readonly string StdError { get; }
        public readonly int ExitCode { get; }
        public readonly ImmutableArray<byte> Heap { get; }

        public MainResult(string stdOut, string stdErr, BytecodeProcessor interpreter)
        {
            StdOutput = stdOut;
            StdError = stdErr;
            ExitCode = interpreter.Memory.AsSpan().Get<int>(interpreter.Registers.StackPointer);
            Heap = interpreter.Memory.AsImmutableUnsafe();
        }
    }

    public static MainGeneratorSettings MainGeneratorSettings => new(MainGeneratorSettings.Default)
    {
        StackSize = MainGeneratorSettings.Default.StackSize,
    };

    public static BytecodeInterpreterSettings BytecodeInterpreterSettings => new()
    {
        HeapSize = 2048,
        StackSize = BytecodeInterpreterSettings.Default.StackSize,
    };

    public static (MainResult Optimized, MainResult Unoptimized) Run(string file, string input, Action<List<IExternalFunction>>? externalFunctionAdder = null)
    {
        FixedIO io = new(input);
        List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions(io);
        externalFunctionAdder?.Invoke(externalFunctions);

        DiagnosticsCollection diagnostics = new();

        CompilerResult compiled = StatementCompiler.CompileFile(file, new CompilerSettings(Utils.GetCompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings))
        {
            ExternalFunctions = externalFunctions.ToImmutableArray(),
            Optimizations = OptimizationSettings.All,
            PreprocessorVariables = PreprocessorVariables.Normal,
            CompileEverything = true,
        }, diagnostics);
        diagnostics.Throw();

        BBLangGeneratorResult generatedOptimized = CodeGeneratorForMain.Generate(compiled, MainGeneratorSettings, null, diagnostics);
        diagnostics.Throw();

        compiled = StatementCompiler.CompileFile(file, new CompilerSettings(Utils.GetCompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings))
        {
            ExternalFunctions = externalFunctions.ToImmutableArray(),
            Optimizations = OptimizationSettings.None,
            PreprocessorVariables = PreprocessorVariables.Normal,
            CompileEverything = true,
        }, diagnostics);
        diagnostics.Throw();
        BBLangGeneratorResult generatedUnoptimized = CodeGeneratorForMain.Generate(compiled, new MainGeneratorSettings(MainGeneratorSettings)
        {
            Optimizations = GeneratorOptimizationSettings.None,
        }, null, diagnostics);
        diagnostics.Throw();

        /*
        compiled = StatementCompiler.CompileFile(file, new CompilerSettings(BytecodeCompilerSettings)
        {
            ExternalFunctions = externalFunctions.ToImmutableArray(),
            DontOptimize = false,
            PreprocessorVariables = PreprocessorVariables.Normal,
            AdditionalImports = AdditionalImports,
            SourceProviders = ImmutableArray.Create<ISourceProvider>(
                new FileSourceProvider()
                {
                    ExtraDirectories = new string[]
                    {
                        BasePath
                    },
                }
            ),
        }, diagnostics);
        BBLangGeneratorResult generatedCodeIL = CodeGeneratorForMain.Generate(compiled, new MainGeneratorSettings(MainGeneratorSettings)
        {
            ILGeneratorSettings = new LanguageCore.IL.Generator.ILGeneratorSettings()
            {
                AllowCrash = true,
                AllowPointers = true,
                AllowHeap = true,
            },
        }, null, diagnostics);

        compiled = StatementCompiler.CompileFile(file, new CompilerSettings(BytecodeCompilerSettings)
        {
            ExternalFunctions = externalFunctions.ToImmutableArray(),
            DontOptimize = true,
            PreprocessorVariables = PreprocessorVariables.Normal,
            AdditionalImports = AdditionalImports,
            SourceProviders = ImmutableArray.Create<ISourceProvider>(
                new FileSourceProvider()
                {
                    ExtraDirectories = new string[]
                    {
                        BasePath
                    },
                }
            ),
        }, diagnostics);
        BBLangGeneratorResult generatedCodeILUnoptimized = CodeGeneratorForMain.Generate(compiled, new MainGeneratorSettings(MainGeneratorSettings)
        {
            DontOptimize = true,
            ILGeneratorSettings = new LanguageCore.IL.Generator.ILGeneratorSettings()
            {
                AllowCrash = true,
                AllowPointers = true,
                AllowHeap = true,
            },
        }, null, diagnostics);
        */

        GC.Collect();

        MainResult Execute(BBLangGeneratorResult code)
        {
            io.Reset();

            BytecodeProcessor interpreter = new(
                BytecodeInterpreterSettings,
                code.Code,
                null,
                code.DebugInfo,
                externalFunctions,
                code.GeneratedUnmanagedFunctions
            );

            while (!interpreter.IsDone)
            { interpreter.Tick(); }

            return new MainResult(io.Output.ToString(), string.Empty, interpreter);
        }

        MainResult result = Execute(generatedOptimized);
        MainResult unoptimizedResult = Execute(generatedUnoptimized);
        //MainResult unoptimizedResultIL = Execute(generatedCodeILUnoptimized);
        //MainResult resultIL = Execute(generatedCodeIL);

        //if (result.StdOutput != unoptimizedResult.StdOutput)
        //{ throw new AssertFailedException($"StdOutput are different on optimized and unoptimized version (\"{result.StdOutput.Escape()}\" != \"{unoptimizedResult.StdOutput.Escape()}\")"); }

        //if (result.StdOutput != unoptimizedResultIL.StdOutput)
        //{ throw new AssertFailedException($"StdOutput are different on optimized normal and unoptimized IL version (\"{result.StdOutput.Escape()}\" != \"{unoptimizedResultIL.StdOutput.Escape()}\")"); }

        //if (result.StdOutput != resultIL.StdOutput)
        //{ throw new AssertFailedException($"StdOutput are different on normal and IL version (\"{result.StdOutput.Escape()}\" != \"{resultIL.StdOutput.Escape()}\")"); }

        //if (result.ExitCode != unoptimizedResult.ExitCode)
        //{ throw new AssertFailedException($"ExitCode are different on optimized and unoptimized version ({result.ExitCode} != {unoptimizedResult.ExitCode})"); }

        //if (result.ExitCode != unoptimizedResultIL.ExitCode)
        //{ throw new AssertFailedException($"ExitCode are different on optimized normal and unoptimized IL version ({result.ExitCode} != {unoptimizedResultIL.ExitCode})"); }

        //if (result.ExitCode != resultIL.ExitCode)
        //{ throw new AssertFailedException($"ExitCode are different on normal and IL version ({result.ExitCode} != {resultIL.ExitCode})"); }

        return (result, unoptimizedResult);
    }
}

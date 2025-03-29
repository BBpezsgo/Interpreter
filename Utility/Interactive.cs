using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore;

public static class Interactive
{
    public static void Run()
    {
        while (true)
        {
            Console.Write(" >>> ");
            string? input = Console.ReadLine();
            if (input == "exit") break;
            Evaluate(input);
        }
    }

    static void Evaluate(string? source)
    {
        source ??= string.Empty;
        DiagnosticsCollection diagnostics = new();
        BBLangGeneratorResult generated;

        ImmutableArray<ISourceProvider> sourceProviders = ImmutableArray.Create<ISourceProvider>(
            new MemorySourceProvider(new Dictionary<string, string>()
            {
                { "memory:///", source }
            }),
            new FileSourceProvider()
            {
                ExtraDirectories = new string[]
                {
                    "/home/BB/Projects/BBLang/Core/StandardLibrary"
                },
            }
        );

        try
        {
            List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions();

            CompilerResult compiled = StatementCompiler.CompileFile(
                "memory:///",
                new CompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings)
                {
                    ExternalFunctions = externalFunctions.ToImmutableArray(),
                    PreprocessorVariables = PreprocessorVariables.Interactive,
                    SourceProviders = sourceProviders,
                },
                diagnostics);

            generated = CodeGeneratorForMain.Generate(
                compiled,
                MainGeneratorSettings.Default,
                null,
                diagnostics);
        }
        catch (LanguageException ex)
        {
            diagnostics.Add((Diagnostic)ex);
            return;
        }
        finally
        {
            diagnostics.Print(sourceProviders);
        }

        if (diagnostics.HasErrors) return;

        BytecodeProcessor interpreter = new(BytecodeInterpreterSettings.Default, generated.Code, null, generated.DebugInfo, null, generated.GeneratedUnmanagedFunctions);

        interpreter.IO.OnStdOut += Console.Write;
        interpreter.IO.OnNeedInput += () => interpreter.IO.SendKey(Console.ReadKey().KeyChar);

        int exitCodeAddress = interpreter.Registers.StackPointer + (sizeof(int) * ProcessorState.StackDirection);

        while (!interpreter.IsDone)
        { interpreter.Tick(); }

        {
            int exitCode = interpreter.Memory.AsSpan().Get<int>(exitCodeAddress);

            Console.WriteLine();
            Console.WriteLine($"Exit code: {exitCode}");
        }
    }
}

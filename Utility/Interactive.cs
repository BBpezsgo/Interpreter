using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Parser.Statement;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;

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

        try
        {
            ImmutableArray<Token> tokens = StringTokenizer.Tokenize(source, new(), PreprocessorVariables.Interactive, Utils.AssemblyFile).Tokens;
            if (tokens.Length == 0) return;
            Statement statement = Parser.Parser.ParseStatement(tokens, Utils.AssemblyFile, diagnostics);

            List<IExternalFunction> externalFunctions = BytecodeProcessorEx.GetExternalFunctions();

            CompilerResult compiled = Compiler.StatementCompiler.CompileInteractive(
                statement,
                new CompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings)
                {
                    BasePath = "/home/BB/Projects/BBLang/Core/StandardLibrary",
                    ExternalFunctions = externalFunctions.ToImmutableArray(),
                    PreprocessorVariables = PreprocessorVariables.Interactive,
                },
                diagnostics,
                Utils.AssemblyFile);

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
            diagnostics.Print(v => v == Utils.AssemblyFile ? source : null);
        }

        if (diagnostics.HasErrors) return;

        BytecodeProcessorEx interpreter = new(BytecodeInterpreterSettings.Default, generated.Code, null, generated.DebugInfo);

        interpreter.IO.OnStdOut += Console.Write;
        interpreter.IO.OnNeedInput += () => interpreter.IO.SendKey(Console.ReadKey().KeyChar);

        int exitCodeAddress = interpreter.Processor.Registers.StackPointer + (sizeof(int) * BytecodeProcessor.StackDirection);

        while (!interpreter.Processor.IsDone)
        { interpreter.Tick(); }

        {
            int exitCode = interpreter.Processor.Memory.AsSpan().Get<int>(exitCodeAddress);

            Console.WriteLine();
            Console.WriteLine($"Exit code: {exitCode}");
        }
    }
}

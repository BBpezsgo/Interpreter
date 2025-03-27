using System.IO;
using System.Runtime.CompilerServices;
using LanguageCore;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace Examples;

public static class ExecutionManager
{
    static string GetScriptPath([CallerFilePath] string path = null!)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "execution_manager.bbc"));

    static string GetStandardLibraryPath([CallerFilePath] string path = null!)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "StandardLibrary"));

    static BytecodeProcessor BytecodeProcessor = null;

    static ExposedFunction InitFunction;
    static ExposedFunction TickFunction;
    static ExposedFunction EndFunction;

    public static void Run()
    {
        string scriptPath = GetScriptPath();
        string standardLibraryPath = GetStandardLibraryPath();

        List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions();
        DiagnosticsCollection diagnostics = new();
        CompilerResult compiled = StatementCompiler.CompileFile(scriptPath, new CompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings)
        {
            ExternalFunctions = externalFunctions.ToImmutableArray(),
            SourceProviders = ImmutableArray.Create<ISourceProvider>(
                new FileSourceProvider()
                {
                    ExtraDirectories = new string[]
                    {
                        standardLibraryPath
                    },
                }
            ),
        }, diagnostics);
        BBLangGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, new MainGeneratorSettings(MainGeneratorSettings.Default)
        {
            CleanupGlobalVaraibles = false,
        }, null, diagnostics);
        diagnostics.Print();
        diagnostics.Throw();

        BytecodeProcessor = new(
            BytecodeInterpreterSettings.Default,
            generatedCode.Code,
            null,
            generatedCode.DebugInfo,
            externalFunctions);
        BytecodeProcessor.IO.RegisterStandardIO();

        InitFunction = generatedCode.ExposedFunctions["init"];
        TickFunction = generatedCode.ExposedFunctions["tick"];
        EndFunction = generatedCode.ExposedFunctions["end"];

        Simulate();
    }

    static void Simulate()
    {
        BytecodeProcessor.RunUntilCompletion();

        BytecodeProcessor.CallSync(InitFunction);

        for (int i = 0; i < 10; i++)
        {
            BytecodeProcessor.CallSync(TickFunction);
        }

        BytecodeProcessor.CallSync(EndFunction);
    }
}

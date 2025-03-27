/*
This demonstrates how you can provide constants from C# to the BBC compiler.
*/

using System.IO;
using System.Runtime.CompilerServices;
using LanguageCore;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace Examples;

public static class ExternalConstants
{
    static string GetScriptPath([CallerFilePath] string path = null!)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "external_constants.bbc"));

    static string GetStandardLibraryPath([CallerFilePath] string path = null!)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "StandardLibrary"));

    public static void Run()
    {
        string scriptPath = GetScriptPath();
        string standardLibraryPath = GetStandardLibraryPath();

        List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions();

        DiagnosticsCollection diagnostics = new();
        CompilerResult compiled = StatementCompiler.CompileFile(scriptPath, new CompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings)
        {
            // Here you can provide the external constants.
            ExternalConstants = ImmutableArray.Create(
                new ExternalConstant("external_1", 86),
                new ExternalConstant("external_2", 54),
                new ExternalConstant("external_3", 75),
                new ExternalConstant("external_4", 12)
            ),
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
        BBLangGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, MainGeneratorSettings.Default, null, diagnostics);
        diagnostics.Print();
        diagnostics.Throw();

        BytecodeProcessor interpreter = new(
            BytecodeInterpreterSettings.Default,
            generatedCode.Code,
            null,
            generatedCode.DebugInfo,
            externalFunctions);

        interpreter.IO.RegisterStandardIO();

        while (!interpreter.IsDone)
        { interpreter.Tick(); }
    }
}
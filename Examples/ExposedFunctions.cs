/*
This demonstrates how you can call functions in a script from C#.
*/

using System.IO;
using System.Runtime.CompilerServices;
using LanguageCore;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace Examples;

public static class ExposedFunctions
{
    static string GetScriptPath([CallerFilePath] string path = null!)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "exposed_functions.bbc"));

    static string GetStandardLibraryPath([CallerFilePath] string path = null!)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "StandardLibrary"));

    public static void Run()
    {
        string scriptPath = GetScriptPath();
        string standardLibraryPath = GetStandardLibraryPath();

        List<IExternalFunction> externalFunctions = BytecodeProcessorEx.GetExternalFunctions();
        DiagnosticsCollection diagnostics = new();
        CompilerResult compiled = StatementCompiler.CompileFile(new Uri(scriptPath), new CompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings)
        {
            BasePath = standardLibraryPath,
            ExternalFunctions = externalFunctions.ToImmutableArray(),
        }, diagnostics);
        BBLangGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, MainGeneratorSettings.Default, null, diagnostics);
        diagnostics.Print();
        diagnostics.Throw();

        BytecodeProcessorEx interpreter = new(
            BytecodeInterpreterSettings.Default,
            generatedCode.Code,
            null,
            generatedCode.DebugInfo,
            externalFunctions);

        interpreter.IO.RegisterStandardIO();

        interpreter.Call(generatedCode.ExposedFunctions["hello"]);
        interpreter.Call(generatedCode.ExposedFunctions["with_arguments"], 4, -17);
        UserCall promise3 = interpreter.Call(generatedCode.ExposedFunctions["with_return_value"], 64, 2);

        while (interpreter.Tick()) ;

        int result3 = promise3.Result!.To<int>();

        Console.WriteLine($"Function \"with_return_value\" returned {result3}");
    }
}
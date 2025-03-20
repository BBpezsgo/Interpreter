/*
This demonstrates how you can call C# functions from script files.
*/

using System.IO;
using System.Runtime.CompilerServices;
using LanguageCore;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace Examples;

public static class ExternalFunctions
{
    static string GetScriptPath([CallerFilePath] string path = null!)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "external_functions.bbc"));

    static string GetStandardLibraryPath([CallerFilePath] string path = null!)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "StandardLibrary"));

    public static void Run()
    {
        string scriptPath = GetScriptPath();
        string standardLibraryPath = GetStandardLibraryPath();

        List<IExternalFunction> externalFunctions = BytecodeProcessorEx.GetExternalFunctions();

        // "External functions" are called from the interpreter by its id and not its name, so you have to provide
        // the functions' ids too. You can generate a unique id with the `externalFunctions.GenerateId()` extension function.

        // The parameters' types are automatically converted to the correct format that can be used by the interpreter.
        // In theory you can define parameters with complex types, but I didn't tested it yet.

        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(), "hello", () =>
        {
            Console.WriteLine($"This was called from the script!!!");
        }));

        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(), "with_arguments", (int a, int b) =>
        {
            Console.WriteLine($"This was called with these arguments: {a}, {b}");
        }));

        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(), "with_return_value", (int a, int b) =>
        {
            int result = a + b;
            Console.WriteLine($"This was called with these arguments: {a}, {b} and the result is {result}");
            return result;
        }));

        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create<float, float>(externalFunctions.GenerateId(), "sin", MathF.Sin));

        DiagnosticsCollection diagnostics = new();
        CompilerResult compiled = StatementCompiler.CompileFile(new Uri(scriptPath), new CompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings)
        {
            ExternalFunctions = externalFunctions.ToImmutableArray(),
            BasePath = standardLibraryPath,
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

        while (!interpreter.Processor.IsDone)
        { interpreter.Tick(); }
    }
}
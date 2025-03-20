/*
This demonstrates a simple usage of this project.
*/

using System.IO;
using System.Runtime.CompilerServices;
using LanguageCore;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace Examples;

public static class HelloWorld
{
    static string GetScriptPath([CallerFilePath] string path = null!)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "hello_world.bbc"));

    static string GetStandardLibraryPath([CallerFilePath] string path = null!)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "StandardLibrary"));

    public static void Run()
    {
        // This will get the example script's path. With the `CallerFilePath` attribute it should be correct on other systems too.
        string scriptPath = GetScriptPath();
        // This is the same but with the standard library.
        string standardLibraryPath = GetStandardLibraryPath();

        // This will generate some stub function informations, so it will not produce warnings.
        // This kinda a bad implementation, so I have to fix it sometime.
        List<IExternalFunction> externalFunctions = BytecodeProcessorEx.GetExternalFunctions();

        // With this `DiagnosticsCollection` you can get all the diagnostics that the compiler and the generator produced.
        // This contains all the fatal errors too, if you use the generated output while there are errors, there will be some unexpected behavior.
        DiagnosticsCollection diagnostics = new();

        // This will collect and compiles the necessary files and does some basic optimizations.
        CompilerResult compiled = StatementCompiler.CompileFile(new Uri(scriptPath), new CompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings)
        {
            BasePath = standardLibraryPath,
            ExternalFunctions = externalFunctions.ToImmutableArray(),
            PreprocessorVariables = PreprocessorVariables.Normal,
        }, diagnostics);

        // Now you can actually generate the bytecodes.
        BBLangGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, MainGeneratorSettings.Default, null, diagnostics);

        // This will print all the diagnostics to the console, expect for debugging and optimization items.
        diagnostics.Print();

        // If there are fatal errors, make sure not to execute the malformed code.
        diagnostics.Throw();

        // This will generate some predefined "external functions" and prepares the processor for executing code.
        BytecodeProcessorEx interpreter = new(
            BytecodeInterpreterSettings.Default,
            generatedCode.Code,
            null,
            generatedCode.DebugInfo,
            externalFunctions);

        // You can define your own IO handler, but this will use the standard IO (the `System.Console`).
        interpreter.IO.RegisterStandardIO();

        // This will execute all the bytecodes until there are none left.
        while (interpreter.Tick()) ;
    }
}
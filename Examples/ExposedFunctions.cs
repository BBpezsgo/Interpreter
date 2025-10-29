/*
This demonstrates how you can call functions in a script from C#.
*/

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

        List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions(StandardIO.Instance);
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
            // The global variables would be cleaned up at the end of the top-level statements.
            // To prevent undefined behavior when accessing global variables from an exposed function,
            // disable the cleanup of the variables.
            CleanupGlobalVaraibles = false,
        }, null, diagnostics);
        diagnostics.Print();
        diagnostics.Throw();

        BytecodeProcessor interpreter = new(
            BytecodeInterpreterSettings.Default,
            generatedCode.Code,
            null,
            generatedCode.DebugInfo,
            externalFunctions,
            generatedCode.GeneratedUnmanagedFunctions
        );

        // These will not interpret the bytecodes, only queues the calls. They will be executed automatically
        // when the interpreter has no bytecodes to execute. (ie when the top-level statements or any previous calls are done)

        interpreter.Call(generatedCode.ExposedFunctions["hello"]);
        interpreter.Call(generatedCode.ExposedFunctions["with_arguments"], 4, -17);
        UserCall promise3 = interpreter.Call(generatedCode.ExposedFunctions["with_return_value"], 64, 2);

        // The `Result` field of the promise will be set after the function is executed with the result of the call.
        // Until then, it will be `null`.

        interpreter.RunUntilCompletion();

        // There are no more bytecodes and no more calls to execute, so it will definietly have a `Result`.
        // You can convert the `byte[]` to other types with the `To<T>` extension function.
        int result3 = promise3.Result!.To<int>();

        Console.WriteLine($"Function \"with_return_value\" returned {result3}");
    }
}

using System.IO;
using System.Runtime.CompilerServices;
using LanguageCore;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace Examples;

public static class Strings
{
    static string GetScriptPath([CallerFilePath] string path = null!)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "strings.bbc"));

    static string GetStandardLibraryPath([CallerFilePath] string path = null!)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "StandardLibrary"));

    public static void Run()
    {
        string scriptPath = GetScriptPath();
        string standardLibraryPath = GetStandardLibraryPath();

        byte[] memory = new byte[BytecodeInterpreterSettings.Default.HeapSize + BytecodeInterpreterSettings.Default.StackSize];

        List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions();

        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(), "str", (int stringPtr) =>
        {
            string? value = HeapUtils.GetString(memory, stringPtr);
            Console.WriteLine($"String was passed: \"{value}\"");
        }));

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
        BBLangGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, MainGeneratorSettings.Default, null, diagnostics);
        diagnostics.Print();
        diagnostics.Throw();

        BytecodeProcessor interpreter = new(
            BytecodeInterpreterSettings.Default,
            generatedCode.Code,
            memory,
            generatedCode.DebugInfo,
            externalFunctions);

        interpreter.IO.RegisterStandardIO();

        while (interpreter.Tick()) { }

        // Converting the string to bytes
        byte[] text = Encoding.Unicode.GetBytes("拜拜");

        // Allocating memory for the string and for the null character
        UserCall allocCall = interpreter.Call(generatedCode.ExposedFunctions["alloc"], text.Length + 2);
        while (interpreter.Tick()) { }

        // Copying the text bytes into the memory
        text.CopyTo(interpreter.Memory.AsSpan()[allocCall.Result!.To<int>()..]);
        // Adding a null character to the end
        ((Span<byte>)interpreter.Memory).Set(allocCall.Result!.To<int>() + text.Length, '\0');

        // Calling the exposed function with the pointer to the string
        interpreter.Call(generatedCode.ExposedFunctions["str"], allocCall.Result!.To<int>());
        while (interpreter.Tick()) { }
    }
}

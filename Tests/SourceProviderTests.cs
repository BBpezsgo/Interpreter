using System.Collections.Immutable;
using System.Text;
using LanguageCore;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace Tests;

[TestClass, TestCategory("Source Provider")]
public class SourceProviderTests
{
    [TestMethod]
    public void TestHttp()
    {
        using HttpServer _ = new("http://localhost:6789/", new Dictionary<string, string>()
        {
            {
                "/main.bbc",
                """
                using stdlib;

                u16[] message = "Hello world";
                Print(&message);
                """
            },
            {
                "/stdlib.bbc",
                """
                [External("stdout")]
                void Print(u16 c);

                export void Print(u16[]* message)
                {
                    for (i32 i = 0; message[i]; i++)
                    {
                        Print(message[i]);
                    }
                }
                """
            },
        });

        DiagnosticsCollection diagnostics = new();

        List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions();

        CompilerResult compiled = StatementCompiler.CompileFile("http://localhost:6789/main.bbc", new CompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings)
        {
            ExternalFunctions = externalFunctions.ToImmutableArray(),
            PreprocessorVariables = PreprocessorVariables.Normal,
            SourceProviders = ImmutableArray.Create<ISourceProvider>(
                new HttpSourceProvider()
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
            externalFunctions,
            generatedCode.GeneratedUnmanagedFunctions);

        StringBuilder output = new();

        interpreter.IO.OnStdOut += c => output.Append(c);

        while (interpreter.Tick()) ;

        Assert.AreEqual(
            "Hello world",
            output.ToString()
        );
    }

    [Ignore]
    [TestMethod]
    public void TestGithub()
    {
        DiagnosticsCollection diagnostics = new();

        List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions();

        CompilerResult compiled = StatementCompiler.CompileFile("https://raw.githubusercontent.com/BBpezsgo/Interpreter/refs/heads/master/Examples/hello_world.bbc", new CompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings)
        {
            ExternalFunctions = externalFunctions.ToImmutableArray(),
            PreprocessorVariables = PreprocessorVariables.Normal,
            SourceProviders = ImmutableArray.Create<ISourceProvider>(
                new HttpSourceProvider(),
                new FileSourceProvider()
                {
                    AllowLocalFilesFromWeb = true,
                    ExtraDirectories = [
                        "/home/BB/Projects/BBLang/Core/StandardLibrary"
                    ],
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
            externalFunctions,
            generatedCode.GeneratedUnmanagedFunctions);

        StringBuilder output = new();

        interpreter.IO.OnStdOut += c => output.Append(c);

        while (interpreter.Tick()) ;

        Assert.AreEqual(
            "Hello world!\r\n",
            output.ToString()
        );
    }

    [TestMethod]
    public void TestCallback()
    {
        DiagnosticsCollection diagnostics = new();

        List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions();

        CompilerResult compiled = StatementCompiler.CompileFile("/home/BB/Projects/BBLang/Core/Examples/hello_world.bbc", new CompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings)
        {
            ExternalFunctions = externalFunctions.ToImmutableArray(),
            PreprocessorVariables = PreprocessorVariables.Normal,
            SourceProviders = ImmutableArray.Create<ISourceProvider>(
                new CallbackSourceProviderAsync(static file =>
                {
                    if (!file.IsFile) return null;
                    if (!File.Exists(file.AbsolutePath)) return null;
                    return Task.FromResult<Stream>(File.OpenRead(file.AbsolutePath));
                }, "/home/BB/Projects/BBLang/Core/StandardLibrary")
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
            externalFunctions,
            generatedCode.GeneratedUnmanagedFunctions);

        StringBuilder output = new();

        interpreter.IO.OnStdOut += c => output.Append(c);

        while (interpreter.Tick()) ;

        Assert.AreEqual(
            "Hello world!\r\n",
            output.ToString()
        );
    }
}

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

        List<IExternalFunction> externalFunctions = BytecodeProcessorEx.GetExternalFunctions();

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

        BytecodeProcessorEx interpreter = new(
            BytecodeInterpreterSettings.Default,
            generatedCode.Code,
            null,
            generatedCode.DebugInfo,
            externalFunctions);

        StringBuilder output = new();

        interpreter.IO.OnStdOut += c => output.Append(c);

        while (interpreter.Tick()) ;

        Assert.AreEqual(
            "Hello world",
            output.ToString()
        );
    }
}

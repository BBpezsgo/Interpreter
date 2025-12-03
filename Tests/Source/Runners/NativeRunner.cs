using System.Diagnostics;
using System.Runtime.Versioning;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Native.Generator;

namespace LanguageCore.Tests;

static class NativeRunner
{
    public readonly struct AssemblyResult : IResult
    {
        public readonly string StdOutput { get; }
        public readonly string StdError { get; }

        public readonly int ExitCode { get; }

        public AssemblyResult(Process process)
        {
            StdOutput = process.StandardOutput.ReadToEnd();
            StdError = process.StandardError.ReadToEnd();

            ExitCode = process.ExitCode;
        }

        public AssemblyResult(string stdOutput, string stdError, int exitCode)
        {
            StdOutput = stdOutput;
            StdError = stdError;
            ExitCode = exitCode;
        }
    }

#if NET
    [SupportedOSPlatform("linux")]
#endif
    public static AssemblyResult Run(string file, string input)
    {
        DiagnosticsCollection diagnostics = new();

        CompilerResult compiled = StatementCompiler.CompileFile(file, new CompilerSettings(Utils.GetCompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings))
        {
            CompileEverything = true,
            Optimizations = OptimizationSettings.None,
        }, diagnostics);

        using NativeFunction f = CodeGeneratorForNative.Generate(compiled, diagnostics);

        diagnostics.Throw();

        int result;
        string output;

        using (StringReader stdin = new(input))
        using (StringWriter stdout = new())
        {
            TextReader originalStdin = Console.In;
            TextWriter originalStdout = Console.Out;

            Console.SetIn(stdin);
            Console.SetOut(stdout);

            result = f.AsDelegate<CodeGeneratorForNative.JitFn>()();

            output = stdout.ToString();

            Console.SetIn(originalStdin);
            Console.SetOut(originalStdout);
        }

        return new AssemblyResult(output, string.Empty, result);

        /*
                BBLangGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, MainGeneratorSettings, null, diagnostics);
                diagnostics.Throw();

                string asm = LanguageCore.Assembly.Generator.ConverterForAsm.Convert(generatedCode.Code.AsSpan(), generatedCode.DebugInfo, BitWidth._32);

                string outputFile = file + "_executable";

                LanguageCore.Assembly.Assembler.Assemble(asm, outputFile);

                if (!File.Exists(outputFile)) throw new FileNotFoundException($"File not found", outputFile);

                Process? process = Process.Start(new ProcessStartInfo(outputFile)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });

                if (process == null)
                { Assert.Fail($"Failed to start process \"{outputFile}\""); }

                process.WaitForExit();

                if (ProcessRuntimeException.TryGetFromExitCode(process.ExitCode, out ProcessRuntimeException? runtimeException))
                { throw runtimeException; }

                return new AssemblyResult(process);
        */
    }
}

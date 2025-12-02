using System.Text;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.Tests;

static class BrainfuckRunner
{
    public readonly struct BrainfuckResult : IResult
    {
        public readonly string StdOutput { get; }

        public readonly byte ExitCode => Memory[0];
        int IResult.ExitCode => ExitCode;

        public readonly int CodePointer { get; }
        public readonly int MemoryPointer { get; }

        public readonly byte[] Memory { get; }

        public BrainfuckResult(string stdOut, Brainfuck.InterpreterBase interpreter)
        {
            StdOutput = stdOut;

            CodePointer = interpreter.CodePointer;
            MemoryPointer = interpreter.MemoryPointer;

            Memory = interpreter.Memory;
        }
    }

    public static Brainfuck.Generator.BrainfuckGeneratorSettings BrainfuckGeneratorSettings => new(Brainfuck.Generator.BrainfuckGeneratorSettings.Default)
    {
        GenerateDebugInformation = false,
        ShowProgress = false,
    };

    public static byte[] GenerateBrainfuckMemory(int length)
    {
        byte[] result = new byte[length];
        int offset = Brainfuck.HeapCodeHelper.GetOffsettedStart(BrainfuckGeneratorSettings.HeapStart) + (Brainfuck.HeapCodeHelper.BlockSize * BrainfuckGeneratorSettings.HeapSize) + Brainfuck.HeapCodeHelper.DataOffset;
        result[offset] = 126;
        return result;
    }

    public static (BrainfuckResult Normal, BrainfuckResult Compact, BrainfuckResult Unoptimized) Run(string file, string input)
    {
        BrainfuckResult resultNormal;
        BrainfuckResult resultCompact;
        BrainfuckResult resultUnoptimized;

        InputBuffer inputBuffer;
        StringBuilder stdOutput = new();

        void OutputCallback(byte data) => stdOutput.Append(Brainfuck.CharCode.GetChar(data));
        byte InputCallback() => Brainfuck.CharCode.GetByte(inputBuffer.Read());

        DiagnosticsCollection diagnostics = new();
        CompilerResult compiled = StatementCompiler.CompileFile(file, new CompilerSettings(Utils.GetCompilerSettings(Brainfuck.Generator.CodeGeneratorForBrainfuck.DefaultCompilerSettings))
        {
            Optimizations = OptimizationSettings.All,
        }, diagnostics);
        Brainfuck.Generator.BrainfuckGeneratorResult generated = Brainfuck.Generator.CodeGeneratorForBrainfuck.Generate(compiled, BrainfuckGeneratorSettings, null, diagnostics);
        diagnostics.Throw();

        diagnostics = new DiagnosticsCollection();
        CompilerResult compiledUnoptimized = StatementCompiler.CompileFile(file, new CompilerSettings(Utils.GetCompilerSettings(Brainfuck.Generator.CodeGeneratorForBrainfuck.DefaultCompilerSettings))
        {
            Optimizations = OptimizationSettings.None,
        }, diagnostics);
        Brainfuck.Generator.BrainfuckGeneratorResult generatedUnoptimized = Brainfuck.Generator.CodeGeneratorForBrainfuck.Generate(compiledUnoptimized, new Brainfuck.Generator.BrainfuckGeneratorSettings(BrainfuckGeneratorSettings)
        { DontOptimize = true }, null, diagnostics);
        diagnostics.Throw();

        {
            inputBuffer = new(input);
            stdOutput.Clear();

            Brainfuck.Interpreter interpreter = new(OutputCallback, InputCallback);
            interpreter.LoadCode(generated.Code, false, generated.DebugInfo);
            interpreter.DebugInfo = new CompiledDebugInformation(generated.DebugInfo);
            interpreter.Run();

            resultNormal = new BrainfuckResult(stdOutput.ToString(), interpreter);
        }

        {
            inputBuffer = new(input);
            stdOutput.Clear();

            Brainfuck.InterpreterCompact interpreter = new(OutputCallback, InputCallback);
            interpreter.LoadCode(generated.Code, false, generated.DebugInfo);
            interpreter.DebugInfo = new CompiledDebugInformation(generated.DebugInfo);
            interpreter.Run();

            resultCompact = new BrainfuckResult(stdOutput.ToString(), interpreter);
        }

        {
            inputBuffer = new(input);
            stdOutput.Clear();

            Brainfuck.InterpreterCompact interpreter = new(OutputCallback, InputCallback);
            interpreter.LoadCode(generatedUnoptimized.Code, false, generatedUnoptimized.DebugInfo);
            interpreter.DebugInfo = new CompiledDebugInformation(generatedUnoptimized.DebugInfo);
            interpreter.Run();

            resultUnoptimized = new BrainfuckResult(stdOutput.ToString(), interpreter);
        }

        return (resultNormal, resultCompact, resultUnoptimized);
    }
}

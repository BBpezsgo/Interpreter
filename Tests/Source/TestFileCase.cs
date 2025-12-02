using LanguageCore.Runtime;
using System.Runtime.Versioning;

namespace LanguageCore.Tests;

sealed class TestFileCase
{
    public readonly string SourceFile;
    public readonly ExpectedResult ExpectedResult;
    public readonly string Input;

    public TestFileCase(string sourceFile, string resultFile, string? inputFile)
    {
        SourceFile = sourceFile;
        ExpectedResult = new(resultFile);
        Input = (inputFile is null) ? string.Empty : File.ReadAllText(inputFile);
    }

    public void DoMain(bool heapShouldBeEmpty = true, Action<List<IExternalFunction>>? externalFunctionAdder = null)
    {
        (InterpreterRunner.MainResult optimized, InterpreterRunner.MainResult unoptimized) = InterpreterRunner.Run(SourceFile, Input, externalFunctionAdder);

        ExpectedResult.Assert(optimized, heapShouldBeEmpty);
        ExpectedResult.Assert(unoptimized, heapShouldBeEmpty);
    }

    public void DoIL()
    {
        int result = MsilRunner.Run(SourceFile, Input);

        if (ExpectedResult.ExitCode != result)
        { throw new AssertFailedException($"Exit code isn't what is expected:{Environment.NewLine}Expected: {ExpectedResult.ExitCode}{Environment.NewLine}Actual:   {result}"); }
    }

    public void DoBrainfuck(bool memoryShouldBeEmpty = true, int? expectedMemoryPointer = 0)
    {
        (BrainfuckRunner.BrainfuckResult result, BrainfuckRunner.BrainfuckResult resultCompact, BrainfuckRunner.BrainfuckResult resultUnoptimized) = BrainfuckRunner.Run(SourceFile, Input);

        if (result.StdOutput != resultCompact.StdOutput) throw new AssertFailedException($"Compacted brainfuck code made different result (stdout)");
        if (result.MemoryPointer != resultCompact.MemoryPointer) throw new AssertFailedException($"Compacted brainfuck code made different result (memory pointer)");
        if (!result.Memory.SequenceEqual(resultCompact.Memory)) throw new AssertFailedException($"Compacted brainfuck code made different result (memory)");

        if (resultCompact.StdOutput != resultUnoptimized.StdOutput) throw new AssertFailedException($"Optimized brainfuck code made different result (stdout) (optimized: \"{resultCompact.StdOutput}\" unoptimized: \"{resultUnoptimized.StdOutput}\")");
        if (resultCompact.MemoryPointer != resultUnoptimized.MemoryPointer) throw new AssertFailedException($"Optimized brainfuck code made different result (memory pointer)");
        // if (!resultCompact.Memory.SequenceEqual(resultUnoptimized.Memory)) throw new AssertFailedException($"Optimized brainfuck code made different result (memory)");

        ExpectedResult.Assert(result, memoryShouldBeEmpty, expectedMemoryPointer);
        ExpectedResult.Assert(resultCompact, memoryShouldBeEmpty, expectedMemoryPointer);
        ExpectedResult.Assert(resultUnoptimized, memoryShouldBeEmpty, expectedMemoryPointer);
    }

#if NET
    [SupportedOSPlatform("linux")]
#endif
    public void DoAssembly()
    {
        NativeRunner.AssemblyResult result = NativeRunner.Run(SourceFile, Input);
        ExpectedResult.Assert(result);
    }
}

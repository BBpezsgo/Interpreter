using System.Diagnostics;
using System.Text;
using LanguageCore;
using LanguageCore.BBCode.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;
using TheProgram;

namespace Tests;

public readonly struct TestFile
{
    public readonly string SourceFile;
    public readonly string ResultFile;
    public readonly string? InputFile;

    public string GetSource() => File.ReadAllText(SourceFile);
    public ExpectedResult GetExpectedResult() => new(ResultFile);
    public string GetInput() => (InputFile is null) ? string.Empty : File.ReadAllText(InputFile);

    public TestFile(string sourceFile, string resultFile, string? inputFile)
    {
        SourceFile = sourceFile;
        ResultFile = resultFile;
        InputFile = inputFile;
    }

    /// <exception cref="AssertFailedException"/>
    public MainResult DoMain(bool heapShouldBeEmpty = true)
    {
        MainResult result = Utils.RunMain(new FileInfo(SourceFile), GetInput());
        Console.Write($"ExitCode: {result.ExitCode}");
        ExpectedResult expected = GetExpectedResult();
        expected.Assert(result, heapShouldBeEmpty);
        return result;
    }

    /// <exception cref="AssertFailedException"/>
    public BrainfuckResult DoBrainfuck(bool memoryShouldBeEmpty = true, int? expectedMemoryPointer = 0)
    {
        BrainfuckResult result = Utils.RunBrainfuck(new FileInfo(SourceFile), GetInput());
        ExpectedResult expected = GetExpectedResult();
        expected.Assert(result, memoryShouldBeEmpty, expectedMemoryPointer);
        return result;
    }

    /// <exception cref="AssertFailedException"/>
    public AssemblyResult DoAssembly()
    {
        AssemblyResult result = Utils.RunAssembly(new FileInfo(SourceFile), GetInput());
        Console.Write($"ExitCode: {result.ExitCode}");
        ExpectedResult expected = GetExpectedResult();
        expected.Assert(result);
        return result;
    }
}

public readonly struct ExpectedResult
{
    public readonly string StdOutput;
    public readonly int ExitCode;

    enum ExpectedResultParserState
    {
        Normal,
        Escape,
        Tag,
        TagEnd,
    }

    public ExpectedResult(string resultFile)
    {
        string resultText = File.ReadAllText(resultFile);
        StringBuilder builder = new(resultFile.Length);
        StringBuilder? tagBuilder = null;
        List<string> tags = new();

        ExpectedResultParserState state = ExpectedResultParserState.Normal;

        for (int i = 0; i < resultText.Length; i++)
        {
            char c = resultText[i];
            switch (state)
            {
                case ExpectedResultParserState.Normal:
                    switch (c)
                    {
                        case '\\':
                            state = ExpectedResultParserState.Escape;
                            break;
                        case '#':
                            state = ExpectedResultParserState.Tag;
                            break;
                        default:
                            builder.Append(c);
                            break;
                    }
                    break;
                case ExpectedResultParserState.Escape:
                    builder.Append(c);
                    state = ExpectedResultParserState.Normal;
                    break;
                case ExpectedResultParserState.Tag:
                    switch (c)
                    {
                        case '\r':
                        case '\n':
                            state = ExpectedResultParserState.TagEnd;
                            break;
                        default:
                            tagBuilder ??= new StringBuilder();
                            tagBuilder.Append(c);
                            break;
                    }
                    break;
                case ExpectedResultParserState.TagEnd:
                    switch (c)
                    {
                        case '\r':
                        case '\n':
                            break;
                        default:
                            if (tagBuilder != null)
                            {
                                tags.Add(tagBuilder.ToString());
                                tagBuilder = null;
                            }
                            state = ExpectedResultParserState.Normal;
                            i--;
                            break;
                    }
                    break;
                default:
                    break;
            }
        }

        if (tagBuilder != null)
        { tags.Add(tagBuilder.ToString()); }

        StdOutput = builder.ToString();
        ExitCode = 0;

        for (int i = 0; i < tags.Count; i++)
        {
            string tag = tags[i].Trim().ToLowerInvariant();
            string[] parts = tag.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 2 && parts[0] == "exitcode")
            { int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out ExitCode); }
        }
    }

    /// <exception cref="AssertFailedException"/>
    public ExpectedResult Assert(IResult other)
    {
        if (!string.Equals(StdOutput, other.StdOutput, StringComparison.Ordinal))
        { throw new AssertFailedException($"Standard output isn't what is expected:{Environment.NewLine}Expected: \"{StdOutput.Escape()}\"{Environment.NewLine}Actual: \"{other.StdOutput.Escape()}\""); }

        if (ExitCode != other.ExitCode)
        { throw new AssertFailedException($"Exit code isn't what is expected:{Environment.NewLine}Expected: {ExitCode}{Environment.NewLine}Actual: {other.ExitCode}"); }

        return this;
    }

    /// <exception cref="AssertFailedException"/>
    public ExpectedResult Assert(MainResult other, bool heapShouldBeEmpty)
    {
        Assert((IResult)other);

        if (heapShouldBeEmpty && other.Heap.UsedSize != 0)
        { throw new AssertFailedException($"Heap isn't empty"); }

        return this;
    }

    /// <exception cref="AssertFailedException"/>
    public ExpectedResult Assert(BrainfuckResult other, bool memoryShouldBeEmpty, int? expectedMemoryPointer)
    {
        Assert((IResult)other);

        if (memoryShouldBeEmpty)
        {
            // Span<byte> expectedMemory = Utils.GenerateBrainfuckMemory(other.Memory.Length).AsSpan()[1..];
            // Span<byte> actualMemory = other.Memory.AsSpan()[1..];
            // 
            // if (!MemoryExtensions.SequenceEqual(expectedMemory, actualMemory))
            // { throw new AssertFailedException($"Memory isn't empty"); }
        }

        if (expectedMemoryPointer.HasValue && other.MemoryPointer != expectedMemoryPointer.Value)
        { throw new AssertFailedException($"Memory pointer isn't what is expected:{Environment.NewLine}Expected: \"{expectedMemoryPointer.Value}\"{Environment.NewLine}Actual: \"{other.MemoryPointer}\""); }

        return this;
    }

    /// <exception cref="AssertFailedException"/>
    public ExpectedResult Assert(AssemblyResult other)
    {
        Assert((IResult)other);

        return this;
    }
}

public struct Utils
{
    public const int HeapSize = 2048;

    const string BasePath = "../StandardLibrary/";

    public const long BaseTimeout = 2000;
    public const long BrainfuckTimeout = 5000;

    const string TestFilesPath = $@"{TestConstants.TheProjectPath}\TestFiles\";

    const int TestCount = 50;
    static readonly int TestFileNameWidth = (int)Math.Floor(Math.Log10(TestCount)) + 1;

    public static LanguageCore.Brainfuck.Generator.BrainfuckGeneratorSettings BrainfuckSettings => LanguageCore.Brainfuck.Generator.BrainfuckGeneratorSettings.Default;

    public static byte[] GenerateBrainfuckMemory(int length)
    {
        byte[] result = new byte[length];
        int offset = LanguageCore.Brainfuck.BasicHeapCodeHelper.GetOffsettedStart(BrainfuckSettings.HeapStart) + (LanguageCore.Brainfuck.BasicHeapCodeHelper.BLOCK_SIZE * BrainfuckSettings.HeapSize) + LanguageCore.Brainfuck.BasicHeapCodeHelper.OFFSET_DATA;
        result[offset] = 126;
        return result;
    }

    public static void GenerateTestFiles()
    {
        Directory.CreateDirectory(TestFilesPath);

        for (int i = 1; i <= TestCount; i++)
        {
            string sourceFile = $"{TestFilesPath}{i.ToString().PadLeft(TestFileNameWidth, '0')}.bbc";
            string resultFile = $"{TestFilesPath}{i.ToString().PadLeft(TestFileNameWidth, '0')}.result";

            if (!File.Exists(sourceFile))
            { File.CreateText(sourceFile); }

            if (!File.Exists(resultFile))
            { File.CreateText(resultFile); }
        }
    }

    public static TestFile GetTest(int i)
    {
        string sourceFile = $"{TestFilesPath}{i.ToString().PadLeft(TestFileNameWidth, '0')}.bbc";
        string resultFile = $"{TestFilesPath}{i.ToString().PadLeft(TestFileNameWidth, '0')}.result";
        string? inputFile = $"{TestFilesPath}{i.ToString().PadLeft(TestFileNameWidth, '0')}.txt";

        if (!File.Exists(sourceFile))
        { throw new FileNotFoundException($"Source file not found", sourceFile); }

        if (!File.Exists(resultFile))
        { throw new FileNotFoundException($"Result file not found", sourceFile); }

        if (!File.Exists(inputFile))
        { inputFile = null; }

        return new TestFile(sourceFile, resultFile, inputFile);
    }

    public static MainResult RunMain(FileInfo file, string input)
    {
        InputBuffer inputBuffer = new(input);

        Interpreter interpreter = new();

        StringBuilder stdOutput = new();
        StringBuilder stdError = new();

        interpreter.OnStdOut += (sender, data) => stdOutput.Append(data);
        interpreter.OnStdError += (sender, data) => stdError.Append(data);

        interpreter.OnNeedInput += (sender) => sender.OnInput(inputBuffer.Read());

        Dictionary<string, ExternalFunctionBase> externalFunctions = new();

        interpreter.GenerateExternalFunctions(externalFunctions);

        if (file.Directory != null)
        {
            string dllsFolderPath = Path.Combine(file.Directory.FullName, BasePath.Replace('/', '\\'));

            if (Directory.Exists(dllsFolderPath))
            {
                string[] dlls = Directory.GetFiles(dllsFolderPath, "*.dll");
                for (int i = 0; i < dlls.Length; i++)
                { externalFunctions.LoadAssembly(dlls[i]); }
            }
        }

        AnalysisCollection analysisCollection = new();

        CompilerResult compiled = Compiler.CompileFile(file, externalFunctions, new CompilerSettings() { BasePath = BasePath }, null, analysisCollection);
        BBCodeGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, GeneratorSettings.Default, null, analysisCollection);

        analysisCollection.Throw();

        interpreter.CompilerResult = generatedCode;

        interpreter.Initialize(generatedCode.Code, new BytecodeInterpreterSettings()
        {
            HeapSize = HeapSize,
            StackMaxSize = BytecodeInterpreterSettings.Default.StackMaxSize,
        }, externalFunctions);

        // Stopwatch stopwatch = Stopwatch.StartNew();

        while (interpreter.IsExecutingCode)
        {
            interpreter.Update();

            // if (stopwatch.ElapsedMilliseconds > BaseTimeout)
            // {
            //     stopwatch.Stop();
            //     throw new TimeExceededException($"Time exceeded ({stopwatch.ElapsedMilliseconds} ms)");
            // }
        }

        if (interpreter.BytecodeInterpreter == null)
        { throw new UnreachableException($"{nameof(interpreter.BytecodeInterpreter)} is null"); }

        return new MainResult(stdOutput.ToString(), stdError.ToString(), interpreter.BytecodeInterpreter);
    }

    public static BrainfuckResult RunBrainfuck(FileInfo file, string input)
    {
        InputBuffer inputBuffer = new(input);
        StringBuilder stdOutput = new();

        void OutputCallback(byte data) => stdOutput.Append(LanguageCore.Brainfuck.CharCode.GetChar(data));
        byte InputCallback() => LanguageCore.Brainfuck.CharCode.GetByte(inputBuffer.Read());

        AnalysisCollection analysisCollection = new();

        CompilerResult compiled = Compiler.CompileFile(file, null, new CompilerSettings() { BasePath = BasePath }, null, analysisCollection);
        LanguageCore.Brainfuck.Generator.BrainfuckGeneratorResult generated = LanguageCore.Brainfuck.Generator.CodeGeneratorForBrainfuck.Generate(compiled, LanguageCore.Brainfuck.Generator.BrainfuckGeneratorSettings.Default, null, analysisCollection);

        analysisCollection.Throw();

        LanguageCore.Brainfuck.InterpreterCompact interpreter = new(generated.Code, OutputCallback, InputCallback);

        interpreter.Run();

        return new BrainfuckResult(stdOutput.ToString(), interpreter);
    }

    public static AssemblyResult RunAssembly(FileInfo file, string input)
    {
        AnalysisCollection analysisCollection = new();

        CompilerResult compiled = Compiler.CompileFile(file, null, new CompilerSettings() { BasePath = BasePath, }, null, analysisCollection);

        LanguageCore.ASM.Generator.AsmGeneratorResult code = LanguageCore.ASM.Generator.CodeGeneratorForAsm.Generate(compiled, default, null, analysisCollection);

        analysisCollection.Throw();

        string? fileDirectoryPath = file.DirectoryName;
        string fileNameNoExt = Path.GetFileNameWithoutExtension(file.Name);

        fileDirectoryPath ??= ".\\";

        string outputFile = Path.Combine(fileDirectoryPath, fileNameNoExt);

        LanguageCore.ASM.Assembler.Assemble(code.AssemblyCode, outputFile);

        if (!File.Exists(outputFile + ".exe"))
        { Assert.Fail(); }

        Process? process = Process.Start(new ProcessStartInfo(outputFile + ".exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        });

        if (process == null)
        { Assert.Fail(); }

        process.StandardInput.Write(input);

        process.WaitForExit();

        process.StandardInput.Close();

        if (File.Exists(outputFile + ".exe"))
        { File.Delete(outputFile + ".exe"); }

        if (ProcessRuntimeException.TryGetFromExitCode(process.ExitCode, out ProcessRuntimeException? runtimeException))
        { throw runtimeException; }

        return new AssemblyResult(process);
    }
}

public interface IResult
{
    public string StdOutput { get; }
    public int ExitCode { get; }
}

public readonly struct MainResult : IResult
{
    public readonly string StdOutput { get; }
    public readonly string StdError { get; }

    public readonly int ExitCode { get; }

    public readonly IReadOnlyHeap Heap { get; }
    public readonly IReadOnlyStack<DataItem> Stack { get; }

    public MainResult(string stdOut, string stdErr, BytecodeInterpreter interpreter)
    {
        StdOutput = stdOut;
        StdError = stdErr;

        ExitCode = interpreter.Memory.Stack.Last.RoundToInt32(null);

        Heap = interpreter.Memory.Heap;
        Stack = interpreter.Memory.Stack;
    }
}

public readonly struct BrainfuckResult : IResult
{
    public readonly string StdOutput { get; }

    public readonly int ExitCode => Memory[0];

    public readonly int CodePointer { get; }
    public readonly int MemoryPointer { get; }

    public readonly byte[] Memory { get; }

    public BrainfuckResult(string stdOut, LanguageCore.Brainfuck.InterpreterBase interpreter)
    {
        StdOutput = stdOut;

        CodePointer = interpreter.CodePointer;
        MemoryPointer = interpreter.MemoryPointer;

        Memory = interpreter.Memory;
    }
}

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
}

public static class AssertUtils
{
    public static int GetHashCollisions<T>(params T[] values)
    {
        int[] hashes = new int[values.Length];

        for (int i = 0; i < values.Length; i++)
        { hashes[i] = (values[i]!.GetHashCode()); }

        Array.Sort(hashes, values);

        int collisions = 0;

        for (int i = 1; i < hashes.Length; i++)
        {
            if (hashes[i] == hashes[i - 1] && !values[i]!.Equals(values[i - 1]))
            {
#if DEBUG
                int aHash = hashes[i];
                int bHash = hashes[i - 1];
                T aVal = values[i];
                T bVal = values[i - 1];
#endif
                collisions++;
            }
        }

        return collisions;
    }

    public static bool AreEqual<T>(T[] expected, T[] actual) where T : IEquatable<T>
    {
        if (expected == null && actual == null) return true;
        if (expected == null || actual == null) return false;
        if (expected.Length != actual.Length) return false;

        for (int i = 0; i < expected.Length; i++)
        {
            if (!((IEquatable<T>)expected[i]).Equals(actual[i]))
            { return false; }
        }
        return true;
    }

    /// <exception cref="AssertFailedException"/>
    public static void PositionEquals(IPositioned[] positions, params Position[] expected)
    {
        Assert.AreEqual(positions.Length, expected.Length);

        for (int i = 0; i < positions.Length; i++)
        {
            if (!positions[i].Position.Equals(expected[i]))
            {
                throw new AssertFailedException(
                    $"Position isn't what expected:{Environment.NewLine}" +
                    $"Expected: {expected[i].ToStringRange()}{Environment.NewLine}" +
                    $"Actual: {positions[i].Position.ToStringRange()}{Environment.NewLine}" +
                    $"at index {i}");
            }
        }
    }

    /// <exception cref="AssertFailedException"/>
    public static void ContentEquals(LanguageCore.Tokenizing.Token[] tokens, params string[] expected)
    {
        Assert.AreEqual(tokens.Length, expected.Length);

        for (int i = 0; i < tokens.Length; i++)
        { Assert.AreEqual(tokens[i].Content, expected[i], $"\"{tokens[i].Content}\" != \"{expected[i]}\" (i: {i})"); }
    }
}

public sealed class InputBuffer
{
    readonly string Buffer;
    int Position;

    public InputBuffer(string buffer)
    {
        Buffer = buffer;
        Position = 0;
    }

    public bool Has => Position < Buffer.Length;
    public char Current => Buffer[Position];

    public string Read(int length)
    {
        if (!Has) throw new Exception($"No more characters in the buffer");
        string result = Buffer.Substring(Position, length);
        Position += length;
        return result;
    }

    public char Read()
    {
        if (!Has) throw new Exception($"No more characters in the buffer");
        char result = Buffer[Position];
        Position++;
        return result;
    }

    public string ReadLine()
    {
        StringBuilder result = new();
        while (Has && Current != '\n')
        { result.Append(Read()); }
        Position++;
        return result.ToString();
    }
}

public class TimeExceededException : Exception
{
    public TimeExceededException() { }
    public TimeExceededException(string message) : base(message) { }
    public TimeExceededException(string message, Exception inner) : base(message, inner) { }
}

public static class RandomExtensions
{
    static readonly byte[] Buffer2 = new byte[2];

    public static short NextInt16(this Random random)
    {
        random.NextBytes(Buffer2);
        return BitConverter.ToInt16(Buffer2, 0);
    }

    public static ushort NextUInt16(this Random random)
    {
        random.NextBytes(Buffer2);
        return BitConverter.ToUInt16(Buffer2, 0);
    }
}

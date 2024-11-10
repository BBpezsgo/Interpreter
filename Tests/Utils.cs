using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using LanguageCore;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

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
    public void DoMain(bool heapShouldBeEmpty = true, Action<List<IExternalFunction>>? externalFunctionAdder = null)
    {
        MainResult result = Utils.RunMain(LanguageCore.Utils.ToFileUri(SourceFile), GetInput(), externalFunctionAdder);
        Console.Write($"ExitCode: {result.ExitCode}");
        ExpectedResult expected = GetExpectedResult();
        expected.Assert(result, heapShouldBeEmpty);
    }

    /// <exception cref="AssertFailedException"/>
    public void DoBrainfuck(bool memoryShouldBeEmpty = true, int? expectedMemoryPointer = 0)
    {
        (BrainfuckResult result, BrainfuckResult resultCompact, BrainfuckResult resultUnoptimized) = Utils.RunBrainfuck(LanguageCore.Utils.ToFileUri(SourceFile), GetInput());

        if (result.StdOutput != resultCompact.StdOutput) throw new AssertFailedException($"Compacted brainfuck code made different result (stdout)");
        if (result.MemoryPointer != resultCompact.MemoryPointer) throw new AssertFailedException($"Compacted brainfuck code made different result (memory pointer)");
        if (!result.Memory.SequenceEqual(resultCompact.Memory)) throw new AssertFailedException($"Compacted brainfuck code made different result (memory)");

        if (resultCompact.StdOutput != resultUnoptimized.StdOutput) throw new AssertFailedException($"Optimized brainfuck code made different result (stdout) (optimized: \"{resultCompact.StdOutput}\" unoptimized: \"{resultUnoptimized.StdOutput}\")");
        if (resultCompact.MemoryPointer != resultUnoptimized.MemoryPointer) throw new AssertFailedException($"Optimized brainfuck code made different result (memory pointer)");
        if (!resultCompact.Memory.SequenceEqual(resultUnoptimized.Memory)) throw new AssertFailedException($"Optimized brainfuck code made different result (memory)");

        ExpectedResult expected = GetExpectedResult();

        expected.Assert(result, memoryShouldBeEmpty, expectedMemoryPointer);
        expected.Assert(resultCompact, memoryShouldBeEmpty, expectedMemoryPointer);
        expected.Assert(resultUnoptimized, memoryShouldBeEmpty, expectedMemoryPointer);
    }

    /// <exception cref="AssertFailedException"/>
    public void DoAssembly()
    {
        AssemblyResult result = Utils.RunAssembly(LanguageCore.Utils.ToFileUri(SourceFile), GetInput());
        Console.Write($"ExitCode: {result.ExitCode}");
        ExpectedResult expected = GetExpectedResult();
        expected.Assert(result);
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
        { throw new AssertFailedException($"Standard output isn't what is expected:{Environment.NewLine}Expected: \"{StdOutput.Escape()}\"{Environment.NewLine}Actual:   \"{other.StdOutput.Escape()}\""); }

        if (ExitCode != other.ExitCode)
        { throw new AssertFailedException($"Exit code isn't what is expected:{Environment.NewLine}Expected: {ExitCode}{Environment.NewLine}Actual:   {other.ExitCode}"); }

        return this;
    }

    /// <exception cref="AssertFailedException"/>
    public ExpectedResult Assert(BrainfuckResult other)
    {
        if (!string.Equals(StdOutput, other.StdOutput, StringComparison.Ordinal))
        { throw new AssertFailedException($"Standard output isn't what is expected:{Environment.NewLine}Expected: \"{StdOutput.Escape()}\"{Environment.NewLine}Actual:   \"{other.StdOutput.Escape()}\""); }

        if (unchecked((byte)ExitCode) != other.ExitCode)
        { throw new AssertFailedException($"Exit code isn't what is expected:{Environment.NewLine}Expected: {unchecked((byte)ExitCode)}{Environment.NewLine}Actual:   {other.ExitCode}"); }

        return this;
    }

    /// <exception cref="AssertFailedException"/>
    public ExpectedResult Assert(MainResult other, bool heapShouldBeEmpty)
    {
        Assert(other);

        if (heapShouldBeEmpty && HeapUtils.GetUsedSize(other.Heap.AsSpan()) != 0)
        { throw new AssertFailedException($"Heap isn't empty"); }

        return this;
    }

    /// <exception cref="AssertFailedException"/>
    public ExpectedResult Assert(BrainfuckResult other, bool memoryShouldBeEmpty, int? expectedMemoryPointer)
    {
        Assert(other);

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

public static class Utils
{
    public const int HeapSize = 2048;

    static string BasePath => $"{LanguageCore.Program.ProjectPath}/StandardLibrary/";
    static readonly ImmutableArray<string> AdditionalImports = ImmutableArray.Create(
        $"{LanguageCore.Program.ProjectPath}/StandardLibrary/Primitives.bbc"
    );

    public const long BaseTimeout = 2000;
    public const long BrainfuckTimeout = 5000;

    static readonly string TestFilesPath = $"{LanguageCore.Program.ProjectPath}/TestFiles/";

    const int TestCount = 92;
    static readonly int TestFileNameWidth = (int)Math.Floor(Math.Log10(TestCount)) + 1;

    public static LanguageCore.Brainfuck.Generator.BrainfuckGeneratorSettings BrainfuckGeneratorSettings => new(LanguageCore.Brainfuck.Generator.BrainfuckGeneratorSettings.Default)
    {
        GenerateDebugInformation = false,
        ShowProgress = false,
    };

    public static MainGeneratorSettings MainGeneratorSettings => new(MainGeneratorSettings.Default)
    {
        GenerateDebugInstructions = false,
        StackSize = MainGeneratorSettings.Default.StackSize,
    };

    public static CompilerSettings CompilerSettings => new(CompilerSettings.Default)
    {

    };

    public static BytecodeInterpreterSettings BytecodeInterpreterSettings => new()
    {
        HeapSize = HeapSize,
        StackSize = BytecodeInterpreterSettings.Default.StackSize,
    };

    public static byte[] GenerateBrainfuckMemory(int length)
    {
        byte[] result = new byte[length];
        int offset = LanguageCore.Brainfuck.HeapCodeHelper.GetOffsettedStart(BrainfuckGeneratorSettings.HeapStart) + (LanguageCore.Brainfuck.HeapCodeHelper.BlockSize * BrainfuckGeneratorSettings.HeapSize) + LanguageCore.Brainfuck.HeapCodeHelper.DataOffset;
        result[offset] = 126;
        return result;
    }

    public static void GenerateTestFiles()
    {
        Directory.CreateDirectory(TestFilesPath);

        for (int i = 1; i <= TestCount; i++)
        {
            string sourceFile = $"{TestFilesPath}{i.ToString().PadLeft(TestFileNameWidth, '0')}.{LanguageConstants.LanguageExtension}";
            string resultFile = $"{TestFilesPath}{i.ToString().PadLeft(TestFileNameWidth, '0')}.result";

            if (!File.Exists(sourceFile))
            { File.CreateText(sourceFile); }

            if (!File.Exists(resultFile))
            { File.CreateText(resultFile); }
        }
    }

    public static TestFile GetTest(int i)
    {
        string sourceFile = $"{TestFilesPath}{i.ToString().PadLeft(TestFileNameWidth, '0')}.{LanguageConstants.LanguageExtension}";
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

    public static MainResult RunMain(Uri file, string input, Action<List<IExternalFunction>>? externalFunctionAdder = null)
    {
        List<IExternalFunction> externalFunctions = BytecodeProcessorEx.GetExternalFunctions();

        externalFunctionAdder?.Invoke(externalFunctions);

        DiagnosticsCollection diagnostics = new();

        CompilerResult compiled = Compiler.CompileFile(file, externalFunctions, new CompilerSettings(CompilerSettings) { BasePath = BasePath }, PreprocessorVariables.Normal, null, diagnostics, null, null, AdditionalImports);
        BBLangGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, MainGeneratorSettings, null, diagnostics);
        compiled = Compiler.CompileFile(file, externalFunctions, new CompilerSettings(CompilerSettings) { BasePath = BasePath }, PreprocessorVariables.Normal, null, diagnostics, null, null, AdditionalImports);
        BBLangGeneratorResult generatedCodeUnoptimized = CodeGeneratorForMain.Generate(compiled, new MainGeneratorSettings(MainGeneratorSettings) { DontOptimize = true }, null, diagnostics);

        diagnostics.Throw();

        (BytecodeProcessorEx, MainResult) Execute(BBLangGeneratorResult code, string input)
        {
            List<IExternalFunction> _externalFunctions = new();
            externalFunctionAdder?.Invoke(_externalFunctions);

            BytecodeProcessorEx interpreter = new(BytecodeInterpreterSettings, code.Code, null, code.DebugInfo, _externalFunctions);

            InputBuffer inputBuffer = new(input);
            StringBuilder stdOutput = new();
            StringBuilder stdError = new();

            interpreter.IO.OnStdOut += (data) => stdOutput.Append(data);
            interpreter.IO.OnNeedInput += () => interpreter.IO.SendKey(inputBuffer.Read());

            while (!interpreter.Processor.IsDone)
            { interpreter.Tick(); }

            return (interpreter, new MainResult(stdOutput.ToString(), stdError.ToString(), interpreter.Processor));
        }

        (BytecodeProcessorEx interpreterUnoptimized, MainResult unoptimizedResult) = Execute(generatedCodeUnoptimized, input);
        (BytecodeProcessorEx interpreter, MainResult result) = Execute(generatedCode, input);

        if (interpreter.Processor.Registers.BasePointer != interpreterUnoptimized.Processor.Registers.BasePointer)
        { throw new AssertFailedException($"BasePointer are different on optimized and unoptimized version ({interpreter.Processor.Registers.BasePointer} != {interpreterUnoptimized.Processor.Registers.BasePointer})"); }

        if (interpreter.Processor.Registers.StackPointer != interpreterUnoptimized.Processor.Registers.StackPointer)
        { throw new AssertFailedException($"BasePointer are different on optimized and unoptimized version ({interpreter.Processor.Registers.StackPointer} != {interpreterUnoptimized.Processor.Registers.StackPointer})"); }

        if (interpreter.Processor.Registers.StackPointer != interpreterUnoptimized.Processor.Registers.StackPointer)
        { throw new AssertFailedException($"BasePointer are different on optimized and unoptimized version ({interpreter.Processor.Registers.StackPointer} != {interpreterUnoptimized.Processor.Registers.StackPointer})"); }

        if (result.StdOutput != unoptimizedResult.StdOutput)
        { throw new AssertFailedException($"StdOutput are different on optimized and unoptimized version (\"{result.StdOutput.Escape()}\" != \"{unoptimizedResult.StdOutput.Escape()}\")"); }

        if (result.StdError != unoptimizedResult.StdError)
        { throw new AssertFailedException($"StdError are different on optimized and unoptimized version (\"{result.StdError.Escape()}\" != \"{unoptimizedResult.StdError.Escape()}\")"); }

        if (result.ExitCode != unoptimizedResult.ExitCode)
        { throw new AssertFailedException($"ExitCode are different on optimized and unoptimized version ({result.ExitCode} != {unoptimizedResult.ExitCode})"); }

        return result;
    }

    public static (BrainfuckResult Normal, BrainfuckResult Compact, BrainfuckResult Unoptimized) RunBrainfuck(Uri file, string input)
    {
        BrainfuckResult resultNormal;
        BrainfuckResult resultCompact;
        BrainfuckResult resultUnoptimized;

        InputBuffer inputBuffer;
        StringBuilder stdOutput = new();

        void OutputCallback(byte data) => stdOutput.Append(LanguageCore.Brainfuck.CharCode.GetChar(data));
        byte InputCallback() => LanguageCore.Brainfuck.CharCode.GetByte(inputBuffer.Read());

        DiagnosticsCollection diagnostics = new();
        CompilerResult compiled = Compiler.CompileFile(file, null, new CompilerSettings(CompilerSettings) { BasePath = BasePath }, PreprocessorVariables.Brainfuck, null, diagnostics, null, null, AdditionalImports);
        LanguageCore.Brainfuck.Generator.BrainfuckGeneratorResult generated = LanguageCore.Brainfuck.Generator.CodeGeneratorForBrainfuck.Generate(compiled, BrainfuckGeneratorSettings, null, diagnostics);
        diagnostics.Throw();

        diagnostics = new DiagnosticsCollection();
        CompilerResult compiledUnoptimized = Compiler.CompileFile(file, null, new CompilerSettings(CompilerSettings) { BasePath = BasePath }, PreprocessorVariables.Brainfuck, null, diagnostics, null, null, AdditionalImports);
        LanguageCore.Brainfuck.Generator.BrainfuckGeneratorResult generatedUnoptimized = LanguageCore.Brainfuck.Generator.CodeGeneratorForBrainfuck.Generate(compiledUnoptimized, new LanguageCore.Brainfuck.Generator.BrainfuckGeneratorSettings(BrainfuckGeneratorSettings)
        { DontOptimize = true }, null, diagnostics);
        diagnostics.Throw();

        {
            inputBuffer = new(input);
            stdOutput.Clear();

            LanguageCore.Brainfuck.Interpreter interpreter = new(OutputCallback, InputCallback);
            interpreter.LoadCode(generated.Code, false, generated.DebugInfo);
            interpreter.DebugInfo = new CompiledDebugInformation(generated.DebugInfo);
            interpreter.Run();

            resultNormal = new BrainfuckResult(stdOutput.ToString(), interpreter);
        }

        {
            inputBuffer = new(input);
            stdOutput.Clear();

            LanguageCore.Brainfuck.InterpreterCompact interpreter = new(OutputCallback, InputCallback);
            interpreter.LoadCode(generated.Code, false, generated.DebugInfo);
            interpreter.DebugInfo = new CompiledDebugInformation(generated.DebugInfo);
            interpreter.Run();

            resultCompact = new BrainfuckResult(stdOutput.ToString(), interpreter);
        }

        {
            inputBuffer = new(input);
            stdOutput.Clear();

            LanguageCore.Brainfuck.InterpreterCompact interpreter = new(OutputCallback, InputCallback);
            interpreter.LoadCode(generatedUnoptimized.Code, false, generatedUnoptimized.DebugInfo);
            interpreter.DebugInfo = new CompiledDebugInformation(generatedUnoptimized.DebugInfo);
            interpreter.Run();

            resultUnoptimized = new BrainfuckResult(stdOutput.ToString(), interpreter);
        }

        return (resultNormal, resultCompact, resultUnoptimized);
    }

    public static AssemblyResult RunAssembly(Uri file, string input)
    {
        DiagnosticsCollection diagnostics = new();

        CompilerResult compiled = Compiler.CompileFile(file, null, new CompilerSettings(CompilerSettings) { BasePath = BasePath, }, Enumerable.Empty<string>(), null, diagnostics, null, null, AdditionalImports);
        BBLangGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, MainGeneratorSettings, null, diagnostics);
        diagnostics.Throw();

        string asm = LanguageCore.ASM.Generator.ConverterForAsm.Convert(generatedCode.Code.AsSpan(), generatedCode.DebugInfo, BitWidth._32);

        string outputFile = file.LocalPath + "_executable";

        LanguageCore.ASM.Assembler.Assemble(asm, outputFile);

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
    }

    static string? GetLatestResultFile(string testResultsDirectory)
    {
        string[] files = Directory.GetFiles(testResultsDirectory, "*.trx");
        if (files.Length == 0) return null;
        FileInfo[] fileInfos = files.Select(v => new FileInfo(v)).ToArray();
        Array.Sort(fileInfos, (a, b) => b.CreationTimeUtc.CompareTo(a.CreationTimeUtc));
        return fileInfos[0].FullName;
    }

    record struct TrxTestDefinition(string Name, string[] Categories, string? ExecutionId, string? MethodClassName, string? MethodName);
    record struct TrxTestResult(string Id, string Name, string Outcome, string? ErrorMessage);

    static (Dictionary<string, TrxTestDefinition> Definitions, TrxTestResult[] Results) LoadTestResults(string trxFile)
    {
        XmlDocument xml = new();
        xml.LoadXml(File.ReadAllText(trxFile));

        Dictionary<string, TrxTestDefinition> definitions = new();

        {
            XmlElement? _definitions = xml["TestRun"]?["TestDefinitions"];
            if (_definitions != null)
            {
                for (int i = 0; i < _definitions.ChildNodes.Count; i++)
                {
                    XmlNode? _definition = _definitions.ChildNodes.Item(i);

                    if (_definition is null)
                    { continue; }

                    string? id = _definition.Attributes?["id"]?.Value;
                    string? name = _definition.Attributes?["name"]?.Value;
                    List<string> categories = new();
                    XmlElement? _categories = _definition["TestCategory"];
                    if (_categories != null)
                    {
                        for (int j = 0; j < _categories.ChildNodes.Count; j++)
                        {
                            XmlNode? _category = _categories.ChildNodes[j];
                            if (_category == null) continue;
                            string? category = _category.Attributes?["TestCategory"]?.Value;
                            if (category == null) continue;
                            categories.Add(category);
                        }
                    }
                    string? executionId = _definition["Execution"]?.Attributes["id"]?.Value;
                    string? methodClassName = _definition["TestMethod"]?.Attributes["className"]?.Value;
                    string? methodName = _definition["TestMethod"]?.Attributes["name"]?.Value;

                    if (id is null || name is null)
                    { continue; }

                    definitions[id] = new TrxTestDefinition(name, categories.ToArray(), executionId, methodClassName, methodName);
                }
            }
        }

        List<TrxTestResult> results = new();

        {
            XmlElement? _results = xml["TestRun"]?["Results"];
            if (_results != null)
            {
                for (int i = 0; i < _results.ChildNodes.Count; i++)
                {
                    XmlNode? _result = _results.ChildNodes.Item(i);

                    if (_result is null)
                    { continue; }

                    string? id = _result.Attributes?["testId"]?.Value;
                    string? name = _result.Attributes?["testName"]?.Value;
                    string? outcome = _result.Attributes?["outcome"]?.Value;

                    if (id is null || name is null || outcome is null)
                    { continue; }

                    XmlElement? errorMessage_ = _result["Output"]?["ErrorInfo"]?["Message"];
                    string? errorMessage = null;
                    if (errorMessage_ is not null && errorMessage_.FirstChild?.NodeType == XmlNodeType.Text)
                    { errorMessage = errorMessage_.FirstChild.Value; }

                    results.Add(new TrxTestResult(id, name, outcome, errorMessage));
                }
            }
        }

        return (definitions, results.ToArray());
    }

    public static void GenerateResultsFile(string testResultsDirectory, string resultFile)
    {
        string? latest = GetLatestResultFile(testResultsDirectory);

        if (latest is null)
        { throw new FileNotFoundException($"No test result file found in directory {testResultsDirectory}"); }

        (Dictionary<string, TrxTestDefinition> definitions, TrxTestResult[] results) = LoadTestResults(latest);

        Dictionary<string, List<(string? Category, string Outcome, string? ErrorMessage)>> testFiles = new();

        int passingTestCount = 0;
        int failedTestCount = 0;
        int notRunTestCount = 0;

        foreach ((string id, string name, string outcome, string? errorMessage) in results)
        {
            string[] categories = definitions[id].Categories;

            switch (outcome)
            {
                case "Passed": passingTestCount++; break;
                case "Failed": failedTestCount++; break;
                case "NotExecuted": notRunTestCount++; break;
            }

            string? category = null;
            bool isFileTest = false;
            for (int i = 0; i < categories.Length; i++)
            {
                if (categories[i] == "FileTest")
                { isFileTest = true; }
                else
                { category = categories[i]; }
            }

            if (!isFileTest) continue;

            if (!testFiles.TryGetValue(name, out List<(string? Category, string Outcome, string? ErrorMessage)>? fileResults))
            {
                fileResults = new List<(string? Category, string Outcome, string? ErrorMessage)>();
                testFiles[name] = fileResults;
            }

            fileResults.Add((category, outcome, errorMessage));
        }

        (int SerialNumber, List<(string? Category, string Outcome, string? ErrorMessage)> Value)[] sortedTestFiles = testFiles.Select(v => (int.Parse(v.Key[4..]), v.Value)).ToArray();
        Array.Sort(sortedTestFiles, (a, b) => a.SerialNumber.CompareTo(b.SerialNumber));

        using StreamWriter file = File.CreateText(resultFile);

        file.WriteLine("# Test Results");

        file.WriteLine($"[![](https://svg.test-summary.com/dashboard.svg?p={passingTestCount}&f={failedTestCount}&s={notRunTestCount})](#)");

        file.Write($"[![](https://img.shields.io/badge/Passing-{passingTestCount}-brightgreen?style=plastic])](#) ");
        file.Write($"[![](https://img.shields.io/badge/Failing-{failedTestCount}-red?style=plastic])](#) ");
        file.Write($"[![](https://img.shields.io/badge/Skipped-{notRunTestCount}-silver?style=plastic])](#)");
        file.WriteLine();

        file.WriteLine();

        file.WriteLine("| File | Bytecode | Brainfuck |");
        file.WriteLine("|:----:|:--------:|:---------:|");

        foreach ((int serialNumber, List<(string? Category, string Outcome, string? ErrorMessage)>? fileResults) in sortedTestFiles)
        {
            (string? Outcome, string? ErrorMessage) bytecodeResult = (null, null);
            (string? Outcome, string? ErrorMessage) brainfuckResult = (null, null);

            foreach ((string? category, string outcome, string? errorMessage) in fileResults)
            {
                switch (category)
                {
                    case "Main": bytecodeResult = (outcome, errorMessage); break;
                    case "Brainfuck": brainfuckResult = (outcome, errorMessage); break;
                }
            }

            if (bytecodeResult.Outcome == "NotExecuted" &&
                brainfuckResult.Outcome == "NotExecuted")
            { continue; }

            static string? TranslateOutcome(string? outcome) => outcome switch
            {
                "Passed" => "✅",
                "Failed" => "❌",
                "NotExecuted" => "✖",
                _ => outcome,
            };

            bytecodeResult.Outcome = TranslateOutcome(bytecodeResult.Outcome);
            brainfuckResult.Outcome = TranslateOutcome(brainfuckResult.Outcome);

            string translatedName = $"https://github.com/BBpezsgo/Interpreter/blob/master/TestFiles/{serialNumber.ToString().PadLeft(2, '0')}.{LanguageConstants.LanguageExtension}";
            translatedName = $"[{serialNumber}]({translatedName})";

            file.Write("| ");
            file.Write(translatedName);
            file.Write(" | ");

            file.Write(bytecodeResult.Outcome);
            if (bytecodeResult.ErrorMessage is not null)
            {
                file.Write(' ');
                file.Write(bytecodeResult.ErrorMessage.ReplaceLineEndings(" "));
            }

            file.Write(" | ");

            file.Write(brainfuckResult.Outcome);
            if (brainfuckResult.ErrorMessage is not null)
            {
                file.Write(' ');
                file.Write(brainfuckResult.ErrorMessage.ReplaceLineEndings(" "));
            }

            file.Write(" |");
            file.WriteLine();
        }
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
    public readonly ImmutableArray<byte> Heap { get; }

    public MainResult(string stdOut, string stdErr, BytecodeProcessor interpreter)
    {
        StdOutput = stdOut;
        StdError = stdErr;
        ExitCode = interpreter.Memory.AsSpan().Get<int>(interpreter.Registers.StackPointer);
        Heap = ImmutableCollectionsMarshal.AsImmutableArray(interpreter.Memory);
    }
}

public readonly struct BrainfuckResult : IResult
{
    public readonly string StdOutput { get; }

    public readonly byte ExitCode => Memory[0];
    int IResult.ExitCode => ExitCode;

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
    public static int GetHashCollisions<T>(params T[] values) where T : notnull
    {
        int[] hashes = new int[values.Length];

        for (int i = 0; i < values.Length; i++)
        { hashes[i] = values[i].GetHashCode(); }

        Array.Sort(hashes, values);

        int collisions = 0;

        for (int i = 1; i < hashes.Length; i++)
        {
            if (hashes[i] == hashes[i - 1] && !values[i].Equals(values[i - 1]))
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
    public static void PositionEquals(IReadOnlyList<IPositioned> positions, params ReadOnlySpan<Position> expected)
    {
        Assert.AreEqual(positions.Count, expected.Length);

        for (int i = 0; i < positions.Count; i++)
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
    public static void ContentEquals(IReadOnlyList<LanguageCore.Tokenizing.Token> tokens, params string[] expected)
    {
        Assert.AreEqual(tokens.Count, expected.Length);

        for (int i = 0; i < tokens.Count; i++)
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

    public static short NextInt16(this System.Random random)
    {
        random.NextBytes(Buffer2);
        return BitConverter.ToInt16(Buffer2, 0);
    }

    public static ushort NextUInt16(this System.Random random)
    {
        random.NextBytes(Buffer2);
        return BitConverter.ToUInt16(Buffer2, 0);
    }
}

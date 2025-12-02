using System.Text;
using LanguageCore.Runtime;

namespace LanguageCore.Tests;

readonly struct ExpectedResult
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

    public ExpectedResult Assert(IResult other)
    {
        if (!string.Equals(StdOutput.Replace("\r", ""), other.StdOutput.Replace("\r", ""), StringComparison.Ordinal))
        { throw new AssertFailedException($"Standard output isn't what is expected:{Environment.NewLine}Expected: \"{StdOutput.Replace("\r", "").Escape()}\"{Environment.NewLine}Actual:   \"{other.StdOutput.Replace("\r", "").Escape()}\""); }

        if (ExitCode != other.ExitCode)
        { throw new AssertFailedException($"Exit code isn't what is expected:{Environment.NewLine}Expected: {ExitCode}{Environment.NewLine}Actual:   {other.ExitCode}"); }

        return this;
    }

    public ExpectedResult Assert(BrainfuckRunner.BrainfuckResult other)
    {
        if (!string.Equals(StdOutput.Replace("\r", ""), other.StdOutput.Replace("\r", ""), StringComparison.Ordinal))
        { throw new AssertFailedException($"Standard output isn't what is expected:{Environment.NewLine}Expected: \"{StdOutput.Replace("\r", "").Escape()}\"{Environment.NewLine}Actual:   \"{other.StdOutput.Replace("\r", "").Escape()}\""); }

        if (unchecked((byte)ExitCode) != other.ExitCode)
        { throw new AssertFailedException($"Exit code isn't what is expected:{Environment.NewLine}Expected: {unchecked((byte)ExitCode)}{Environment.NewLine}Actual:   {other.ExitCode}"); }

        return this;
    }

    public ExpectedResult Assert(InterpreterRunner.MainResult other, bool heapShouldBeEmpty)
    {
        Assert(other);

        if (heapShouldBeEmpty && BytecodeHeapImplementation.GetUsedSize(other.Heap.AsSpan()) != 0)
        { throw new AssertFailedException($"Heap isn't empty"); }

        return this;
    }

    public ExpectedResult Assert(BrainfuckRunner.BrainfuckResult other, bool memoryShouldBeEmpty, int? expectedMemoryPointer)
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

    public ExpectedResult Assert(NativeRunner.AssemblyResult other)
    {
        Assert((IResult)other);

        return this;
    }
}

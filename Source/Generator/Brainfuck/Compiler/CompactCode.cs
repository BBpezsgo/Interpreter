using LanguageCore.Runtime;

namespace LanguageCore.Brainfuck;

public readonly struct CompactCodeSegment
{
    public OpCodesCompact OpCode { get; }

    public int Count { get; init; }
    public sbyte Arg1 { get; init; }
    public sbyte Arg2 { get; init; }
    public sbyte Arg3 { get; init; }
    public sbyte Arg4 { get; init; }

    public CompactCodeSegment(OpCodesCompact opCode)
    {
        OpCode = opCode;
        Count = 1;
    }

    public CompactCodeSegment(char opCode)
    {
        OpCode = (OpCodesCompact)CompactCode.ToOpCode(opCode);
        Count = 1;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        switch (OpCode)
        {
            case OpCodesCompact.PointerRight:
                result.Append('>');
                break;
            case OpCodesCompact.PointerLeft:
                result.Append('<');
                break;
            case OpCodesCompact.Add:
                result.Append('+');
                break;
            case OpCodesCompact.Sub:
                result.Append('-');
                break;
            case OpCodesCompact.BranchStart:
                result.Append('[');
                break;
            case OpCodesCompact.BranchEnd:
                result.Append(']');
                break;
            case OpCodesCompact.Out:
                result.Append('.');
                break;
            case OpCodesCompact.In:
                result.Append(',');
                break;
            case OpCodesCompact.Clear:
                result.Append("[-]");
                break;
            case OpCodesCompact.Move:
                result.Append('(');
                result.Append('M');

                if (Arg1 != 0) result.Append($"{Arg1};");
                if (Arg2 != 0) result.Append($"{Arg2};");
                if (Arg3 != 0) result.Append($"{Arg3};");
                if (Arg4 != 0) result.Append($"{Arg4};");

                result.Append(')');
                break;
            default:
                result.Append("NULL");
                break;
        }
        if (Count > 1) result.Append(Count);

        return result.ToString();
    }
}

public enum OpCodes
{
    NULL = '\0',
    PointerRight = '>',
    PointerLeft = '<',
    Add = '+',
    Sub = '-',
    BranchStart = '[',
    BranchEnd = ']',
    Out = '.',
    In = ',',
    Break = '$',
}

public enum OpCodesCompact
{
    NULL = '\0',
    PointerRight = '>',
    PointerLeft = '<',
    Add = '+',
    Sub = '-',
    BranchStart = '[',
    BranchEnd = ']',
    Out = '.',
    In = ',',

    Break = '$',
    Clear = 'C',
    Move = 'M',
}

public static class CompactCode
{
    public static OpCodes ToOpCode(char c) => c switch
    {
        '>' => OpCodes.PointerRight,
        '<' => OpCodes.PointerLeft,
        '+' => OpCodes.Add,
        '-' => OpCodes.Sub,
        '[' => OpCodes.BranchStart,
        ']' => OpCodes.BranchEnd,
        '.' => OpCodes.Out,
        ',' => OpCodes.In,
        '$' => OpCodes.Break,
        _ => 0,
    };

    public static char FromOpCode(OpCodes c) => c switch
    {
        OpCodes.PointerRight => '>',
        OpCodes.PointerLeft => '<',
        OpCodes.Add => '+',
        OpCodes.Sub => '-',
        OpCodes.BranchStart => '[',
        OpCodes.BranchEnd => ']',
        OpCodes.Out => '.',
        OpCodes.In => ',',
        OpCodes.Break => '$',
        _ => '\0',
    };

    public static ImmutableArray<OpCodes> ToOpCode(string c)
    {
        OpCodes[] result = new OpCodes[c.Length];
        for (int i = 0; i < c.Length; i++)
        { result[i] = ToOpCode(c[i]); }
        return result.AsImmutableUnsafe();
    }

    public static ImmutableArray<char> FromOpCode(OpCodes[] c)
    {
        char[] result = new char[c.Length];
        for (int i = 0; i < c.Length; i++)
        { result[i] = FromOpCode(c[i]); }
        return result.AsImmutableUnsafe();
    }

    static readonly ImmutableArray<OpCodesCompact> Duplicatable = ImmutableArray.Create(
        (OpCodesCompact)'>', (OpCodesCompact)'<',
        (OpCodesCompact)'+', (OpCodesCompact)'-'
    );

    static bool TryGenerateDataMovement(ReadOnlySpan<char> code, ref int index, [NotNullWhen(true)] out CompactCodeSegment result, DebugInformation? debugInfo, ref int removed)
    {
        result = default;

        if (!BrainfuckCode.GetDataMovement(code[index..], out ImmutableArray<(int Offset, int Modification)> destinations, out int _removed))
        { return false; }

        result = new CompactCodeSegment(OpCodesCompact.Move)
        {
            Arg1 = destinations.Length >= 1 ? (sbyte)destinations[0].Offset : (sbyte)0,
            Arg2 = destinations.Length >= 2 ? (sbyte)destinations[1].Offset : (sbyte)0,
            Arg3 = destinations.Length >= 3 ? (sbyte)destinations[2].Offset : (sbyte)0,
            Arg4 = destinations.Length >= 4 ? (sbyte)destinations[3].Offset : (sbyte)0,
        };
        debugInfo?.OffsetCodeFrom(index - removed, -_removed);
        index += _removed;
        removed += _removed;
        return true;
    }

    static bool TryGenerateClear(ReadOnlySpan<char> code, ref int index, [NotNullWhen(true)] out CompactCodeSegment result, DebugInformation? debugInfo, ref int removed)
    {
        result = default;

        if (!BrainfuckCode.ExpectSequence(code[index..], "[-]"))
        { return false; }

        result = new CompactCodeSegment(OpCodesCompact.Clear);
        debugInfo?.OffsetCodeFrom(index - removed, -2);
        index += 2;
        removed += 2;
        return true;
    }

    public static ImmutableArray<CompactCodeSegment> Generate(ReadOnlySpan<char> code, bool showProgress, DebugInformation? debugInfo)
    {
        using ConsoleProgressLabel progressLabel = new("Compacting code ...", ConsoleColor.DarkGray, showProgress);
        using ConsoleProgressBar progress = new(ConsoleColor.DarkGray, showProgress);

        List<CompactCodeSegment> result = new();
        int removed = 0;

        for (int i = 0; i < code.Length; i++)
        {
            progress.Print(i, code.Length);

            OpCodesCompact c = (OpCodesCompact)ToOpCode(code[i]);

            if (TryGenerateDataMovement(code, ref i, out CompactCodeSegment dataMovement, debugInfo, ref removed))
            {
                result.Add(dataMovement);
                continue;
            }

            if (TryGenerateClear(code, ref i, out CompactCodeSegment clear, debugInfo, ref removed))
            {
                result.Add(clear);
                continue;
            }

            if (result.Count > 0 && result[^1].OpCode == c && Duplicatable.Contains(c))
            {
                result[^1] = new CompactCodeSegment(c)
                {
                    Count = result[^1].Count + 1,
                };
                debugInfo?.OffsetCodeFrom(i - removed, -1);
                removed++;
                continue;
            }

            result.Add(new CompactCodeSegment(c));
        }

        return result.ToImmutableArray();
    }
}

using LanguageCore.Runtime;

namespace LanguageCore.Brainfuck;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
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

    string GetDebuggerDisplay() => $"{OpCode switch
    {
        OpCodesCompact.PointerRight => ">",
        OpCodesCompact.PointerLeft => "<",
        OpCodesCompact.Add => "+",
        OpCodesCompact.Sub => "-",
        OpCodesCompact.BranchStart => "[",
        OpCodesCompact.BranchEnd => "]",
        OpCodesCompact.Out => ".",
        OpCodesCompact.In => ",",
        OpCodesCompact.Break => "$",
        _ => OpCode.ToString(),
    }} x{Count}";

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

[SuppressMessage("Roslynator", "RCS1154")]
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

[SuppressMessage("Roslynator", "RCS1154")]
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

    public static OpCodes[] ToOpCode(string c)
    {
        OpCodes[] result = new OpCodes[c.Length];
        for (int i = 0; i < c.Length; i++)
        { result[i] = CompactCode.ToOpCode(c[i]); }
        return result;
    }

    public static char[] FromOpCode(OpCodes[] c)
    {
        char[] result = new char[c.Length];
        for (int i = 0; i < c.Length; i++)
        { result[i] = CompactCode.FromOpCode(c[i]); }
        return result;
    }

    static readonly OpCodesCompact[] Duplicatable = new OpCodesCompact[]
    {
        (OpCodesCompact)'>', (OpCodesCompact)'<',
        (OpCodesCompact)'+', (OpCodesCompact)'-',
    };

    static int ExpectStuff(ReadOnlySpan<char> code, ref int index, char increment, char decrement)
    {
        int result = 0;
        for (; index < code.Length; index++)
        {
            if (code[index] == increment) result++;
            else if (code[index] == decrement) result--;
            else break;
        }
        return result;
    }

    static bool TryGenerateDataMovement(ReadOnlySpan<char> code, ref int index, [NotNullWhen(true)] out CompactCodeSegment result, DebugInformation? debugInfo)
    {
        result = default;

        if (code[index] != '[')
        { return false; }

        ReadOnlySpan<char> slice = code[index..];
        int end = slice.IndexOf(']');
        if (end < 0)
        { return false; }

        slice = slice[..(end + 1)];
        if (slice.Length < 6)
        { return false; }

        slice = slice[1..^1];

        List<(int Offset, int Modification)> destinations = new();

        if (slice[0] is '+' or '-')
        {
            int subIndex = 0;

            int sourceModification = ExpectStuff(slice, ref subIndex, '+', '-');

            if (sourceModification != -1)
            { return false; }

            int moveBack;

            while (true)
            {
                int movement = ExpectStuff(slice, ref subIndex, '>', '<');
                if (movement == 0)
                { return false; }

                if (subIndex >= slice.Length)
                {
                    moveBack = movement;
                    break;
                }
                int modification = ExpectStuff(slice, ref subIndex, '+', '-');
                if (modification == 0)
                { return false; }

                destinations.Add((movement, modification));
                if (destinations.Count > 4)
                { return false; }
            }

            if (destinations.Count == 0)
            { return false; }

            for (int i = 0; i < destinations.Count; i++)
            {
                if (destinations[i].Modification != 1)
                { return false; }
            }

            int totalMovement = 0;
            for (int i = 0; i < destinations.Count; i++)
            {
                totalMovement += destinations[i].Offset;
                destinations[i] = (totalMovement, destinations[i].Modification);
            }

            if (totalMovement + moveBack != 0)
            { return false; }
        }
        else if (slice[0] is '>' or '<')
        {
            int subIndex = 0;

            while (true)
            {
                int movement = ExpectStuff(slice, ref subIndex, '>', '<');
                if (movement == 0)
                { break; }

                int modification = ExpectStuff(slice, ref subIndex, '+', '-');
                if (modification == 0)
                { return false; }

                destinations.Add((movement, modification));
                if (destinations.Count > 4)
                { return false; }
            }

            if (destinations.Count == 0)
            { return false; }

            int totalMovement = 0;
            for (int i = 0; i < destinations.Count; i++)
            {
                totalMovement += destinations[i].Offset;
                destinations[i] = (totalMovement, destinations[i].Modification);
            }

            if (destinations[^1].Offset != 0)
            { return false; }
            int sourceModification = destinations[^1].Modification;
            if (sourceModification != -1)
            { return false; }

            destinations.RemoveAt(destinations.Count - 1);

            for (int i = 0; i < destinations.Count; i++)
            {
                if (destinations[i].Modification != 1)
                { return false; }
            }
        }
        else
        { return false; }

        result = new CompactCodeSegment(OpCodesCompact.Move)
        {
            Arg1 = destinations.Count >= 1 ? (sbyte)destinations[0].Offset : (sbyte)0,
            Arg2 = destinations.Count >= 2 ? (sbyte)destinations[1].Offset : (sbyte)0,
            Arg3 = destinations.Count >= 3 ? (sbyte)destinations[2].Offset : (sbyte)0,
            Arg4 = destinations.Count >= 4 ? (sbyte)destinations[3].Offset : (sbyte)0,
        };
        debugInfo?.OffsetCodeFrom(index, -(slice.Length + 2 - 1));
        index += slice.Length + 2 - 1;
        return true;
    }

    public static CompactCodeSegment[] Generate(ReadOnlySpan<char> code, bool showProgress, DebugInformation? debugInfo)
    {
        using ConsoleProgressLabel progressLabel = new("Compacting code ...", ConsoleColor.DarkGray, showProgress);
        using ConsoleProgressBar progress = new(ConsoleColor.DarkGray, showProgress);

        List<CompactCodeSegment> result = new();

        for (int i = 0; i < code.Length; i++)
        {
            progress.Print(i, code.Length);

            OpCodesCompact c = (OpCodesCompact)ToOpCode(code[i]);

            if (i < code.Length - 3 && code.Slice(i, 3).SequenceEqual("[-]"))
            {
                result.Add(new CompactCodeSegment(OpCodesCompact.Clear));
                debugInfo?.OffsetCodeFrom(i, -2);
                i += 2;
                continue;
            }

            if (TryGenerateDataMovement(code, ref i, out CompactCodeSegment dataMovement, debugInfo))
            {
                result.Add(dataMovement);
                continue;
            }

            if (result.Count > 0 && result[^1].OpCode == c && Duplicatable.Contains(c))
            {
                result[^1] = new CompactCodeSegment(c)
                {
                    Count = result[^1].Count + 1,
                };
                debugInfo?.OffsetCodeFrom(i, -1);
                continue;
            }

            result.Add(new CompactCodeSegment(c));
        }

        return result.ToArray();
    }
}

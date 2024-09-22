using LanguageCore.Runtime;

namespace LanguageCore.Brainfuck;

public static class CharCode
{
    public static byte GetByte(char c) => Encoding.ASCII.GetBytes(new char[1] { c }, 0, 1)[0];

    public static char GetChar(byte v) => Encoding.ASCII.GetChars(new byte[1] { v }, 0, 1)[0];
}

public static class BrainfuckCode
{
    public static bool IsCode(char c)
        => c is '+' or '-' or '<' or '>' or '[' or ']' or '.' or ',' or '$';

    public static string GenerateModification(int modification, char increment, char decrement)
    {
        if (modification > 0) return new string(increment, modification);
        else if (modification < 0) return new string(decrement, -modification);
        else return string.Empty;
    }

    public static bool GetDataMovement(ReadOnlySpan<char> code, [NotNullWhen(true)] out ImmutableArray<(int Offset, int Modification)> result, out int removed)
    {
        result = ImmutableArray.Create<(int Offset, int Modification)>();
        removed = 0;

        if (code[0] != '[')
        { return false; }

        int end = code.IndexOf(']');
        if (end < 0)
        { return false; }

        code = code[..(end + 1)];
        if (code.Length < 6)
        { return false; }

        code = code[1..^1];

        List<(int Offset, int Modification)> destinations = new();

        if (code[0] is '+' or '-')
        {
            int subIndex = 0;

            int sourceModification = Modifications(code, ref subIndex, '+', '-');

            if (sourceModification != -1)
            { return false; }

            int moveBack;

            while (true)
            {
                int movement = Modifications(code, ref subIndex, '>', '<');
                if (movement == 0)
                { return false; }

                if (subIndex >= code.Length)
                {
                    moveBack = movement;
                    break;
                }
                int modification = Modifications(code, ref subIndex, '+', '-');
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
        else if (code[0] is '>' or '<')
        {
            int subIndex = 0;

            while (true)
            {
                int movement = Modifications(code, ref subIndex, '>', '<');
                if (movement == 0)
                { break; }

                int modification = Modifications(code, ref subIndex, '+', '-');
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

        result = destinations.ToImmutableArray();
        removed = code.Length + 2 - 1;
        return true;
    }

    public static bool ExpectSequence(ReadOnlySpan<char> code, ReadOnlySpan<char> sequence)
    {
        if (code.Length < sequence.Length)
        { return false; }
        code = code[..sequence.Length];
        if (!code.SequenceEqual(sequence))
        { return false; }

        return true;
    }

    public static int ContiguousLength(ReadOnlySpan<char> values, char value)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (!values[i].Equals(value))
            { return i; }
        }

        return values.Length;
    }

    public static int Modifications(ReadOnlySpan<char> code, char increment, char decrement)
    {
        int result = 0;

        for (int i = 0; i < code.Length; i++)
        {
            if (code[i] == increment) result++;
            else if (code[i] == decrement) result--;
            else break;
        }

        return result;
    }

    public static int Modifications(ReadOnlySpan<char> values, char increment, char decrement, out int length)
    {
        int result = 0;
        length = 0;

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i].Equals(increment))
            { result++; length++; }
            else if (values[i].Equals(decrement))
            { result--; length++; }
            else
            { break; }
        }

        return result;
    }

    public static int Modifications(ReadOnlySpan<char> code, ref int i, char increment, char decrement)
    {
        int result = 0;

        for (; i < code.Length; i++)
        {
            if (code[i] == increment) result++;
            else if (code[i] == decrement) result--;
            else break;
        }

        return result;
    }

    public static int Modifications(ReadOnlySpan<char> values, ref int i, char increment, char decrement, out int length)
    {
        int result = 0;
        length = 0;

        for (; i < values.Length; i++)
        {
            if (values[i].Equals(increment))
            { result++; length++; }
            else if (values[i].Equals(decrement))
            { result--; length++; }
            else
            { break; }
        }

        return result;
    }

    public static string RemoveNoncodes(string code, bool showProgress, DebugInformation? debugInformation)
    {
        StringBuilder result = new(code.Length);

        if (showProgress)
        {
            using ConsoleProgressLabel label = new($"Remove comments ...", ConsoleColor.DarkGray, true);
            label.Print();

            using ConsoleProgressBar progress = new(ConsoleColor.DarkGray, true);

            for (int i = 0; i < code.Length; i++)
            {
                progress.Print(i, code.Length);

                if (!IsCode(code[i]))
                {
                    debugInformation?.OffsetCodeFrom(result.Length, -1);
                    continue;
                }

                result.Append(code[i]);
            }
        }
        else
        {
            for (int i = 0; i < code.Length; i++)
            {
                if (!IsCode(code[i]))
                {
                    debugInformation?.OffsetCodeFrom(result.Length, -1);
                    continue;
                }

                result.Append(code[i]);
            }
        }

        return result.ToString();
    }

    public static string RemoveCodes(string code)
    {
        StringBuilder result = new(code.Length);
        for (int i = 0; i < code.Length; i++)
        {
            if (IsCode(code[i])) continue;
            result.Append(code[i]);
        }
        return result.ToString();
    }

    public static string ReplaceNoncodes(string code, char replaceWith)
    {
        StringBuilder builder = new(code);
        for (int i = 0; i < builder.Length; i++)
        {
            if (!IsCode(code[i]))
            {
                builder[i] = replaceWith;
            }
        }
        return builder.ToString();
    }

    public static string ReplaceCodes(string code, char replaceWith)
    {
        StringBuilder builder = new(code);
        for (int i = 0; i < builder.Length; i++)
        {
            if (IsCode(code[i]))
            {
                builder[i] = replaceWith;
            }
        }
        return builder.ToString();
    }

    public static void PrintCode(string code)
    {
        bool expectNumber = false;
        for (int i = 0; i < code.Length; i++)
        {
            switch (code[i])
            {
                case '>':
                case '<':
                    Console.ForegroundColor = ConsoleColor.Red;
                    expectNumber = true;
                    break;
                case '+':
                case '-':
                    Console.ForegroundColor = ConsoleColor.Blue;
                    expectNumber = true;
                    break;
                case '[':
                case ']':
                    Console.ForegroundColor = ConsoleColor.Green;
                    expectNumber = false;
                    break;
                case '.':
                case ',':
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    expectNumber = false;
                    break;
                default:
                #if NET_STANDARD
                    if (expectNumber && unchecked((uint)(code[i] - '0')) <= 9)
                #else
                    if (expectNumber && char.IsAsciiDigit(code[i]))
                #endif
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                    else if (IsCode(code[i]))
                    {
                        expectNumber = false;
                        Console.ForegroundColor = ConsoleColor.Magenta;
                    }
                    else
                    {
                        expectNumber = false;
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                    }
                    break;
            }
            Console.Write(code[i]);
        }
        Console.ResetColor();
    }

    public static ConsoleColor GetColor(char code) => code switch
    {
        '>' or '<' => ConsoleColor.Red,
        '+' or '-' => ConsoleColor.Blue,
        '[' or ']' => ConsoleColor.Green,
        '.' or ',' => ConsoleColor.Magenta,
        _ => IsCode(code) ? ConsoleColor.Magenta : ConsoleColor.DarkGray,
    };

    /// <summary>
    /// <b>Warning:</b> This will not call <see cref="Console.ResetColor"/>
    /// </summary>
    public static void PrintCodeChar(char code)
    {
        Console.ForegroundColor = GetColor(code);
        Console.Write(code);
    }
}

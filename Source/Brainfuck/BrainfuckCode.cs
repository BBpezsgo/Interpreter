using LanguageCore.Runtime;

namespace LanguageCore.Brainfuck;

public static class CharCode
{
    public static byte GetByte(char c) => Encoding.ASCII.GetBytes(new char[1] { c }, 0, 1)[0];

    public static char GetChar(byte v) => Encoding.ASCII.GetChars(new byte[1] { v }, 0, 1)[0];
}

public static class BrainfuckCode
{
    public static readonly char[] CodeCharacters = new char[]
    {
        '+', '-',
        '<', '>',
        '[', ']',
        '.', ',',
        '$',
    };

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

                if (!CodeCharacters.Contains(code[i]))
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
                if (!CodeCharacters.Contains(code[i]))
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
            if (CodeCharacters.Contains(code[i])) continue;
            result.Append(code[i]);
        }
        return result.ToString();
    }

    public static string ReplaceNoncodes(string code, char replaceWith)
    {
        StringBuilder builder = new(code);
        for (int i = 0; i < builder.Length; i++)
        {
            if (!CodeCharacters.Contains(code[i]))
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
            if (CodeCharacters.Contains(code[i]))
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
                    if (expectNumber && char.IsAsciiDigit(code[i]))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                    else if (CodeCharacters.Contains(code[i]))
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
        _ => CodeCharacters.Contains(code) ? ConsoleColor.Magenta : ConsoleColor.DarkGray,
    };

    /// <summary>
    /// <b>Warning:</b> This will not call <see cref="Console.ResetColor"/>
    /// </summary>
    public static void PrintCodeChar(char code)
    {
        Console.ForegroundColor = GetColor(code);
        Console.Write(code);
    }

    public static void PrintDebugInfo(string code, DebugInformation? debugInformation)
    {
        if (debugInformation == null)
        { return; }

        int j = 0;
        FunctionInformations last = default;
        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            ImmutableArray<FunctionInformations> funcInfos = debugInformation.GetFunctionInformationsNested(i);
            if (funcInfos.Length > 0)
            {
                if (!last.IsValid || last.Instructions != funcInfos[0].Instructions)
                {
                    j++;
                    last = funcInfos[0];
                }
                if (j % 2 == 0)
                {
                    Console.BackgroundColor = ConsoleColor.Gray;
                }
                else
                {
                    Console.BackgroundColor = ConsoleColor.DarkGray;
                }
            }
            else
            { Console.BackgroundColor = ConsoleColor.Black; }

            Console.ForegroundColor = BrainfuckCode.GetColor(c);
            Console.Write(c);
        }
        Console.ResetColor();
        Console.WriteLine();
    }

    public static void PrintDebugInfo(CompactCodeSegment[] code, DebugInformation? debugInformation)
    {
        if (debugInformation == null)
        { return; }

        int j = 0;
        FunctionInformations last = default;
        for (int i = 0; i < code.Length; i++)
        {
            CompactCodeSegment c = code[i];
            ImmutableArray<FunctionInformations> funcInfos = debugInformation.GetFunctionInformationsNested(i);
            if (funcInfos.Length > 0)
            {
                if (!last.IsValid || last.Instructions != funcInfos[0].Instructions)
                {
                    j++;
                    last = funcInfos[0];
                }
                if (j % 2 == 0)
                {
                    Console.BackgroundColor = ConsoleColor.Gray;
                }
                else
                {
                    Console.BackgroundColor = ConsoleColor.DarkGray;
                }
            }
            else
            { Console.BackgroundColor = ConsoleColor.Black; }

            string yeah = c.ToString();
            for (int k = 0; k < yeah.Length; k++)
            {
                Console.ForegroundColor = BrainfuckCode.GetColor(yeah[k]);
                Console.Write(yeah[k]);
            }
        }
        Console.ResetColor();
        Console.WriteLine();
    }
}

using System.ComponentModel;
using System.Runtime.CompilerServices;

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
    };

    public static string RemoveNoncodes(string code, bool showProgress)
    {
        if (showProgress)
        {
            using ConsoleProgressLabel label = new($"Remove comments ...", ConsoleColor.DarkGray, true);
            label.Print();

            using ConsoleProgressBar progress = new(ConsoleColor.DarkGray, true);
            StringBuilder result = new(code.Length);

            for (int i = 0; i < code.Length; i++)
            {
                if ((i & 0b_0011_1111_1111_1111) == 0)
                { progress.Print(i, code.Length); }

                if (!CodeCharacters.Contains(code[i])) continue;
                result.Append(code[i]);
            }

            return result.ToString();
        }
        else
        {
            StringBuilder result = new(code.Length);

            for (int i = 0; i < code.Length; i++)
            {
                if (!CodeCharacters.Contains(code[i])) continue;
                result.Append(code[i]);
            }

            return result.ToString();
        }
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

    /// <summary>
    /// <b>Warning:</b> This will not call <see cref="Console.ResetColor"/>
    /// </summary>
    public static void PrintCodeChar(char code)
    {
        switch (code)
        {
            case '>':
            case '<':
                Console.ForegroundColor = ConsoleColor.Red;
                break;
            case '+':
            case '-':
                Console.ForegroundColor = ConsoleColor.Blue;
                break;
            case '[':
            case ']':
                Console.ForegroundColor = ConsoleColor.Green;
                break;
            case '.':
            case ',':
                Console.ForegroundColor = ConsoleColor.Magenta;
                break;
            default:
                if (CodeCharacters.Contains(code))
                { Console.ForegroundColor = ConsoleColor.Magenta; }
                else
                { Console.ForegroundColor = ConsoleColor.DarkGray; }
                break;
        }
        Console.Write(code);
    }
}

using System;

namespace LanguageCore.Brainfuck;

public static class Minifier
{
    public static string Minify(string code)
    {
        string result = code;
        int pass = 1;
        int prevLength = result.Length;

        ConsoleProgressLabel label = new("Minify ...", ConsoleColor.DarkGray, true);

        while (true)
        {
            label.Label = $"Minify ... (pass: {pass++} length: {result.Length} - {prevLength - result.Length})";
            prevLength = result.Length;
            label.Print();

            if (Utils.TryReplace(ref result, "<>", string.Empty))
            { continue; }

            if (Utils.TryReplace(ref result, "><", string.Empty))
            { continue; }

            if (Utils.TryReplace(ref result, "+-", string.Empty))
            { continue; }

            if (Utils.TryReplace(ref result, "-+", string.Empty))
            { continue; }

            if (Utils.TryReplace(ref result, "[-][-]", "[-]"))
            { continue; }

            if (Utils.TryReplace(ref result, "][-]", "]"))
            { continue; }

            if (RemoveRedundantClears(ref result))
            { continue; }

            break;
        }

        label.Dispose();

        return result;
    }

    static bool RemoveRedundantClears(ref string result)
    {
        ReadOnlySpan<char> span = result.AsSpan();
        bool res = RemoveRedundantClears(ref span);
        result = span.ToString();
        return res;
    }
    static bool RemoveRedundantClears(ref ReadOnlySpan<char> result)
    {
        PredictedNumber<int> alreadyThere = 0;
        for (int i = 0; i < result.Length; i++)
        {
            if (result[i] == '[' &&
                i + 3 + 1 < result.Length &&
                result.Slice(i, 3).Equals("[-]", StringComparison.Ordinal) &&
                !alreadyThere.IsUnknown)
            {
                ReadOnlySpan<char> slice = result[(i + 3)..];

                string yeah;
                if (alreadyThere.Value > 0)
                { yeah = new string('+', alreadyThere.Value); }
                else
                { yeah = new string('-', -alreadyThere.Value); }

                if (slice.StartsWith(yeah) && slice.Length - alreadyThere.Value > 0)
                {
                    slice = slice[yeah.Length..];
                    result = result[..i];

                    Span<char> final = new char[result.Length + slice.Length];

                    result.CopyTo(final[..result.Length]);
                    slice.CopyTo(final[result.Length..]);

                    result = final;
                    return true;
                }
            }

            switch (result[i])
            {
                case '+':
                {
                    alreadyThere++;
                    break;
                }
                case '-':
                {
                    alreadyThere--;
                    break;
                }
                case '<':
                case '>':
                case '[':
                case ',':
                {
                    alreadyThere = PredictedNumber<int>.Unknown;
                    break;
                }
                case ']':
                {
                    alreadyThere = 0;
                    break;
                }
                case '.':
                default: break;
            }
        }

        return false;
    }
}

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

            if (RemoveRedundantClears(ref result))
            { continue; }

            if (RemoveRedundantBranches(ref result))
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
        result = new string(span);
        return res;
    }

    static bool RemoveRedundantClears(ref ReadOnlySpan<char> result)
    {
        PredictedNumber<int> alreadyThere = 0;
        for (int i = 0; i < result.Length; i++)
        {
            if (result[i] == '[' &&
                i + 3 + 1 < result.Length &&
                result.Slice(i, 3).SequenceEqual("[-]") &&
                !alreadyThere.IsUnknown)
            {
                ReadOnlySpan<char> slice = result[(i + 3)..];

                string? yeah = null;
                if (alreadyThere.Value > 0)
                { yeah = new string('+', alreadyThere.Value); }
                else if (alreadyThere.Value < 0)
                { yeah = new string('-', -alreadyThere.Value); }

                if (yeah is not null &&
                    slice.StartsWith(yeah) &&
                    slice.Length - alreadyThere.Value > 0)
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

            alreadyThere = result[i] switch
            {
                '+' => alreadyThere + 1,
                '-' => alreadyThere - 1,
                ']' => 0,
                '.' => alreadyThere,
                _ => PredictedNumber<int>.Unknown,
            };
        }

        return false;
    }

    static bool RemoveRedundantBranches(ref string result)
    {
        ReadOnlySpan<char> span = result.AsSpan();
        bool res = RemoveRedundantBranches(ref span);
        result = new string(span);
        return res;
    }

    static bool RemoveRedundantBranches(ref ReadOnlySpan<char> result)
    {
        for (int i = 0; i < result.Length; i++)
        {
            if (result[i] == ']' &&
                i + 2 < result.Length &&
                result.Slice(i, 2).SequenceEqual("]["))
            {
                ReadOnlySpan<char> slice = result[i..];
                slice = slice[1..];
                int depth = 0;
                int end = -1;
                for (int j = 0; j < slice.Length; j++)
                {
                    if (slice[j] == '[')
                    {
                        depth++;
                    }
                    else if (slice[j] == ']')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            end = j;
                            break;
                        }
                    }
                }

                if (end == -1)
                { return false; }

                slice = slice[(end + 1)..];
                int removed = end + 1;

                Span<char> final = new char[result.Length - removed];

                result[..(i + 1)].CopyTo(final);
                slice.CopyTo(final[(i + 1)..]);

                result = final;
                return true;
            }
        }

        return false;
    }
}

using LanguageCore.Runtime;

namespace LanguageCore.Brainfuck;

public static class Minifier
{
    public static string Minify(string code, DebugInformation? debugInformation)
    {
        string result = code;
        int pass = 1;
        int prevLength = result.Length;

        ConsoleProgressLabel label = new("Minify ...", ConsoleColor.DarkGray, true);

        if (debugInformation is null)
        {
            while (true)
            {
                label.Label = $"Minify ... (pass: {pass++} length: {result.Length} - {prevLength - result.Length})";
                prevLength = result.Length;
                label.Print();

                if (Remove(ref result, "<>"))
                { continue; }

                if (Remove(ref result, "><"))
                { continue; }

                if (Remove(ref result, "+-"))
                { continue; }

                if (Remove(ref result, "-+"))
                { continue; }

                if (Remove(ref result, "+[-]"))
                { continue; }

                if (Remove(ref result, "-[-]"))
                { continue; }

                if (RemoveRedundantInitializations(ref result, null))
                { continue; }

                if (RemoveRedundantClears(ref result, null))
                { continue; }

                if (CorrectInitializationAddresses(ref result, debugInformation))
                { continue; }

                break;
            }
        }
        else
        {
            while (true)
            {
                label.Label = $"Minify ... (pass: {pass++} length: {result.Length} - {prevLength - result.Length})";
                prevLength = result.Length;
                label.Print();

                if (Remove(ref result, "<>", debugInformation))
                { continue; }

                if (Remove(ref result, "><", debugInformation))
                { continue; }

                if (Remove(ref result, "+-", debugInformation))
                { continue; }

                if (Remove(ref result, "-+", debugInformation))
                { continue; }

                if (Remove(ref result, "+[-]", debugInformation))
                { continue; }

                if (Remove(ref result, "-[-]", debugInformation))
                { continue; }

                if (RemoveRedundantInitializations(ref result, debugInformation))
                { continue; }

                if (RemoveRedundantClears(ref result, debugInformation))
                { continue; }

                if (CorrectInitializationAddresses(ref result, debugInformation))
                { continue; }

                break;
            }
        }

        label.Dispose();

        return result;
    }

    static bool Remove(ref string @string, string value, DebugInformation debugInformation)
    {
        int indexOf = @string.IndexOf(value);
        if (indexOf == -1) return false;
        @string = @string[..indexOf] + @string[(indexOf + value.Length)..];
        debugInformation.OffsetCodeFrom(indexOf, -value.Length);
        return true;
    }

    static bool Remove(ref string @string, string value)
    {
        string old = @string;
        @string = @string.Replace(value, null);
        return !object.ReferenceEquals(old, @string);
    }

    static bool RemoveRedundantInitializations(ref string result, DebugInformation? debugInformation)
    {
        ReadOnlySpan<char> span = result.AsSpan();
        bool res = RemoveRedundantInitializations(ref span, debugInformation);
        result = new string(span);
        return res;
    }

    static bool RemoveRedundantInitializations(ref ReadOnlySpan<char> result, DebugInformation? debugInformation)
    {
        PredictedNumber<int> alreadyThere = 0;
        for (int i = 0; i < result.Length; i++)
        {
            if (result[i] == '[' &&
                i + 3 + 1 < result.Length &&
                result.Slice(i, 3).SequenceEqual("[-]") &&
                !alreadyThere.IsUnknown &&
                alreadyThere != 0)
            {
                ReadOnlySpan<char> slice = result[(i + 3)..];

                int redundantModification = BrainfuckCode.Modifications(slice, '+', '-', out int redundantModificationLength);

                if (redundantModification != 0 &&
                    slice.Length - redundantModificationLength > 0)
                {
                    slice = slice[redundantModificationLength..];

                    int correction = redundantModification - alreadyThere.Value;
                    int correctionLength = Math.Abs(correction);
                    ReadOnlySpan<char> correction_ = correctionLength > 0 ? new string(correction > 0 ? '+' : '-', correctionLength) : string.Empty;

                    debugInformation?.OffsetCodeFrom(i, -(redundantModificationLength + 3 - correctionLength));

                    Span<char> final = new char[i + slice.Length + correctionLength];
                    result[..i].CopyTo(final[..i]);
                    correction_.CopyTo(final[i..]);
                    slice.CopyTo(final[(i + correctionLength)..]);
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

    static bool RemoveRedundantClears(ref string result, DebugInformation? debugInformation)
    {
        ReadOnlySpan<char> span = result.AsSpan();
        bool res = RemoveRedundantClears(ref span, debugInformation);
        result = new string(span);
        return res;
    }

    static bool RemoveRedundantClears(ref ReadOnlySpan<char> result, DebugInformation? debugInformation)
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

                debugInformation?.OffsetCodeFrom(i + 1, -removed);

                Span<char> final = new char[result.Length - removed];

                result[..(i + 1)].CopyTo(final);
                slice.CopyTo(final[(i + 1)..]);

                result = final;
                return true;
            }
        }

        return false;
    }

    static bool CorrectInitializationAddresses(ref string result, DebugInformation? debugInformation)
    {
        ReadOnlySpan<char> span = result.AsSpan();
        bool res = CorrectInitializationAddresses(ref span, debugInformation);
        result = new string(span);
        return res;
    }

    static bool CorrectInitializationAddresses(ref ReadOnlySpan<char> result, DebugInformation? debugInformation)
    {
        PredictedNumber<int> alreadyThere = 0;
        int initializationStarted = -1;

        for (int i = 0; i < result.Length; i++)
        {
            if (!alreadyThere.IsUnknown &&
                BrainfuckCode.GetDataMovement(result[i..], out ImmutableArray<(int Offset, int Modification)> destinations, out int removed) &&
                destinations.Length == 1 &&
                destinations[0].Modification == 1)
            {
                int movementLength = removed + 1;
                ReadOnlySpan<char> slice = result[initializationStarted..(i + movementLength)];
                if (slice[^1] is not ']') throw new InternalException();
                string initialization = BrainfuckCode.GenerateModification(alreadyThere.Value, '+', '-');
                if (!slice[..initialization.Length].SequenceEqual(initialization)) throw new InternalException();

                string offset = BrainfuckCode.GenerateModification(destinations[0].Offset, '>', '<');
                string backOffset = BrainfuckCode.GenerateModification(-destinations[0].Offset, '>', '<');
                string corrected = offset + initialization + backOffset;
                removed = slice.Length - corrected.Length;

                debugInformation?.OffsetCodeFrom(i, -removed);

                Span<char> newResult = new char[result.Length - removed];
                result[..initializationStarted].CopyTo(newResult[..initializationStarted]);
                corrected.CopyTo(newResult[initializationStarted..(i + movementLength - removed)]);
                result[(i + movementLength)..].CopyTo(newResult[(i + movementLength - removed)..]);
                result = newResult;
                return true;
            }

            switch (result[i])
            {
                case '+':
                {
                    alreadyThere++;
                    if (initializationStarted == -1) initializationStarted = i;
                    break;
                }

                case '-':
                {
                    alreadyThere--;
                    if (initializationStarted == -1) initializationStarted = i;
                    break;
                }

                case ']':
                {
                    alreadyThere = 0;
                    initializationStarted = -1;
                    break;
                }

                // case '.': break;

                default:
                {
                    alreadyThere = PredictedNumber<int>.Unknown;
                    initializationStarted = -1;
                    break;
                }
            }
        }

        return false;
    }
}

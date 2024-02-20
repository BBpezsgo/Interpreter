using System;

namespace LanguageCore.Brainfuck
{
    public static class Minifier
    {
        public static string Minify(string code)
        {
            string result = code;
            while (true)
            {
                if (result.Contains("<>", StringComparison.Ordinal))
                { result = result.Replace("<>", string.Empty, StringComparison.Ordinal); }
                else if (result.Contains("><", StringComparison.Ordinal))
                { result = result.Replace("><", string.Empty, StringComparison.Ordinal); }
                else if (result.Contains("+-", StringComparison.Ordinal))
                { result = result.Replace("+-", string.Empty, StringComparison.Ordinal); }
                else if (result.Contains("-+", StringComparison.Ordinal))
                { result = result.Replace("-+", string.Empty, StringComparison.Ordinal); }
                else if (result.Contains("[-][-]", StringComparison.Ordinal))
                { result = result.Replace("[-][-]", "[-]", StringComparison.Ordinal); }
                else if (result.Contains("][-]", StringComparison.Ordinal))
                { result = result.Replace("][-]", "]", StringComparison.Ordinal); }
                else if (MinifyPrints(ref result))
                { }
                else break;
            }

            return result;
        }

        static bool MinifyPrints(ref string result)
        {
            PredictedNumber<int> alreadyThere = 0;
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] == '[' &&
                    i + 3 + 1 < result.Length &&
                    result.Substring(i, 3) == "[-]" &&
                    !alreadyThere.IsUnknown)
                {
                    string slice = result[(i + 3)..];

                    string yeah;
                    if (alreadyThere.Value > 0)
                    { yeah = new string('+', alreadyThere.Value); }
                    else
                    { yeah = new string('-', -alreadyThere.Value); }

                    if (slice.StartsWith(yeah) && slice.Length - alreadyThere.Value > 0)
                    {
                        slice = slice[yeah.Length..];
                        result = result[..i];
                        result += slice;
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
}

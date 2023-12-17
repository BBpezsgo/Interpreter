namespace LanguageCore.Brainfuck
{
    public static class Minifier
    {
        public static string Minify(string code)
        {
            string result = code;
            while (true)
            {
                if (result.Contains("<>", System.StringComparison.Ordinal))
                { result = result.Replace("<>", string.Empty, System.StringComparison.Ordinal); }
                else if (result.Contains("><", System.StringComparison.Ordinal))
                { result = result.Replace("><", string.Empty, System.StringComparison.Ordinal); }
                else if (result.Contains("+-", System.StringComparison.Ordinal))
                { result = result.Replace("+-", string.Empty, System.StringComparison.Ordinal); }
                else if (result.Contains("-+", System.StringComparison.Ordinal))
                { result = result.Replace("-+", string.Empty, System.StringComparison.Ordinal); }
                else if (result.Contains("[-][-]", System.StringComparison.Ordinal))
                { result = result.Replace("[-][-]", "[-]", System.StringComparison.Ordinal); }
                else if (result.Contains("][-]", System.StringComparison.Ordinal))
                { result = result.Replace("][-]", "]", System.StringComparison.Ordinal); }
                else break;
            }
            return result;
        }
    }
}

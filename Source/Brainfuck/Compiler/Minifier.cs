namespace LanguageCore.Brainfuck
{
    public class Minifier
    {
        public static string Minify(string code)
        {
            string result = code;
            while (true)
            {
                if (result.Contains("<>"))
                { result = result.Replace("<>", string.Empty); }
                else if (result.Contains("><"))
                { result = result.Replace("><", string.Empty); }
                else if (result.Contains("+-"))
                { result = result.Replace("+-", string.Empty); }
                else if (result.Contains("-+"))
                { result = result.Replace("-+", string.Empty); }
                else if (result.Contains("[-][-]"))
                { result = result.Replace("[-][-]", "[-]"); }
                else if (result.Contains("][-]"))
                { result = result.Replace("][-]", "]"); }
                else break;
            }
            return result;
        }
    }
}

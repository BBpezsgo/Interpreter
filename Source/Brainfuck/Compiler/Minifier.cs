
#nullable enable

namespace ProgrammingLanguage.Brainfuck
{
    internal class Minifier
    {
        internal static string Minify(string code)
        {
            string result = code;
            while (true)
            {
                if (result.Contains("<>"))
                { result = result.Replace("<>", ""); }
                else if (result.Contains("><"))
                { result = result.Replace("><", ""); }
                else if (result.Contains("+-"))
                { result = result.Replace("+-", ""); }
                else if (result.Contains("-+"))
                { result = result.Replace("-+", ""); }
                else if (result.Contains("[-][-]"))
                { result = result.Replace("[-][-]", "[-]"); }
                else break;
            }
            return result;
        }
    }
}

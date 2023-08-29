
#nullable enable

namespace ProgrammingLanguage.Brainfuck
{
    internal static class Simplifier
    {
        internal static string Simplify(string code)
        {
            string result = "";

            char multipleChar = '\0';
            int multipleCount = 0;
            for (int i = 0; i < code.Length; i++)
            {
                char v = code[i];
                if (multipleChar != '\0' && multipleChar != v)
                {
                    result += multipleChar;
                    if (multipleCount != 1) result += multipleCount;

                    multipleChar = '\0';
                    multipleCount = 0;
                }
                switch (v)
                {
                    case '+':
                    case '-':
                    case '<':
                    case '>':
                        if (multipleChar == v)
                        {
                            multipleCount++;
                        }
                        else
                        {
                            multipleChar = v;
                            multipleCount = 1;
                        }
                        break;
                    default:
                        result += v;
                        break;
                }
            }

            if (multipleChar != '\0')
            {
                result += multipleChar;
                if (multipleCount != 1) result += multipleCount;
            }

            return result;
        }
    }
}

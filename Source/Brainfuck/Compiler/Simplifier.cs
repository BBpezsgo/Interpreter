using System.Text;

namespace LanguageCore.Brainfuck
{
    public static class Simplifier
    {
        public static string Simplify(string code)
        {
            StringBuilder result = new(code.Length);

            char multipleChar = '\0';
            int multipleCount = 0;
            for (int i = 0; i < code.Length; i++)
            {
                char v = code[i];
                if (multipleChar != '\0' && multipleChar != v)
                {
                    result.Append(multipleChar);
                    if (multipleCount != 1) result.Append(multipleCount);

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
                        result.Append(v);
                        break;
                }
            }

            if (multipleChar != '\0')
            {
                result.Append(multipleChar);
                if (multipleCount != 1) result.Append(multipleCount);
            }

            return result.ToString();
        }
    }
}

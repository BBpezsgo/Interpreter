using System;
using System.Linq;
using System.Text;

namespace LanguageCore.Brainfuck
{
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
            '$', // IDK what it is
        };

        public static string RemoveNoncodes(string code)
        {
            StringBuilder result = new(code.Length);
            for (int i = 0; i < code.Length; i++)
            {
                if (!CodeCharacters.Contains(code[i])) continue;
                result.Append(code[i]);
            }
            return result.ToString();
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
    }
}

using System;
using System.Linq;
using System.Text;

#nullable enable

namespace LanguageCore.Brainfuck
{
    internal static class CharCode
    {
        public static byte GetByte(char c)
        {
            return Encoding.ASCII.GetBytes(new char[1] { c }, 0, 1)[0];
        }
        public static char GetChar(byte v)
        {
            return Encoding.ASCII.GetChars(new byte[1] { v }, 0, 1)[0];
        }
    }

    internal static class Utils
    {
        internal static readonly char[] CodeCharacters = new char[]
        { 
            '+', '-',
            '<', '>',
            '[', ']',
            '.', ',',
            '$',
        };

        internal static string RemoveNoncodes(string code)
        {
            string result = "";
            for (int i = 0; i < code.Length; i++)
            {
                if (!CodeCharacters.Contains(code[i])) continue;
                result += code[i];
            }
            return result;
        }

        internal static string RemoveCodes(string code)
        {
            string result = "";
            for (int i = 0; i < code.Length; i++)
            {
                if (CodeCharacters.Contains(code[i])) continue;
                result += code[i];
            }
            return result;
        }

        internal static string ReplaceNoncodes(string code, char replaceWith)
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

        internal static string ReplaceCodes(string code, char replaceWith)
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

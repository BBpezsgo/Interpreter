using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LanguageCore
{
    public static partial class GeneralExtensions
    {
        readonly struct EscapedCharacters
        {
            public const string QuotationMark = "\\\"";
            public const string Backslash = @"\\";
            public const string Null = @"\0";
            public const string A = @"\a";
            public const string B = @"\b";
            public const string F = @"\f";
            public const string N = @"\n";
            public const string R = @"\r";
            public const string Tab = @"\t";
            public const string V = @"\v";
            public const string U = @"\u";
        }

        public static string Escape(this char v)
        {
            switch (v)
            {
                case '\"': return EscapedCharacters.QuotationMark;
                case '\\': return EscapedCharacters.Backslash;
                case '\0': return EscapedCharacters.Null;
                case '\a': return EscapedCharacters.A;
                case '\b': return EscapedCharacters.B;
                case '\f': return EscapedCharacters.F;
                case '\n': return EscapedCharacters.N;
                case '\r': return EscapedCharacters.R;
                case '\t': return EscapedCharacters.Tab;
                case '\v': return EscapedCharacters.V;
                default:
                    if (v >= 0x20 && v <= 0x7e)
                    { return v.ToString(); }
                    else
                    { return EscapedCharacters.U + ((int)v).ToString("x4", CultureInfo.InvariantCulture); }
            }
        }

        public static string Escape(this string v)
        {
            StringBuilder literal = new(v.Length);
            for (int i = 0; i < v.Length; i++)
            { literal.Append(v[i].Escape()); }
            return literal.ToString();
        }

        public static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> v, IEnumerable<KeyValuePair<TKey, TValue>> elements) where TKey : notnull
        {
            foreach (KeyValuePair<TKey, TValue> pair in elements)
            { v.Add(pair.Key, pair.Value); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
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

        public static T Sum<T>(this IEnumerable<T> list) where T : INumberBase<T>
        {
            T result = T.Zero;
            foreach (T item in list)
            { result += item; }
            return result;
        }
    }

    public static class RangeExtensions
    {
        public static Range<T> Union<T>(this ref Range<T> self, Range<T> range)
            where T : IEquatable<T>, INumber<T>
            => new(T.Min(self.Start, range.Start), T.Max(self.End, range.End));

        public static Range<T> Union<T>(this ref Range<T> self, T start, T end)
            where T : IEquatable<T>, INumber<T>
            => new(T.Min(self.Start, start), T.Max(self.End, end));

        public static T Size<T>(this Range<T> range) where T : INumber<T>
            => T.Max(range.Start, range.End) - T.Min(range.Start, range.End);

        public static bool Contains<TRange, TValue>(this Range<TRange> range, TValue value)
            where TRange : IEquatable<TRange>, IComparisonOperators<TRange, TValue, bool>
            => range.Start <= value && range.End >= value;

        public static bool Overlaps<T>(this Range<T> a, Range<T> b)
            where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        {
            T maxStart = (a.Start > b.Start) ? a.Start : b.Start;
            T minEnd = (a.End < b.End) ? a.End : b.End;
            return maxStart <= minEnd;
        }
    }
}

using System;
using System.Numerics;

namespace LanguageCore
{
    public partial struct Range
    {
        public static bool Overlaps<T>(Range<T> a, Range<T> b)
            where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        {
            T maxStart = (a.Start > b.Start) ? a.Start : b.Start;
            T minEnd = (a.End < b.End) ? a.End : b.End;
            return maxStart <= minEnd;
        }

        public static Range<T> Union<T>(Range<T> a, Range<T> b)
            where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        {
            if (a.Start > b.Start)
            { a.Start = b.Start; }
            if (a.End < b.End)
            { a.End = b.End; }
            return a;
        }

        public static Range<T> Union<T>(Range<T> a, T b)
            where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        {
            if (a.Start > b)
            { a.Start = b; }
            if (a.End < b)
            { a.End = b; }
            return a;
        }
    }

    public static class RangeExtensions
    {
        public static T Size<T>(this Range<T> range) where T : INumber<T>
            => T.Max(range.Start, range.End) - T.Min(range.Start, range.End);

        public static bool Contains<TRange, TValue>(this Range<TRange> range, TValue value)
            where TRange : IEquatable<TRange>, IComparisonOperators<TRange, TValue, bool>
            => range.Start <= value && range.End >= value;

        public static bool Overlaps<T>(this Range<T> a, Range<T> b)
            where T : IEquatable<T>, IComparisonOperators<T, T, bool>
            => Range.Overlaps(a, b);
    }
}

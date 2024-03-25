namespace LanguageCore;

public struct Range
{
    public static bool Overlaps<T>(MutableRange<T> a, MutableRange<T> b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        T maxStart = (a.Start > b.Start) ? a.Start : b.Start;
        T minEnd = (a.End < b.End) ? a.End : b.End;
        return maxStart <= minEnd;
    }

    public static bool Inside<T>(MutableRange<T> outer, MutableRange<T> inner)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        => inner.Start >= outer.Start && inner.End <= outer.End;

    public static MutableRange<T> Union<T>(MutableRange<T> a, MutableRange<T> b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        if (a.Start > b.Start)
        { a.Start = b.Start; }
        if (a.End < b.End)
        { a.End = b.End; }
        return a;
    }

    public static MutableRange<T> Union<T>(MutableRange<T> a, T b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        if (a.Start > b)
        { a.Start = b; }
        if (a.End < b)
        { a.End = b; }
        return a;
    }

    public static bool Overlaps<T>(Range<T> a, Range<T> b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        T maxStart = (a.Start > b.Start) ? a.Start : b.Start;
        T minEnd = (a.End < b.End) ? a.End : b.End;
        return maxStart <= minEnd;
    }

    public static bool Inside<T>(Range<T> outer, Range<T> inner)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        => inner.Start >= outer.Start && inner.End <= outer.End;

    public static Range<T> Union<T>(Range<T> a, Range<T> b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        T start = a.Start;
        T end = a.End;

        if (a.Start > b.Start)
        { start = b.Start; }

        if (a.End < b.End)
        { end = b.End; }

        return new Range<T>(start, end);
    }

    public static Range<T> Union<T>(Range<T> a, T b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        T start = a.Start;
        T end = a.End;

        if (a.Start > b)
        { start = b; }

        if (a.End < b)
        { end = b; }

        return new Range<T>(start, end);
    }
}

public static class RangeExtensions
{
    public static T Size<T>(this MutableRange<T> range) where T : INumber<T>
        => T.Max(range.Start, range.End) - T.Min(range.Start, range.End);

    public static bool Contains<TRange, TValue>(this MutableRange<TRange> range, TValue value)
        where TRange : IEquatable<TRange>, IComparisonOperators<TRange, TValue, bool>
        => range.Start <= value && range.End >= value;

    public static bool Overlaps<T>(this MutableRange<T> a, MutableRange<T> b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        => Range.Overlaps(a, b);

    public static T Size<T>(this Range<T> range) where T : INumber<T>
        => T.Max(range.Start, range.End) - T.Min(range.Start, range.End);

    public static bool Contains<TRange, TValue>(this Range<TRange> range, TValue value)
        where TRange : IEquatable<TRange>, IComparisonOperators<TRange, TValue, bool>
        => range.Start <= value && range.End >= value;

    public static bool Overlaps<T>(this Range<T> a, Range<T> b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        => Range.Overlaps(a, b);

    public static T Middle<T>(this Range<T> a)
        where T : IEquatable<T>, IAdditionOperators<T, T, T>, IDivisionOperators<T, int, T>
        => (a.Start + a.End) / 2;

    public static IEnumerable<int> ForEach(this Range<int> a)
    {
        if (a.End < a.Start) // Reversed
        { return Enumerable.Range(a.End + 1, a.Start - a.End); }
        else
        { return Enumerable.Range(a.Start, a.End - a.Start); }
    }
}

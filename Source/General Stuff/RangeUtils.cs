namespace LanguageCore;

public struct Range
{
    public static bool Overlaps<T>(MutableRange<T> a, MutableRange<T> b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        T maxStart = Utils.Max(a.Start, b.Start);
        T minEnd = Utils.Min(a.End, b.End);
        return maxStart <= minEnd;
    }

    public static bool Inside<T>(MutableRange<T> outer, MutableRange<T> inner)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        outer = outer.Fix();
        inner = inner.Fix();
        return inner.Start >= outer.Start && inner.End <= outer.End;
    }

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
        T maxStart = Utils.Max(a.Start, b.Start);
        T minEnd = Utils.Min(a.End, b.End);
        return maxStart <= minEnd;
    }

    public static bool Inside<T>(Range<T> outer, Range<T> inner)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        outer = outer.Fix();
        inner = inner.Fix();
        return inner.Start >= outer.Start && inner.End <= outer.End;
    }

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

    public static Range<T> Intersect<T>(Range<T> a, Range<T> b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        bool isBackward = a.IsBackward();

        if (a.IsBackward() != b.IsBackward())
        { b = new Range<T>(b.End, b.Start); }

        if (isBackward)
        {
            a = new Range<T>(a.End, a.Start);
            b = new Range<T>(b.End, b.Start);
        }

        T start = Utils.Max(a.Start, b.Start);
        T end = Utils.Min(a.End, b.End);

        if (isBackward)
        { return new Range<T>(end, start); }
        else
        { return new Range<T>(start, end); }
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
    public static bool IsBackward<T>(this Range<T> range)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        => range.Start > range.End;

    public static T Size<T>(this MutableRange<T> range)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>, ISubtractionOperators<T, T, T>
        => Utils.Max(range.Start, range.End) - Utils.Min(range.Start, range.End);

    public static bool Contains<TRange, TValue>(this MutableRange<TRange> range, TValue value)
        where TRange : IEquatable<TRange>, IComparisonOperators<TRange, TValue, bool>, IComparisonOperators<TRange, TRange, bool>
    {
        range = range.Fix();
        return range.Start <= value && range.End >= value;
    }

    public static bool Overlaps<T>(this MutableRange<T> a, MutableRange<T> b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        => Range.Overlaps(a, b);

    public static T Size<T>(this Range<T> range)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>, ISubtractionOperators<T, T, T>
        => Utils.Max(range.Start, range.End) - Utils.Min(range.Start, range.End);

    public static bool Contains<TRange, TValue>(this Range<TRange> range, TValue value)
        where TRange : IEquatable<TRange>, IComparisonOperators<TRange, TValue, bool>, IComparisonOperators<TRange, TRange, bool>
    {
        range = range.Fix();
        return range.Start <= value && range.End >= value;
    }

    public static bool Overlaps<T>(this Range<T> a, Range<T> b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        => Range.Overlaps(a, b);

    public static T Middle<T>(this Range<T> a)
        where T : IEquatable<T>, IAdditionOperators<T, T, T>, IDivisionOperators<T, int, T>
        => (a.Start + a.End) / 2;

    public static Range<T> Fix<T>(this Range<T> v)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        T start = Utils.Min(v.Start, v.End);
        T end = Utils.Max(v.Start, v.End);
        return new Range<T>(start, end);
    }

    public static MutableRange<T> Fix<T>(this MutableRange<T> v)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        T start = Utils.Min(v.Start, v.End);
        T end = Utils.Max(v.Start, v.End);
        return new MutableRange<T>(start, end);
    }

    public static Range<TRange> Offset<TRange, TOffset>(this Range<TRange> v, TOffset offset)
        where TRange : IEquatable<TRange>, IAdditionOperators<TRange, TOffset, TRange>
        => new(v.Start + offset, v.End + offset);

    public static IEnumerable<int> ForEach(this Range<int> a)
    {
        if (a.End < a.Start) // Reversed
        { return Enumerable.Range(a.End + 1, a.Start - a.End); }
        else
        { return Enumerable.Range(a.Start, a.End - a.Start); }
    }
}

namespace LanguageCore;

public static class RangeUtils
{
#if LANG_11

    public static bool Overlaps<T>(MutableRange<T> a, MutableRange<T> b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        T maxStart = General.Max(a.Start, b.Start);
        T minEnd = General.Min(a.End, b.End);
        return maxStart <= minEnd;
    }

    public static bool Overlaps<T>(Range<T> a, Range<T> b)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        T maxStart = General.Max(a.Start, b.Start);
        T minEnd = General.Min(a.End, b.End);
        return maxStart <= minEnd;
    }

    public static bool Inside<T>(Range<T> outer, Range<T> inner)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        outer = outer.Fix();
        inner = inner.Fix();
        return inner.Start >= outer.Start && inner.End <= outer.End;
    }

    public static bool Inside<T>(MutableRange<T> outer, MutableRange<T> inner)
        where T : IEquatable<T>, IComparisonOperators<T, T, bool>
    {
        outer = outer.Fix();
        inner = inner.Fix();
        return inner.Start >= outer.Start && inner.End <= outer.End;
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

        T start = General.Max(a.Start, b.Start);
        T end = General.Min(a.End, b.End);

        if (isBackward)
        { return new Range<T>(end, start); }
        else
        { return new Range<T>(start, end); }
    }

#endif

    public static bool Overlaps(MutableRange<int> a, MutableRange<int> b)
    {
        int maxStart = General.Max(a.Start, b.Start);
        int minEnd = General.Min(a.End, b.End);
        return maxStart <= minEnd;
    }

    public static bool Overlaps(Range<int> a, Range<int> b)
    {
        int maxStart = General.Max(a.Start, b.Start);
        int minEnd = General.Min(a.End, b.End);
        return maxStart <= minEnd;
    }

    public static bool Inside(Range<int> outer, Range<int> inner)
    {
        outer = outer.Fix();
        inner = inner.Fix();
        return inner.Start >= outer.Start && inner.End <= outer.End;
    }

    public static bool Inside(MutableRange<int> outer, MutableRange<int> inner)
    {
        outer = outer.Fix();
        inner = inner.Fix();
        return inner.Start >= outer.Start && inner.End <= outer.End;
    }

    public static Range<int> Intersect(Range<int> a, Range<int> b)
    {
        bool isBackward = a.IsBackward();

        if (a.IsBackward() != b.IsBackward())
        { b = new Range<int>(b.End, b.Start); }

        if (isBackward)
        {
            a = new Range<int>(a.End, a.Start);
            b = new Range<int>(b.End, b.Start);
        }

        int start = General.Max(a.Start, b.Start);
        int end = General.Min(a.End, b.End);

        if (isBackward)
        { return new Range<int>(end, start); }
        else
        { return new Range<int>(start, end); }
    }

    #region Union

#if LANG_11

    public static Range<T> Union<T>(Range<T> a, T b) where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        => new(General.Min(a.Start, b), General.Max(a.End, b));
    public static MutableRange<T> Union<T>(MutableRange<T> a, T b) where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        => new(General.Min(a.Start, b), General.Max(a.End, b));

#endif

    public static Range<int> Union(Range<int> a, int b)
        => new(Math.Min(a.Start, b), Math.Max(a.End, b));

    public static MutableRange<int> Union(MutableRange<int> a, int b)
        => new(Math.Min(a.Start, b), Math.Max(a.End, b));

#if LANG_11

    public static Range<T> Union<T>(Range<T> a, Range<T> b) where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        => new(General.Min(a.Start, b.Start), General.Max(a.End, b.End));
    public static MutableRange<T> Union<T>(MutableRange<T> a, Range<T> b) where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        => new(General.Min(a.Start, b.Start), General.Max(a.End, b.End));
    public static MutableRange<T> Union<T>(MutableRange<T> a, MutableRange<T> b) where T : IEquatable<T>, IComparisonOperators<T, T, bool>
        => new(General.Min(a.Start, b.Start), General.Max(a.End, b.End));

#endif

    public static Range<int> Union(Range<int> a, Range<int> b)
        => new(Math.Min(a.Start, b.Start), Math.Max(a.End, b.End));

    public static MutableRange<int> Union(MutableRange<int> a, MutableRange<int> b)
        => new(Math.Min(a.Start, b.Start), Math.Max(a.End, b.End));

    #endregion
}

namespace LanguageCore;

public static class RangeUtils
{
    public static bool Overlaps(MutableRange<int> a, MutableRange<int> b)
    {
        int maxStart = Math.Max(a.Start, b.Start);
        int minEnd = Math.Min(a.End, b.End);
        return maxStart <= minEnd;
    }

    public static bool Overlaps(MutableRange<SinglePosition> a, MutableRange<SinglePosition> b)
    {
        SinglePosition maxStart = SinglePosition.Max(a.Start, b.Start);
        SinglePosition minEnd = SinglePosition.Min(a.End, b.End);
        return maxStart <= minEnd;
    }

    public static bool Overlaps(Range<int> a, Range<int> b)
    {
        int maxStart = Math.Max(a.Start, b.Start);
        int minEnd = Math.Min(a.End, b.End);
        return maxStart <= minEnd;
    }

    public static bool Overlaps(Range<SinglePosition> a, Range<SinglePosition> b)
    {
        SinglePosition maxStart = SinglePosition.Max(a.Start, b.Start);
        SinglePosition minEnd = SinglePosition.Min(a.End, b.End);
        return maxStart <= minEnd;
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

        int start = Math.Max(a.Start, b.Start);
        int end = Math.Min(a.End, b.End);

        if (isBackward)
        { return new Range<int>(end, start); }
        else
        { return new Range<int>(start, end); }
    }

    public static MutableRange<int> Union(MutableRange<int> a, int b)
        => new(Math.Min(a.Start, b), Math.Max(a.End, b));

    public static Range<SinglePosition> Union(Range<SinglePosition> a, Range<SinglePosition> b)
        => new(SinglePosition.Min(a.Start, b.Start), SinglePosition.Max(a.End, b.End));

    public static Range<int> Union(Range<int> a, Range<int> b)
        => new(Math.Min(a.Start, b.Start), Math.Max(a.End, b.End));
}

namespace LanguageCore;

public static class RangeExtensions
{
    public static bool IsBackward(this Range<int> range)
        => range.Start > range.End;

    public static bool IsBackward(this Range<SinglePosition> range)
        => range.Start > range.End;

    public static int Size(this MutableRange<int> range)
        => Math.Max(range.Start, range.End) - Math.Min(range.Start, range.End);

    public static bool Contains(this MutableRange<int> range, int value)
    {
        range = range.Fix();
        return range.Start <= value && range.End >= value;
    }

    public static bool Contains(this MutableRange<SinglePosition> range, SinglePosition value)
    {
        range = range.Fix();
        return range.Start <= value && range.End >= value;
    }

    public static bool Contains(this Range<int> range, int value)
    {
        range = range.Fix();
        return range.Start <= value && range.End >= value;
    }

    public static bool Contains(this Range<SinglePosition> range, SinglePosition value)
    {
        range = range.Fix();
        return range.Start <= value && range.End >= value;
    }

    public static Range<int> Fix(this Range<int> v)
    {
        int start = Math.Min(v.Start, v.End);
        int end = Math.Max(v.Start, v.End);
        return new Range<int>(start, end);
    }

    public static Range<SinglePosition> Fix(this Range<SinglePosition> v)
    {
        SinglePosition start = SinglePosition.Min(v.Start, v.End);
        SinglePosition end = SinglePosition.Max(v.Start, v.End);
        return new Range<SinglePosition>(start, end);
    }

    public static MutableRange<int> Fix(this MutableRange<int> v)
    {
        int start = Math.Min(v.Start, v.End);
        int end = Math.Max(v.Start, v.End);
        return new MutableRange<int>(start, end);
    }

    public static MutableRange<SinglePosition> Fix(this MutableRange<SinglePosition> v)
    {
        SinglePosition start = SinglePosition.Min(v.Start, v.End);
        SinglePosition end = SinglePosition.Max(v.Start, v.End);
        return new MutableRange<SinglePosition>(start, end);
    }

    public static Range<int> Offset(this Range<int> v, int offset)
        => new(v.Start + offset, v.End + offset);

    public static int Size(this Range<int> range)
        => Math.Max(range.Start, range.End) - Math.Min(range.Start, range.End);

    public static IEnumerable<int> ForEach(this Range<int> a)
    {
        if (a.End < a.Start) // Reversed
        { return Enumerable.Range(a.End + 1, a.Start - a.End); }
        else
        { return Enumerable.Range(a.Start, a.End - a.Start); }
    }

    public static MutableRange<T> ToMutable<T>(this Range<T> v)
        where T : IEquatable<T>
        => new(v.Start, v.End);
}

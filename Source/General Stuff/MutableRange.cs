namespace LanguageCore;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public partial struct MutableRange<T> :
    IEquatable<MutableRange<T>>,
    IEquatable<Range<T>>,
    IEquatable<T>,
    IEqualityOperators<MutableRange<T>, MutableRange<T>, bool>
    where T : IEquatable<T>
{
    public T Start;
    public T End;

    public MutableRange(T both)
    {
        Start = both;
        End = both;
    }
    public MutableRange(T start, T end)
    {
        Start = start;
        End = end;
    }

    public override bool Equals(object? obj) => obj is MutableRange<T> other && Equals(other);
    public bool Equals(MutableRange<T> other) => Start.Equals(other.Start) && End.Equals(other.End);
    public bool Equals(Range<T> other) => Start.Equals(other.Start) && End.Equals(other.End);
    public bool Equals(T start, T end) => Start.Equals(start) && End.Equals(end);
    public bool Equals(T? other) => Start.Equals(other) && End.Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Start, End);

    public override readonly string ToString() => $"({Start}, {End})";

    public static bool operator ==(MutableRange<T> left, MutableRange<T> right) => left.Equals(right);
    public static bool operator !=(MutableRange<T> left, MutableRange<T> right) => !left.Equals(right);

    public static implicit operator MutableRange<T>(ValueTuple<T, T> v) => new(v.Item1, v.Item2);
    public static implicit operator Range<T>(MutableRange<T> v) => new(v.Start, v.End);
}

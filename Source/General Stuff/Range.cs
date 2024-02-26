using System;
using System.Diagnostics;
using System.Numerics;

namespace LanguageCore;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public partial struct Range<T> :
    IEquatable<Range<T>>,
    IEquatable<ValueTuple<T, T>>,
    IEquatable<T>,
    IEqualityOperators<Range<T>, Range<T>, bool>,
    IEqualityOperators<Range<T>, ValueTuple<T, T>, bool>,
    IEqualityOperators<Range<T>, T, bool>
    where T : IEquatable<T>
{
    public T Start;
    public T End;

    public Range(T both)
    {
        Start = both;
        End = both;
    }
    public Range(T start, T end)
    {
        Start = start;
        End = end;
    }

    public override bool Equals(object? obj) => obj is Range<T> other && Equals(other);
    public bool Equals(Range<T> other) => Start.Equals(other.Start) && End.Equals(other.End);
    public bool Equals(T start, T end) => Start.Equals(start) && End.Equals(end);
    public bool Equals(T? other) => Start.Equals(other) && End.Equals(other);
    public bool Equals(ValueTuple<T, T> other) => Start.Equals(other.Item1) && End.Equals(other.Item2);

    public override readonly int GetHashCode() => HashCode.Combine(Start, End);

    public override readonly string ToString() => $"({Start}, {End})";

    public static bool operator ==(Range<T> left, Range<T> right) => left.Equals(right);
    public static bool operator !=(Range<T> left, Range<T> right) => !left.Equals(right);

    public static bool operator ==(Range<T> left, ValueTuple<T, T> right) => left.Equals(right);
    public static bool operator !=(Range<T> left, ValueTuple<T, T> right) => !left.Equals(right);

    public static bool operator ==(Range<T> left, T? right) => left.Equals(right);
    public static bool operator !=(Range<T> left, T? right) => !left.Equals(right);

    public static implicit operator ValueTuple<T, T>(Range<T> v) => new(v.Start, v.End);

    public static implicit operator Range<T>(ValueTuple<T, T> v) => new(v.Item1, v.Item2);
}

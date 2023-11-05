using System;
using System.Diagnostics;

namespace LanguageCore
{
    [DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
    public partial struct Range<T> : IEquatable<Range<T>>, IEquatable<ValueTuple<T, T>>, IEquatable<T> where T : IEquatable<T>
    {
        public T Start;
        public T End;

        public static Range<T> Default => default;

        public T this[int i]
        {
            readonly get => i switch
            {
                0 => Start,
                1 => End,
                _ => throw new ArgumentOutOfRangeException(nameof(i)),
            };
            set
            {
                switch (i)
                {
                    case 0:
                        Start = value;
                        break;
                    case 1:
                        End = value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(i));
                }
            }
        }

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
        public bool Equals(T? both) => Start.Equals(both) && End.Equals(both);
        public bool Equals(ValueTuple<T, T> other) => Start.Equals(other.Item1) && End.Equals(other.Item2);
        public override readonly int GetHashCode() => HashCode.Combine(Start, End);

        public override readonly string ToString() => $"({Start}, {End})";

        public static bool operator ==(Range<T> left, Range<T> right) => left.Equals(right);
        public static bool operator !=(Range<T> left, Range<T> right) => !left.Equals(right);

        public static bool operator ==(Range<T> left, ValueTuple<T, T> right) => left.Equals(right);
        public static bool operator !=(Range<T> left, ValueTuple<T, T> right) => !left.Equals(right);

        public static bool operator ==(ValueTuple<T, T> left, Range<T> right) => right.Equals(left);
        public static bool operator !=(ValueTuple<T, T> left, Range<T> right) => !right.Equals(left);

        public static bool operator ==(Range<T> left, T right) => left.Equals(right);
        public static bool operator !=(Range<T> left, T right) => !left.Equals(right);

        public static implicit operator ValueTuple<T, T>(Range<T> v) => new(v.Start, v.End);
        public static implicit operator Range<T>(ValueTuple<T, T> v) => new(v.Item1, v.Item2);
    }
}

using System;
using System.Diagnostics;

namespace LanguageCore
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public partial struct Range<T> : IEquatable<Range<T>> where T : IEquatable<T>
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

        public override bool Equals(object obj) => obj is Range<T> other && Equals(other);
        public bool Equals(Range<T> other) =>
            Start.Equals(other.Start) &&
            End.Equals(other.End);
        public override readonly int GetHashCode() => HashCode.Combine(Start, End);

        public override readonly string ToString() => $"({Start}, {End})";
        private readonly string GetDebuggerDisplay() => ToString();

        public static bool operator ==(Range<T> left, Range<T> right) => left.Equals(right);
        public static bool operator !=(Range<T> left, Range<T> right) => !left.Equals(right);

        public static implicit operator ValueTuple<T, T>(Range<T> v) => new(v.Start, v.End);
        public static implicit operator Range<T>(ValueTuple<T, T> v) => new(v.Item1, v.Item2);
    }
}

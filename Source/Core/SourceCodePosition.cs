using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LanguageCore
{
    public interface IThingWithPosition
    {
        public Position Position { get; }
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct Position : IEquatable<Position>
    {
        public static Position UnknownPosition => new(new Range<SinglePosition>(SinglePosition.Undefined), new Range<int>(-1));

        public Range<int> AbsoluteRange;
        public Range<SinglePosition> Range;

        public Position(Range<SinglePosition> range, Range<int> absoluteRange)
        {
            Range = range;
            AbsoluteRange = absoluteRange;
        }

        public Position(params IThingWithPosition?[] elements)
        {
            if (elements.Length == 0) throw new ArgumentException($"Array {nameof(elements)} length is 0");

            Range = Position.UnknownPosition.Range;
            AbsoluteRange = Position.UnknownPosition.AbsoluteRange;

            for (int i = 0; i < elements.Length; i++)
            {
                IThingWithPosition? element = elements[i];
                if (element == null) continue;
                if (element is Tokenizing.Token token && token.IsAnonymous) continue;
                Position position = element.Position;
                Range = position.Range;
                AbsoluteRange = position.AbsoluteRange;
                break;
            }

            for (int i = 1; i < elements.Length; i++)
            { Union(elements[i]); }
        }
        public Position(IEnumerable<IThingWithPosition?> elements) : this(elements.ToArray())
        { }

        public Position Union(Position other)
        {
            if (other.AbsoluteRange == Position.UnknownPosition.AbsoluteRange) return this;

            if (Range.Start.Line > other.Range.Start.Line)
            {
                Range.Start.Line = other.Range.Start.Line;
                Range.Start.Character = other.Range.Start.Character;
            }
            else if (Range.Start.Character > other.Range.Start.Character && Range.Start.Line == other.Range.Start.Line)
            {
                Range.Start.Character = other.Range.Start.Character;
            }

            if (Range.End.Line < other.Range.End.Line)
            {
                Range.End.Line = other.Range.End.Line;
                Range.End.Character = other.Range.End.Character;
            }
            else if (Range.End.Character < other.Range.End.Character && Range.End.Line == other.Range.End.Line)
            {
                Range.End.Character = other.Range.End.Character;
            }

            if (AbsoluteRange.Start > other.AbsoluteRange.Start)
            {
                AbsoluteRange.Start = other.AbsoluteRange.Start;
            }

            if (AbsoluteRange.End < other.AbsoluteRange.End)
            {
                AbsoluteRange.End = other.AbsoluteRange.End;
            }

            return this;
        }
        public Position Union(IThingWithPosition? other)
        {
            if (other == null) return this;
            if (other is Tokenizing.Token token && token.IsAnonymous) return this;
            return Union(other.Position);
        }
        public Position Union(params IThingWithPosition?[]? elements)
        {
            if (elements == null) return this;
            if (elements.Length == 0) return this;

            for (int i = 0; i < elements.Length; i++)
            { Union(elements[i]); }

            return this;
        }

        public readonly string ToStringRange()
        {
            if (Range.Start == Range.End) return Range.Start.ToStringMin();
            if (Range.Start.Line == Range.End.Line) return $"{Range.Start.Line}:({Range.Start.Character}-{Range.End.Character})";
            return $"{Range.Start.ToStringMin()}-{Range.End.ToStringMin()}";
        }

        public readonly string? ToStringCool(string prefix = "", string postfix = "")
        {
            if (Range.Start.Line < 0)
            { return null; }

            if (this == Position.UnknownPosition)
            { return null; }

            if (Range.Start.Character < 0)
            { return $"{prefix}line {Range.Start.Character}{postfix}"; }

            return $"{prefix}line {Range.Start.Line} and column {Range.Start.Character}{postfix}";
        }

        readonly string GetDebuggerDisplay()
        {
            if (this == Position.UnknownPosition)
            { return "?"; }
            return this.ToStringRange();
        }

        public readonly Position After() => new(new Range<SinglePosition>(new SinglePosition(this.Range.End.Line, this.Range.End.Character), new SinglePosition(this.Range.End.Line, this.Range.End.Character + 1)), new Range<int>(this.AbsoluteRange.End, this.AbsoluteRange.End + 1));

        public override bool Equals(object? obj) => obj is Position position && Equals(position);
        public bool Equals(Position other) => AbsoluteRange.Equals(other.AbsoluteRange) && Range.Equals(other.Range);

        public override readonly int GetHashCode() => HashCode.Combine(AbsoluteRange, Range);

        public readonly (Position Left, Position Right) CutInHalf()
        {
            if (Range.Start.Line != Range.End.Line)
            { throw new NotImplementedException(); }

            Position left = default;
            Position right = default;

            {
                ref Range<SinglePosition> leftRange = ref left.Range;
                ref Range<SinglePosition> rightRange = ref right.Range;

                int rangeSize = Range.End.Character - Range.Start.Character;

                if (rangeSize < 0)
                { throw new NotImplementedException(); }

                int leftRangeSize = rangeSize / 2;

                leftRange.Start = Range.Start;
                leftRange.End = new SinglePosition(Range.Start.Line, Range.Start.Character + leftRangeSize);

                rightRange.Start = leftRange.End;
                rightRange.End = Range.End;
            }

            {
                ref Range<int> leftRange = ref left.AbsoluteRange;
                ref Range<int> rightRange = ref right.AbsoluteRange;

                int rangeSize = AbsoluteRange.End - AbsoluteRange.Start;

                if (rangeSize < 0)
                { throw new NotImplementedException(); }

                int leftRangeSize = rangeSize / 2;

                leftRange.Start = AbsoluteRange.Start;
                leftRange.End = AbsoluteRange.Start + leftRangeSize;

                rightRange.Start = leftRange.End;
                rightRange.End = AbsoluteRange.End;
            }

            return (left, right);
        }

        public static bool operator ==(Position left, Position right) => left.Equals(right);
        public static bool operator !=(Position left, Position right) => !left.Equals(right);
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct SinglePosition :
        IEquatable<SinglePosition>,
        System.Numerics.IComparisonOperators<SinglePosition, SinglePosition, bool>,
        System.Numerics.IEqualityOperators<SinglePosition, SinglePosition, bool>,
        System.Numerics.IMinMaxValue<SinglePosition>
    {
        public int Line;
        public int Character;

        public readonly bool IsUndefined => Line < 0 || Character < 0;

        public static SinglePosition MaxValue => new(int.MaxValue, int.MaxValue);
        public static SinglePosition MinValue => new(0, 0);
        public static SinglePosition Undefined => new(-1, -1);
        public static SinglePosition Zero => new(0, 0);

        public SinglePosition(int line, int character)
        {
            Line = line;
            Character = character;
        }

        public static implicit operator SinglePosition(ValueTuple<int, int> v) => new(v.Item1, v.Item2);

        public static bool operator ==(SinglePosition a, SinglePosition b) => a.Line == b.Line && a.Character == b.Character;
        public static bool operator !=(SinglePosition a, SinglePosition b) => a.Line != b.Line || a.Character != b.Character;

        public static bool operator ==(SinglePosition a, int b) => a.Line == b && a.Character == b;
        public static bool operator !=(SinglePosition a, int b) => a.Line != b || a.Character != b;

        public static bool operator >(SinglePosition a, SinglePosition b)
        {
            if (a.Line > b.Line) return true;
            if (a.Character > b.Character && a.Line == b.Line) return true;
            return false;
        }

        public static bool operator <(SinglePosition a, SinglePosition b)
        {
            if (a.Line < b.Line) return true;
            if (a.Character < b.Character && a.Line == b.Line) return true;
            return false;
        }

        public static bool operator >=(SinglePosition a, SinglePosition b)
        {
            if (a.Line > b.Line) return true;
            if (a.Character >= b.Character && a.Line == b.Line) return true;
            return false;
        }

        public static bool operator <=(SinglePosition a, SinglePosition b)
        {
            if (a.Line < b.Line) return true;
            if (a.Character <= b.Character && a.Line == b.Line) return true;
            return false;
        }

        public override readonly string ToString() => $"({Line}:{Character})";
        public readonly string ToStringMin() => $"{Line}:{Character}";
        readonly string GetDebuggerDisplay()
        {
            if (this == SinglePosition.Undefined)
            { return "?"; }
            if (this == SinglePosition.Zero)
            { return "0"; }
            return ToString();
        }

        public override readonly bool Equals(object? obj) => obj is SinglePosition position && Equals(position);
        public readonly bool Equals(SinglePosition other) => Line == other.Line && Character == other.Character;
        public override readonly int GetHashCode() => HashCode.Combine(Line, Character);
    }
}

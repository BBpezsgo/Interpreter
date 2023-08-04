using ProgrammingLanguage.BBCode;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProgrammingLanguage.Core
{
    public interface IThingWithPosition
    {
        public Position GetPosition();
    }

    public struct Position
    {
        public static Position UnknownPosition => new(-1);

        public Range<int> AbsolutePosition;

        public SinglePosition Start;
        public SinglePosition End;

        public readonly Range<SinglePosition> Range => new(Start, End);

        public Position(int line)
        {
            Start = new SinglePosition(line, -1);
            End = new SinglePosition(line, -1);
            AbsolutePosition = new Range<int>(-1, -1);
        }

        public Position(int line, int column)
        {
            Start = new SinglePosition(line, column);
            End = new SinglePosition(line, column);
            AbsolutePosition = new Range<int>(-1, -1);
        }

        public Position(int line, int column, Range<int> absolutePosition)
        {
            Start = new SinglePosition(line, column);
            End = new SinglePosition(line, column);
            AbsolutePosition = absolutePosition;
        }

        public Position(Range<SinglePosition> position, Range<int> absolutePosition)
        {
            Start = position.Start;
            End = position.End;
            AbsolutePosition = absolutePosition;
        }

        public Position(params IThingWithPosition[] elements)
        {
            if (elements.Length == 0) throw new ArgumentException($"Array {nameof(elements)} length is 0");

            Start = Position.UnknownPosition.Start;
            End = Position.UnknownPosition.End;
            AbsolutePosition = Position.UnknownPosition.AbsolutePosition;

            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i] == null) continue;
                if (elements[i] is Token token && token.IsAnonymous) continue;
                Start = elements[0].GetPosition().Start;
                End = elements[0].GetPosition().End;
                AbsolutePosition = elements[0].GetPosition().AbsolutePosition;
                break;
            }

            for (int i = 1; i < elements.Length; i++)
            { Extend(elements[i]); }
        }
        public Position(IEnumerable<IThingWithPosition> elements)
            : this((elements ?? throw new ArgumentNullException(nameof(elements))).ToArray())
        { }
        internal void Extend(Position other)
        {
            if (other.AbsolutePosition == Position.UnknownPosition.AbsolutePosition) return;

            if (Start.Line > other.Start.Line)
            {
                Start.Line = other.Start.Line;
                Start.Character = other.Start.Character;
            }
            else if (Start.Character > other.Start.Character && Start.Line == other.Start.Line)
            {
                Start.Character = other.Start.Character;
            }

            if (End.Line < other.End.Line)
            {
                End.Line = other.End.Line;
                End.Character = other.End.Character;
            }
            else if (End.Character < other.End.Character && End.Line == other.End.Line)
            {
                End.Character = other.End.Character;
            }

            if (AbsolutePosition.Start > other.AbsolutePosition.Start)
            {
                AbsolutePosition.Start = other.AbsolutePosition.Start;
            }

            if (AbsolutePosition.End < other.AbsolutePosition.End)
            {
                AbsolutePosition.End = other.AbsolutePosition.End;
            }
        }

        internal void Extend(Range<int> absolutePosition) => AbsolutePosition.Extend(absolutePosition.Start, absolutePosition.End);
        internal void Extend(int start, int end) => AbsolutePosition.Extend(start, end);
        internal void Extend(IThingWithPosition other)
        {
            if (other == null) return;
            if (other is Token token && token.IsAnonymous) return;
            Extend(other.GetPosition());
        }
        internal void Extend(params IThingWithPosition[] elements)
        {
            if (elements == null) return;
            if (elements.Length == 0) return;

            for (int i = 0; i < elements.Length; i++)
            { Extend(elements[i]); }
        }
        internal void Extend(IEnumerable<IThingWithPosition> elements)
        {
            if (elements == null) return;

            foreach (IThingWithPosition element in elements)
            { Extend(element); }
        }

        public readonly string ToMinString()
        {
            if (Start == End) return Start.ToMinString();
            if (Start.Line == End.Line) return $"{Start.Line}:({Start.Character}-{End.Character})";
            return $"{Start.ToMinString()}-{End.ToMinString()}";
        }

        public readonly Position After() => new(new Range<SinglePosition>(new SinglePosition(this.End.Line, this.End.Character), new SinglePosition(this.End.Line, this.End.Character + 1)), new Range<int>(this.AbsolutePosition.End, this.AbsolutePosition.End + 1));
    }

    [DebuggerDisplay($"{{{nameof(ToMinString)}(),nq}}")]
    public struct SinglePosition : IEquatable<SinglePosition>
    {
        public int Line;
        public int Character;

        public SinglePosition(int line, int character)
        {
            Line = line;
            Character = character;
        }

        public static bool operator ==(SinglePosition a, SinglePosition b) => a.Line == b.Line && a.Character == b.Character;
        public static bool operator !=(SinglePosition a, SinglePosition b) => a.Line != b.Line || a.Character != b.Character;

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

        public override readonly string ToString() => $"SinglePos{{line: {Line}, char: {Character}}}";
        public readonly string ToMinString() => $"{Line}:{Character}";
        public override readonly bool Equals(object obj) => obj is SinglePosition position && Equals(position);
        public readonly bool Equals(SinglePosition other) => Line == other.Line && Character == other.Character;
        public override readonly int GetHashCode() => HashCode.Combine(Line, Character);
    }

    public struct Range<T> : IEquatable<Range<T>> where T : IEquatable<T>
    {
        public T Start;
        public T End;

        public Range(T start, T end)
        {
            Start = start;
            End = end;
        }

        public static Range<SinglePosition> Create(params Token[] tokens)
        {
            if (tokens.Length == 0) throw new ArgumentException($"Array 'tokens' length is 0");

            Range<SinglePosition> result = new()
            {
                Start = tokens[0].Position.Start,
                End = tokens[0].Position.End
            };

            for (int i = 1; i < tokens.Length; i++)
            {
                Token token = tokens[i];
                result = result.Extend(token.Position);
            }

            return result;
        }

        public override bool Equals(object obj)
            => obj is Range<T> other && Equals(other);

        public bool Equals(Range<T> other) =>
            Start.Equals(other.Start) &&
            End.Equals(other.End);

        public override readonly int GetHashCode() => HashCode.Combine(Start, End);

        public override readonly string ToString() => $"Range{{start: {Start}, end: {End}}}";

        public static bool operator ==(Range<T> left, Range<T> right) =>
            (IEquatable<T>)left.Start == (IEquatable<T>)right.Start &&
            (IEquatable<T>)left.End == (IEquatable<T>)right.End;
        public static bool operator !=(Range<T> left, Range<T> right) => !(left == right);
    }

    public struct Couples<T1, T2>
    {
        public T1 V1;
        public T2 V2;

        public Couples(T1 v1, T2 v2)
        { V1 = v1; V2 = v2; }
    }

    public struct Couples<T1, T2, T3>
    {
        public T1 V1;
        public T2 V2;
        public T3 V3;

        public Couples(T1 v1, T2 v2, T3 v3)
        { V1 = v1; V2 = v2; V3 = v3; }
    }

    public struct Couples<T1, T2, T3, T4>
    {
        public T1 V1;
        public T2 V2;
        public T3 V3;
        public T4 V4;

        public Couples(T1 v1, T2 v2, T3 v3, T4 v4)
        { V1 = v1; V2 = v2; V3 = v3; V4 = v4; }
    }
}

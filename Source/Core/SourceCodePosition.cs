using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LanguageCore
{
    public interface IThingWithPosition
    {
        public Position GetPosition();
    }

    public struct Position : IEquatable<Position>
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

        public Position(params IThingWithPosition?[] elements)
        {
            if (elements.Length == 0) throw new ArgumentException($"Array {nameof(elements)} length is 0");

            Start = Position.UnknownPosition.Start;
            End = Position.UnknownPosition.End;
            AbsolutePosition = Position.UnknownPosition.AbsolutePosition;

            for (int i = 0; i < elements.Length; i++)
            {
                IThingWithPosition? element = elements[i];
                if (element == null) continue;
                if (element is Tokenizing.Token token && token.IsAnonymous) continue;
                Start = element.GetPosition().Start;
                End = element.GetPosition().End;
                AbsolutePosition = element.GetPosition().AbsolutePosition;
                break;
            }

            for (int i = 1; i < elements.Length; i++)
            { Extend(elements[i]); }
        }
        public Position(IEnumerable<IThingWithPosition> elements)
            : this((elements ?? throw new ArgumentNullException(nameof(elements))).ToArray())
        { }
        internal Position Extend(Position other)
        {
            if (other.AbsolutePosition == Position.UnknownPosition.AbsolutePosition) return this;

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

            return this;
        }

        internal Position Extend(Range<int> absolutePosition)
        {
            AbsolutePosition.Extend(absolutePosition.Start, absolutePosition.End);
            return this;
        }
        internal Position Extend(int start, int end)
        {
            AbsolutePosition.Extend(start, end);
            return this;
        }
        internal Position Extend(IThingWithPosition? other)
        {
            if (other == null) return this;
            if (other is Tokenizing.Token token && token.IsAnonymous) return this;
            Extend(other.GetPosition());
            return this;
        }
        internal Position Extend(params IThingWithPosition?[]? elements)
        {
            if (elements == null) return this;
            if (elements.Length == 0) return this;

            for (int i = 0; i < elements.Length; i++)
            { Extend(elements[i]); }
            return this;
        }
        internal Position Extend(IEnumerable<IThingWithPosition?>? elements)
        {
            if (elements == null) return this;

            foreach (IThingWithPosition? element in elements)
            { Extend(element); }
            return this;
        }

        public readonly string ToMinString()
        {
            if (Start == End) return Start.ToMinString();
            if (Start.Line == End.Line) return $"{Start.Line}:({Start.Character}-{End.Character})";
            return $"{Start.ToMinString()}-{End.ToMinString()}";
        }

        public readonly Position After() => new(new Range<SinglePosition>(new SinglePosition(this.End.Line, this.End.Character), new SinglePosition(this.End.Line, this.End.Character + 1)), new Range<int>(this.AbsolutePosition.End, this.AbsolutePosition.End + 1));

        public override bool Equals(object? obj) => obj is Position position && Equals(position);
        public bool Equals(Position other) =>
            AbsolutePosition.Equals(other.AbsolutePosition) &&
            Start.Equals(other.Start) &&
            End.Equals(other.End);

        public override readonly int GetHashCode() => HashCode.Combine(AbsolutePosition, Start, End);

        public static bool operator ==(Position left, Position right) => left.Equals(right);
        public static bool operator !=(Position left, Position right) => !left.Equals(right);
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

        public override readonly bool Equals(object? obj) => obj is SinglePosition position && Equals(position);
        public readonly bool Equals(SinglePosition other) => Line == other.Line && Character == other.Character;
        public override readonly int GetHashCode() => HashCode.Combine(Line, Character);
    }
}

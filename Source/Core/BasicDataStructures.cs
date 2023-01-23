using IngameCoding.BBCode;

using System;
using System.Diagnostics;

namespace IngameCoding.Core
{
    public struct Position
    {
        public static Position UnknownPosition => new(-1);

        public Range<int> AbsolutePosition;

        public SinglePosition Start;
        public SinglePosition End;

        public Range<SinglePosition> Range => new(Start, End);

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

        public Position(params Token[] tokens)
        {
            if (tokens.Length == 0) throw new ArgumentException($"Array 'tokens' length is 0");
            Start = tokens[0].Position.Start;
            End = tokens[0].Position.End;
            AbsolutePosition = tokens[0].AbsolutePosition;

            for (int i = 1; i < tokens.Length; i++)
            {
                Token token = tokens[i];
                Extend(new Position(token.Position, token.AbsolutePosition));
            }
        }
        internal void Extend(Position other)
        {
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
        internal void Extend(Tokenizer.BaseToken token) => AbsolutePosition.Extend(token.AbsolutePosition);
        internal void Extend(BBCode.Parser.Statements.Statement statement) => AbsolutePosition.Extend(statement.TotalPosition().AbsolutePosition);
    
        public string ToMinString()
        {
            if (Start == End) return Start.ToMinString();
            if (Start.Line == End.Line) return $"{Start.Line}:({Start.Character}-{End.Character})";
            return $"{Start.ToMinString()}-{End.ToMinString()}";
        }
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

        public override string ToString() => $"SinglePos{{line: {Line}, char: {Character}}}";
        public string ToMinString() => $"{Line}:{Character}";
        public override bool Equals(object obj) => obj is SinglePosition position && Equals(position);
        public bool Equals(SinglePosition other) => Line == other.Line && Character == other.Character;
        public override int GetHashCode() => HashCode.Combine(Line, Character);
    }

    public struct Range<T>
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

        public override string ToString() => $"Range{{start: {Start}, end: {End}}}";
    }
}

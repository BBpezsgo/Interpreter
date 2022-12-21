using System;

namespace IngameCoding.Core
{
    public static class Extensions
    {
        public static string Repeat(this string v, int count)
        {
            string output = "";
            for (uint i = 0; i < count; i++)
            {
                output += v;
            }
            return output;
        }
    }

    public struct Interval
    {
        public int Start;
        public int End;

        public Interval(int start, int end)
        {
            Start = start;
            End = end;
        }
        internal void Extend(Interval interval) => Extend(interval.Start, interval.End);

        internal void Extend(int start, int end)
        {
            Start = Math.Min(Start, start);
            End = Math.Max(End, end);
        }
    }

    public struct Position
    {
        public static Position UnknownPosition => new(-1);

        readonly bool unknown;
        int line;
        readonly int col;

        public bool Unknown => unknown;
        public int Line
        {
            get { return line; }
            set { line = value; }
        }
        public int Col => col;

        public Interval AbsolutePosition;

        public Position(int line)
        {
            if (line > -1)
            {
                unknown = false;
                this.line = line;
            }
            else
            {
                unknown = true;
                this.line = -1;
            }
            col = -1;
            AbsolutePosition = new Interval(-1, -1);
        }

        public Position(int line, int column)
        {
            unknown = false;
            this.line = line;
            col = column;
            AbsolutePosition = new Interval(-1, -1);
        }

        public Position(int line, int column, Interval absolutePosition)
        {
            unknown = false;
            this.line = line;
            col = column;
            AbsolutePosition = absolutePosition;
        }

        internal void Extend(Interval absolutePosition) => AbsolutePosition.Extend(absolutePosition.Start, absolutePosition.End);
        internal void Extend(int start, int end) => AbsolutePosition.Extend(start, end);
    }
}

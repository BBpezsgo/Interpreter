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

    struct Interval
    {
        internal int Start;
        internal int End;

        internal Interval(int start, int end)
        {
            this.Start = start;
            this.End = end;
        }
        internal void Extend(Interval interval) => this.Extend(interval.Start, interval.End);

        internal void Extend(int start, int end)
        {
            this.Start = Math.Min(this.Start, start);
            this.End = Math.Max(this.End, end);
        }
    }

    struct Position
    {
        readonly bool unknown;
        int line;
        readonly int col;

        internal bool Unknown => unknown;
        internal int Line
        {
            get { return line; }
            set { line = value; }
        }
        internal int Col => col;

        internal Interval AbsolutePosition;

        internal Position(int line)
        {
            if (line > -1)
            {
                this.unknown = false;
                this.line = line;
            }
            else
            {
                this.unknown = true;
                this.line = -1;
            }
            this.col = -1;
            this.AbsolutePosition = new Interval(-1, -1);
        }

        internal Position(int line, int column)
        {
            this.unknown = false;
            this.line = line;
            this.col = column;
            this.AbsolutePosition = new Interval(-1, -1);
        }

        internal Position(int line, int column, Interval absolutePosition)
        {
            this.unknown = false;
            this.line = line;
            this.col = column;
            this.AbsolutePosition = absolutePosition;
        }

        internal void Extend(Interval absolutePosition) => this.AbsolutePosition.Extend(absolutePosition.Start, absolutePosition.End);
        internal void Extend(int start, int end) => this.AbsolutePosition.Extend(start, end);
    }
}

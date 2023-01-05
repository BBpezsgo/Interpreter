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
        public static bool Contains(this Range<int> self, int v) => v >= self.Start && v <= self.End;
        public static bool Contains(this Range<SinglePosition> self, SinglePosition v)
        {
            if (self.Start > v) return false;
            if (self.End < v) return false;

            return true;
        }
        public static bool Contains(this Range<SinglePosition> self, int Line, int Column)
        {
            if (self.Start.Line > Line) return false;
            if (self.Start.Character > Column) return false;

            if (self.End.Line < Line) return false;
            if (self.End.Character < Column) return false;

            return true;
        }
        public static Range<int> Extend(this Range<int> self, Range<int> range) => self.Extend(range.Start, range.End);
        public static Range<int> Extend(this Range<int> self, int start, int end) => new()
        {
            Start = System.Math.Min(self.Start, start),
            End = System.Math.Max(self.End, end),
        };
        public static Range<SinglePosition> Extend(this Range<SinglePosition> self, Range<SinglePosition> other)
        {
            Range<SinglePosition> result = new()
            {
                Start = new SinglePosition(self.Start.Line, self.Start.Character),
                End = new SinglePosition(self.End.Line, self.End.Character),
            };

            if (result.Start.Line > other.Start.Line)
            {
                result.Start.Line = other.Start.Line;
                result.Start.Character = other.Start.Character;
            }
            else if (result.Start.Character > other.Start.Character && result.Start.Line == other.Start.Line)
            {
                result.Start.Character = other.Start.Character;
            }

            if (result.End.Line < other.End.Line)
            {
                result.End.Line = other.End.Line;
                result.End.Character = other.End.Character;
            }
            else if (result.End.Character < other.End.Character && result.End.Line == other.End.Line)
            {
                result.End.Character = other.End.Character;
            }

            return result;
        }
        public static string ToMinString(this Range<SinglePosition> self)
        {
            if (self.Start == self.End) return self.Start.ToMinString();
            if (self.Start.Line == self.End.Line) return $"{self.Start.Line}:({self.Start.Character}-{self.End.Character})";
            return $"{self.Start.ToMinString()}-{self.End.ToMinString()}";
        }
        public static bool IsUnset(this Range<int> self) => self.Start == 0 && self.End == 0;
        public static bool IsUnset(this Range<SinglePosition> self) => self.Start.IsUnset() && self.End.IsUnset();
        public static bool IsUnset(this SinglePosition self) => self.Line == 0 && self.Character == 0;

        public static T[] Add<T>(this T[] self, params T[] values)
        {
            System.Collections.Generic.List<T> selfList = new(self);
            selfList.AddRange(values);
            return selfList.ToArray();
        }
        public static T[] Add<T>(this T[] self, T value)
        {
            System.Collections.Generic.List<T> selfList = new(self);
            selfList.Add(value);
            return selfList.ToArray();
        }
    }
}

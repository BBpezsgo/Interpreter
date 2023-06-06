using IngameCoding.BBCode;
using IngameCoding.Bytecode;
using IngameCoding.Tokenizer;

using System.Text;

namespace IngameCoding.Core
{
    public static class Extensions
    {
        public static object Value(this DataItem item) => item.type switch
        {
            RuntimeType.INT => item.ValueInt,
            RuntimeType.FLOAT => item.ValueFloat,
            RuntimeType.BOOLEAN => item.ValueBoolean,
            RuntimeType.CHAR => item.ValueChar,
            _ => null,
        };

        public static bool EqualType(this DataItem item, BuiltinType type) => item.type switch
        {
            RuntimeType.INT => type == BuiltinType.INT,
            RuntimeType.FLOAT => type == BuiltinType.FLOAT,
            RuntimeType.BOOLEAN => type == BuiltinType.BOOLEAN,
            RuntimeType.CHAR => type == BuiltinType.CHAR,
            _ => false,
        };

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

        /*
        [Obsolete("Don't use this")]
        public static T[] Add<T>(this T[] self, params T[] values)
        {
            System.Collections.Generic.List<T> selfList = new(self);
            selfList.AddRange(values);
            return selfList.ToArray();
        }
        [Obsolete("Don't use this")]
        public static T[] Add<T>(this T[] self, T value) => (new System.Collections.Generic.List<T>(self) { value }).ToArray();
        */

        public static Position After(this BaseToken self) => new(new Range<SinglePosition>(new SinglePosition(self.Position.End.Line, self.Position.End.Character), new SinglePosition(self.Position.End.Line, self.Position.End.Character + 1)), new Range<int>(self.AbsolutePosition.End, self.AbsolutePosition.End + 1));

        public static string Escape(this char v)
        {
            switch (v)
            {
                case '\"': return "\\\"";
                case '\\': return @"\\";
                case '\0': return @"\0";
                case '\a': return @"\a";
                case '\b': return @"\b";
                case '\f': return @"\f";
                case '\n': return @"\n";
                case '\r': return @"\r";
                case '\t': return @"\t";
                case '\v': return @"\v";
                default:
                    if (v >= 0x20 && v <= 0x7e)
                    { return v.ToString(); }
                    else
                    { return @"\u" + ((int)v).ToString("x4"); }
            }
        }
        public static string Escape(this string v)
        {
            if (v == null) return null;
            StringBuilder literal = new(v.Length);
            for (int i = 0; i < v.Length; i++)
            { literal.Append(v[i].Escape()); }
            return literal.ToString();
        }
    }
}

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using LanguageCore.BBCode.Compiler;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;

namespace LanguageCore
{
    public static class Extensions
    {
        public static bool Contains(this Token[] tokens, string value)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].Content == value)
                { return true; }
            }
            return false;
        }

        public static Type Convert(this RuntimeType v) => v switch
        {
            RuntimeType.BYTE => Type.BYTE,
            RuntimeType.INT => Type.INT,
            RuntimeType.FLOAT => Type.FLOAT,
            RuntimeType.CHAR => Type.CHAR,
            _ => throw new System.NotImplementedException(),
        };
        public static RuntimeType Convert(this Type v) => v switch
        {
            Type.BYTE => RuntimeType.BYTE,
            Type.INT => RuntimeType.INT,
            Type.FLOAT => RuntimeType.FLOAT,
            Type.CHAR => RuntimeType.CHAR,
            _ => throw new System.NotImplementedException(),
        };

        internal static bool TryGetAttribute<T0, T1, T2>(
            this Dictionary<string, AttributeValues> attributes,
            string attributeName,
            [NotNullWhen(true)] out T0? value0,
            [NotNullWhen(true)] out T1? value1,
            [NotNullWhen(true)] out T2? value2
            )
        {
            value0 = default;
            value1 = default;
            value2 = default;

            if (!attributes.TryGetValue(attributeName, out var values)) return false;

            if (!values.TryGetValue<T0>(0, out value0)) return false;
            if (!values.TryGetValue<T1>(1, out value1)) return false;
            if (!values.TryGetValue<T2>(2, out value2)) return false;

            return true;
        }

        internal static bool TryGetAttribute<T0, T1>(
            this Dictionary<string, AttributeValues> attributes,
            string attributeName,
            [NotNullWhen(true)] out T0? value0,
            [NotNullWhen(true)] out T1? value1
            )
        {
            value0 = default;
            value1 = default;

            if (!attributes.TryGetValue(attributeName, out var values)) return false;

            if (!values.TryGetValue<T0>(0, out value0)) return false;
            if (!values.TryGetValue<T1>(1, out value1)) return false;

            return true;
        }

        internal static bool TryGetAttribute<T0>(
            this Dictionary<string, AttributeValues> attributes,
            string attributeName,
            [NotNullWhen(true)] out T0? value0
            )
        {
            value0 = default;

            if (!attributes.TryGetValue(attributeName, out var values)) return false;

            if (!values.TryGetValue<T0>(0, out value0)) return false;

            return true;
        }

        internal static bool TryGetAttribute(
            this Dictionary<string, AttributeValues> attributes,
            string attributeName)
            => attributes.TryGetValue(attributeName, out _);

        internal static bool HasAttribute(
            this Dictionary<string, AttributeValues> attributes,
            string attributeName)
        {
            if (!attributes.TryGetValue(attributeName, out AttributeValues values)) return false;
            if (values.parameters.Count != 0) return false;

            return true;
        }

        internal static bool HasAttribute<T0>(
            this Dictionary<string, AttributeValues> attributes,
            string attributeName,
            T0 value0)
            where T0 : System.IEquatable<T0>
        {
            if (!attributes.TryGetValue(attributeName, out AttributeValues values)) return false;
            if (values.parameters.Count != 1) return false;

            if (!values.parameters[0].TryConvert(out T0 ?v0) ||
                !value0.Equals(v0))
            { return false; }

            return true;
        }

        internal static bool HasAttribute<T0, T1>(
            this Dictionary<string, AttributeValues> attributes,
            string attributeName,
            T0 value0,
            T1 value1)
            where T0 : System.IEquatable<T0>
            where T1 : System.IEquatable<T1>
        {
            if (!attributes.TryGetValue(attributeName, out AttributeValues values)) return false;
            if (values.parameters.Count != 2) return false;

            if (!values.parameters[0].TryConvert(out T0 ?v0) ||
                !value0.Equals(v0))
            { return false; }

            if (!values.parameters[1].TryConvert(out T1 ?v1) ||
                !value1.Equals(v1))
            { return false; }

            return true;
        }

        internal static bool HasAttribute<T0, T1, T2>(
            this Dictionary<string, AttributeValues> attributes,
            string attributeName,
            T0 value0,
            T1 value1,
            T2 value2)
            where T0 : System.IEquatable<T0>
            where T1 : System.IEquatable<T1>
            where T2 : System.IEquatable<T2>
        {
            if (!attributes.TryGetValue(attributeName, out AttributeValues values)) return false;
            if (values.parameters.Count != 3) return false;

            if (!values.parameters[0].TryConvert(out T0? v0) ||
                !value0.Equals(v0))
            { return false; }

            if (!values.parameters[1].TryConvert(out T1? v1) ||
                !value1.Equals(v1))
            { return false; }

            if (!values.parameters[2].TryConvert(out T2? v2) ||
                !value2.Equals(v2))
            { return false; }

            return true;
        }

        public static string Repeat(this string v, int count)
        {
            string output = string.Empty;
            for (uint i = 0; i < count; i++)
            {
                output += v;
            }
            return output;
        }
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
        public static bool IsUnset(this Range<SinglePosition> self) => self.Start.IsUnset() && self.End.IsUnset();
        public static bool IsUnset(this SinglePosition self) => self.Line == 0 && self.Character == 0;

        public static Position After(this BaseToken self) => new(new Range<SinglePosition>(new SinglePosition(self.Position.End.Line, self.Position.End.Character), new SinglePosition(self.Position.End.Line, self.Position.End.Character + 1)), new Range<int>(self.AbsolutePosition.End, self.AbsolutePosition.End + 1));
    }
}

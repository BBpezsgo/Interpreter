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
            RuntimeType.UInt8 => Type.Byte,
            RuntimeType.SInt32 => Type.Integer,
            RuntimeType.Single => Type.Float,
            RuntimeType.UInt16 => Type.Char,
            _ => throw new System.NotImplementedException(),
        };
        public static RuntimeType Convert(this Type v) => v switch
        {
            Type.Byte => RuntimeType.UInt8,
            Type.Integer => RuntimeType.SInt32,
            Type.Float => RuntimeType.Single,
            Type.Char => RuntimeType.UInt16,
            _ => throw new System.NotImplementedException(),
        };

        public static bool Contains(this Range<SinglePosition> self, SinglePosition v)
        {
            if (self.Start > v) return false;
            if (self.End < v) return false;

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
    }
}

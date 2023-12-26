using System.Diagnostics;

namespace LanguageCore
{
    using Compiler;
    using Runtime;
    using Tokenizing;

    public static class Extensions
    {
        public static bool Contains(this Token[] tokens, string value)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (string.Equals(tokens[i].Content, value))
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
            RuntimeType.Null => throw new System.NotImplementedException(),
            _ => throw new UnreachableException(),
        };

        public static RuntimeType Convert(this Type v) => v switch
        {
            Type.Byte => RuntimeType.UInt8,
            Type.Integer => RuntimeType.SInt32,
            Type.Float => RuntimeType.Single,
            Type.Char => RuntimeType.UInt16,
            Type.NotBuiltin => throw new System.NotImplementedException(),
            Type.Void => throw new System.NotImplementedException(),
            Type.Unknown => throw new System.NotImplementedException(),
            _ => throw new UnreachableException(),
        };
    }
}

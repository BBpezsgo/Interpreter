namespace LanguageCore;

using Compiler;
using Parser;
using Runtime;
using Tokenizing;

public static class Extensions
{
    public static ImmutableArray<TItem> Or<TItem>(this ImmutableArray<TItem> self, TItem value)
    {
        if (self.IsDefaultOrEmpty)
        { return ImmutableArray.Create(value); }
        return self;
    }

    public static bool IsSame<TFunction>(this TFunction a, TFunction b)
        where TFunction : FunctionThingDefinition, ICompiledFunction
    {
        if (!a.Type.Equals(b.Type)) return false;
        if (!a.Identifier.Content.Equals(b.Identifier.Content)) return false;
        if (!Utils.SequenceEquals(a.ParameterTypes, b.ParameterTypes)) return false;
        return true;
    }

    public static bool Contains(this ImmutableArray<Token> tokens, string value)
    {
        for (int i = 0; i < tokens.Length; i++)
        {
            if (string.Equals(tokens[i].Content, value))
            { return true; }
        }
        return false;
    }

    /// <exception cref="NotImplementedException"/>
    public static BasicType ToType(this BitWidth v) => v switch
    {
        BitWidth._8 => BasicType.Byte,
        BitWidth._16 => BasicType.Char,
        BitWidth._32 => BasicType.Integer,
        BitWidth._64 => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

    /// <exception cref="NotImplementedException"/>
    public static BasicType Convert(this RuntimeType v) => v switch
    {
        RuntimeType.Byte => BasicType.Byte,
        RuntimeType.Integer => BasicType.Integer,
        RuntimeType.Single => BasicType.Float,
        RuntimeType.Char => BasicType.Char,
        RuntimeType.Null => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };
}

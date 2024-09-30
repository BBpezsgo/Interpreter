using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;

namespace LanguageCore;

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
        BitWidth._8 => BasicType.U8,
        BitWidth._16 => BasicType.Char,
        BitWidth._32 => BasicType.I32,
        BitWidth._64 => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

    /// <exception cref="NotImplementedException"/>
    public static BasicType Convert(this RuntimeType v) => v switch
    {
        RuntimeType.U8 => BasicType.U8,
        RuntimeType.I8 => BasicType.I8,
        RuntimeType.Char => BasicType.Char,
        RuntimeType.I16 => BasicType.I16,
        RuntimeType.U32 => BasicType.U32,
        RuntimeType.I32 => BasicType.I32,
        RuntimeType.F32 => BasicType.F32,
        RuntimeType.Null => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };
}

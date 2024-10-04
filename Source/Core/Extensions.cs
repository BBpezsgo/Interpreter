using LanguageCore.Compiler;
using LanguageCore.Parser;
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
}

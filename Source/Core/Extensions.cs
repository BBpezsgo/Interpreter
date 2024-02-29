namespace LanguageCore;

using Compiler;
using Parser;
using Runtime;
using Tokenizing;

public static class Extensions
{
    public static bool Get(this IEnumerable<AttributeUsage> attributes, string? identifier, [NotNullWhen(true)] out AttributeUsage? attribute)
        => (attribute = Get(attributes, identifier)) is not null;
    public static AttributeUsage? Get(this IEnumerable<AttributeUsage> attributes, string? identifier)
    {
        if (identifier is null) return null;
        foreach (AttributeUsage attribute in attributes)
        {
            if (attribute.Identifier.Content == identifier)
            { return attribute; }
        }
        return null;
    }

    public static bool Contains(this Token[] tokens, string value)
    {
        for (int i = 0; i < tokens.Length; i++)
        {
            if (string.Equals(tokens[i].Content, value))
            { return true; }
        }
        return false;
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
    /// <exception cref="UnreachableException"/>
    public static BasicType Convert(this RuntimeType v) => v switch
    {
        RuntimeType.UInt8 => BasicType.Byte,
        RuntimeType.SInt32 => BasicType.Integer,
        RuntimeType.Single => BasicType.Float,
        RuntimeType.UInt16 => BasicType.Char,
        RuntimeType.Null => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="UnreachableException"/>
    public static RuntimeType Convert(this BasicType v) => v switch
    {
        BasicType.Byte => RuntimeType.UInt8,
        BasicType.Integer => RuntimeType.SInt32,
        BasicType.Float => RuntimeType.Single,
        BasicType.Char => RuntimeType.UInt16,
        BasicType.Void => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };
}

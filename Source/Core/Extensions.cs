namespace LanguageCore;

using Compiler;
using Parser;
using Runtime;
using Tokenizing;

public static class Extensions
{
    public static bool ContainsNull<T>([NotNullWhen(false)] this T?[] values, [NotNullWhen(false)] out T[]? nonnullValues) where T : class
    {
        nonnullValues = null;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] is null) return true;
        }
#pragma warning disable CS8619
        nonnullValues = values;
#pragma warning restore CS8619
        return false;
    }

    public static bool ContainsNull<T>([NotNullWhen(false)] this IEnumerable<T?> values, [NotNullWhen(false)] out IEnumerable<T>? nonnullValues) where T : class
    {
        nonnullValues = null;
        foreach (T? item in values)
        {
            if (item is null) return true;
        }
#pragma warning disable CS8619
        nonnullValues = values;
#pragma warning restore CS8619
        return false;
    }

    public static IEnumerable<T> Duplicate<T>(this IEnumerable<T> values)
        where T : IDuplicatable<T>
        => values.Select(item => item.Duplicate());

    public static int IndexOf(this StringBuilder stringBuilder, ReadOnlySpan<char> value)
    {
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            int res = chunk.Span.IndexOf(value);
            if (res != -1)
            { return res; }
        }
        return -1;
    }

    public static int IndexOf(this StringBuilder stringBuilder, ReadOnlySpan<char> value, StringComparison comparisonType)
    {
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            int res = chunk.Span.IndexOf(value, comparisonType);
            if (res != -1)
            { return res; }
        }
        return -1;
    }

    public static int IndexOf(this StringBuilder stringBuilder, char value)
    {
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            int res = chunk.Span.IndexOf(value);
            if (res != -1)
            { return res; }
        }
        return -1;
    }

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
        RuntimeType.Byte => BasicType.Byte,
        RuntimeType.Integer => BasicType.Integer,
        RuntimeType.Single => BasicType.Float,
        RuntimeType.Char => BasicType.Char,
        RuntimeType.Null => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="UnreachableException"/>
    public static RuntimeType Convert(this BasicType v) => v switch
    {
        BasicType.Byte => RuntimeType.Byte,
        BasicType.Integer => RuntimeType.Integer,
        BasicType.Float => RuntimeType.Single,
        BasicType.Char => RuntimeType.Char,
        BasicType.Void => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };
}

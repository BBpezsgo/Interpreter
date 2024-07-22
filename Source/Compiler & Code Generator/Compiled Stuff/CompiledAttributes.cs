namespace LanguageCore.Compiler;

using Parser;

public static class CompiledAttributes
{
    public static bool TryGetAttribute(this IEnumerable<AttributeUsage> attributes, string key, [NotNullWhen(true)] out AttributeUsage? value)
    {
        value = null;
        foreach (AttributeUsage attribute in attributes)
        {
            if (!attribute.Identifier.Content.Equals(key)) continue;
            if (value is not null) return false;
            value = attribute;
        }
        return value is not null;
    }
}

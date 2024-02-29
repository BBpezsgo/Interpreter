namespace LanguageCore.Compiler;

using Parser;

public static class CompiledAttributes
{
    /*
    public static IEnumerable<KeyValuePair<string, AttributeUsage>> ToDictionary(this IEnumerable<AttributeUsage> attributes)
    {
        foreach (AttributeUsage attribute in attributes)
        { yield return new KeyValuePair<string, AttributeUsage>(attribute.Identifier.Content, attribute); }
    }

    public static bool TryGetAttribute<T0, T1, T2>(
        this IReadOnlyDictionary<string, AttributeUsage> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out T1? value1,
        [NotNullWhen(true)] out T2? value2
        )
    {
        value0 = default;
        value1 = default;
        value2 = default;

        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;

        if (!values.TryGetValue<T0>(0, out value0)) return false;
        if (!values.TryGetValue<T1>(1, out value1)) return false;
        if (!values.TryGetValue<T2>(2, out value2)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0, T1>(
        this IReadOnlyDictionary<string, AttributeUsage> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out T1? value1
        )
    {
        value0 = default;
        value1 = default;

        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;

        if (!values.TryGetValue<T0>(0, out value0)) return false;
        if (!values.TryGetValue<T1>(1, out value1)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0>(
        this IReadOnlyDictionary<string, AttributeUsage> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0
        )
    {
        value0 = default;

        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;

        if (!values.TryGetValue<T0>(0, out value0)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0, T1, T2>(
        this IReadOnlyDictionary<string, AttributeUsage> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out T1? value1,
        [NotNullWhen(true)] out T2? value2,
        [NotNullWhen(true)] out AttributeUsage? attribute
        )
    {
        value0 = default;
        value1 = default;
        value2 = default;
        attribute = null;

        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        attribute = values;

        if (!values.TryGetValue<T0>(0, out value0)) return false;
        if (!values.TryGetValue<T1>(1, out value1)) return false;
        if (!values.TryGetValue<T2>(2, out value2)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0, T1>(
        this IReadOnlyDictionary<string, AttributeUsage> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out T1? value1,
        [NotNullWhen(true)] out AttributeUsage? attribute
        )
    {
        value0 = default;
        value1 = default;
        attribute = null;

        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        attribute = values;

        if (!values.TryGetValue<T0>(0, out value0)) return false;
        if (!values.TryGetValue<T1>(1, out value1)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0>(
        this IReadOnlyDictionary<string, AttributeUsage> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out AttributeUsage? attribute
        )
    {
        value0 = default;
        attribute = null;

        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        attribute = values;

        if (!values.TryGetValue<T0>(0, out value0)) return false;

        return true;
    }

    public static bool TryGetAttribute(
        this IReadOnlyDictionary<string, AttributeUsage> attributes,
        string attributeName)
        => attributes.TryGetValue(attributeName, out _);

    public static bool HasAttribute(
        this IReadOnlyDictionary<string, AttributeUsage> attributes,
        string attributeName)
    {
        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        if (values.Parameters.Length != 0) return false;

        return true;
    }

    public static bool HasAttribute<T0>(
        this IReadOnlyDictionary<string, AttributeUsage> attributes,
        string attributeName,
        T0 value0)
        where T0 : IEquatable<T0>
    {
        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        if (values.Parameters.Length != 1) return false;

        if (!values.Parameters[0].TryConvert(out T0? v0) ||
            !value0.Equals(v0))
        { return false; }

        return true;
    }

    public static bool HasAttribute<T0, T1>(
        this IReadOnlyDictionary<string, AttributeUsage> attributes,
        string attributeName,
        T0 value0,
        T1 value1)
        where T0 : IEquatable<T0>
        where T1 : IEquatable<T1>
    {
        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        if (values.Parameters.Length != 2) return false;

        if (!values.Parameters[0].TryConvert(out T0? v0) ||
            !value0.Equals(v0))
        { return false; }

        if (!values.Parameters[1].TryConvert(out T1? v1) ||
            !value1.Equals(v1))
        { return false; }

        return true;
    }

    public static bool HasAttribute<T0, T1, T2>(
        this IReadOnlyDictionary<string, AttributeUsage> attributes,
        string attributeName,
        T0 value0,
        T1 value1,
        T2 value2)
        where T0 : IEquatable<T0>
        where T1 : IEquatable<T1>
        where T2 : IEquatable<T2>
    {
        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        if (values.Parameters.Length != 3) return false;

        if (!values.Parameters[0].TryConvert(out T0? v0) ||
            !value0.Equals(v0))
        { return false; }

        if (!values.Parameters[1].TryConvert(out T1? v1) ||
            !value1.Equals(v1))
        { return false; }

        if (!values.Parameters[2].TryConvert(out T2? v2) ||
            !value2.Equals(v2))
        { return false; }

        return true;
    }
    */

    static bool TryGetValue(this IEnumerable<AttributeUsage> attributes, string key, [NotNullWhen(true)]out AttributeUsage? value)
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

    public static bool TryGetAttribute<T0, T1, T2>(
        this IEnumerable<AttributeUsage> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out T1? value1,
        [NotNullWhen(true)] out T2? value2
        )
    {
        value0 = default;
        value1 = default;
        value2 = default;

        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;

        if (!values.TryGetValue<T0>(0, out value0)) return false;
        if (!values.TryGetValue<T1>(1, out value1)) return false;
        if (!values.TryGetValue<T2>(2, out value2)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0, T1>(
        this IEnumerable<AttributeUsage> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out T1? value1
        )
    {
        value0 = default;
        value1 = default;

        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;

        if (!values.TryGetValue<T0>(0, out value0)) return false;
        if (!values.TryGetValue<T1>(1, out value1)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0>(
        this IEnumerable<AttributeUsage> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0
        )
    {
        value0 = default;

        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;

        if (!values.TryGetValue<T0>(0, out value0)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0, T1, T2>(
        this IEnumerable<AttributeUsage> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out T1? value1,
        [NotNullWhen(true)] out T2? value2,
        [NotNullWhen(true)] out AttributeUsage? attribute
        )
    {
        value0 = default;
        value1 = default;
        value2 = default;
        attribute = null;

        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        attribute = values;

        if (!values.TryGetValue<T0>(0, out value0)) return false;
        if (!values.TryGetValue<T1>(1, out value1)) return false;
        if (!values.TryGetValue<T2>(2, out value2)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0, T1>(
        this IEnumerable<AttributeUsage> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out T1? value1,
        [NotNullWhen(true)] out AttributeUsage? attribute
        )
    {
        value0 = default;
        value1 = default;
        attribute = null;

        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        attribute = values;

        if (!values.TryGetValue<T0>(0, out value0)) return false;
        if (!values.TryGetValue<T1>(1, out value1)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0>(
        this IEnumerable<AttributeUsage> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out AttributeUsage? attribute
        )
    {
        value0 = default;
        attribute = null;

        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        attribute = values;

        if (!values.TryGetValue<T0>(0, out value0)) return false;

        return true;
    }

    public static bool TryGetAttribute(
        this IEnumerable<AttributeUsage> attributes,
        string attributeName)
        => attributes.TryGetValue(attributeName, out _);

    public static bool HasAttribute(
        this IEnumerable<AttributeUsage> attributes,
        string attributeName)
    {
        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        if (values.Parameters.Length != 0) return false;

        return true;
    }

    public static bool HasAttribute<T0>(
        this IEnumerable<AttributeUsage> attributes,
        string attributeName,
        T0 value0)
        where T0 : IEquatable<T0>
    {
        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        if (values.Parameters.Length != 1) return false;

        if (!values.Parameters[0].TryConvert(out T0? v0) ||
            !value0.Equals(v0))
        { return false; }

        return true;
    }

    public static bool HasAttribute<T0, T1>(
        this IEnumerable<AttributeUsage> attributes,
        string attributeName,
        T0 value0,
        T1 value1)
        where T0 : IEquatable<T0>
        where T1 : IEquatable<T1>
    {
        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        if (values.Parameters.Length != 2) return false;

        if (!values.Parameters[0].TryConvert(out T0? v0) ||
            !value0.Equals(v0))
        { return false; }

        if (!values.Parameters[1].TryConvert(out T1? v1) ||
            !value1.Equals(v1))
        { return false; }

        return true;
    }

    public static bool HasAttribute<T0, T1, T2>(
        this IEnumerable<AttributeUsage> attributes,
        string attributeName,
        T0 value0,
        T1 value1,
        T2 value2)
        where T0 : IEquatable<T0>
        where T1 : IEquatable<T1>
        where T2 : IEquatable<T2>
    {
        if (!attributes.TryGetValue(attributeName, out AttributeUsage? values)) return false;
        if (values.Parameters.Length != 3) return false;

        if (!values.Parameters[0].TryConvert(out T0? v0) ||
            !value0.Equals(v0))
        { return false; }

        if (!values.Parameters[1].TryConvert(out T1? v1) ||
            !value1.Equals(v1))
        { return false; }

        if (!values.Parameters[2].TryConvert(out T2? v2) ||
            !value2.Equals(v2))
        { return false; }

        return true;
    }
}

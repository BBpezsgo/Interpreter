namespace LanguageCore;

public static class GeneralExtensions
{
    readonly struct EscapedCharacters
    {
        public const string QuotationMark = "\\\"";
        public const string Backslash = @"\\";
        public const string Null = @"\0";
        public const string A = @"\a";
        public const string B = @"\b";
        public const string F = @"\f";
        public const string N = @"\n";
        public const string R = @"\r";
        public const string Tab = @"\t";
        public const string V = @"\v";
        public const string U = @"\u";
    }

    public static string Escape(this char v)
    {
        switch (v)
        {
            case '\"': return EscapedCharacters.QuotationMark;
            case '\\': return EscapedCharacters.Backslash;
            case '\0': return EscapedCharacters.Null;
            case '\a': return EscapedCharacters.A;
            case '\b': return EscapedCharacters.B;
            case '\f': return EscapedCharacters.F;
            case '\n': return EscapedCharacters.N;
            case '\r': return EscapedCharacters.R;
            case '\t': return EscapedCharacters.Tab;
            case '\v': return EscapedCharacters.V;
            default:
                if (v is >= (char)0x20 and <= (char)0x7e)
                {
                    return char.ToString(v);
                }
                else
                {
                    return EscapedCharacters.U + ((int)v).ToString("x4", CultureInfo.InvariantCulture);
                }
        }
    }

    public static string Escape(this string v)
    {
        StringBuilder literal = new(v.Length);
        for (int i = 0; i < v.Length; i++)
        { literal.Append(v[i].Escape()); }
        return literal.ToString();
    }

    [return: NotNullIfNotNull(nameof(text))]
    public static string? Surround(this string? text, string prefix, string suffix)
    {
        if (text is null) return null;
        return prefix + text + suffix;
    }

    public static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> v, IEnumerable<KeyValuePair<TKey, TValue>> elements) where TKey : notnull
    {
        foreach (KeyValuePair<TKey, TValue> pair in elements)
        { v.Add(pair.Key, pair.Value); }
    }

    public static void AddSorted<T>(this List<T> list, T value)
    {
        int x = list.BinarySearch(value);
        list.Insert((x >= 0) ? x : ~x, value);
    }

    public static void AddRangeIf<T>(this ICollection<T> collection, IEnumerable<T> items, Func<T, bool> condition)
    {
        foreach (T item in items)
        {
            if (condition.Invoke(item))
            {
                collection.Add(item);
            }
        }
    }

    public static void Set<T>(this List<T> collection, ImmutableArray<T> newValues)
    {
        collection.Clear();
#if NET_STANDARD
        collection.AddRange(newValues);
#else
        collection.AddRange(newValues.AsSpan());
#endif
    }

    public static bool Contains<T>(this ReadOnlySpan<T> collection, Predicate<T> predicate)
    {
        for (int i = 0; i < collection.Length; i++)
        {
            if (predicate.Invoke(collection[i]))
            {
                return true;
            }
        }
        return false;
    }

    public static bool Contains(this StringBuilder stringBuilder, char value)
    {
#if NET_STANDARD
        return stringBuilder.Contains(value);
#else
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            if (chunk.Span.Contains(value))
            { return true; }
        }
        return false;
#endif
    }

    public static bool Contains(this StringBuilder stringBuilder, ReadOnlySpan<char> value)
    {
#if NET_STANDARD
        return stringBuilder.Contains(value);
#else
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            if (chunk.Span.Contains(value, StringComparison.Ordinal))
            { return true; }
        }
        return false;
#endif
    }

    public static int IndexOf(this StringBuilder stringBuilder, ReadOnlySpan<char> value)
    {
#if NET_STANDARD
        return stringBuilder.IndexOf(value);
#else
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            int res = chunk.Span.IndexOf(value, StringComparison.Ordinal);
            if (res != -1)
            { return res; }
        }
        return -1;
#endif
    }

    public static int IndexOf(this StringBuilder stringBuilder, char value)
    {
#if NET_STANDARD
        return stringBuilder.IndexOf(value);
#else
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            int res = chunk.Span.IndexOf(value);
            if (res != -1)
            { return res; }
        }
        return -1;
#endif
    }

    public static void AppendIndented(this StringBuilder stringBuilder, string indent, string value)
    {
        string[] lines = value.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (!string.IsNullOrWhiteSpace(line))
            {
                stringBuilder.Append(indent);
                stringBuilder.Append(value);
            }
            stringBuilder.AppendLine();
        }
    }

    public static void Indent(this StringBuilder stringBuilder, int indent) => stringBuilder.Append(' ', indent * 2);
}

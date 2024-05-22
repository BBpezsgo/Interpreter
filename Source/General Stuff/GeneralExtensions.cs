﻿namespace LanguageCore;

public static class GeneralExtensions
{
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

    public static void Set<T>(this List<T> collection, IEnumerable<T> newValues)
    {
        collection.Clear();
        collection.AddRange(newValues);
    }

    public static void Set<T>(this List<T> collection, ReadOnlySpan<T> newValues)
    {
        collection.Clear();
        collection.AddRange(newValues);
    }

    public static void Set<T>(this List<T> collection, T[] newValues)
    {
        collection.Clear();
        collection.AddRange(newValues);
    }

    public static bool ContainsNull<T>([NotNullWhen(false)] this ImmutableArray<T?> values, [NotNullWhen(false)] out ImmutableArray<T> nonnullValues) where T : class
    {
        nonnullValues = default;
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

    public static bool Contains(this StringBuilder stringBuilder, char value)
    {
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            if (chunk.Span.Contains(value))
            { return true; }
        }
        return false;
    }

    public static bool Contains(this StringBuilder stringBuilder, ReadOnlySpan<char> value)
    {
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            if (chunk.Span.Contains(value, StringComparison.Ordinal))
            { return true; }
        }
        return false;
    }

    public static int IndexOf(this StringBuilder stringBuilder, ReadOnlySpan<char> value)
    {
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            int res = chunk.Span.IndexOf(value, StringComparison.Ordinal);
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
}

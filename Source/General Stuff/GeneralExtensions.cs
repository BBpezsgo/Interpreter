﻿namespace LanguageCore;

public static class GeneralExtensions
{
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
}

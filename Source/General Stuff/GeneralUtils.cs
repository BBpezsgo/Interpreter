﻿using System.IO;

namespace LanguageCore;

public interface IDuplicatable<T> : ICloneable
    where T : notnull
{
    public T Duplicate();

    object ICloneable.Clone() => Duplicate();
}

public static partial class Utils
{
    public static bool SequenceEquals<T1, T2>(IEnumerable<T1> collectionA, IEnumerable<T2> collectionB)
        where T1 : IEquatable<T2>
    {
        using IEnumerator<T1> e1 = collectionA.GetEnumerator();
        using IEnumerator<T2> e2 = collectionB.GetEnumerator();

        while (e1.MoveNext())
        {
            if (!(e2.MoveNext() && e1.Current.Equals(e2.Current)))
            { return false; }
        }

        return !e2.MoveNext();
    }

    public static bool SequenceEquals<T1, T2>(IEnumerable<T1> collectionA, IEnumerable<T2> collectionB, Func<T1, T2, bool> checker)
    {
        using IEnumerator<T1> e1 = collectionA.GetEnumerator();
        using IEnumerator<T2> e2 = collectionB.GetEnumerator();

        while (e1.MoveNext())
        {
            if (!(e2.MoveNext() && checker.Invoke(e1.Current, e2.Current)))
            { return false; }
        }

        return !e2.MoveNext();
    }

    /// <summary>
    /// Source: <see href="https://stackoverflow.com/questions/3855956/check-if-an-executable-exists-in-the-windows-path"/>
    /// </summary>
    public static bool GetFullPath(string fileName, [NotNullWhen(true)] out string? fullPath) => (fullPath = GetFullPath(fileName)) is not null;
    /// <summary>
    /// Source: <see href="https://stackoverflow.com/questions/3855956/check-if-an-executable-exists-in-the-windows-path"/>
    /// </summary>
    public static string? GetFullPath(string fileName)
    {
        if (File.Exists(fileName))
        { return Path.GetFullPath(fileName); }

        string? values = Environment.GetEnvironmentVariable("PATH");
        if (values is null) return null;

        foreach (string path in values.Split(Path.PathSeparator))
        {
            string fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
            { return fullPath; }
        }

        return null;
    }

    public static bool PowerOf2(int n) => n != 0 && (n & (n - 1)) == 0;

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
        => Utils.Escape(v, out _);

    public static string Escape(this char v, out bool modified)
    {
        modified = true;
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
                if (v >= 0x20 && v <= 0x7e)
                {
                    modified = false;
                    return char.ToString(v);
                }
                else
                {
                    return $"{EscapedCharacters.U}{((int)v).ToString("x4", CultureInfo.InvariantCulture)}";
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

    public static bool Escape(ref string v)
    {
        StringBuilder literal = new(v.Length);
        bool modified = false;
        for (int i = 0; i < v.Length; i++)
        {
            literal.Append(v[i].Escape(out bool _modified));
            modified = modified || _modified;
        }
        v = literal.ToString();
        return modified;
    }

    public static string GetElapsedTime(double ms)
    {
        double result = ms;

        if (result <= 750)
        { return result.ToString("N3", CultureInfo.InvariantCulture) + " ms"; }
        result /= 1000;

        if (result <= 50)
        { return result.ToString("N2", CultureInfo.InvariantCulture) + " sec"; }
        result /= 60;

        if (result <= 50)
        { return result.ToString("N1", CultureInfo.InvariantCulture) + " min"; }
        result /= 60;

        if (result <= 20)
        { return result.ToString("N1", CultureInfo.InvariantCulture) + " hour"; }
        result /= 24;

        return result.ToString("N1", CultureInfo.InvariantCulture) + " day";
    }

    public static Dictionary<TKey, TValue> ConcatDictionary<TKey, TValue>(params IReadOnlyDictionary<TKey, TValue>?[] dictionaries) where TKey : notnull
    {
        Dictionary<TKey, TValue> result = new();
        for (int i = 0; i < dictionaries.Length; i++)
        {
            IReadOnlyDictionary<TKey, TValue>? dict = dictionaries[i];
            if (dict == null) continue;

            foreach (KeyValuePair<TKey, TValue> pair in dict)
            { result[pair.Key] = pair.Value; }
        }
        return result;
    }

    /// <exception cref="ArgumentException"/>
    public static void Map<TSourceKey, TSourceValue, TDestinationKey, TDestinationValue>(TSourceKey[] keys, TSourceValue[] values, Func<TSourceKey, TSourceValue, ValueTuple<TDestinationKey, TDestinationValue>> mapper, Dictionary<TDestinationKey, TDestinationValue> dictionary)
        where TDestinationKey : notnull
    {
        if (keys.Length != values.Length)
        { throw new ArgumentException($"There should be the same number of keys as values"); }

        for (int i = 0; i < keys.Length; i++)
        {
            ValueTuple<TDestinationKey, TDestinationValue> pair = mapper.Invoke(keys[i], values[i]);
            dictionary[pair.Item1] = pair.Item2;
        }
    }

    /// <exception cref="ArgumentException"/>
    public static Dictionary<TDestinationKey, TDestinationValue> Map<TSourceKey, TSourceValue, TDestinationKey, TDestinationValue>(TSourceKey[] keys, TSourceValue[] values, Func<TSourceKey, TSourceValue, ValueTuple<TDestinationKey, TDestinationValue>> mapper)
        where TDestinationKey : notnull
    {
        Dictionary<TDestinationKey, TDestinationValue> result = new();
        Utils.Map(keys, values, mapper, result);
        return result;
    }

    /// <exception cref="ArgumentException"/>
    public static void Map<TKey, TValue>(TKey[] keys, TValue[] values, Dictionary<TKey, TValue> dictionary)
        where TKey : notnull
    {
        if (keys.Length != values.Length)
        { throw new ArgumentException($"There should be the same number of keys as values"); }

        for (int i = 0; i < keys.Length; i++)
        { dictionary[keys[i]] = values[i]; }
    }

    /// <exception cref="ArgumentException"/>
    public static Dictionary<TKey, TValue> Map<TKey, TValue>(TKey[] keys, TValue[] values)
        where TKey : notnull
    {
        Dictionary<TKey, TValue> result = new();
        Utils.Map(keys, values, result);
        return result;
    }

    public static T[] Duplicate<T>(T[] array)
    {
        T[] result = new T[array.Length];
        Array.Copy(array, result, array.Length);
        return result;
    }
}

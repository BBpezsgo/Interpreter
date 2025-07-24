using System.IO;

namespace LanguageCore;

public interface IDuplicatable<T> where T : notnull
{
    T Duplicate();
}

public static class Utils
{
    public static string MakeUnique(string value, Func<string, bool> uniqueChecker)
    {
        if (uniqueChecker.Invoke(value)) return value;

        for (int i = 0; i < 64; i++)
        {
            string candidate = $"{value}_{i}";
            if (uniqueChecker.Invoke(candidate)) return candidate;
            continue;
        }

        throw new InternalExceptionWithoutContext($"Failed to generate unique id for {value}");
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

    public static bool SequenceEquals<T1, T2>(ImmutableArray<T1> collectionA, ImmutableArray<T2> collectionB)
        where T1 : IEquatable<T2>
    {
        if (collectionA.Length != collectionB.Length) return false;

        for (int i = 0; i < collectionA.Length; i++)
        {
            T1 a = collectionA[i];
            T2 b = collectionB[i];

            if (!a.Equals(b)) return false;
        }

        return true;
    }

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

    public static bool SequenceEquals<T1, T2>(IEnumerable<T1> collectionA, IEnumerable<T2> collectionB, Func<int, T1, T2, bool> checker)
    {
        using IEnumerator<T1> e1 = collectionA.GetEnumerator();
        using IEnumerator<T2> e2 = collectionB.GetEnumerator();

        int i = 0;

        while (e1.MoveNext())
        {
            if (!(e2.MoveNext() && checker.Invoke(i++, e1.Current, e2.Current)))
            { return false; }
        }

        return !e2.MoveNext();
    }

    public static bool SequenceEquals<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> collectionA, IReadOnlyDictionary<TKey, TValue> collectionB, Func<TValue, TValue, bool> checker)
        => collectionA.Keys.Count() == collectionB.Keys.Count() &&
        collectionA.Keys.All(k => collectionB.ContainsKey(k) && checker(collectionB[k], collectionA[k]));

    public static bool PowerOf2(int n) => n != 0 && (n & (n - 1)) == 0;

    public static Uri ToFileUri(string path) => new UriBuilder()
    {
        Host = null,
        Scheme = Uri.UriSchemeFile,
        Path = path,
    }.Uri;
}
